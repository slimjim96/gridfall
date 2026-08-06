using System.Text.RegularExpressions;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The structural invariants from the verification workflow, as tests rather
/// than as a promise. ADR-0002's whole argument is that a grep is a complete
/// audit -- so the grep should run automatically, not when someone remembers.
/// </summary>
public class SourcePurityTests
{
    private static string CoreDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Gridfall.Core")))
            dir = dir.Parent;

        Assert.True(dir is not null, "could not locate the repository root");
        return Path.Combine(dir!.FullName, "Gridfall.Core");
    }

    private static IEnumerable<(string file, int line, string text)> CoreLines()
    {
        foreach (string path in Directory.EnumerateFiles(CoreDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i];
                string trimmed = text.TrimStart();
                // Comments discuss the banned constructs on purpose.
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*")) continue;
                yield return (Path.GetFileName(path), i + 1, text);
            }
        }
    }

    private static void AssertAbsent(string pattern, string why, params string[] allowedFiles)
    {
        var regex = new Regex(pattern);
        var hits = CoreLines()
            .Where(l => regex.IsMatch(l.text) && !allowedFiles.Contains(l.file))
            .Select(l => $"{l.file}:{l.line}: {l.text.Trim()}")
            .ToList();

        Assert.True(hits.Count == 0, $"{why}\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Core_ContainsNoFloatOrDouble()
        // Fix32.ToFloat is the documented boundary conversion for the view layer.
        => AssertAbsent(@"\b(float|double)\b",
            "Gridfall.Core must contain no floating point (ADR-0002).",
            "Fix32.cs");

    [Fact]
    public void Core_ContainsNoSystemRandom()
        => AssertAbsent(@"\bnew Random\b|System\.Random",
            "Gridfall.Core must use SimRandom only.");

    [Fact]
    public void Core_ContainsNoClock()
        => AssertAbsent(@"\bDateTime\b|\bStopwatch\b|Environment\.TickCount",
            "Gridfall.Core must not read the clock.");

    [Fact]
    public void Core_ContainsNoGuid()
        => AssertAbsent(@"\bGuid\b", "Gridfall.Core must not use Guid -- entity ids are sequential ints.");

    [Fact]
    public void Core_ContainsNoParallelism()
        => AssertAbsent(@"\bParallel\.|\bAsParallel\b|\bTask\.Run\b",
            "Gridfall.Core is single-threaded by design.");

    [Fact]
    public void Core_ReferencesNoGodotType()
        => AssertAbsent(@"\bGodot\b", "Gridfall.Core must never reference Godot (ADR-0001).");

    [Fact]
    public void CoreProject_ReferencesNoGodotPackage()
    {
        string csproj = File.ReadAllText(Path.Combine(CoreDirectory(), "Gridfall.Core.csproj"));
        // Strip XML comments first: the csproj explains *why* it is not a Godot
        // project, and that explanation names Godot.
        string withoutComments = Regex.Replace(csproj, "<!--.*?-->", "", RegexOptions.Singleline);

        Assert.DoesNotContain("GodotSharp", withoutComments, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Godot.NET.Sdk", withoutComments, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Godot", withoutComments, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Core_DoesNotIterateDictionariesOrHashSets()
        => AssertAbsent(@"foreach\s*\([^)]*\bin\b[^)]*\.(Keys|Values)\b",
            "Iteration order of a hash container is not stable state (engine guide 08).");

    [Fact]
    public void Core_DoesNotTouchTheFilesystem()
        => AssertAbsent(@"\bFile\.|\bDirectory\.|\bPath\.Combine\b",
            "Core never touches the filesystem -- ContentLoader receives strings (engine guide 07).");

    [Fact]
    public void TheAuditActuallyScansSomething()
    {
        // Guards every test above: a broken path finder would make them all pass.
        int files = CoreLines().Select(l => l.file).Distinct().Count();
        Assert.True(files >= 15, $"only found {files} source files in Gridfall.Core -- the audit is not scanning");
    }
}
