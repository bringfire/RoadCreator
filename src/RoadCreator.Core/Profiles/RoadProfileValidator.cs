namespace RoadCreator.Core.Profiles;

/// <summary>
/// Three-tier validation result for a RoadProfileDefinition.
/// </summary>
public sealed class RoadProfileValidationResult
{
    public bool StructuralValid { get; init; }
    public bool FootprintReady { get; init; }
    public bool RealizationReady { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Validates RoadProfileDefinition at three tiers.
/// Pure — no Rhino dependency. Does not mutate the profile.
/// </summary>
public static class RoadProfileValidator
{
    private static readonly HashSet<string> ValidFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        RoadProfileFeatureTypes.CenterlineReference,
        RoadProfileFeatureTypes.CarriagewayEdge,
        RoadProfileFeatureTypes.EdgeOfPavement,
        RoadProfileFeatureTypes.CurbFace,
        RoadProfileFeatureTypes.BikeLaneInner,
        RoadProfileFeatureTypes.BikeLaneOuter,
        RoadProfileFeatureTypes.SidewalkInner,
        RoadProfileFeatureTypes.SidewalkOuter,
        RoadProfileFeatureTypes.MedianEdge,
        RoadProfileFeatureTypes.LaneDivider,
        RoadProfileFeatureTypes.ShoulderEdge,
        RoadProfileFeatureTypes.RightOfWay,
        RoadProfileFeatureTypes.Ditch,
        RoadProfileFeatureTypes.Custom,
    };

    private static readonly HashSet<string> ValidSurfaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProfileSurfaceTypes.Pavement,
        ProfileSurfaceTypes.Sidewalk,
        ProfileSurfaceTypes.Median,
        ProfileSurfaceTypes.Shoulder,
    };

    private static readonly HashSet<string> ValidElementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProfileElementTypes.Curb,
        ProfileElementTypes.Guardrail,
        ProfileElementTypes.Barrier,
        ProfileElementTypes.Ditch,
    };

    public static RoadProfileValidationResult Validate(RoadProfileDefinition profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // ── Structural validation ────────────────────────────────────────

        if (string.IsNullOrWhiteSpace(profile.Name))
            errors.Add("name is required");

        if (profile.Features.Count == 0)
            errors.Add("features must contain at least one feature");

        if (!string.Equals(profile.Baseline, "centerline", StringComparison.OrdinalIgnoreCase))
            errors.Add($"baseline must be 'centerline' in v1 (got '{profile.Baseline}')");

        // Feature IDs unique
        var featureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in profile.Features)
        {
            if (string.IsNullOrWhiteSpace(f.Id))
                errors.Add("feature id is required");
            else if (!featureIds.Add(f.Id))
                errors.Add($"duplicate feature id: '{f.Id}'");

            if (!string.IsNullOrWhiteSpace(f.Type) && !ValidFeatureTypes.Contains(f.Type))
                errors.Add($"feature '{f.Id}' has unknown type '{f.Type}'");
        }

        // Surface validation
        foreach (var s in profile.Surfaces)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                errors.Add("surface id is required");

            if (s.Between.Count != 2)
                errors.Add($"surface '{s.Id}': between must have exactly 2 feature ids (got {s.Between.Count})");

            foreach (var refId in s.Between)
            {
                if (!featureIds.Contains(refId))
                    errors.Add($"surface '{s.Id}': references unknown feature '{refId}'");
            }

            if (!string.IsNullOrWhiteSpace(s.Type) && !ValidSurfaceTypes.Contains(s.Type))
                errors.Add($"surface '{s.Id}' has unknown type '{s.Type}'");
        }

        // Element validation
        foreach (var e in profile.Elements)
        {
            if (string.IsNullOrWhiteSpace(e.Id))
                errors.Add("element id is required");

            if (!featureIds.Contains(e.At))
                errors.Add($"element '{e.Id}': references unknown feature '{e.At}'");

            if (!string.IsNullOrWhiteSpace(e.Type) && !ValidElementTypes.Contains(e.Type))
                errors.Add($"element '{e.Id}' has unknown type '{e.Type}'");

            // Type-specific field validation
            ValidateElementFields(e, errors);
        }

        bool structuralValid = errors.Count == 0;

        // ── Footprint readiness ──────────────────────────────────────────

        bool hasNegative = profile.Features.Any(f => f.Offset < -1e-10);
        bool hasPositive = profile.Features.Any(f => f.Offset > 1e-10);
        bool footprintReady = structuralValid && hasNegative && hasPositive;

        if (structuralValid && !footprintReady)
            warnings.Add("Profile needs at least one negative and one positive offset feature for footprint projection");

        // ── Realization readiness ────────────────────────────────────────

        bool realizationReady = false;
        if (footprintReady)
        {
            bool hasSurfaces = profile.Surfaces.Count > 0;
            bool surfacesValid = profile.Surfaces.All(s =>
                s.Between.Count == 2 &&
                s.Between.All(featureIds.Contains));

            realizationReady = hasSurfaces && surfacesValid;

            if (!hasSurfaces)
                warnings.Add("No surfaces defined \u2014 profile is 2D-only");
        }

        return new RoadProfileValidationResult
        {
            StructuralValid = structuralValid,
            FootprintReady = footprintReady,
            RealizationReady = realizationReady,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private static void ValidateElementFields(ProfileElement element, List<string> errors)
    {
        switch (element.Type)
        {
            case ProfileElementTypes.Curb:
                if (element.Height == null)
                    errors.Add($"element '{element.Id}' (curb): height is required");
                if (element.TopWidth == null)
                    errors.Add($"element '{element.Id}' (curb): topWidth is required");
                break;

            case ProfileElementTypes.Barrier:
                if (string.IsNullOrWhiteSpace(element.Variant))
                    errors.Add($"element '{element.Id}' (barrier): variant is required");
                break;

            case ProfileElementTypes.Ditch:
                if (element.Depth == null)
                    errors.Add($"element '{element.Id}' (ditch): depth is required");
                if (element.Width == null)
                    errors.Add($"element '{element.Id}' (ditch): width is required");
                break;
        }
    }
}
