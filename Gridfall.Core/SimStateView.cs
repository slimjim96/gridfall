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
    public int Lives => _state.Lives;
    public int WaveIndex => _state.WaveIndex;

    /// <summary>
    /// The tick this wave's group `i` next spawns at. Read-only, like everything
    /// here -- exposed so the view can show an incoming-wave shape, and so wave
    /// variance is testable as the schedule it actually produces.
    /// </summary>
    public int WaveEntryNextTick(int i) => _state.WaveEntryNextTick[i];
    public bool WaveActive => _state.WaveActive;

    // ---- creeps -----------------------------------------------------------

    public int CreepCount => _state.CreepCount;

    /// <summary>Slot of the k-th live creep in ascending id order. Iterate with this.</summary>
    public int CreepSlotByOrder(int k) => _state.CreepSlotByOrder(k);

    /// <summary>Slot for an id, or -1 if it is gone.</summary>
    public int SlotOfCreep(int id) => _state.SlotOfCreep(id);

    public int CreepId(int slot) => _state.CreepId[slot];
    public ushort CreepDefIndex(int slot) => _state.CreepDefIndex[slot];
    public int CreepCellIndex(int slot) => _state.CreepCellIndex[slot];
    public Fix32 CreepProgress(int slot) => _state.CreepProgress[slot];
    public byte CreepHeading(int slot) => _state.CreepHeading[slot];
    public int CreepHp(int slot) => _state.CreepHp[slot];

    // ---- towers -----------------------------------------------------------

    public int TowerCount => _state.TowerCount;
    public int TowerSlotByOrder(int k) => _state.TowerSlotByOrder(k);
    public int SlotOfTower(int id) => _state.SlotOfTower(id);

    public int TowerId(int slot) => _state.TowerId[slot];
    public ushort TowerDefIndex(int slot) => _state.TowerDefIndex[slot];
    public int TowerCellIndex(int slot) => _state.TowerCellIndex[slot];
    public int TowerCooldown(int slot) => _state.TowerCooldown[slot];
    public byte TowerLevel(int slot) => _state.TowerLevel[slot];
    public int TowerHp(int slot) => _state.TowerHp[slot];

    // ---- projectiles ------------------------------------------------------

    public int ProjectileCount => _state.ProjectileCount;
    public int ProjectileId(int slot) => _state.ProjectileId[slot];
    public FixVec2 ProjectilePos(int slot) => _state.ProjectilePos[slot];
    public int ProjectileTargetCreepId(int slot) => _state.ProjectileTargetCreepId[slot];
}
