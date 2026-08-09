using System.Reflection;
using System.Text.RegularExpressions;
using Gridfall.Core;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The point of SimStateView is a compile-time guarantee, and a guarantee nobody
/// checks decays into a convention. These tests are the check.
/// </summary>
public class SimStateViewTests
{
    [Fact]
    public void View_ExposesNoWritableMember()
    {
        var offenders = new List<string>();

        foreach (PropertyInfo p in typeof(SimStateView).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.CanWrite) offenders.Add($"settable property {p.Name}");

        foreach (FieldInfo f in typeof(SimStateView).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (!f.IsInitOnly) offenders.Add($"mutable field {f.Name}");

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Fact]
    public void View_HandsOutNoArrayOrReference()
    {
        // Returning SimState, or any array, would let a caller write through the
        // value it was given -- which is the whole thing this type prevents.
        var offenders = new List<string>();

        foreach (PropertyInfo p in typeof(SimStateView).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.PropertyType.IsArray || p.PropertyType == typeof(SimState))
                offenders.Add($"{p.Name} returns {p.PropertyType.Name}");

        foreach (MethodInfo m in typeof(SimStateView).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.DeclaringType != typeof(SimStateView)) continue;
            if (m.ReturnType.IsArray || m.ReturnType == typeof(SimState))
                offenders.Add($"{m.Name}() returns {m.ReturnType.Name}");
            if (m.ReturnType.IsByRef) offenders.Add($"{m.Name}() returns by ref");
        }

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Fact]
    public void Sim_State_IsTheReadOnlyView()
        => Assert.Equal(typeof(SimStateView), typeof(Sim).GetProperty("State")!.PropertyType);

    [Fact]
    public void Sim_MutableState_IsNotPublic()
    {
        PropertyInfo? mutable = typeof(Sim).GetProperty("MutableState",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(mutable);   // internal only -- the Godot project cannot see it
    }

    [Fact]
    public void View_ReadsTheSameValuesAsTheState()
    {
        Sim sim = TestContent.NewSim(TestContent.ArenaMap, seed: 5);
        sim.Enqueue(new BuildCommand(new GridCell(4, 3), sim.Content.StationIndexOf("arrow-station")));
        sim.Enqueue(new StartWaveCommand());
        for (int t = 0; t < 60; t++) sim.Tick();

        SimState raw = sim.MutableState;
        SimStateView view = sim.State;

        Assert.Equal(raw.Gold, view.Gold);
        Assert.Equal(raw.Patience, view.Patience);
        Assert.Equal(raw.VisitorCount, view.VisitorCount);
        Assert.Equal(raw.StationCount, view.StationCount);

        for (int k = 0; k < raw.VisitorCount; k++)
        {
            int slot = raw.VisitorSlotByOrder(k);
            Assert.Equal(slot, view.VisitorSlotByOrder(k));
            Assert.Equal(raw.VisitorId[slot], view.VisitorId(slot));
            Assert.Equal(raw.VisitorAppetite[slot], view.VisitorAppetite(slot));
            Assert.Equal(raw.VisitorCellIndex[slot], view.VisitorCellIndex(slot));
            Assert.Equal(raw.VisitorProgress[slot], view.VisitorProgress(slot));
        }
    }

    /// <summary>
    /// The boundary that actually matters. Core's internals are visible to the
    /// test suite and the harness on purpose; the renderer must never reach them.
    /// </summary>
    [Fact]
    public void TheGodotProject_NeverTouchesMutableState()
    {
        string godot = GodotSourceDirectory();
        var offenders = new List<string>();

        foreach (string path in Directory.EnumerateFiles(godot, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains("/obj/") || path.Contains("/bin/") || path.Contains("/.godot/")) continue;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(lines[i], @"\bMutableState\b"))
                    offenders.Add($"{Path.GetFileName(path)}:{i + 1}");
        }

        Assert.True(offenders.Count == 0,
            "The view layer must not reach mutable simulation state (ADR-0001): " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void TheGodotScan_ActuallyScansSomething()
    {
        // Guards the test above: a wrong path would make it pass trivially.
        int files = Directory.EnumerateFiles(GodotSourceDirectory(), "*.cs", SearchOption.AllDirectories)
            .Count(p => !p.Contains("/obj/") && !p.Contains("/bin/") && !p.Contains("/.godot/"));
        Assert.True(files >= 8, $"only found {files} C# files under godot/ -- the scan is not working");
    }

    private static string GodotSourceDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "godot")))
            dir = dir.Parent;

        Assert.True(dir is not null, "could not locate the repository root");
        return Path.Combine(dir!.FullName, "godot");
    }
}
