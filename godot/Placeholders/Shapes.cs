using Godot;

namespace Gridfall.View.Placeholders;

/// <summary>
/// The shape vocabulary from presentation/docs/placeholder-standard.md.
/// Procedural, cheap, and chosen so no two archetypes share a silhouette --
/// the one placeholder property that survives into the final art, and therefore
/// the only one worth caring about.
/// </summary>
public static class Shapes
{
    /// <summary>Tall thin prism -- precision, reach. Long-range stations.</summary>
    public static Mesh TallPrism(float width = 0.32f, float height = 1.5f)
        => new BoxMesh { Size = new Vector3(width, height, width) };

    /// <summary>Squat wide cylinder -- bulk, area. Splash stations.</summary>
    public static Mesh SquatCylinder(float radius = 0.34f, float height = 0.6f)
        => new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius * 1.15f,
            Height = height,
            RadialSegments = 12,
        };

    /// <summary>Tapered spire -- cold, still. Support stations.</summary>
    public static Mesh TaperedSpire(float radius = 0.26f, float height = 1.6f)
        => new CylinderMesh
        {
            TopRadius = radius * 0.25f,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 6,
        };

    /// <summary>Low sphere -- speed. Fast visitors.</summary>
    public static Mesh LowSphere(float radius = 0.22f)
        => new SphereMesh { Radius = radius, Height = radius * 1.6f, RadialSegments = 10, Rings = 6 };

    /// <summary>Broad box -- toughness. Fussinessed visitors.</summary>
    public static Mesh BroadBox(float width = 0.42f, float height = 0.36f)
        => new BoxMesh { Size = new Vector3(width, height, width * 0.8f) };

    /// <summary>Squat hexagonal drum -- heavy, plated. Fussinessed visitors.</summary>
    public static Mesh PlatedDrum(float radius = 0.26f, float height = 0.34f)
        => new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 6,
        };

    /// <summary>Stacked cones -- reads as a swarm unit. Small and fast.</summary>
    public static Mesh StackedCones(float radius = 0.15f)
        => new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = radius,
            Height = radius * 2.4f,
            RadialSegments = 5,
        };

    /// <summary>
    /// Inverted four-sided wedge, point down -- a drill. Reads as the thing that
    /// chews on your buildings.
    ///
    /// Every other visitor is rounded or points up, so "point down" is the whole
    /// signal: the one visitor that destroys stations must be identifiable at a
    /// glance, or a lost station has no visible cause (pillar 4).
    /// </summary>
    /// Tall and narrow rather than squat: the iso camera looks down, so a wide
    /// low cone shows almost nothing but its top face and reads as a flat plate.
    public static Mesh DrillWedge(float radius = 0.17f, float height = 0.58f)
        => new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = 0.0f,
            Height = height,
            RadialSegments = 4,
        };

    /// <summary>Small unshaded quad for projectiles.</summary>
    public static Mesh Pip(float size = 0.12f)
        => new SphereMesh { Radius = size, Height = size * 2f, RadialSegments = 6, Rings = 3 };

    /// <summary>A flat ground quad, for decals and range rings.</summary>
    public static Mesh GroundQuad(float size = 1.0f)
        => new QuadMesh { Size = new Vector2(size, size), Orientation = PlaneMesh.OrientationEnum.Y };
}
