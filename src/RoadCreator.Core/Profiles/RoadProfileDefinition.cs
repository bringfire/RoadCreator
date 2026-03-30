namespace RoadCreator.Core.Profiles;

public static class RoadProfileSchemas
{
    public const string DefinitionV1 = "roadcreator.road-profile/v1";
    public const string CompactV1 = "roadcreator.road-profile.compact/v1";
}

public static class RoadProfileFeatureTypes
{
    public const string CenterlineReference = "centerline_reference";
    public const string CarriagewayEdge = "carriageway_edge";
    public const string EdgeOfPavement = "edge_of_pavement";
    public const string CurbFace = "curb_face";
    public const string BikeLaneInner = "bike_lane_inner";
    public const string BikeLaneOuter = "bike_lane_outer";
    public const string SidewalkInner = "sidewalk_inner";
    public const string SidewalkOuter = "sidewalk_outer";
    public const string MedianEdge = "median_edge";
    public const string LaneDivider = "lane_divider";
    public const string ShoulderEdge = "shoulder_edge";
    public const string RightOfWay = "row";
    public const string Ditch = "ditch";
    public const string Custom = "custom";
}

public static class ProfileSurfaceTypes
{
    public const string Pavement = "pavement";
    public const string Sidewalk = "sidewalk";
    public const string Median = "median";
    public const string Shoulder = "shoulder";
}

public static class ProfileElementTypes
{
    public const string Curb = "curb";
    public const string Guardrail = "guardrail";
    public const string Barrier = "barrier";
    public const string Ditch = "ditch";
}

public static class RoadProfileBoundaryRoles
{
    public const string CarriagewaySurface = "carriageway_surface";
    public const string CurbReturnDriver = "curb_return_driver";
    public const string EdgeOfPavement = "edge_of_pavement";
    public const string OuterEnvelope = "outer_envelope";
    public const string IntersectionTopologyCandidate = "intersection_topology_candidate";
}

public sealed class RoadProfileDefinition
{
    public string Schema { get; init; } = RoadProfileSchemas.DefinitionV1;
    public string Name { get; init; } = "";
    public int? Version { get; init; }
    public string Units { get; init; } = "m";
    public string Description { get; init; } = "";
    public bool Symmetric { get; init; } = true;
    public string Baseline { get; init; } = "centerline";
    public double? TotalWidth { get; init; }
    public RoadProfileSourceMetadata? Source { get; init; }
    public List<RoadProfileFeatureDefinition> Features { get; init; } = new();
    public List<ProfileSurface> Surfaces { get; init; } = new();
    public List<ProfileElement> Elements { get; init; } = new();
    public Dictionary<string, string> LayerMap { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public RoadProfileCrossSectionDefaults? CrossSectionDefaults { get; init; }
    public RoadProfileIntersectionDefaults? IntersectionDefaults { get; init; }

    public IEnumerable<RoadProfileFeatureDefinition> OrderedFeatures =>
        Features.OrderBy(feature => feature.Order).ThenBy(feature => feature.Id, StringComparer.Ordinal);

    public bool HasUnilateralFeatures => Features.Any(feature => !feature.Bilateral);

    public double MaxOffset =>
        Features.Count == 0 ? 0.0 : Features.Max(feature => global::System.Math.Abs(feature.Offset));

    public IEnumerable<RoadProfileFeatureDefinition> GetFeaturesByType(string type) =>
        Features.Where(feature => string.Equals(feature.Type, type, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RoadProfileFeatureDefinition> GetFeaturesByBoundaryRole(string boundaryRole) =>
        Features.Where(feature => feature.BoundaryRoles.Any(
            role => string.Equals(role, boundaryRole, StringComparison.OrdinalIgnoreCase)));
}

public sealed class RoadProfileFeatureDefinition
{
    public string Id { get; init; } = "";
    public int Order { get; init; }
    public string Type { get; init; } = "";
    public double Offset { get; init; }
    public double Width { get; init; }
    public bool Bilateral { get; init; } = true;
    public string Label { get; init; } = "";
    public string? Baseline { get; init; }
    public string? StyleRef { get; init; }
    public string? Side { get; init; }
    public List<string> BoundaryRoles { get; init; } = new();
    public bool EligibleForIntersectionTopology { get; init; }
}

public sealed class ProfileSurface
{
    public string Id { get; init; } = "";
    public List<string> Between { get; init; } = new();
    public string Type { get; init; } = "";
    public double? Crossfall { get; init; }
}

public sealed class ProfileElement
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string At { get; init; } = "";
    public string? Side { get; init; }

    // Curb-specific
    public double? Height { get; init; }
    public double? TopWidth { get; init; }

    // Guardrail-specific
    public double? PostSpacing { get; init; }

    // Barrier-specific
    public string? Variant { get; init; }

    // Ditch-specific
    public double? Depth { get; init; }
    public double? Width { get; init; }
}

public sealed class RoadProfileSourceMetadata
{
    public string? Project { get; init; }
    public string? File { get; init; }
    public string? ExtractedAt { get; init; }
    public string? CenterlineId { get; init; }
    public double? Chainage { get; init; }
}

public sealed class RoadProfileCrossSectionDefaults
{
    public string? RoadCategoryCode { get; init; }
    public double? CrossfallStraight { get; init; }
    public double? CrossfallCurve { get; init; }
    public bool? IncludeVerge { get; init; }
    public double? VergeWidth { get; init; }
}

public sealed class RoadProfileIntersectionDefaults
{
    public double? ArmLengthOuterEnvelopeMultiplier { get; init; }
    public double? ArmLengthCarriagewayMultiplier { get; init; }
    public double? ArmLengthDiagonalMultiplier { get; init; }
    public double? ArmLengthRadiusMultiplier { get; init; }
    public double? ArmLengthMin { get; init; }
    public double? ArmLengthMax { get; init; }
}

public sealed class RoadProfileSummary
{
    public string Name { get; init; } = "";
    public bool Symmetric { get; init; }
    public bool HasUnilateralFeatures { get; init; }
    public bool RequiresSideSelection { get; init; }
    public double TotalWidth { get; init; }
    public double MaxOffset { get; init; }
    public double CarriagewaySurfaceOffset { get; init; }
    public double CurbReturnDriverOffset { get; init; }
    public double EdgeOfPavementOffset { get; init; }
    public double OuterEnvelopeOffset { get; init; }
}

public static class RoadProfileSummaryBuilder
{
    public static RoadProfileSummary Build(RoadProfileDefinition profile)
    {
        var maxOffset = profile.MaxOffset;
        var carriagewaySurfaceOffset = ResolveOffset(
            profile,
            RoadProfileBoundaryRoles.CarriagewaySurface,
            RoadProfileFeatureTypes.CarriagewayEdge);
        var curbReturnDriverOffset = ResolveOffset(
            profile,
            RoadProfileBoundaryRoles.CurbReturnDriver,
            RoadProfileFeatureTypes.CurbFace,
            RoadProfileFeatureTypes.CarriagewayEdge);
        var edgeOfPavementOffset = ResolveOffset(
            profile,
            RoadProfileBoundaryRoles.EdgeOfPavement,
            RoadProfileFeatureTypes.EdgeOfPavement);
        if (edgeOfPavementOffset <= 0.0)
            edgeOfPavementOffset = carriagewaySurfaceOffset;

        var outerEnvelopeOffset = ResolveOffset(
            profile,
            RoadProfileBoundaryRoles.OuterEnvelope,
            RoadProfileFeatureTypes.RightOfWay);
        if (outerEnvelopeOffset <= 0.0)
            outerEnvelopeOffset = maxOffset;

        return new RoadProfileSummary
        {
            Name = profile.Name,
            Symmetric = profile.Symmetric,
            HasUnilateralFeatures = profile.HasUnilateralFeatures,
            RequiresSideSelection = !profile.Symmetric && profile.HasUnilateralFeatures,
            TotalWidth = profile.TotalWidth ?? 0.0,
            MaxOffset = maxOffset,
            CarriagewaySurfaceOffset = carriagewaySurfaceOffset,
            CurbReturnDriverOffset = curbReturnDriverOffset,
            EdgeOfPavementOffset = edgeOfPavementOffset,
            OuterEnvelopeOffset = outerEnvelopeOffset,
        };
    }

    private static double ResolveOffset(
        RoadProfileDefinition profile,
        string boundaryRole,
        params string[] fallbackFeatureTypes)
    {
        var boundaryRoleOffset = profile
            .GetFeaturesByBoundaryRole(boundaryRole)
            .Select(feature => global::System.Math.Abs(feature.Offset))
            .DefaultIfEmpty(0.0)
            .Max();
        if (boundaryRoleOffset > 0.0)
            return boundaryRoleOffset;

        return fallbackFeatureTypes
            .SelectMany(profile.GetFeaturesByType)
            .Select(feature => global::System.Math.Abs(feature.Offset))
            .DefaultIfEmpty(0.0)
            .Max();
    }
}
