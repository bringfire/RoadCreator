using RoadCreator.Core.Footprint;

namespace RoadCreator.Core.Profiles;

/// <summary>
/// Result of projecting a RoadProfileDefinition to an OffsetProfile.
/// </summary>
public sealed class RoadProfileProjectionResult
{
    public OffsetProfile OffsetProfile { get; init; } = null!;
    public int DroppedSurfaces { get; init; }
    public int DroppedElements { get; init; }
}

/// <summary>
/// Projects RoadProfileDefinition down to OffsetProfile for 2D consumers.
/// Pure — no Rhino dependency. Does not guess missing semantics.
/// </summary>
public static class RoadProfileProjector
{
    /// <summary>
    /// Project a RoadProfileDefinition to an OffsetProfile.
    /// Features become OffsetFeatures. Surfaces and elements are dropped.
    /// </summary>
    public static RoadProfileProjectionResult Project(RoadProfileDefinition profile)
    {
        var features = profile.Features.Select(f => new OffsetFeature(
            Id: f.Id,
            Offset: f.Offset,
            Role: f.Type,
            StyleRef: f.StyleRef ?? ""
        )).ToList();

        var offsetProfile = new OffsetProfile(
            name: profile.Name,
            units: profile.Units,
            baseline: profile.Baseline,
            features: features
        );

        return new RoadProfileProjectionResult
        {
            OffsetProfile = offsetProfile,
            DroppedSurfaces = profile.Surfaces.Count,
            DroppedElements = profile.Elements.Count,
        };
    }
}
