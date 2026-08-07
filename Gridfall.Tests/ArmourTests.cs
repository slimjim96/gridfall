using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Flat, per-hit damage reduction. Flat rather than percentage so it punishes
/// many-small-hits and rewards few-big-hits -- the axis a roster of pure
/// stat-variants cannot express (enemy-roster design spec).
/// </summary>
public class ArmourTests
{
    private const string Plated = """
    { "id": "plated", "name": "Plated", "hp": 1000, "speed": 0.04, "armour": 8, "bounty": 5 }
    """;
    private const string WallWave = """
    { "map": "t", "waves": [ { "index": 1, "entries": [
        { "enemy": "wall", "count": 1, "spacingTicks": 10 } ] } ] }
    """;

    private const string Bare = """
    { "id": "bare", "name": "Bare", "hp": 1000, "speed": 0.04, "armour": 0, "bounty": 5 }
    """;

    private static EnemyDef[] Enemies() =>
        ContentLoader.LoadEnemies(new[] { ("plated.json", Plated), ("bare.json", Bare) });

    [Fact]
    public void ArmourDefaultsToZero_SoExistingContentIsUnaffected()
        => Assert.All(TestContent.BuildContent().Enemies, e => Assert.Equal(0, e.Armour));

    [Fact]
    public void ArmourIsLoadedFromTheData()
    {
        EnemyDef[] e = Enemies();
        Assert.Equal(8, e.Single(x => x.Id == "plated").Armour);
        Assert.Equal(0, e.Single(x => x.Id == "bare").Armour);
    }

    /// <summary>
    /// Runs one wave of a single archetype past one tower and reports the total
    /// damage that actually landed.
    /// </summary>
    private static int DamageDealt(string enemyId, string towerId)
    {
        string waves = $$"""
        { "map": "t", "waves": [ { "index": 1, "entries": [
            { "enemy": "{{enemyId}}", "count": 1, "spacingTicks": 10 } ] } ] }
        """;

        EnemyDef[] enemies = Enemies();
        var content = new ContentSet
        {
            Towers = TestContent.BuildContent().Towers,
            Enemies = enemies,
            Waves = ContentLoader.LoadWaves(waves, enemies, "w.json"),
        };

        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), content.TowerIndexOf(towerId)));
        sim.Enqueue(new StartWaveCommand());

        int dealt = 0;
        for (int t = 0; t < 400; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.CreepDamaged) dealt += e.B;
        }
        return dealt;
    }

    [Fact]
    public void ArmourApplies_PerHit_NotToTheTickTotal()
    {
        // The whole design. Per-hit is what makes rapid-fire towers weak; applying
        // armour to a tick's total would leave them almost unaffected.
        int bare = DamageDealt("bare", "arrow-tower");
        int plated = DamageDealt("plated", "arrow-tower");

        Assert.True(plated > 0, "armour must never zero a tower out");
        Assert.True(plated < bare / 2,
            $"arrow vs armour 8 dealt {plated} against {bare} unarmoured -- 12 damage should land as 4");
    }

    [Fact]
    public void ArmourHurtsRapidFireFarMoreThanBurst()
    {
        // Criterion 7: provable from the numbers, not asserted in a doc.
        double arrowLoss = 1.0 - DamageDealt("plated", "arrow-tower") / (double)DamageDealt("bare", "arrow-tower");
        double cannonLoss = 1.0 - DamageDealt("plated", "cannon") / (double)DamageDealt("bare", "cannon");

        Assert.True(arrowLoss > cannonLoss + 0.2,
            $"arrow lost {arrowLoss:P0} to armour, cannon lost {cannonLoss:P0} -- " +
            "the gap is the decision the mechanic exists to create");
    }

    [Fact]
    public void NoHitEverDealsLessThanOne()
    {
        // An enemy immune to a tower is a soft-lock waiting to happen.
        const string wall = """
        { "id": "wall", "name": "Wall", "hp": 500, "speed": 0.04, "armour": 9999, "bounty": 1 }
        """;
        EnemyDef[] enemies = ContentLoader.LoadEnemies(new[] { ("wall.json", wall) });
        var content = new ContentSet
        {
            Towers = TestContent.BuildContent().Towers,
            Enemies = enemies,
            Waves = ContentLoader.LoadWaves(WallWave, enemies, "w.json"),
        };

        var sim = new Sim(TestContent.Map(TestContent.ArenaMap), content, 1);
        sim.Enqueue(new BuildCommand(new GridCell(5, 3), content.TowerIndexOf("arrow-tower")));
        sim.Enqueue(new StartWaveCommand());

        var amounts = new List<int>();
        for (int t = 0; t < 300; t++)
        {
            sim.Tick();
            foreach (SimEvent e in sim.Events.Span)
                if (e.Kind == EventKind.CreepDamaged) amounts.Add(e.B);
        }

        Assert.NotEmpty(amounts);
        Assert.All(amounts, a => Assert.Equal(1, a));
    }

    [Fact]
    public void TheRosterHasFourArchetypes_AndNoSharedSilhouette()
    {
        // Criterion 5. Silhouette is a view concern, so this asserts the roster
        // size and leaves the shapes to the placeholder factory -- which has a
        // distinct case per id, checked by reading it.
        string dir = Path.Combine(RepoRoot(), "content-data", "enemies");
        string[] ids = Directory.EnumerateFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray()!;

        Assert.Equal(new[] { "brute", "husk", "mite", "runner" }, ids);

        string factory = File.ReadAllText(
            Path.Combine(RepoRoot(), "godot", "Placeholders", "PlaceholderFactory.cs"));
        foreach (string id in ids)
            Assert.Contains($"\"{id}\" =>", factory);
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
