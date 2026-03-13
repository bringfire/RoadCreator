using System.Text.Json;

namespace RoadCreator.Core.Profiles;

public static class RoadProfileCompactSerializer
{
    private static readonly IReadOnlyDictionary<string, string> BoundaryRoleToCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RoadProfileBoundaryRoles.CarriagewaySurface] = "cs",
            [RoadProfileBoundaryRoles.CurbReturnDriver] = "cr",
            [RoadProfileBoundaryRoles.EdgeOfPavement] = "ep",
            [RoadProfileBoundaryRoles.OuterEnvelope] = "oe",
            [RoadProfileBoundaryRoles.IntersectionTopologyCandidate] = "it",
        };

    private static readonly IReadOnlyDictionary<string, string> CodeToBoundaryRole =
        BoundaryRoleToCode.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static string Serialize(RoadProfileDefinition profile)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("s", RoadProfileSchemas.CompactV1);
            writer.WriteString("n", profile.Name);
            writer.WriteString("u", profile.Units);
            writer.WriteBoolean("sym", profile.Symmetric);

            if (profile.TotalWidth.HasValue)
                writer.WriteNumber("tw", profile.TotalWidth.Value);

            writer.WritePropertyName("f");
            writer.WriteStartArray();
            foreach (var feature in profile.OrderedFeatures)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(feature.Id);
                writer.WriteStringValue(feature.Type);
                writer.WriteNumberValue(feature.Offset);
                writer.WriteNumberValue(feature.Width);
                writer.WriteBooleanValue(feature.Bilateral);
                writer.WriteStringValue(EncodeBoundaryRoles(feature.BoundaryRoles));
                if (!string.IsNullOrWhiteSpace(feature.Baseline))
                    writer.WriteStringValue(feature.Baseline);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            if (profile.LayerMap.Count > 0)
            {
                writer.WritePropertyName("lm");
                writer.WriteStartObject();
                foreach (var pair in profile.LayerMap.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    writer.WriteString(pair.Key, pair.Value);
                writer.WriteEndObject();
            }

            if (profile.Tags.Count > 0)
            {
                writer.WritePropertyName("tg");
                writer.WriteStartArray();
                foreach (var tag in profile.Tags)
                    writer.WriteStringValue(tag);
                writer.WriteEndArray();
            }

            if (profile.Source != null)
            {
                writer.WritePropertyName("src");
                writer.WriteStartObject();
                if (!string.IsNullOrWhiteSpace(profile.Source.Project))
                    writer.WriteString("p", profile.Source.Project);
                if (!string.IsNullOrWhiteSpace(profile.Source.File))
                    writer.WriteString("f", profile.Source.File);
                if (!string.IsNullOrWhiteSpace(profile.Source.ExtractedAt))
                    writer.WriteString("x", profile.Source.ExtractedAt);
                if (!string.IsNullOrWhiteSpace(profile.Source.CenterlineId))
                    writer.WriteString("c", profile.Source.CenterlineId);
                if (profile.Source.Chainage.HasValue)
                    writer.WriteNumber("ch", profile.Source.Chainage.Value);
                writer.WriteEndObject();
            }

            if (profile.CrossSectionDefaults != null)
            {
                writer.WritePropertyName("x");
                writer.WriteStartObject();
                if (!string.IsNullOrWhiteSpace(profile.CrossSectionDefaults.RoadCategoryCode))
                    writer.WriteString("rc", profile.CrossSectionDefaults.RoadCategoryCode);
                if (profile.CrossSectionDefaults.CrossfallStraight.HasValue)
                    writer.WriteNumber("cfs", profile.CrossSectionDefaults.CrossfallStraight.Value);
                if (profile.CrossSectionDefaults.CrossfallCurve.HasValue)
                    writer.WriteNumber("cfc", profile.CrossSectionDefaults.CrossfallCurve.Value);
                if (profile.CrossSectionDefaults.IncludeVerge.HasValue)
                    writer.WriteBoolean("v", profile.CrossSectionDefaults.IncludeVerge.Value);
                if (profile.CrossSectionDefaults.VergeWidth.HasValue)
                    writer.WriteNumber("vw", profile.CrossSectionDefaults.VergeWidth.Value);
                writer.WriteEndObject();
            }

            if (profile.IntersectionDefaults != null)
            {
                writer.WritePropertyName("ix");
                writer.WriteStartObject();
                if (profile.IntersectionDefaults.ArmLengthOuterEnvelopeMultiplier.HasValue)
                    writer.WriteNumber("ao", profile.IntersectionDefaults.ArmLengthOuterEnvelopeMultiplier.Value);
                if (profile.IntersectionDefaults.ArmLengthCarriagewayMultiplier.HasValue)
                    writer.WriteNumber("ac", profile.IntersectionDefaults.ArmLengthCarriagewayMultiplier.Value);
                if (profile.IntersectionDefaults.ArmLengthDiagonalMultiplier.HasValue)
                    writer.WriteNumber("ad", profile.IntersectionDefaults.ArmLengthDiagonalMultiplier.Value);
                if (profile.IntersectionDefaults.ArmLengthRadiusMultiplier.HasValue)
                    writer.WriteNumber("ar", profile.IntersectionDefaults.ArmLengthRadiusMultiplier.Value);
                if (profile.IntersectionDefaults.ArmLengthMin.HasValue)
                    writer.WriteNumber("mn", profile.IntersectionDefaults.ArmLengthMin.Value);
                if (profile.IntersectionDefaults.ArmLengthMax.HasValue)
                    writer.WriteNumber("mx", profile.IntersectionDefaults.ArmLengthMax.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RoadProfileDefinition? Deserialize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("s", out var schemaElement))
            {
                var schema = schemaElement.GetString();
                if (string.IsNullOrWhiteSpace(schema) ||
                    !schema.StartsWith("roadcreator.road-profile.compact/", StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            if (!root.TryGetProperty("n", out var nameElement) || string.IsNullOrWhiteSpace(nameElement.GetString()))
                return null;

            if (!root.TryGetProperty("f", out var featuresElement) || featuresElement.ValueKind != JsonValueKind.Array)
                return null;

            var features = new List<RoadProfileFeatureDefinition>();
            var index = 0;
            foreach (var featureElement in featuresElement.EnumerateArray())
            {
                if (featureElement.ValueKind != JsonValueKind.Array)
                    return null;

                var tuple = featureElement.EnumerateArray().ToList();
                if (tuple.Count < 6)
                    return null;

                var id = tuple[0].GetString();
                var type = tuple[1].GetString();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type))
                    return null;

                var roles = DecodeBoundaryRoles(tuple[5].GetString());
                features.Add(new RoadProfileFeatureDefinition
                {
                    Id = id!,
                    Order = index,
                    Type = type!,
                    Offset = tuple[2].GetDouble(),
                    Width = tuple[3].GetDouble(),
                    Bilateral = tuple[4].GetBoolean(),
                    BoundaryRoles = roles,
                    Baseline = tuple.Count >= 7 && tuple[6].ValueKind == JsonValueKind.String
                        ? tuple[6].GetString()
                        : null,
                    EligibleForIntersectionTopology =
                        roles.Contains(RoadProfileBoundaryRoles.IntersectionTopologyCandidate, StringComparer.OrdinalIgnoreCase),
                });
                index++;
            }

            if (features.Count == 0)
                return null;

            return new RoadProfileDefinition
            {
                Schema = RoadProfileSchemas.DefinitionV1,
                Name = nameElement.GetString()!,
                Units = root.TryGetProperty("u", out var unitsElement) && !string.IsNullOrWhiteSpace(unitsElement.GetString())
                    ? unitsElement.GetString()!
                    : "m",
                Symmetric = !root.TryGetProperty("sym", out var symmetricElement) || symmetricElement.GetBoolean(),
                TotalWidth = root.TryGetProperty("tw", out var totalWidthElement) ? totalWidthElement.GetDouble() : null,
                Features = features,
                LayerMap = ReadLayerMap(root),
                Tags = ReadTags(root),
                Source = ReadSource(root),
                CrossSectionDefaults = ReadCrossSectionDefaults(root),
                IntersectionDefaults = ReadIntersectionDefaults(root),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string EncodeBoundaryRoles(IReadOnlyList<string> boundaryRoles)
    {
        if (boundaryRoles.Count == 0)
            return "";

        return string.Join("|", boundaryRoles
            .Select(role => BoundaryRoleToCode.TryGetValue(role, out var code) ? code : role)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> DecodeBoundaryRoles(string? encodedRoles)
    {
        if (string.IsNullOrWhiteSpace(encodedRoles))
            return new List<string>();

        return encodedRoles
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => CodeToBoundaryRole.TryGetValue(code, out var role) ? role : code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> ReadLayerMap(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("lm", out var layerMapElement) || layerMapElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in layerMapElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                result[property.Name] = property.Value.GetString() ?? "";
        }

        return result;
    }

    private static List<string> ReadTags(JsonElement root)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("tg", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in tagsElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
                result.Add(element.GetString()!);
        }

        return result;
    }

    private static RoadProfileSourceMetadata? ReadSource(JsonElement root)
    {
        if (!root.TryGetProperty("src", out var sourceElement) || sourceElement.ValueKind != JsonValueKind.Object)
            return null;

        return new RoadProfileSourceMetadata
        {
            Project = sourceElement.TryGetProperty("p", out var projectElement) ? projectElement.GetString() : null,
            File = sourceElement.TryGetProperty("f", out var fileElement) ? fileElement.GetString() : null,
            ExtractedAt = sourceElement.TryGetProperty("x", out var extractedAtElement) ? extractedAtElement.GetString() : null,
            CenterlineId = sourceElement.TryGetProperty("c", out var centerlineElement) ? centerlineElement.GetString() : null,
            Chainage = sourceElement.TryGetProperty("ch", out var chainageElement) ? chainageElement.GetDouble() : null,
        };
    }

    private static RoadProfileCrossSectionDefaults? ReadCrossSectionDefaults(JsonElement root)
    {
        if (!root.TryGetProperty("x", out var defaultsElement) || defaultsElement.ValueKind != JsonValueKind.Object)
            return null;

        return new RoadProfileCrossSectionDefaults
        {
            RoadCategoryCode = defaultsElement.TryGetProperty("rc", out var roadCategoryElement)
                ? roadCategoryElement.GetString()
                : null,
            CrossfallStraight = defaultsElement.TryGetProperty("cfs", out var crossfallStraightElement)
                ? crossfallStraightElement.GetDouble()
                : null,
            CrossfallCurve = defaultsElement.TryGetProperty("cfc", out var crossfallCurveElement)
                ? crossfallCurveElement.GetDouble()
                : null,
            IncludeVerge = defaultsElement.TryGetProperty("v", out var includeVergeElement)
                ? includeVergeElement.GetBoolean()
                : null,
            VergeWidth = defaultsElement.TryGetProperty("vw", out var vergeWidthElement)
                ? vergeWidthElement.GetDouble()
                : null,
        };
    }

    private static RoadProfileIntersectionDefaults? ReadIntersectionDefaults(JsonElement root)
    {
        if (!root.TryGetProperty("ix", out var defaultsElement) || defaultsElement.ValueKind != JsonValueKind.Object)
            return null;

        return new RoadProfileIntersectionDefaults
        {
            ArmLengthOuterEnvelopeMultiplier = defaultsElement.TryGetProperty("ao", out var ao) ? ao.GetDouble() : null,
            ArmLengthCarriagewayMultiplier = defaultsElement.TryGetProperty("ac", out var ac) ? ac.GetDouble() : null,
            ArmLengthDiagonalMultiplier = defaultsElement.TryGetProperty("ad", out var ad) ? ad.GetDouble() : null,
            ArmLengthRadiusMultiplier = defaultsElement.TryGetProperty("ar", out var ar) ? ar.GetDouble() : null,
            ArmLengthMin = defaultsElement.TryGetProperty("mn", out var mn) ? mn.GetDouble() : null,
            ArmLengthMax = defaultsElement.TryGetProperty("mx", out var mx) ? mx.GetDouble() : null,
        };
    }
}
