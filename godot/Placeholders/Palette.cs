using Godot;

namespace Gridfall.View.Placeholders;

/// <summary>
/// The palette slots from presentation/docs/art-direction.md. Placeholder colours
/// are the real colours -- the slot is the decision, the geometry is disposable.
/// </summary>
public static class Palette
{
    // Terrain: low-saturation and cool, never competing with a unit for attention.
    //
    // These were originally within ~20 raw levels of each other, which looked
    // reasonable as hex and was indistinguishable once rendered -- ACES tonemapping
    // plus ambient light compresses the dark end hard. Separated on the evidence of
    // an actual captured frame. Judge terrain contrast from a screenshot, never
    // from the hex values.
    public static readonly Color TerrainPathOnly = Color.FromHtml("323f4d");
    public static readonly Color TerrainBuildable = Color.FromHtml("55697d");
    public static readonly Color TerrainBlocked = Color.FromHtml("1b2229");
    public static readonly Color TerrainSpawn = Color.FromHtml("7a5aa0");
    public static readonly Color TerrainGoal = Color.FromHtml("46a07a");

    // Player towers: the only warm saturated things on the board.
    public static readonly Color TowerArrow = Color.FromHtml("d98f45");
    public static readonly Color TowerCannon = Color.FromHtml("c46a3a");
    public static readonly Color TowerFrost = Color.FromHtml("6fc7d9");
    public static readonly Color TowerUnknown = Color.FromHtml("b0864f");

    // Creeps: hue carries threat tier, cool to hot.
    public static readonly Color CreepRunner = Color.FromHtml("7fc4a8");
    public static readonly Color CreepBrute = Color.FromHtml("b8a05a");
    public static readonly Color CreepUnknown = Color.FromHtml("9aa8b8");

    /// <summary>
    /// One red, used nowhere else. Reserved for danger and refusal -- if red
    /// means three things it means nothing.
    /// </summary>
    public static readonly Color Danger = Color.FromHtml("e2483d");

    /// <summary>
    /// The route creeps currently take. Cool and quiet -- it is always on screen --
    /// but it has to clear the buildable terrain it sits on, and the first attempt
    /// (7f96ad) was close enough to that slate to vanish into it.
    /// </summary>
    public static readonly Color RouteLive = Color.FromHtml("cfe2f2");

    /// <summary>The route a pending build would create. Brighter: it is the answer to a question you just asked.</summary>
    public static readonly Color RoutePreview = Color.FromHtml("e8c46a");

    public static readonly Color Projectile = Color.FromHtml("f2e6c8");
    public static readonly Color HitFlash = Color.FromHtml("ffffff");
    public static readonly Color BuildPreviewOk = Color.FromHtml("8fd98f");

    public static Color ForTower(string contentId) => contentId switch
    {
        "arrow-tower" => TowerArrow,
        "cannon" => TowerCannon,
        "frost-spire" => TowerFrost,
        _ => TowerUnknown,
    };

    public static Color ForCreep(string contentId) => contentId switch
    {
        "runner" => CreepRunner,
        "brute" => CreepBrute,
        _ => CreepUnknown,
    };

    /// <summary>
    /// Flat matte, no specular. The art direction is readable solids, not
    /// spectacle -- and a shiny placeholder reads as a considered art decision,
    /// which invites feedback nobody wants to give or receive.
    /// </summary>
    public static StandardMaterial3D Matte(Color albedo, bool unshaded = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = albedo,
            Roughness = 1.0f,
            Metallic = 0.0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
        if (unshaded) material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        return material;
    }
}
