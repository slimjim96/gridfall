using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The path band is absolute, and that has a consequence: some boards the
/// validator permits (up to 64×64) cannot satisfy it at any layout.
///
/// The point of these tests is the *distinction*. "path 63 outside 18-30" reads
/// as "repaint your map" and is unactionable; "spawn and goal are 63 apart" names
/// the thing you can actually change.
/// </summary>
public class MapTargetsAtScaleTests
{
    private static string Messages(MapDraft draft)
        => string.Join(" | ", MapValidator.Validate(draft).Select(f => f.ToString()));

    [Fact]
    public void GeometricFloor_IsExactManhattan_BecauseMovementIsFourWay()
    {
        // Exact, not an estimate: Directions has four members, so no diagonal
        // shortcut exists and Manhattan IS the shortest possible route.
        Assert.Equal(19, MapValidator.GeometricFloor(new GridCell(0, 4), new GridCell(19, 4)));
        Assert.Equal(15, MapValidator.GeometricFloor(new GridCell(0, 1), new GridCell(8, 8)));
        Assert.Equal(0, MapValidator.GeometricFloor(new GridCell(3, 3), new GridCell(3, 3)));
    }

    [Fact]
    public void ABoardTooLargeIsToldWhy_NotJustThatItsPathIsLong()
    {
        // 64x64 blank: spawn on the west edge, goal on the east, 63 apart. No
        // painting can shorten that, so the band warning would be noise.
        MapDraft draft = MapDraft.Blank(64, 64);
        Assert.Equal(63, MapValidator.GeometricFloor(draft.Spawns[0], draft.Goal));

        string messages = Messages(draft);

        Assert.Contains("board too large", messages);
        Assert.Contains("63 cells apart", messages);
        Assert.DoesNotContain("unmazed path", messages);
    }

    [Fact]
    public void ABoardInsideTheCapStillGetsTheOrdinaryBandWarning()
    {
        // The size check must not swallow the band check on boards it does not
        // apply to. A 20x12 blank has a 19-cell floor and a straight 19 route,
        // which is inside 18-30 -- so widen it until the path is over.
        MapDraft small = MapDraft.Blank(12, 10);
        Assert.True(MapValidator.GeometricFloor(small.Spawns[0], small.Goal)
                    <= MapTargets.MaxSpawnGoalDistance);

        string messages = Messages(small);
        Assert.DoesNotContain("board too large", messages);
        Assert.Contains("unmazed path", messages);   // an 11-cell route is under the floor of 18
    }

    [Fact]
    public void NeitherShippedMapBecomesTooLarge()
    {
        // The cap was chosen so no shipped map changes verdict. If this fails,
        // a content change moved a spawn or a goal past what the combat model
        // was tuned for, and that is a balance decision, not a map edit.
        foreach (string id in new[] { "crossroads", "gauntlet" })
        {
            MapDef map = ContentLoader.LoadMap(
                File.ReadAllText(Path.Combine(RepoRoot(), "content-data", "maps", id + ".json")),
                id + ".json");

            int floor = MapValidator.GeometricFloor(map.Spawns[0], map.Goal);
            Assert.True(floor <= MapTargets.MaxSpawnGoalDistance,
                $"{id} spawn-goal distance {floor} exceeds the {MapTargets.MaxSpawnGoalDistance} cap");
        }
    }

    [Fact]
    public void Tenths_IsExactAndNeedsNoFloats()
    {
        // Core forbids floating point. Integer tenths cannot drift between
        // machines the way a formatted double eventually would.
        Assert.Equal("1.0", MapValidator.Tenths(19, 19));
        Assert.Equal("1.9", MapValidator.Tenths(29, 15));
        Assert.Equal("3.3", MapValidator.Tenths(10, 3));
        Assert.Equal("?", MapValidator.Tenths(10, 0));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }
}
