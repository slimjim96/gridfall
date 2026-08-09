using Gridfall.Core.Math;

namespace Gridfall.Core;

/// <summary>
/// A read-only window onto simulation state, for the renderer.
///
/// This is what makes "the view never mutates state" a compile-time fact rather
/// than a code-review convention (engine guide 05). There is no setter here, and
/// deliberately no way to reach an underlying array: every accessor returns a
/// copied value, so a caller cannot take a reference and write through it.
///
/// A struct wrapping a reference: passing it around copies one pointer, and
/// there is nothing to allocate per frame.
///
/// Widen this reluctantly. Every accessor added is something the view can couple
/// to and the next refactor has to preserve. When the view wants to know
/// something new, ask first whether an event would say it better -- events
/// describe what happened, which is usually what a renderer actually needs.
/// </summary>
public readonly struct SimStateView
{
    private readonly SimState _state;

    internal SimStateView(SimState state) => _state = state;

    // ---- globals ----------------------------------------------------------

    public int Gold => _state.Gold;
    public int Patience => _state.Patience;
    public int WaveIndex => _state.WaveIndex;
    public int PrepTicksRemaining => _state.PrepTicksRemaining;

    /// <summary>
    /// The tick this wave's group `i` next spawns at. Read-only, like everything
    /// here -- exposed so the view can show an incoming-wave shape, and so wave
    /// variance is testable as the schedule it actually produces.
    /// </summary>
    public int WaveEntryNextTick(int i) => _state.WaveEntryNextTick[i];
    public bool WaveActive => _state.WaveActive;

    // ---- visitors -----------------------------------------------------------

    public int VisitorCount => _state.VisitorCount;

    /// <summary>Slot of the k-th live visitor in ascending id order. Iterate with this.</summary>
    public int VisitorSlotByOrder(int k) => _state.VisitorSlotByOrder(k);

    /// <summary>Slot for an id, or -1 if it is gone.</summary>
    public int SlotOfVisitor(int id) => _state.SlotOfVisitor(id);

    public int VisitorId(int slot) => _state.VisitorId[slot];
    public ushort VisitorDefIndex(int slot) => _state.VisitorDefIndex[slot];
    public int VisitorCellIndex(int slot) => _state.VisitorCellIndex[slot];
    public Fix32 VisitorProgress(int slot) => _state.VisitorProgress[slot];
    public byte VisitorHeading(int slot) => _state.VisitorHeading[slot];
    public int VisitorAppetite(int slot) => _state.VisitorAppetite[slot];

    // ---- stations -----------------------------------------------------------

    public int StationCount => _state.StationCount;
    public int StationSlotByOrder(int k) => _state.StationSlotByOrder(k);
    public int SlotOfStation(int id) => _state.SlotOfStation(id);

    public int StationId(int slot) => _state.StationId[slot];
    public ushort StationDefIndex(int slot) => _state.StationDefIndex[slot];
    public int StationCellIndex(int slot) => _state.StationCellIndex[slot];
    public int StationCooldown(int slot) => _state.StationCooldown[slot];
    public byte StationLevel(int slot) => _state.StationLevel[slot];
    public int StationStock(int slot) => _state.StationStock[slot];

    // ---- projectiles ------------------------------------------------------

    public int ProjectileCount => _state.ProjectileCount;
    public int ProjectileId(int slot) => _state.ProjectileId[slot];
    public FixVec2 ProjectilePos(int slot) => _state.ProjectilePos[slot];
    public int ProjectileTargetVisitorId(int slot) => _state.ProjectileTargetVisitorId[slot];
}
