using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoadCreator.Core.Profiles;

public static class RoadProfileSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(RoadProfileDefinition profile) =>
        JsonSerializer.Serialize(ProfileDto.From(profile), Options);

    public static RoadProfileDefinition? Deserialize(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ProfileDto>(json, Options);
            if (dto == null)
                return null;

            if (!string.IsNullOrEmpty(dto.Schema) &&
                !dto.Schema.StartsWith("roadcreator.road-profile/", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Features.Count == 0)
                return null;

            return dto.ToProfile();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ProfileDto
    {
        [JsonPropertyName("schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public int? Version { get; set; }

        [JsonPropertyName("units")]
        public string? Units { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("symmetric")]
        public bool Symmetric { get; set; } = true;

        [JsonPropertyName("totalWidth")]
        public double? TotalWidth { get; set; }

        [JsonPropertyName("source")]
        public SourceDto? Source { get; set; }

        [JsonPropertyName("baseline")]
        public string? Baseline { get; set; }

        [JsonPropertyName("features")]
        public List<FeatureDto> Features { get; set; } = new();

        [JsonPropertyName("surfaces")]
        public List<SurfaceDto> Surfaces { get; set; } = new();

        [JsonPropertyName("elements")]
        public List<ElementDto> Elements { get; set; } = new();

        [JsonPropertyName("layerMap")]
        public Dictionary<string, string> LayerMap { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("crossSectionDefaults")]
        public CrossSectionDefaultsDto? CrossSectionDefaults { get; set; }

        [JsonPropertyName("intersectionDefaults")]
        public IntersectionDefaultsDto? IntersectionDefaults { get; set; }

        public static ProfileDto From(RoadProfileDefinition profile) => new()
        {
            Schema = profile.Schema,
            Name = profile.Name,
            Version = profile.Version,
            Units = profile.Units,
            Description = profile.Description,
            Symmetric = profile.Symmetric,
            Baseline = profile.Baseline == "centerline" ? null : profile.Baseline,
            TotalWidth = profile.TotalWidth,
            Source = SourceDto.From(profile.Source),
            Features = profile.Features.Select(FeatureDto.From).ToList(),
            Surfaces = profile.Surfaces.Count > 0
                ? profile.Surfaces.Select(SurfaceDto.From).ToList()
                : null!,
            Elements = profile.Elements.Count > 0
                ? profile.Elements.Select(ElementDto.From).ToList()
                : null!,
            LayerMap = new Dictionary<string, string>(profile.LayerMap, StringComparer.Ordinal),
            Tags = profile.Tags.ToList(),
            CrossSectionDefaults = CrossSectionDefaultsDto.From(profile.CrossSectionDefaults),
            IntersectionDefaults = IntersectionDefaultsDto.From(profile.IntersectionDefaults),
        };

        public RoadProfileDefinition ToProfile()
        {
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var features = new List<RoadProfileFeatureDefinition>(Features.Count);

            for (int index = 0; index < Features.Count; index++)
            {
                var dto = Features[index];
                var type = string.IsNullOrWhiteSpace(dto.ResolvedType) ? RoadProfileFeatureTypes.Custom : dto.ResolvedType.Trim();

                if (!typeCounts.TryGetValue(type, out var currentCount))
                    currentCount = 0;
                typeCounts[type] = currentCount + 1;

                var boundaryRoles = dto.BoundaryRoles.Count > 0
                    ? dto.BoundaryRoles
                        .Where(role => !string.IsNullOrWhiteSpace(role))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : InferBoundaryRoles(type);

                features.Add(new RoadProfileFeatureDefinition
                {
                    Id = string.IsNullOrWhiteSpace(dto.Id) ? $"{type}-{currentCount}" : dto.Id.Trim(),
                    Order = dto.Order ?? index,
                    Type = type,
                    Offset = dto.Offset,
                    Width = dto.Width,
                    Bilateral = dto.Bilateral ?? true,
                    Label = dto.Label?.Trim() ?? "",
                    Baseline = string.IsNullOrWhiteSpace(dto.Baseline) ? null : dto.Baseline.Trim(),
                    StyleRef = string.IsNullOrWhiteSpace(dto.StyleRef) ? null : dto.StyleRef!.Trim(),
                    Side = string.IsNullOrWhiteSpace(dto.Side) ? null : dto.Side!.Trim(),
                    BoundaryRoles = boundaryRoles,
                    EligibleForIntersectionTopology =
                        dto.EligibleForIntersectionTopology ?? InferIntersectionEligibility(type),
                });
            }

            return new RoadProfileDefinition
            {
                Schema = string.IsNullOrWhiteSpace(Schema) ? RoadProfileSchemas.DefinitionV1 : Schema!,
                Name = Name.Trim(),
                Version = Version,
                Units = string.IsNullOrWhiteSpace(Units) ? "m" : Units!,
                Description = Description?.Trim() ?? "",
                Symmetric = Symmetric,
                Baseline = string.IsNullOrWhiteSpace(Baseline) ? "centerline" : Baseline!.Trim(),
                TotalWidth = TotalWidth,
                Source = Source?.ToModel(),
                Features = features,
                Surfaces = (Surfaces ?? new())
                    .Select(s => new ProfileSurface
                    {
                        Id = s.Id?.Trim() ?? "",
                        Between = s.Between?.Where(b => !string.IsNullOrWhiteSpace(b)).ToList() ?? new(),
                        Type = s.Type?.Trim() ?? "",
                        Crossfall = s.Crossfall,
                    })
                    .ToList(),
                Elements = (Elements ?? new())
                    .Select(e => new ProfileElement
                    {
                        Id = e.Id?.Trim() ?? "",
                        Type = e.Type?.Trim() ?? "",
                        At = e.At?.Trim() ?? "",
                        Side = string.IsNullOrWhiteSpace(e.Side) ? null : e.Side!.Trim(),
                        Height = e.Height,
                        TopWidth = e.TopWidth,
                        PostSpacing = e.PostSpacing,
                        Variant = e.Variant?.Trim(),
                        Depth = e.Depth,
                        Width = e.Width,
                    })
                    .ToList(),
                LayerMap = new Dictionary<string, string>(LayerMap, StringComparer.Ordinal),
                Tags = Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList(),
                CrossSectionDefaults = CrossSectionDefaults?.ToModel(),
                IntersectionDefaults = IntersectionDefaults?.ToModel(),
            };
        }

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

    private sealed class FeatureDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("order")]
        public int? Order { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>Alias for "type" — accepted on input, normalized to Type.</summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("bilateral")]
        public bool? Bilateral { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("baseline")]
        public string? Baseline { get; set; }

        [JsonPropertyName("styleRef")]
        public string? StyleRef { get; set; }

        [JsonPropertyName("side")]
        public string? Side { get; set; }

        [JsonPropertyName("boundaryRoles")]
        public List<string> BoundaryRoles { get; set; } = new();

        [JsonPropertyName("eligibleForIntersectionTopology")]
        public bool? EligibleForIntersectionTopology { get; set; }

        /// <summary>
        /// Resolve the effective feature type: "type" takes precedence, "role" is the alias.
        /// Design-vocabulary aliases (e.g. "centerline", "sidewalk_back", "right_of_way")
        /// are normalized to the canonical internal vocabulary.
        /// </summary>
        public string ResolvedType
        {
            get
            {
                var raw = !string.IsNullOrWhiteSpace(Type) ? Type!
                    : !string.IsNullOrWhiteSpace(Role) ? Role!
                    : "";
                return NormalizeFeatureType(raw);
            }
        }

        /// <summary>
        /// Normalize design-vocabulary aliases to canonical feature type names.
        /// Existing canonical names pass through unchanged.
        /// </summary>
        private static string NormalizeFeatureType(string raw) => raw.Trim() switch
        {
            "centerline" => RoadProfileFeatureTypes.CenterlineReference,
            "sidewalk_back" => RoadProfileFeatureTypes.SidewalkOuter,
            "right_of_way" => RoadProfileFeatureTypes.RightOfWay,
            "shoulder_edge" => RoadProfileFeatureTypes.ShoulderEdge,
            "lane_divider" => RoadProfileFeatureTypes.LaneDivider,
            _ => raw.Trim(),
        };

        public static FeatureDto From(RoadProfileFeatureDefinition feature) => new()
        {
            Id = feature.Id,
            Order = feature.Order,
            Type = feature.Type,
            Offset = feature.Offset,
            Width = feature.Width,
            Bilateral = feature.Bilateral,
            Label = feature.Label,
            Baseline = feature.Baseline,
            StyleRef = feature.StyleRef,
            Side = feature.Side,
            BoundaryRoles = feature.BoundaryRoles.ToList(),
            EligibleForIntersectionTopology = feature.EligibleForIntersectionTopology,
        };
    }

    private sealed class SurfaceDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("between")]
        public List<string>? Between { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("crossfall")]
        public double? Crossfall { get; set; }

        public static SurfaceDto From(ProfileSurface surface) => new()
        {
            Id = surface.Id,
            Between = surface.Between.ToList(),
            Type = surface.Type,
            Crossfall = surface.Crossfall,
        };
    }

    private sealed class ElementDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("at")]
        public string? At { get; set; }

        [JsonPropertyName("side")]
        public string? Side { get; set; }

        [JsonPropertyName("height")]
        public double? Height { get; set; }

        [JsonPropertyName("topWidth")]
        public double? TopWidth { get; set; }

        [JsonPropertyName("postSpacing")]
        public double? PostSpacing { get; set; }

        [JsonPropertyName("variant")]
        public string? Variant { get; set; }

        [JsonPropertyName("depth")]
        public double? Depth { get; set; }

        [JsonPropertyName("width")]
        public double? Width { get; set; }

        public static ElementDto From(ProfileElement element) => new()
        {
            Id = element.Id,
            Type = element.Type,
            At = element.At,
            Side = element.Side,
            Height = element.Height,
            TopWidth = element.TopWidth,
            PostSpacing = element.PostSpacing,
            Variant = element.Variant,
            Depth = element.Depth,
            Width = element.Width,
        };
    }

    private sealed class SourceDto
    {
        [JsonPropertyName("project")]
        public string? Project { get; set; }

        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("extractedAt")]
        public string? ExtractedAt { get; set; }

        [JsonPropertyName("centerlineId")]
        public string? CenterlineId { get; set; }

        [JsonPropertyName("chainage")]
        public double? Chainage { get; set; }

        public static SourceDto? From(RoadProfileSourceMetadata? source)
        {
            if (source == null)
                return null;

            return new SourceDto
            {
                Project = source.Project,
                File = source.File,
                ExtractedAt = source.ExtractedAt,
                CenterlineId = source.CenterlineId,
                Chainage = source.Chainage,
            };
        }

        public RoadProfileSourceMetadata ToModel() => new()
        {
            Project = Project,
            File = File,
            ExtractedAt = ExtractedAt,
            CenterlineId = CenterlineId,
            Chainage = Chainage,
        };
    }

    private sealed class CrossSectionDefaultsDto
    {
        [JsonPropertyName("roadCategoryCode")]
        public string? RoadCategoryCode { get; set; }

        [JsonPropertyName("crossfallStraight")]
        public double? CrossfallStraight { get; set; }

        [JsonPropertyName("crossfallCurve")]
        public double? CrossfallCurve { get; set; }

        [JsonPropertyName("includeVerge")]
        public bool? IncludeVerge { get; set; }

        [JsonPropertyName("vergeWidth")]
        public double? VergeWidth { get; set; }

        public static CrossSectionDefaultsDto? From(RoadProfileCrossSectionDefaults? defaults)
        {
            if (defaults == null)
                return null;

            return new CrossSectionDefaultsDto
            {
                RoadCategoryCode = defaults.RoadCategoryCode,
                CrossfallStraight = defaults.CrossfallStraight,
                CrossfallCurve = defaults.CrossfallCurve,
                IncludeVerge = defaults.IncludeVerge,
                VergeWidth = defaults.VergeWidth,
            };
        }

        public RoadProfileCrossSectionDefaults ToModel() => new()
        {
            RoadCategoryCode = RoadCategoryCode,
            CrossfallStraight = CrossfallStraight,
            CrossfallCurve = CrossfallCurve,
            IncludeVerge = IncludeVerge,
            VergeWidth = VergeWidth,
        };
    }

    private sealed class IntersectionDefaultsDto
    {
        [JsonPropertyName("armLengthOuterEnvelopeMultiplier")]
        public double? ArmLengthOuterEnvelopeMultiplier { get; set; }

        [JsonPropertyName("armLengthCarriagewayMultiplier")]
        public double? ArmLengthCarriagewayMultiplier { get; set; }

        [JsonPropertyName("armLengthDiagonalMultiplier")]
        public double? ArmLengthDiagonalMultiplier { get; set; }

        [JsonPropertyName("armLengthRadiusMultiplier")]
        public double? ArmLengthRadiusMultiplier { get; set; }

        [JsonPropertyName("armLengthMin")]
        public double? ArmLengthMin { get; set; }

        [JsonPropertyName("armLengthMax")]
        public double? ArmLengthMax { get; set; }

        public static IntersectionDefaultsDto? From(RoadProfileIntersectionDefaults? defaults)
        {
            if (defaults == null)
                return null;

            return new IntersectionDefaultsDto
            {
                ArmLengthOuterEnvelopeMultiplier = defaults.ArmLengthOuterEnvelopeMultiplier,
                ArmLengthCarriagewayMultiplier = defaults.ArmLengthCarriagewayMultiplier,
                ArmLengthDiagonalMultiplier = defaults.ArmLengthDiagonalMultiplier,
                ArmLengthRadiusMultiplier = defaults.ArmLengthRadiusMultiplier,
                ArmLengthMin = defaults.ArmLengthMin,
                ArmLengthMax = defaults.ArmLengthMax,
            };
        }

        public RoadProfileIntersectionDefaults ToModel() => new()
        {
            ArmLengthOuterEnvelopeMultiplier = ArmLengthOuterEnvelopeMultiplier,
            ArmLengthCarriagewayMultiplier = ArmLengthCarriagewayMultiplier,
            ArmLengthDiagonalMultiplier = ArmLengthDiagonalMultiplier,
            ArmLengthRadiusMultiplier = ArmLengthRadiusMultiplier,
            ArmLengthMin = ArmLengthMin,
            ArmLengthMax = ArmLengthMax,
        };
    }
}
