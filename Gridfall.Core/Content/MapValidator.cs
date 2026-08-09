using Gridfall.Core.Path;

namespace Gridfall.Core.Content;

public enum MapSeverity
{
    /// <summary>
    /// Deliberately NOT zero. MapFinding is a struct, so `default(MapFinding)`
    /// exists -- and with Error at 0 it would claim to be an error with an empty
    /// message. Combined with LINQ's FirstOrDefault (which returns a default
    /// rather than null for a struct) that made every valid map fail to load
    /// with the message "fixture:  (0,0)". Leave the numbering alone.
    /// </summary>
    None = 0,

    /// <summary>The game refuses to load this. Blocks a save.</summary>
    Error = 1,
    /// <summary>Outside a MapTargets band. Never blocks anything.</summary>
    Warning = 2,
    Info = 3,
}

public readonly struct MapFinding
{
    public readonly MapSeverity Severity;
    public readonly string Message;
    /// <summary>The offending cell, or Invalid when the finding is about the map as a whole.</summary>
    public readonly GridCell Cell;

    public MapFinding(MapSeverity severity, string message, GridCell? cell = null)
    {
        Severity = severity;
        Message = message;
        // (0,0) is a perfectly legal cell -- spawns sit on the west edge -- so
        // "is it the default?" cannot be inferred from the value. Take a nullable.
        Cell = cell ?? GridCell.Invalid;
    }

    public override string ToString() => Cell.IsValid ? $"{Message} {Cell}" : Message;
}

/// <summary>
/// The single verdict on whether a map is legal, and how well it fits the
/// balance targets.
///
/// The board editor calls this and so does the loader, which is how "the editor
/// cannot disagree with the game about what a legal map is" is true by
/// construction rather than by discipline. The editor contributes no rules of
/// its own; it only decides how to draw what this returns.
/// </summary>
public static class MapValidator
{
    /// <summary>
    /// The shortest route ANY map with this spawn and goal could have.
    ///
    /// Manhattan, and exact rather than an approximation, because movement is
    /// four-way (<see cref="Directions"/>). No amount of painting produces a
    /// shorter route than this, which is what makes it the right thing to
    /// measure a board's size against.
    /// </summary>
    public static int GeometricFloor(GridCell spawn, GridCell goal)
        => System.Math.Abs(spawn.X - goal.X) + System.Math.Abs(spawn.Y - goal.Y);

    /// <summary>
    /// A ratio to one decimal place, as a string, without floats.
    ///
    /// Core forbids floating point. Integer tenths are exact and cannot drift
    /// between machines, which a float formatted for display eventually would.
    /// </summary>
    public static string Tenths(int value, int of)
    {
        if (of <= 0) return "?";
        int tenths = 10 * value / of;
        return $"{tenths / 10}.{tenths % 10}";
    }

    public static List<MapFinding> Validate(MapDraft draft)
    {
        var findings = new List<MapFinding>();

        // ---- errors: the game will not load these -------------------------

        if (draft.Width is < 8 or > 64)
            findings.Add(new MapFinding(MapSeverity.Error, $"width {draft.Width} is outside 8..64"));
        if (draft.Height is < 8 or > 64)
            findings.Add(new MapFinding(MapSeverity.Error, $"height {draft.Height} is outside 8..64"));
        if (draft.Cells.Length != draft.Width * draft.Height)
            findings.Add(new MapFinding(MapSeverity.Error, "cell array does not match the declared size"));

        if (findings.Count > 0) return findings;   // geometry is broken; the rest would throw

        int goals = draft.Cells.Count(c => c == CellKind.Goal);
        if (goals == 0) findings.Add(new MapFinding(MapSeverity.Error, "map has no goal"));
        if (goals > 1) findings.Add(new MapFinding(MapSeverity.Error, $"map has {goals} goals; it must have exactly one"));

        var spawnCells = new List<GridCell>();
        for (int y = 0; y < draft.Height; y++)
        for (int x = 0; x < draft.Width; x++)
            if (draft.Cells[draft.Index(x, y)] == CellKind.Spawn) spawnCells.Add(new GridCell(x, y));

        if (spawnCells.Count == 0) findings.Add(new MapFinding(MapSeverity.Error, "map has no spawn"));

        foreach (GridCell listed in draft.Spawns)
            if (!spawnCells.Contains(listed))
                findings.Add(new MapFinding(MapSeverity.Error, "spawns lists a cell that is not an 'S'", listed));
        foreach (GridCell glyph in spawnCells)
            if (!draft.Spawns.Contains(glyph))
                findings.Add(new MapFinding(MapSeverity.Error, "'S' cell missing from the spawns array", glyph));

        // ---- surfaces: the rule that keeps them view-only -----------------
        //
        // An ERROR, not a warning, and that is the whole design. Surfaces are
        // safe to ignore in the simulation *because* water can only sit where
        // the pathfinder already refuses to go. A board that painted water on a
        // walkable cell would look like it had a river, play like it did not,
        // and nothing downstream could tell -- the exact failure the elevation
        // shelf and the stranded-cell check were both added to prevent.
        if (draft.Surfaces.Length == draft.Width * draft.Height)
        {
            for (int i = 0; i < draft.Surfaces.Length; i++)
            {
                var surface = (CellSurface)draft.Surfaces[i];
                if (surface == CellSurface.Ground) continue;
                if (MapSurfaces.IsLegalOn(surface, draft.Cells[i])) continue;

                findings.Add(new MapFinding(MapSeverity.Error,
                    MapSurfaces.RefusalFor(surface),
                    new GridCell(i % draft.Width, i / draft.Width)));
            }
        }
        else if (draft.Surfaces.Length != 0)
        {
            findings.Add(new MapFinding(MapSeverity.Error,
                "surface array does not match the declared size"));
        }

        if (findings.Any(f => f.Severity == MapSeverity.Error)) return findings;

        // ---- reachability: the hard invariant -----------------------------

        MapDef map = draft.ToMapDef();
        var path = new PathSystem(map);

        foreach (GridCell spawn in map.Spawns)
            if (!path.IsReachable(spawn))
                findings.Add(new MapFinding(MapSeverity.Error, "spawn cannot reach the goal", spawn));

        if (findings.Any(f => f.Severity == MapSeverity.Error)) return findings;

        // ---- warnings: MapTargets, documented in balance-targets.md -------

        int cells = map.Width * map.Height;
        int buildable = map.Cells.Count(c => c == CellKind.Buildable);
        int buildablePercent = 100 * buildable / cells;

        if (buildablePercent < MapTargets.MinBuildablePercent || buildablePercent > MapTargets.MaxBuildablePercent)
            findings.Add(new MapFinding(MapSeverity.Warning,
                $"buildable {buildablePercent}% is outside {MapTargets.MinBuildablePercent}-{MapTargets.MaxBuildablePercent}%"));

        int shortest = path.RouteLength(map.Spawns[0]);
        int floor = GeometricFloor(map.Spawns[0], map.Goal);

        if (floor > MapTargets.MaxSpawnGoalDistance)
        {
            // Instead of, not as well as, the band warning below. On a board
            // this size the band warning is true, implied, and unactionable --
            // it reads as "repaint your map" when no painting can help.
            findings.Add(new MapFinding(MapSeverity.Warning,
                $"board too large for the tuned combat model: spawn and goal are {floor} cells apart, " +
                $"over the {MapTargets.MaxSpawnGoalDistance} cap, so no layout can reach the " +
                $"{MapTargets.MinUnmazedPath}-{MapTargets.MaxUnmazedPath} path band"));
        }
        else if (shortest < MapTargets.MinUnmazedPath || shortest > MapTargets.MaxUnmazedPath)
        {
            findings.Add(new MapFinding(MapSeverity.Warning,
                $"unmazed path {shortest} is outside {MapTargets.MinUnmazedPath}-{MapTargets.MaxUnmazedPath}"));
        }

        if (map.Spawns.Length > MapTargets.MaxLanes)
            findings.Add(new MapFinding(MapSeverity.Warning,
                $"{map.Spawns.Length} spawns exceeds the {MapTargets.MaxLanes}-lane target"));

        int stranded = 0;
        for (int i = 0; i < cells; i++)
            if (map.Cells[i] == CellKind.Buildable && path.DistanceAt(i) == PathSystem.NoDistance)
                stranded++;
        if (stranded > 0)
            findings.Add(new MapFinding(MapSeverity.Warning,
                $"{stranded} buildable cells are walled off and can never matter"));

        // A bridge that touches no water is legal and is usually a mistake: it
        // reads as a stripe of odd-coloured floor. Warning rather than error,
        // because a causeway across a gorge is a real thing once the height field
        // is doing the work -- and a tool that refuses to let you build the
        // strange thing is a tool you stop using.
        //
        // Judged per BRIDGE, not per cell. The first version asked whether each
        // span cell touched water and warned about the middle of every bridge
        // three or more cells long, which is every bridge worth having.
        if (draft.Surfaces.Length == cells)
        {
            var seen = new bool[cells];
            int orphanBridges = 0;

            for (int start = 0; start < cells; start++)
            {
                if (seen[start] || (CellSurface)draft.Surfaces[start] != CellSurface.Span) continue;

                // Flood the connected run of span cells, noting whether any of
                // them has water beside it.
                bool touchesWater = false;
                var queue = new Queue<int>();
                queue.Enqueue(start);
                seen[start] = true;

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = i % map.Width, y = i / map.Width;
                    foreach ((int dx, int dy) in Directions.Offsets)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height) continue;
                        int n = map.Index(nx, ny);
                        var neighbour = (CellSurface)draft.Surfaces[n];
                        if (neighbour == CellSurface.Water) touchesWater = true;
                        if (neighbour != CellSurface.Span || seen[n]) continue;
                        seen[n] = true;
                        queue.Enqueue(n);
                    }
                }

                if (!touchesWater) orphanBridges++;
            }

            if (orphanBridges > 0)
                findings.Add(new MapFinding(MapSeverity.Warning,
                    $"{orphanBridges} bridge(s) touch no water -- a span over nothing reads as odd flooring"));
        }

        // ---- info ---------------------------------------------------------

        findings.Add(new MapFinding(MapSeverity.Info, $"{map.Width}x{map.Height}, {buildablePercent}% buildable"));
        findings.Add(new MapFinding(MapSeverity.Info,
            $"path {shortest}, {Tenths(shortest, floor)}x the {floor}-cell floor, spawns {map.Spawns.Length}"));

        return findings;
    }

    public static bool HasErrors(List<MapFinding> findings)
        => findings.Any(f => f.Severity == MapSeverity.Error);

    /// <summary>
    /// Greedy approximation of the longest path a legal station layout can force.
    ///
    /// A LOWER BOUND, not the answer: finding the true worst case is a search,
    /// not a query. Over the target proves the map fails; under it proves
    /// nothing. Report it as an estimate wherever it appears -- a number that
    /// looks exact will be quoted as one.
    ///
    /// O(buildable^2 * cells), so this is a keypress, never a stroke handler.
    /// </summary>
    public static int EstimateMaxMazedPath(MapDef map)
    {
        var path = new PathSystem(map);

        int best = path.RouteLength(map.Spawns[0]);
        var blocked = new bool[map.Width * map.Height];

        while (true)
        {
            int bestCell = -1;
            int bestLength = best;

            for (int i = 0; i < map.Cells.Length; i++)
            {
                if (blocked[i] || map.Cells[i] != CellKind.Buildable) continue;
                if (!path.WouldRemainConnected(i)) continue;   // the game would refuse this build

                int length = path.PreviewDistanceAt(map.Index(map.Spawns[0]));
                if (length == PathSystem.NoDistance || length <= bestLength) continue;

                bestLength = length;
                bestCell = i;
            }

            if (bestCell < 0) return best;

            blocked[bestCell] = true;
            path.SetBlocked(bestCell, true);
            path.ForceRebuild();
            best = bestLength;
        }
    }
}
