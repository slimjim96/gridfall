using Gridfall.Core.Content;

namespace Gridfall.Verify;

/// <summary>
/// Reads content-data/ off disk and hands Core finished objects. Core itself
/// never touches the filesystem -- this is the boundary (engine guide 07).
/// </summary>
public static class ContentFiles
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException(
            "Could not find content-data/ above " + AppContext.BaseDirectory);
    }

    public static ContentSet LoadContent(string root, string mapId)
    {
        string data = Path.Combine(root, "content-data");

        TowerDef[] towers = ContentLoader.LoadTowers(ReadAll(Path.Combine(data, "towers")));
        EnemyDef[] enemies = ContentLoader.LoadEnemies(ReadAll(Path.Combine(data, "enemies")));

        string wavePath = Path.Combine(data, "waves", mapId + ".json");
        WaveDef[] waves = File.Exists(wavePath)
            ? ContentLoader.LoadWaves(File.ReadAllText(wavePath), enemies, wavePath)
            : Array.Empty<WaveDef>();

        return new ContentSet { Towers = towers, Enemies = enemies, Waves = waves };
    }

    public static MapDef LoadMap(string root, string mapId)
    {
        string path = Path.Combine(root, "content-data", "maps", mapId + ".json");
        return ContentLoader.LoadMap(File.ReadAllText(path), path);
    }

    public static IEnumerable<string> MapIds(string root)
        => Directory.EnumerateFiles(Path.Combine(root, "content-data", "maps"), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => id is not null)
            .Select(id => id!)
            .OrderBy(id => id, StringComparer.Ordinal);

    private static List<(string name, string json)> ReadAll(string directory)
    {
        var files = new List<(string, string)>();
        if (!Directory.Exists(directory)) return files;

        // Sorted so the caller sees a stable order; ContentLoader sorts by id
        // anyway, but a stable input order makes failures reproducible.
        foreach (string path in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            files.Add((Path.GetFileName(path), File.ReadAllText(path)));
        }
        return files;
    }
}
