using Gridfall.Core;
using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The editor writes maps and the game reads them. If these two ever disagree
/// about the format, the editor becomes a way to produce broken content -- so
/// the round trip is the test that matters most here.
/// </summary>
public class MapDraftTests
{
    [Fact]
    public void RoundTrip_ThroughJson_PreservesTheMap()
    {
        MapDef original = TestContent.Map(TestContent.PinchMap);
        MapDraft draft = MapDraft.From(original);

        MapDef reloaded = ContentLoader.LoadMap(draft.ToJson(), "roundtrip.json");

        Assert.Equal(original.Width, reloaded.Width);
        Assert.Equal(original.Height, reloaded.Height);
        Assert.Equal(original.Goal, reloaded.Goal);
        Assert.Equal(original.Spawns, reloaded.Spawns);
        Assert.Equal(original.Cells, reloaded.Cells);
        Assert.Equal(original.StartingGold, reloaded.StartingGold);
        Assert.Equal(original.StartingLives, reloaded.StartingLives);
    }

    [Fact]
    public void ABlankMap_IsImmediatelyValid()
    {
        // A new map that fails validation the moment you create it is a bad
        // first five seconds.
        MapDraft draft = MapDraft.Blank(20, 12);
        Assert.False(MapValidator.HasErrors(MapValidator.Validate(draft)));
        ContentLoader.LoadMap(draft.ToJson(), "blank.json");   // must not throw
    }

    [Fact]
    public void ADraftWithNoGoal_IsFlaggedAndCannotBuildAFlowField()
    {
        // Pins the precondition the board editor's rebuild has to guard on.
        //
        // Painting over the goal is a normal thing to do mid-edit, and the editor
        // must keep drawing the board and report "map has no goal". It did not:
        // RebuildEverything built a PathSystem unconditionally, so painting the
        // goal away and then cycling the theme threw IndexOutOfRange instead of
        // showing the error. If PathSystem ever tolerates a goal-less map this
        // test fails, and the guard can go.
        MapDraft draft = MapDraft.Blank(20, 12);
        draft.Paint(draft.Goal, CellKind.Buildable);

        Assert.False(draft.Goal.IsValid);
        Assert.True(MapValidator.HasErrors(MapValidator.Validate(draft)));
        Assert.Throws<IndexOutOfRangeException>(() => new Gridfall.Core.Path.PathSystem(draft.ToMapDef()));
    }

    [Fact]
    public void PaintingASecondGoal_MovesTheFirst()
    {
        MapDraft draft = MapDraft.Blank(20, 12);
        var moved = new GridCell(5, 2);

        draft.Paint(moved, CellKind.Goal);

        Assert.Equal(moved, draft.Goal);
        Assert.Equal(1, draft.Cells.Count(c => c == CellKind.Goal));
    }

    [Fact]
    public void PaintingKeepsTheSpawnListInSyncWithTheGlyphs()
    {
        // The loader rejects a map whose spawns array disagrees with its 'S'
        // cells, and hand-editing hits that constantly. The editor cannot.
        MapDraft draft = MapDraft.Blank(20, 12);
        var added = new GridCell(0, 2);

        draft.Paint(added, CellKind.Spawn);
        Assert.Contains(added, draft.Spawns);

        draft.Paint(added, CellKind.Buildable);
        Assert.DoesNotContain(added, draft.Spawns);

        Assert.False(MapValidator.HasErrors(MapValidator.Validate(draft)));
    }

    [Fact]
    public void Validator_ReportsAnUnreachableGoalAsAnError()
    {
        MapDraft draft = MapDraft.Blank(20, 12);
        for (int y = 0; y < draft.Height; y++) draft.Paint(new GridCell(10, y), CellKind.Blocked);

        var findings = MapValidator.Validate(draft);

        Assert.True(MapValidator.HasErrors(findings));
        Assert.Contains(findings, f => f.Severity == MapSeverity.Error && f.Message.Contains("reach the goal"));
    }

    [Fact]
    public void Validator_WarnsButDoesNotError_OnTargetMisses()
    {
        // Warnings never block. An unusual map is often a deliberate one.
        MapDraft draft = MapDraft.Blank(9, 9);   // tiny: path well under the target
        var findings = MapValidator.Validate(draft);

        Assert.False(MapValidator.HasErrors(findings));
        Assert.Contains(findings, f => f.Severity == MapSeverity.Warning);
    }

    [Fact]
    public void Resize_KeepsContentAnchoredAndDropsWhatFallsOff()
    {
        MapDraft draft = MapDraft.Blank(20, 12);
        draft.Paint(new GridCell(3, 3), CellKind.PathOnly);

        draft.Resize(12, 10);

        Assert.Equal(12, draft.Width);
        Assert.Equal(CellKind.PathOnly, draft.Cells[draft.Index(3, 3)]);
        Assert.All(draft.Spawns, s => Assert.True(draft.InBounds(s)));
    }

    [Fact]
    public void MazeEstimate_IsAtLeastTheUnmazedPath()
    {
        MapDef map = TestContent.Map(TestContent.LaneMap);
        int unmazed = new Core.Path.PathSystem(map).RouteLength(map.Spawns[0]);

        int mazed = MapValidator.EstimateMaxMazedPath(map);

        // A lower bound, so the only sound assertion is that it does not go down.
        Assert.True(mazed >= unmazed, $"estimate {mazed} below the unmazed path {unmazed}");
    }
}
