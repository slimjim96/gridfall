using System.Text.RegularExpressions;
using Xunit;

namespace Gridfall.Tests;

/// <summary>
/// The camera contract, checked where it can be: in the view's source.
///
/// The test project cannot reference the Godot assembly, so this reads the file
/// — the same approach MapThemeTests uses for the theme registry, and for the
/// same reason: a second copy of the rule here would be a second thing to forget.
/// </summary>
public class CameraContractTests
{
    private static string ViewSource(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "content-data")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate the repository root");

        string path = Path.Combine(new[] { dir!.FullName, "godot", "View" }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), $"not found: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void PanMarginCells_IsActuallyUsed()
    {
        // It was declared for months and read by nothing, while docs/iso-grid.md
        // described the clamp it was supposed to enforce. A constant nobody reads
        // is documentation pretending to be code.
        string isoGrid = ViewSource("IsoGrid.cs");

        int declarations = Regex.Matches(isoGrid, @"const float PanMarginCells").Count;
        int uses = Regex.Matches(isoGrid, @"\bPanMarginCells\b").Count - declarations;

        Assert.True(uses > 0,
            "PanMarginCells is declared but never read — the pan clamp is not implemented.");
    }

    [Fact]
    public void TheCameraRigNeverTouchesPitchOrYaw()
    {
        // iso-grid.md: zoom "never changes the pitch or yaw -- rotating the camera
        // off the contract angles breaks every art asset's implied lighting".
        // The rig moves a focus point and an ortho size, and nothing else.
        string rig = ViewSource("CameraRig.cs");

        Assert.DoesNotContain("CameraPitch =", rig);
        Assert.DoesNotContain("CameraYaw =", rig);
        Assert.DoesNotContain("RotateY", rig);
        Assert.DoesNotContain(".Basis =", rig);
    }

    [Fact]
    public void ShotModeCanLockTheCamera()
    {
        // Six committed captures depend on the board being framed identically
        // every run. If the rig ever stops honouring a lock, they all become
        // non-reproducible at once and nothing else would say so.
        string rig = ViewSource("CameraRig.cs");

        Assert.Contains("public bool Locked", rig);
        Assert.Contains("if (Locked) return", rig);
    }
}
