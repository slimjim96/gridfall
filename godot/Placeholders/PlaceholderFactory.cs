using Godot;
using Gridfall.View.Units;

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
        // Short and thin: the cheap starter, and the shortest reach on the board.
        // Height reads as RANGE across the roster -- see placeholder-standard.md.
        "arrow-tower" => new PlaceholderUnitView(
            Shapes.TallPrism(0.30f, 0.85f), Palette.ForTower(contentId), 0.42f, bobs: false, entityId),

        // Tall and wide: the expensive one that reaches furthest and hits hardest.
        // Still unmistakable beside the arrow tower -- thin vs broad, short vs tall.
        "cannon" => new PlaceholderUnitView(
            Shapes.SquatCylinder(0.40f, 1.55f), Palette.ForTower(contentId), 0.78f, bobs: false, entityId),

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

        // Inverted wedge, point down, and the roster's only red: this one eats
        // your towers, so it is the one creep you must never mistake.
        "sapper" => new PlaceholderUnitView(
            Shapes.DrillWedge(0.17f, 0.58f), Palette.ForCreep(contentId), 0.30f, bobs: true, entityId),

        _ => new PlaceholderUnitView(
            Shapes.LowSphere(0.18f), Palette.CreepUnknown, 0.18f, bobs: true, entityId),
    };

    public static IUnitView CreateProjectile(int entityId)
        => new PlaceholderUnitView(Shapes.Pip(0.07f), Palette.Projectile, 0.45f, bobs: false, entityId);
}
