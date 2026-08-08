using Gridfall.Core.Content;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// Final unit art is discovered by folder name: `presentation/units/[content-id]/`.
///
/// That convention is what removes the registration step, and it is also the
/// whole risk — a folder named `arrow_tower` or `arrowtower` matches no content
/// id, resolves to nothing, and the game quietly keeps drawing the placeholder.
/// Nothing errors, so nothing tells you. These tests are what tell you.
///
/// Same shape as MapThemeTests: the view layer cannot be referenced from here
/// (no Godot assembly), so the filesystem convention is checked directly.
/// </summary>
public class UnitAssetTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");
        return dir!;
    }

    /// <summary>
    /// The production folder and the fixture folder. Both follow the same
    /// convention, so both get the same checks -- a fixture that resolves to
    /// nothing verifies nothing, and would do it silently.
    /// </summary>
    private static IEnumerable<string> AssetDirs()
    {
        foreach (string name in new[] { "units", "units-fixtures" })
        {
            string dir = Path.Combine(RepoRoot().FullName, "presentation", name);
            if (Directory.Exists(dir)) yield return dir;
        }
    }

    private static IEnumerable<string> AssetFolders()
        => AssetDirs().SelectMany(Directory.GetDirectories);

    private static HashSet<string> KnownContentIds()
    {
        string data = Path.Combine(RepoRoot().FullName, "content-data");

        var ids = new HashSet<string>();
        foreach (TowerDef tower in ContentLoader.LoadTowers(ReadAll(Path.Combine(data, "towers"))))
            ids.Add(tower.Id);
        foreach (EnemyDef enemy in ContentLoader.LoadEnemies(ReadAll(Path.Combine(data, "enemies"))))
            ids.Add(enemy.Id);

        Assert.True(ids.Count >= 2, "parsed suspiciously few content ids");
        return ids;
    }

    private static IEnumerable<(string name, string json)> ReadAll(string directory)
        => Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    /// <summary>
    /// Sprite strips in any format the loader reads. Must stay in step with
    /// <c>UnitAssets.StripFiles</c> — a test that only looked for `.png` would
    /// pass a webp-only folder by skipping it, which is the failure these tests
    /// exist to prevent.
    /// </summary>
    private static string[] StripFiles(string dir)
        => Directory.GetFiles(dir, "*.png").Concat(Directory.GetFiles(dir, "*.webp")).ToArray();

    [Fact]
    public void EveryUnitAssetFolderNamesARealContentId()
    {
        HashSet<string> known = KnownContentIds();

        foreach (string dir in AssetFolders())
        {
            string id = Path.GetFileName(dir);
            Assert.True(known.Contains(id),
                $"presentation/.../{id}/ matches no tower or enemy id, so it will never be used. " +
                $"Known: {string.Join(", ", known.OrderBy(x => x))}");
        }
    }

    [Fact]
    public void EveryUnitAssetFolderHasSomethingUsableInIt()
    {
        // Mirrors UnitAssets.Load: a .glb, or at least one standard clip strip.
        // A folder with neither is dropped at runtime with a console line nobody
        // reads, and the unit silently stays a placeholder.
        string[] standardClips = { "idle", "move", "fire", "hit", "death" };

        foreach (string dir in AssetFolders())
        {
            string id = Path.GetFileName(dir);

            bool hasModel = Directory.GetFiles(dir, "*.glb").Length > 0;
            bool hasClip = StripFiles(dir)
                .Any(f => standardClips.Contains(Path.GetFileNameWithoutExtension(f).ToLowerInvariant()));

            Assert.True(hasModel || hasClip,
                $"presentation/.../{id}/ has no .glb and no standard clip strip " +
                $"({string.Join(", ", standardClips)}), so it resolves to nothing.");
        }
    }

    [Fact]
    public void ASpriteFolderDeclaresItsFrameSize()
    {
        // frameCells is the one number a PNG cannot carry. It has a default, so
        // omitting it is legal -- but a sprite folder that relies on the default
        // is almost always a mistake, because the default cannot know how big the
        // unit is meant to be. Flag it here rather than let it ship the wrong size.
        foreach (string dir in AssetFolders())
        {
            if (Directory.GetFiles(dir, "*.glb").Length > 0) continue;   // mesh: scale is in the asset
            if (StripFiles(dir).Length == 0) continue;                   // covered by the test above

            string id = Path.GetFileName(dir);
            Assert.True(File.Exists(Path.Combine(dir, "unit.json")),
                $"presentation/.../{id}/ is a sprite folder with no unit.json, so it will render at " +
                $"the default frame size rather than the size it was drawn for.");
        }
    }
}
