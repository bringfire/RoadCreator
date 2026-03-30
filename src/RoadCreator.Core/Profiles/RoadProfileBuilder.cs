namespace RoadCreator.Core.Profiles;

/// <summary>
/// Result of a profile build operation, including provenance from observation mode.
/// </summary>
public sealed class RoadProfileBuildResult
{
    public RoadProfileDefinition Profile { get; init; } = null!;
    public RoadProfileValidationResult Validation { get; init; } = null!;
    public List<string> Warnings { get; init; } = new();
    public string ExpandedFrom { get; init; } = "features";
    public List<FeatureProvenance>? FeatureProvenance { get; init; }
}

public sealed class FeatureProvenance
{
    public string FeatureId { get; init; } = "";
    public string? CurveId { get; init; }
    public string? Layer { get; init; }
    public string? ObjectName { get; init; }
}

/// <summary>
/// Observation from rc_extract_offsets, annotated with agent-supplied role.
/// </summary>
public sealed class ProfileObservation
{
    public string? CurveId { get; init; }
    public string? Layer { get; init; }
    public string? ObjectName { get; init; }
    public string? Side { get; init; }
    public double Offset { get; init; }
    public string? Role { get; init; }
    public string? StyleRef { get; init; }
    public string? Id { get; init; }
}

/// <summary>
/// Semantic recipe input for Mode B builder expansion.
/// </summary>
public sealed class ProfileRecipeInput
{
    public string Name { get; set; } = "";
    public double LaneWidth { get; set; }
    public int LanesPerDirection { get; set; } = 1;
    public CurbRecipe? Curb { get; set; }
    public SidewalkRecipe? Sidewalk { get; set; }
    public ShoulderRecipe? Shoulder { get; set; }
    public MedianRecipe? Median { get; set; }
    public GuardrailRecipe? Guardrail { get; set; }
}

public sealed class CurbRecipe
{
    public double Height { get; init; }
    public double TopWidth { get; init; }
}

public sealed class SidewalkRecipe
{
    public double Width { get; init; }
}

public sealed class ShoulderRecipe
{
    public double Width { get; init; }
}

public sealed class MedianRecipe
{
    public double Width { get; init; }
}

public sealed class GuardrailRecipe
{
    public double? PostSpacing { get; init; }
}

/// <summary>
/// Builds canonical RoadProfileDefinition from three input modes.
/// Pure — no Rhino dependency.
/// </summary>
public static class RoadProfileBuilder
{
    /// <summary>
    /// Mode A: Build from explicit features (+ optional surfaces/elements).
    /// Normalizes defaults, infers side from offset sign.
    /// </summary>
    public static RoadProfileBuildResult BuildFromFeatures(
        string name,
        List<RoadProfileFeatureDefinition> features,
        List<ProfileSurface>? surfaces = null,
        List<ProfileElement>? elements = null,
        bool? symmetric = null,
        string? units = null)
    {
        // Infer side from offset sign when missing
        var normalizedFeatures = features.Select((f, i) => new RoadProfileFeatureDefinition
        {
            Id = string.IsNullOrWhiteSpace(f.Id) ? $"{f.Type}-{i}" : f.Id,
            Order = f.Order > 0 ? f.Order : i,
            Type = f.Type,
            Offset = f.Offset,
            Width = f.Width,
            Bilateral = f.Bilateral,
            Label = f.Label,
            Baseline = f.Baseline,
            StyleRef = f.StyleRef,
            Side = f.Side ?? InferSide(f.Offset),
            BoundaryRoles = f.BoundaryRoles.Count > 0 ? f.BoundaryRoles : InferBoundaryRoles(f.Type),
            EligibleForIntersectionTopology = f.EligibleForIntersectionTopology,
        }).ToList();

        var profile = new RoadProfileDefinition
        {
            Schema = RoadProfileSchemas.DefinitionV1,
            Name = name,
            Units = units ?? "m",
            Baseline = "centerline",
            Symmetric = symmetric ?? InferSymmetric(normalizedFeatures),
            Features = normalizedFeatures,
            Surfaces = surfaces ?? new(),
            Elements = elements ?? new(),
        };

        var validation = RoadProfileValidator.Validate(profile);

        return new RoadProfileBuildResult
        {
            Profile = profile,
            Validation = validation,
            Warnings = validation.Warnings.ToList(),
            ExpandedFrom = "features",
        };
    }

    /// <summary>
    /// Mode B: Build from semantic recipe.
    /// Expands lane/curb/sidewalk/shoulder/median/guardrail specs into features + surfaces + elements.
    /// </summary>
    public static RoadProfileBuildResult BuildFromRecipe(ProfileRecipeInput recipe)
    {
        var features = new List<RoadProfileFeatureDefinition>();
        var surfaces = new List<ProfileSurface>();
        var elements = new List<ProfileElement>();
        int order = 0;

        // Centerline
        features.Add(new RoadProfileFeatureDefinition
        {
            Id = "cl",
            Order = order++,
            Type = RoadProfileFeatureTypes.CenterlineReference,
            Offset = 0.0,
            StyleRef = "centerline",
            BoundaryRoles = new(),
        });

        // Edge of pavement at ±(laneWidth * lanesPerDirection)
        double eopOffset = recipe.LaneWidth * System.Math.Max(1, recipe.LanesPerDirection);

        features.Add(new RoadProfileFeatureDefinition
        {
            Id = "eop_l",
            Order = order++,
            Type = RoadProfileFeatureTypes.EdgeOfPavement,
            Offset = -eopOffset,
            Width = eopOffset,
            Side = "left",
            StyleRef = "edge",
            BoundaryRoles = InferBoundaryRoles(RoadProfileFeatureTypes.EdgeOfPavement),
            EligibleForIntersectionTopology = true,
        });

        features.Add(new RoadProfileFeatureDefinition
        {
            Id = "eop_r",
            Order = order++,
            Type = RoadProfileFeatureTypes.EdgeOfPavement,
            Offset = eopOffset,
            Width = eopOffset,
            Side = "right",
            StyleRef = "edge",
            BoundaryRoles = InferBoundaryRoles(RoadProfileFeatureTypes.EdgeOfPavement),
            EligibleForIntersectionTopology = true,
        });

        // Carriageway surface
        surfaces.Add(new ProfileSurface
        {
            Id = "carriageway",
            Between = new List<string> { "eop_l", "eop_r" },
            Type = ProfileSurfaceTypes.Pavement,
        });

        double currentOffset = eopOffset;

        // Curb
        if (recipe.Curb != null)
        {
            double curbOffset = currentOffset + recipe.Curb.TopWidth;

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "curb_l",
                Order = order++,
                Type = RoadProfileFeatureTypes.CurbFace,
                Offset = -curbOffset,
                Side = "left",
                StyleRef = "curb",
                BoundaryRoles = InferBoundaryRoles(RoadProfileFeatureTypes.CurbFace),
            });

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "curb_r",
                Order = order++,
                Type = RoadProfileFeatureTypes.CurbFace,
                Offset = curbOffset,
                Side = "right",
                StyleRef = "curb",
                BoundaryRoles = InferBoundaryRoles(RoadProfileFeatureTypes.CurbFace),
            });

            elements.Add(new ProfileElement
            {
                Id = "curb_left",
                Type = ProfileElementTypes.Curb,
                At = "curb_l",
                Side = "left",
                Height = recipe.Curb.Height,
                TopWidth = recipe.Curb.TopWidth,
            });

            elements.Add(new ProfileElement
            {
                Id = "curb_right",
                Type = ProfileElementTypes.Curb,
                At = "curb_r",
                Side = "right",
                Height = recipe.Curb.Height,
                TopWidth = recipe.Curb.TopWidth,
            });

            currentOffset = curbOffset;
        }

        // Sidewalk
        if (recipe.Sidewalk != null)
        {
            double swOffset = currentOffset + recipe.Sidewalk.Width;

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "sw_l",
                Order = order++,
                Type = RoadProfileFeatureTypes.SidewalkOuter,
                Offset = -swOffset,
                Side = "left",
                StyleRef = "sidewalk",
                BoundaryRoles = new(),
            });

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "sw_r",
                Order = order++,
                Type = RoadProfileFeatureTypes.SidewalkOuter,
                Offset = swOffset,
                Side = "right",
                StyleRef = "sidewalk",
                BoundaryRoles = new(),
            });

            // Sidewalk surfaces between curb (or edge) and sidewalk back
            string innerLeft = recipe.Curb != null ? "curb_l" : "eop_l";
            string innerRight = recipe.Curb != null ? "curb_r" : "eop_r";

            surfaces.Add(new ProfileSurface
            {
                Id = "sw_left",
                Between = new List<string> { innerLeft, "sw_l" },
                Type = ProfileSurfaceTypes.Sidewalk,
            });

            surfaces.Add(new ProfileSurface
            {
                Id = "sw_right",
                Between = new List<string> { innerRight, "sw_r" },
                Type = ProfileSurfaceTypes.Sidewalk,
            });

            currentOffset = swOffset;
        }

        // Shoulder
        if (recipe.Shoulder != null)
        {
            double shOffset = currentOffset + recipe.Shoulder.Width;

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "sh_l",
                Order = order++,
                Type = RoadProfileFeatureTypes.ShoulderEdge,
                Offset = -shOffset,
                Side = "left",
                StyleRef = "shoulder",
                BoundaryRoles = new(),
            });

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "sh_r",
                Order = order++,
                Type = RoadProfileFeatureTypes.ShoulderEdge,
                Offset = shOffset,
                Side = "right",
                StyleRef = "shoulder",
                BoundaryRoles = new(),
            });

            surfaces.Add(new ProfileSurface
            {
                Id = "sh_left",
                Between = new List<string> { features[^2].Id == "sh_l" ? (recipe.Sidewalk != null ? "sw_l" : recipe.Curb != null ? "curb_l" : "eop_l") : "eop_l", "sh_l" },
                Type = ProfileSurfaceTypes.Shoulder,
            });

            surfaces.Add(new ProfileSurface
            {
                Id = "sh_right",
                Between = new List<string> { recipe.Sidewalk != null ? "sw_r" : recipe.Curb != null ? "curb_r" : "eop_r", "sh_r" },
                Type = ProfileSurfaceTypes.Shoulder,
            });

            currentOffset = shOffset;
        }

        // Median
        if (recipe.Median != null)
        {
            double halfMedian = recipe.Median.Width / 2.0;

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "med_l",
                Order = order++,
                Type = RoadProfileFeatureTypes.MedianEdge,
                Offset = -halfMedian,
                Side = "left",
                StyleRef = "median",
                BoundaryRoles = new(),
            });

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = "med_r",
                Order = order++,
                Type = RoadProfileFeatureTypes.MedianEdge,
                Offset = halfMedian,
                Side = "right",
                StyleRef = "median",
                BoundaryRoles = new(),
            });

            surfaces.Add(new ProfileSurface
            {
                Id = "median",
                Between = new List<string> { "med_l", "med_r" },
                Type = ProfileSurfaceTypes.Median,
            });
        }

        // Guardrail
        if (recipe.Guardrail != null)
        {
            elements.Add(new ProfileElement
            {
                Id = "guardrail_left",
                Type = ProfileElementTypes.Guardrail,
                At = "eop_l",
                Side = "left",
                PostSpacing = recipe.Guardrail.PostSpacing,
            });

            elements.Add(new ProfileElement
            {
                Id = "guardrail_right",
                Type = ProfileElementTypes.Guardrail,
                At = "eop_r",
                Side = "right",
                PostSpacing = recipe.Guardrail.PostSpacing,
            });
        }

        var profile = new RoadProfileDefinition
        {
            Schema = RoadProfileSchemas.DefinitionV1,
            Name = recipe.Name,
            Units = "m",
            Baseline = "centerline",
            Symmetric = true,
            TotalWidth = currentOffset * 2,
            Features = features,
            Surfaces = surfaces,
            Elements = elements,
        };

        var validation = RoadProfileValidator.Validate(profile);

        return new RoadProfileBuildResult
        {
            Profile = profile,
            Validation = validation,
            Warnings = validation.Warnings.ToList(),
            ExpandedFrom = "recipe",
        };
    }

    /// <summary>
    /// Mode C: Build from annotated observations (capture pipeline).
    /// Converts offset + side to signed offset, carries through role/styleRef.
    /// Returns provenance mapping separately.
    /// </summary>
    public static RoadProfileBuildResult BuildFromObservations(
        string name,
        List<ProfileObservation> observations,
        List<ProfileSurface>? surfaces = null,
        List<ProfileElement>? elements = null)
    {
        var features = new List<RoadProfileFeatureDefinition>();
        var provenance = new List<FeatureProvenance>();
        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var obs in observations)
        {
            string type = obs.Role?.Trim() ?? RoadProfileFeatureTypes.Custom;
            // Normalize design vocabulary
            type = NormalizeType(type);

            if (!typeCounts.TryGetValue(type, out var count))
                count = 0;
            typeCounts[type] = count + 1;

            string id = obs.Id?.Trim() ?? $"{type}-{count}";

            // Convert unsigned offset + side to signed offset
            double signedOffset = obs.Offset;
            if (string.Equals(obs.Side, "left", StringComparison.OrdinalIgnoreCase))
                signedOffset = -System.Math.Abs(obs.Offset);
            else if (string.Equals(obs.Side, "right", StringComparison.OrdinalIgnoreCase))
                signedOffset = System.Math.Abs(obs.Offset);

            features.Add(new RoadProfileFeatureDefinition
            {
                Id = id,
                Order = features.Count,
                Type = type,
                Offset = signedOffset,
                Side = obs.Side?.Trim(),
                StyleRef = obs.StyleRef?.Trim(),
                BoundaryRoles = InferBoundaryRoles(type),
                EligibleForIntersectionTopology = InferIntersectionEligibility(type),
            });

            provenance.Add(new FeatureProvenance
            {
                FeatureId = id,
                CurveId = obs.CurveId,
                Layer = obs.Layer,
                ObjectName = obs.ObjectName,
            });
        }

        var profile = new RoadProfileDefinition
        {
            Schema = RoadProfileSchemas.DefinitionV1,
            Name = name,
            Units = "m",
            Baseline = "centerline",
            Symmetric = InferSymmetric(features),
            Features = features,
            Surfaces = surfaces ?? new(),
            Elements = elements ?? new(),
        };

        var validation = RoadProfileValidator.Validate(profile);

        return new RoadProfileBuildResult
        {
            Profile = profile,
            Validation = validation,
            Warnings = validation.Warnings.ToList(),
            ExpandedFrom = "observations",
            FeatureProvenance = provenance,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string? InferSide(double offset)
    {
        if (offset < -1e-10) return "left";
        if (offset > 1e-10) return "right";
        return null; // zero-offset features stay side-less
    }

    private static bool InferSymmetric(List<RoadProfileFeatureDefinition> features)
    {
        return features.All(f => f.Bilateral);
    }

    private static string NormalizeType(string raw) => raw switch
    {
        "centerline" => RoadProfileFeatureTypes.CenterlineReference,
        "sidewalk_back" => RoadProfileFeatureTypes.SidewalkOuter,
        "right_of_way" => RoadProfileFeatureTypes.RightOfWay,
        "shoulder_edge" => RoadProfileFeatureTypes.ShoulderEdge,
        "lane_divider" => RoadProfileFeatureTypes.LaneDivider,
        _ => raw,
    };

    private static List<string> InferBoundaryRoles(string featureType)
    {
        return featureType switch
        {
            RoadProfileFeatureTypes.CarriagewayEdge => new List<string>
            {
                RoadProfileBoundaryRoles.CarriagewaySurface,
                RoadProfileBoundaryRoles.CurbReturnDriver,
                RoadProfileBoundaryRoles.IntersectionTopologyCandidate,
            },
            RoadProfileFeatureTypes.CurbFace => new List<string>
            {
                RoadProfileBoundaryRoles.CurbReturnDriver,
                RoadProfileBoundaryRoles.IntersectionTopologyCandidate,
            },
            RoadProfileFeatureTypes.EdgeOfPavement => new List<string>
            {
                RoadProfileBoundaryRoles.EdgeOfPavement,
                RoadProfileBoundaryRoles.IntersectionTopologyCandidate,
            },
            RoadProfileFeatureTypes.RightOfWay => new List<string>
            {
                RoadProfileBoundaryRoles.OuterEnvelope,
            },
            _ => new List<string>(),
        };
    }

    private static bool InferIntersectionEligibility(string featureType)
    {
        return featureType is RoadProfileFeatureTypes.CarriagewayEdge
            or RoadProfileFeatureTypes.CurbFace
            or RoadProfileFeatureTypes.EdgeOfPavement;
    }
}
