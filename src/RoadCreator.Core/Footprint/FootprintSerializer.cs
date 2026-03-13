using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoadCreator.Core.Footprint;

/// <summary>
/// JSON serialization for OffsetProfile and StyleSet.
/// Uses System.Text.Json (no external dependencies, .NET 7 compatible).
///
/// Snake_case keys are enforced via explicit [JsonPropertyName] attributes on every
/// DTO property. JsonNamingPolicy.SnakeCaseLower requires .NET 8 and is NOT used here.
/// IMPORTANT: any new DTO property added without a [JsonPropertyName] attribute will
/// serialize with PascalCase, silently breaking the schema contract.
///
/// Deserialization validates the schema prefix and requires a non-empty name.
/// A payload with a mismatched schema string or missing name returns null.
/// Unknown fields (e.g. from a future schema version) are ignored.
/// </summary>
public static class FootprintSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static string SerializeProfile(OffsetProfile profile) =>
        JsonSerializer.Serialize(ProfileDto.From(profile), Options);

    public static OffsetProfile? DeserializeProfile(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ProfileDto>(json, Options);
            if (dto == null) return null;
            if (!string.IsNullOrEmpty(dto.Schema) &&
                !dto.Schema.StartsWith("roadcreator.offset-profile/", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.IsNullOrEmpty(dto.Name)) return null;
            return dto.ToProfile();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeStyleSet(StyleSet styleSet) =>
        JsonSerializer.Serialize(StyleSetDto.From(styleSet), Options);

    public static StyleSet? DeserializeStyleSet(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<StyleSetDto>(json, Options);
            if (dto == null) return null;
            if (!string.IsNullOrEmpty(dto.Schema) &&
                !dto.Schema.StartsWith("roadcreator.style-set/", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.IsNullOrEmpty(dto.Name)) return null;
            return dto.ToStyleSet();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class ProfileDto
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = OffsetProfile.SchemaVersion;

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("units")]
        public string Units { get; set; } = "m";

        [JsonPropertyName("baseline")]
        public string Baseline { get; set; } = "centerline";

        [JsonPropertyName("features")]
        public List<FeatureDto> Features { get; set; } = new();

        public static ProfileDto From(OffsetProfile p) => new()
        {
            Name = p.Name,
            Units = p.Units,
            Baseline = p.Baseline,
            Features = p.Features.Select(FeatureDto.From).ToList(),
        };

        public OffsetProfile ToProfile() =>
            new(Name, Units, Baseline, Features.Select(f => f.ToFeature()).ToList());
    }

    private sealed class FeatureDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("style")]
        public string Style { get; set; } = "";

        public static FeatureDto From(OffsetFeature f) => new()
        {
            Id = f.Id,
            Offset = f.Offset,
            Role = f.Role,
            Style = f.StyleRef,
        };

        public OffsetFeature ToFeature() => new(Id, Offset, Role, Style);
    }

    private sealed class StyleSetDto
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = StyleSet.SchemaVersion;

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("styles")]
        public List<StyleEntryDto> Styles { get; set; } = new();

        public static StyleSetDto From(StyleSet s) => new()
        {
            Name = s.Name,
            Styles = s.Styles.Select(StyleEntryDto.From).ToList(),
        };

        public StyleSet ToStyleSet() =>
            new(Name, Styles.Select(e => e.ToEntry()).ToList());
    }

    private sealed class StyleEntryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("layer")]
        public string Layer { get; set; } = "";

        [JsonPropertyName("linetype")]
        public string Linetype { get; set; } = "Continuous";

        [JsonPropertyName("print_width_mm")]
        public double PrintWidthMm { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        public static StyleEntryDto From(StyleEntry e) => new()
        {
            Id = e.Id,
            Layer = e.Layer,
            Linetype = e.Linetype,
            PrintWidthMm = e.PrintWidthMm,
            Color = e.Color,
        };

        public StyleEntry ToEntry() => new(Id, Layer, Linetype, PrintWidthMm, Color);
    }
}
