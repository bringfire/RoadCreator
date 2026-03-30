using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoadCreator.Core.Profiles;

/// <summary>
/// Serializes a RoadProfileDefinition into a native-friendly summary JSON
/// readable by the C++ intersection handler via CRhinoDoc::GetUserString.
///
/// Uses RoadProfileSummaryBuilder.Build() for offset computation — no second
/// algorithm. The output schema is "roadcreator.road-profile-summary/v1".
/// </summary>
public static class RoadProfileNativeSummarySerializer
{
    public const string SchemaVersion = "roadcreator.road-profile-summary/v1";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(RoadProfileDefinition profile)
    {
        var summary = RoadProfileSummaryBuilder.Build(profile);
        var totalWidth = profile.TotalWidth is > 0.0
            ? profile.TotalWidth.Value
            : profile.MaxOffset * 2.0;

        // The C# summary builder resolves CarriagewaySurfaceOffset from the
        // "carriageway_surface" boundary role → fallback "carriageway_edge" type.
        // Recipe-built profiles use "edge_of_pavement" features which carry only
        // the "edge_of_pavement" boundary role, so CarriagewaySurfaceOffset is 0.
        // The C++ filesystem loader treats edge_of_pavement as a carriageway
        // surface boundary, so we mirror that here: fall back to EdgeOfPavementOffset.
        var carriagewaySurfaceOffset = summary.CarriagewaySurfaceOffset > 0.0
            ? summary.CarriagewaySurfaceOffset
            : summary.EdgeOfPavementOffset;
        var curbReturnDriverOffset = summary.CurbReturnDriverOffset > 0.0
            ? summary.CurbReturnDriverOffset
            : carriagewaySurfaceOffset;

        var dto = new SummaryDto
        {
            Schema = SchemaVersion,
            Name = profile.Name,
            Symmetric = profile.Symmetric,
            TotalWidth = totalWidth,
            MaxOffset = summary.MaxOffset,
            // Document-sourced summaries intentionally collapse carriageway edge and
            // carriageway surface to the same resolved offset. The native bridge only
            // needs a stable carriageway envelope for intersection topology; the more
            // specific distinction remains available in filesystem-authored profiles.
            CarriagewayEdgeOffset = carriagewaySurfaceOffset,
            CarriagewaySurfaceOffset = carriagewaySurfaceOffset,
            CurbReturnDriverOffset = curbReturnDriverOffset,
            EdgeOfPavementOffset = summary.EdgeOfPavementOffset,
            OuterEnvelopeOffset = summary.OuterEnvelopeOffset,
            HasUnilateralFeatures = summary.HasUnilateralFeatures,
            RequiresSideSelection = summary.RequiresSideSelection,
            Features = profile.Features
                .Select(f => new FeatureDto
                {
                    Type = f.Type,
                    Label = f.Label,
                    Offset = f.Offset,
                    Width = f.Width,
                    Bilateral = f.Bilateral,
                })
                .ToList(),
            IntersectionDefaults = profile.IntersectionDefaults != null
                ? new IntersectionDefaultsDto
                {
                    ArmLengthOuterEnvelopeMultiplier = profile.IntersectionDefaults.ArmLengthOuterEnvelopeMultiplier,
                    ArmLengthCarriagewayMultiplier = profile.IntersectionDefaults.ArmLengthCarriagewayMultiplier,
                    ArmLengthDiagonalMultiplier = profile.IntersectionDefaults.ArmLengthDiagonalMultiplier,
                    ArmLengthRadiusMultiplier = profile.IntersectionDefaults.ArmLengthRadiusMultiplier,
                    ArmLengthMin = profile.IntersectionDefaults.ArmLengthMin,
                    ArmLengthMax = profile.IntersectionDefaults.ArmLengthMax,
                }
                : null,
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    private sealed class SummaryDto
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("symmetric")]
        public bool Symmetric { get; set; }

        [JsonPropertyName("totalWidth")]
        public double TotalWidth { get; set; }

        [JsonPropertyName("maxOffset")]
        public double MaxOffset { get; set; }

        [JsonPropertyName("carriagewayEdgeOffset")]
        public double CarriagewayEdgeOffset { get; set; }

        [JsonPropertyName("carriagewaySurfaceOffset")]
        public double CarriagewaySurfaceOffset { get; set; }

        [JsonPropertyName("curbReturnDriverOffset")]
        public double CurbReturnDriverOffset { get; set; }

        [JsonPropertyName("edgeOfPavementOffset")]
        public double EdgeOfPavementOffset { get; set; }

        [JsonPropertyName("outerEnvelopeOffset")]
        public double OuterEnvelopeOffset { get; set; }

        [JsonPropertyName("hasUnilateralFeatures")]
        public bool HasUnilateralFeatures { get; set; }

        [JsonPropertyName("requiresSideSelection")]
        public bool RequiresSideSelection { get; set; }

        [JsonPropertyName("features")]
        public List<FeatureDto> Features { get; set; } = new();

        [JsonPropertyName("intersectionDefaults")]
        public IntersectionDefaultsDto? IntersectionDefaults { get; set; }
    }

    private sealed class FeatureDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("bilateral")]
        public bool Bilateral { get; set; }
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
    }
}
