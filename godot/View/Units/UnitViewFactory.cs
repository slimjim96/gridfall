using Gridfall.View.Placeholders;

namespace Gridfall.View.Units;

/// <summary>
/// Content id → the right <see cref="IUnitView"/> for whatever art exists.
///
/// The single place the three implementations meet, and the whole of ADR-0004's
/// decision in one function: an asset folder decides the format, everything
/// without one keeps its placeholder, and nothing upstream of here knows which
/// happened.
///
/// A folder appearing under `presentation/units/` is therefore the entire act of
/// shipping a final asset. There is no case to add, no registration, and no
/// commit that has to remember to delete a placeholder branch — the placeholder
/// stops being reached the moment real art exists, and starts being reached
/// again if you move the folder away.
/// </summary>
public static class UnitViewFactory
{
    public static IUnitView CreateStation(string contentId, int entityId)
        => Create(contentId) ?? PlaceholderFactory.CreateStation(contentId, entityId);

    public static IUnitView CreateVisitor(string contentId, int entityId)
        => Create(contentId) ?? PlaceholderFactory.CreateVisitor(contentId, entityId);

    /// <summary>Null when there is no usable asset, which is the normal case today.</summary>
    private static IUnitView? Create(string contentId)
    {
        UnitAsset? asset = UnitAssets.For(contentId);
        if (asset is null) return null;

        return asset.Format switch
        {
            UnitAssetFormat.Mesh => new MeshUnitView(asset),
            UnitAssetFormat.Sprite => new SpriteUnitView(asset),
            _ => null,
        };
    }
}
