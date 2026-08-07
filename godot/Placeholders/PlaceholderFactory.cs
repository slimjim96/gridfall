using Godot;

namespace Gridfall.View.Placeholders;

/// <summary>
/// Content id -> a view. One case per id.
///
/// When a final asset arrives, its case returns a SpriteUnitView or a
/// MeshUnitView and the placeholder branch is deleted in the same commit --
/// not left behind "in case" (placeholder-standard.md).
/// </summary>
public static class PlaceholderFactory
{
    public static IUnitView CreateTower(string contentId, int entityId) => contentId switch
    {
        // Tall and thin: precision and reach.
        "arrow-tower" => new PlaceholderUnitView(
            Shapes.TallPrism(0.30f, 1.45f), Palette.ForTower(contentId), 0.72f, bobs: false, entityId),

        // Squat and wide: bulk and area. Unmistakable next to the arrow tower.
        "cannon" => new PlaceholderUnitView(
            Shapes.SquatCylinder(0.36f, 0.62f), Palette.ForTower(contentId), 0.31f, bobs: false, entityId),

        // Tapered hex spire: cold and still. Taller and thinner than either.
        "frost-spire" => new PlaceholderUnitView(
            Shapes.TaperedSpire(0.26f, 1.60f), Palette.ForTower(contentId), 0.80f, bobs: false, entityId),

        _ => new PlaceholderUnitView(
            Shapes.TallPrism(0.28f, 1.0f), Palette.TowerUnknown, 0.50f, bobs: false, entityId),
    };

    public static IUnitView CreateCreep(string contentId, int entityId) => contentId switch
    {
        // Low sphere: speed.
        "runner" => new PlaceholderUnitView(
            Shapes.LowSphere(0.20f), Palette.ForCreep(contentId), 0.20f, bobs: true, entityId),

        // Broad box: toughness. Different shape, not a bigger sphere.
        "brute" => new PlaceholderUnitView(
            Shapes.BroadBox(0.44f, 0.38f), Palette.ForCreep(contentId), 0.22f, bobs: true, entityId),

        // Squat plated drum: heavy, and unmistakable beside a sphere or a box.
        "husk" => new PlaceholderUnitView(
            Shapes.PlatedDrum(0.26f, 0.34f), Palette.ForCreep(contentId), 0.19f, bobs: true, entityId),

        // Stacked cones: small and spiky, reads as one of many.
        "mite" => new PlaceholderUnitView(
            Shapes.StackedCones(0.15f), Palette.ForCreep(contentId), 0.18f, bobs: true, entityId),

        _ => new PlaceholderUnitView(
            Shapes.LowSphere(0.18f), Palette.CreepUnknown, 0.18f, bobs: true, entityId),
    };

    public static IUnitView CreateProjectile(int entityId)
        => new PlaceholderUnitView(Shapes.Pip(0.07f), Palette.Projectile, 0.45f, bobs: false, entityId);
}
