using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.View.Placeholders;
using Gridfall.View.Units;

namespace Gridfall.View;

/// <summary>
/// Visitors, stations, and projectiles. Reads simulation state and never writes it.
///
/// Continuous things (where a visitor is) come from state and are interpolated.
/// Discrete things (that it just died) come from the event stream. Polling for
/// diffs would miss a tick where two things happened -- engine guide 05.
/// </summary>
public sealed partial class UnitRenderer : Node3D
{
    private sealed class Tracked
    {
        public required IUnitView View { get; init; }
        public Vector3 Previous;
        public Vector3 Current;
        public bool Dying;
        public float DyingFor;
    }

    private const float DeathLingerSeconds = 0.15f;

    private readonly Dictionary<int, Tracked> _visitors = new();
    private readonly Dictionary<int, Tracked> _stations = new();
    private readonly Dictionary<int, Tracked> _projectiles = new();
    private readonly List<int> _scratchGone = new();

    private SimDriver _driver = null!;

    /// <summary>-1 so the very first rendered frame always syncs.</summary>
    private int _lastRenderedTick = -1;

    public void Initialise(SimDriver driver) => _driver = driver;

    /// <summary>Called once per frame, after the driver has advanced.</summary>
    public void Render(float delta)
    {
        // Guard on the tick NUMBER, not on the flag alone. In shot mode the
        // driver is stepped by hand and Advance() -- the only thing that clears
        // Ticked and FrameEvents -- never runs, so the flag stays true and the
        // final tick's events replay on every rendered frame. That re-armed the
        // hit flash forever and pinned a depleted station solid white, hiding the
        // very cue the capture existed to verify.
        if (_driver.Ticked && _driver.TickCount != _lastRenderedTick)
        {
            _lastRenderedTick = _driver.TickCount;
            HandleEvents();
            SyncEntities();
        }

        Interpolate(_driver.Alpha);
        AdvanceViews(delta);
    }

    // ---- events -----------------------------------------------------------

    private void HandleEvents()
    {
        foreach (SimEvent e in _driver.FrameEvents)
        {
            switch (e.Kind)
            {
                case EventKind.VisitorServed:
                    if (_visitors.TryGetValue(e.A, out Tracked? hit)) hit.View.PlayClip("hit");
                    break;

                case EventKind.VisitorDied:
                case EventKind.VisitorLeaked:
                    if (_visitors.TryGetValue(e.A, out Tracked? dead) && !dead.Dying)
                    {
                        dead.View.PlayClip("death");
                        dead.Dying = true;   // linger through the collapse, then release
                    }
                    break;

                case EventKind.StationFired:
                    if (_stations.TryGetValue(e.A, out Tracked? station)) station.View.PlayClip("fire");
                    break;

                case EventKind.StationDepleted:
                    if (_stations.TryGetValue(e.A, out Tracked? struck)) struck.View.PlayClip("hit");
                    break;

                case EventKind.StationDestroyed:
                    // ReleaseMissing would collapse it anyway, but only on the
                    // frame after it left the state. Handling the event puts the
                    // collapse on the same frame as the cause.
                    if (_stations.TryGetValue(e.A, out Tracked? razed) && !razed.Dying)
                    {
                        razed.View.PlayClip("death");
                        razed.Dying = true;
                    }
                    break;
            }
        }
    }

    // ---- state sync -------------------------------------------------------

    private void SyncEntities()
    {
        SimStateView state = _driver.State;
        MapDef map = _driver.Map;
        ContentSet content = _driver.Content;

        // Visitors
        for (int k = 0; k < state.VisitorCount; k++)
        {
            int slot = state.VisitorSlotByOrder(k);
            int id = state.VisitorId(slot);
            Vector3 world = VisitorWorldPosition(state, map, slot);

            if (!_visitors.TryGetValue(id, out Tracked? tracked))
            {
                string contentId = content.Visitor(state.VisitorDefIndex(slot)).Id;
                IUnitView view = UnitViewFactory.CreateVisitor(contentId, id);
                AddChild(view.Node);
                tracked = new Tracked { View = view, Previous = world, Current = world };
                _visitors[id] = tracked;
            }
            else
            {
                tracked.Previous = tracked.Current;
                tracked.Current = world;
            }
        }
        ReleaseMissing(_visitors, id => state.SlotOfVisitor(id) >= 0);

        // Stations
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            int id = state.StationId(slot);
            int cellIndex = state.StationCellIndex(slot);
            Vector3 world = IsoGrid.CellCentre(cellIndex % map.Width, cellIndex / map.Width);

            // Both no-op unless the value actually changed, so this is cheap
            // enough to push every frame rather than tracking dirty flags.
            float health = (float)state.StationStock(slot)
                           / content.Station(state.StationDefIndex(slot)).Stock;

            if (_stations.TryGetValue(id, out Tracked? existing))
            {
                existing.View.SetLevel(state.StationLevel(slot));
                existing.View.SetHealthFraction(health);
                continue;
            }

            string contentId = content.Station(state.StationDefIndex(slot)).Id;
            IUnitView view = UnitViewFactory.CreateStation(contentId, id);
            view.SetLevel(state.StationLevel(slot));
            view.SetHealthFraction(health);
            AddChild(view.Node);
            _stations[id] = new Tracked { View = view, Previous = world, Current = world };
        }
        ReleaseMissing(_stations, id => state.SlotOfStation(id) >= 0);

        // Projectiles
        for (int slot = 0; slot < state.ProjectileCount; slot++)
        {
            int id = state.ProjectileId(slot);
            FixVec2 pos = state.ProjectilePos(slot);
            var world = new Vector3(
                pos.X.ToFloat() + IsoGrid.CellSize * 0.5f,
                0.45f,
                pos.Y.ToFloat() + IsoGrid.CellSize * 0.5f);

            if (!_projectiles.TryGetValue(id, out Tracked? tracked))
            {
                IUnitView view = PlaceholderFactory.CreateProjectile(id);
                AddChild(view.Node);
                _projectiles[id] = new Tracked { View = view, Previous = world, Current = world };
            }
            else
            {
                tracked.Previous = tracked.Current;
                tracked.Current = world;
            }
        }
        ReleaseMissing(_projectiles, LiveProjectile);

        bool LiveProjectile(int id)
        {
            for (int i = 0; i < state.ProjectileCount; i++)
                if (state.ProjectileId(i) == id) return true;
            return false;
        }
    }

    /// <summary>
    /// World position from cell + progress along heading. Fix32 becomes float
    /// here and nowhere else in the entity path -- this is the boundary.
    /// </summary>
    private static Vector3 VisitorWorldPosition(SimStateView state, MapDef map, int slot)
    {
        int cellIndex = state.VisitorCellIndex(slot);
        int cx = cellIndex % map.Width;
        int cy = cellIndex / map.Width;

        (int dx, int dy) = Directions.Offsets[state.VisitorHeading(slot)];
        float progress = state.VisitorProgress(slot).ToFloat();

        return IsoGrid.CellCentre(cx, cy) + new Vector3(dx * progress, 0f, dy * progress) * IsoGrid.CellSize;
    }

    private void ReleaseMissing(Dictionary<int, Tracked> tracked, System.Func<int, bool> isAlive)
    {
        _scratchGone.Clear();
        foreach (KeyValuePair<int, Tracked> pair in tracked)
            if (!isAlive(pair.Key) && !pair.Value.Dying)
            {
                // Gone without a death event (leaked, or removed some other way):
                // collapse it rather than popping it out of existence.
                pair.Value.View.PlayClip("death");
                pair.Value.Dying = true;
            }

        foreach (KeyValuePair<int, Tracked> pair in tracked)
        {
            if (!pair.Value.Dying) continue;
            if (pair.Value.DyingFor < DeathLingerSeconds) continue;
            _scratchGone.Add(pair.Key);
        }

        foreach (int id in _scratchGone)
        {
            tracked[id].View.Dispose();
            tracked.Remove(id);
        }
    }

    // ---- per-frame --------------------------------------------------------

    private void Interpolate(float alpha)
    {
        foreach (Tracked t in _visitors.Values)
        {
            // Lerp between world positions, not between cell+progress: progress
            // wraps 0.9 -> 0.1 at a cell boundary and the visitor would snap back.
            t.View.SetWorldPosition(t.Dying ? t.Current : t.Previous.Lerp(t.Current, alpha));
        }
        foreach (Tracked t in _projectiles.Values)
            t.View.SetWorldPosition(t.Previous.Lerp(t.Current, alpha));
        foreach (Tracked t in _stations.Values)
            t.View.SetWorldPosition(t.Current);
    }

    private void AdvanceViews(float delta)
    {
        foreach (Tracked t in _visitors.Values) { t.View.Advance(delta); if (t.Dying) t.DyingFor += delta; }
        foreach (Tracked t in _stations.Values) { t.View.Advance(delta); if (t.Dying) t.DyingFor += delta; }
        foreach (Tracked t in _projectiles.Values) { t.View.Advance(delta); if (t.Dying) t.DyingFor += delta; }
    }
}
