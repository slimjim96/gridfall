using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Gridfall.View.Units;

/// <summary>Which view implementation a content id resolves to.</summary>
public enum UnitAssetFormat
{
    /// <summary>No folder, or nothing usable in it. Procedural geometry.</summary>
    Placeholder,
    /// <summary>A `.glb` was found. <see cref="MeshUnitView"/>.</summary>
    Mesh,
    /// <summary>Clip strips were found. <see cref="SpriteUnitView"/>.</summary>
    Sprite,
}

/// <summary>One asset folder, already resolved to a format.</summary>
public sealed class UnitAsset
{
    public required string ContentId { get; init; }
    public required UnitAssetFormat Format { get; init; }
    public required string Directory { get; init; }

    /// <summary>Absolute path to the `.glb`, for <see cref="UnitAssetFormat.Mesh"/>.</summary>
    public string? ModelPath { get; init; }

    /// <summary>Clip name to strip path, for <see cref="UnitAssetFormat.Sprite"/>.</summary>
    public IReadOnlyDictionary<string, string> ClipStrips { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// World size of one square sprite frame, in cells.
    ///
    /// The one number that genuinely cannot be inferred from a PNG: the image
    /// says how many pixels it is, never how big the thing is meant to be. Read
    /// from `unit.json`, defaulting to a sane unit envelope so a folder of bare
    /// PNGs still works.
    /// </summary>
    public float FrameCells { get; init; } = DefaultFrameCells;

    public const float DefaultFrameCells = 2.0f;
}

/// <summary>
/// Final unit art read off disk: <c>presentation/units/[content-id]/</c>.
///
/// Deliberately the same shape as <see cref="Placeholders.TileLibrary"/> — a
/// folder named for the thing, discovered at runtime, no registration and no
/// Godot import step. If you understand how a tile theme works you already
/// understand this.
///
/// ## What decides the format
///
/// The folder's contents, not a config field:
///
/// | Folder holds | Format |
/// |---|---|
/// | a `.glb` | <see cref="MeshUnitView"/> |
/// | `idle.png`, `fire.png`, … | <see cref="SpriteUnitView"/> |
/// | neither, or no folder | placeholder, exactly as before |
///
/// This is ADR-0004's "asset format becomes a per-entity data field" satisfied by
/// convention rather than by a field. Nothing in `content-data` changes, nothing
/// in Core changes, and the sim cannot observe any of it — the same reason the
/// tile system was built this way.
///
/// A `.glb` wins if a folder somehow has both, and says so, because silently
/// picking one of two formats the author supplied is how you spend an afternoon
/// wondering which one you are looking at.
/// </summary>
public static class UnitAssets
{
    /// <summary>Repo-relative, resolved against ContentFiles.FindRepoRoot().</summary>
    public const string Folder = "presentation/units";

    /// <summary>
    /// The clips the view layer knows how to trigger — ludo-prompt-guide.md
    /// §"The standard clip set". A strip named anything else is loaded but can
    /// never play, so it is reported rather than silently ignored.
    /// </summary>
    public static readonly string[] StandardClips = { "idle", "move", "fire", "hit", "death" };

    /// <summary>Which of those loop. Also from the guide's table.</summary>
    public static bool Loops(string clip) => clip is "idle" or "move";

    private static readonly Dictionary<string, UnitAsset> Assets = new();

    public static IEnumerable<string> Ids => Assets.Keys;

    public static UnitAsset? For(string? contentId)
        => contentId is not null && Assets.TryGetValue(contentId, out UnitAsset? asset) ? asset : null;

    /// <summary>Bumped per scan, so caches keyed on loaded resources can drop them.</summary>
    public static int Generation { get; private set; }

    /// <summary>
    /// Read an asset folder. <paramref name="folderOverride"/> is repo-relative
    /// and exists for verification captures only.
    ///
    /// The override is not a convenience. Fixture assets under the production
    /// folder would change how the *shipped* game looks — the default shot seed
    /// builds arrow stations, so a fixture arrow station silently invalidates
    /// board-baseline, sapper-baseline and repair-baseline. Verification art has
    /// to be opt-in or it stops being verification and becomes an art decision
    /// nobody made.
    /// </summary>
    public static void Scan(string repoRoot, string? folderOverride = null)
    {
        Assets.Clear();
        Generation++;

        string folder = folderOverride ?? Folder;
        string root = Path.Combine(repoRoot, folder);
        if (!Directory.Exists(root))
        {
            GD.Print($"units: no {folder}/ -- every unit uses its placeholder");
            return;
        }

        // Ordinal sort so two machines enumerate identically.
        foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d, System.StringComparer.Ordinal))
        {
            UnitAsset? asset = Load(dir);
            if (asset is not null) Assets[asset.ContentId] = asset;
        }

        GD.Print(Assets.Count == 0
            ? $"units: {Folder}/ has no usable folders"
            : $"units: loaded {string.Join(", ", Assets.Select(a => $"{a.Key} ({a.Value.Format})"))}");
    }

    /// <summary>
    /// Sprite strips, in a fixed order: every `.png`, then every `.webp`.
    ///
    /// `SpriteUnitView` loads through `Image.LoadFromFile`, which reads both, so
    /// this glob was the only thing making the pipeline PNG-only -- a `.webp`
    /// dropped in a unit folder was not rejected, it was **invisible**, and the
    /// folder reported "no .glb and no standard clip strips" as though it were
    /// empty. Generators emit webp routinely; there is no reason to make someone
    /// find a converter to try one.
    ///
    /// PNG first so the order is stable across filesystems, and so the collision
    /// message above always names the same winner.
    /// </summary>
    private static IEnumerable<string> StripFiles(string directory)
        => Directory.GetFiles(directory, "*.png").OrderBy(f => f, System.StringComparer.Ordinal)
            .Concat(Directory.GetFiles(directory, "*.webp").OrderBy(f => f, System.StringComparer.Ordinal));

    private static UnitAsset? Load(string directory)
    {
        string id = Path.GetFileName(directory);

        string[] models = Directory.GetFiles(directory, "*.glb");
        System.Array.Sort(models, System.StringComparer.Ordinal);

        var strips = new Dictionary<string, string>();
        foreach (string file in StripFiles(directory))
        {
            string clip = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if (!StandardClips.Contains(clip))
            {
                GD.Print($"units: {id}/{Path.GetFileName(file)} -- '{clip}' is not a standard clip, ignored");
                continue;
            }
            // Deterministic on a collision, and said out loud. Two files for one
            // clip is an intermediate someone forgot to delete, and silently
            // picking one is how you spend an evening editing the wrong file.
            if (strips.TryGetValue(clip, out string? existing))
            {
                GD.Print($"units: {id}/ has {Path.GetFileName(existing)} and "
                         + $"{Path.GetFileName(file)} for '{clip}'; using {Path.GetFileName(existing)}");
                continue;
            }
            strips[clip] = file;
        }

        if (models.Length > 0)
        {
            if (strips.Count > 0)
                GD.Print($"units: {id} has both a .glb and sprite strips; using the .glb");

            return new UnitAsset
            {
                ContentId = id,
                Format = UnitAssetFormat.Mesh,
                Directory = directory,
                ModelPath = models[0],
            };
        }

        if (strips.Count == 0)
        {
            GD.Print($"units: ignoring {id}/ -- no .glb and no standard clip strips");
            return null;
        }

        return new UnitAsset
        {
            ContentId = id,
            Format = UnitAssetFormat.Sprite,
            Directory = directory,
            ClipStrips = strips,
            FrameCells = ReadFrameCells(directory, id),
        };
    }

    /// <summary>
    /// `unit.json` → `{ "frameCells": 1.7 }`. Absent, malformed, or out of range
    /// falls back to the default rather than throwing: a unit at the wrong size
    /// is a far better failure than a game that will not start, and the console
    /// says which happened.
    /// </summary>
    private static float ReadFrameCells(string directory, string id)
    {
        string path = Path.Combine(directory, "unit.json");
        if (!File.Exists(path)) return UnitAsset.DefaultFrameCells;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("frameCells", out System.Text.Json.JsonElement element))
                return UnitAsset.DefaultFrameCells;

            float cells = element.GetSingle();
            if (cells is > 0f and <= 16f) return cells;

            GD.PrintErr($"units: {id}/unit.json frameCells {cells} outside (0,16], using the default");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"units: {id}/unit.json unreadable ({ex.Message}), using the default");
        }

        return UnitAsset.DefaultFrameCells;
    }
}
