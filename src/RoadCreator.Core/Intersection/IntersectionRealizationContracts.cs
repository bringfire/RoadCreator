using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoadCreator.Core.Intersection;

public static class IntersectionRealizationSchemas
{
    public const string RequestV1 = "roadcreator.intersection-realization-request/v1";
    public const string ResultV1 = "roadcreator.intersection-realization-result/v1";
    public const string JoinContractV1 = "roadcreator.intersection-join-contract/v1";
}

[JsonConverter(typeof(IntersectionPoint3JsonConverter))]
public sealed record IntersectionPoint3(double X, double Y, double Z = 0.0);

public sealed class IntersectionRealizationRequest
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = IntersectionRealizationSchemas.RequestV1;

    [JsonPropertyName("analysisToken")]
    public string AnalysisToken { get; set; } = "";

    [JsonPropertyName("targetLayerRoot")]
    public string TargetLayerRoot { get; set; } = "";

    [JsonPropertyName("namePrefix")]
    public string NamePrefix { get; set; } = "IntersectionAnalysis2D";

    [JsonPropertyName("realizationMode")]
    public string RealizationMode { get; set; } = "planar_surface_with_analysis_guides";

    [JsonPropertyName("analysisStage")]
    public string AnalysisStage { get; set; } = "";

    [JsonPropertyName("selectedCandidate")]
    public IntersectionSelectedCandidate? SelectedCandidate { get; set; }

    [JsonPropertyName("sourceRoads")]
    public List<IntersectionSourceRoad> SourceRoads { get; set; } = new();

    [JsonPropertyName("provisionalBoundary2D")]
    public IntersectionProvisionalBoundary2D ProvisionalBoundary2D { get; set; } = new();

    [JsonPropertyName("analysisGeometry2D")]
    public IntersectionAnalysisGeometry2D AnalysisGeometry2D { get; set; } = new();

    [JsonPropertyName("cornerPairings")]
    public JsonElement CornerPairings { get; set; }

    [JsonPropertyName("trimDecisions")]
    public JsonElement TrimDecisions { get; set; }

    [JsonPropertyName("featureRules")]
    public JsonElement FeatureRules { get; set; }

    [JsonPropertyName("boundaryWalk")]
    public JsonElement BoundaryWalk { get; set; }

    [JsonPropertyName("unresolvedConditions")]
    public JsonElement UnresolvedConditions { get; set; }

    [JsonPropertyName("debugArtifacts")]
    public IntersectionDebugArtifacts DebugArtifacts { get; set; } = new();
}

public sealed class IntersectionSelectedCandidate
{
    [JsonPropertyName("candidateId")]
    public string CandidateId { get; set; } = "";

    [JsonPropertyName("point")]
    public IntersectionPoint3? Point { get; set; }

    [JsonPropertyName("parameterA")]
    public double? ParameterA { get; set; }

    [JsonPropertyName("parameterB")]
    public double? ParameterB { get; set; }

    [JsonPropertyName("tangentA")]
    public IntersectionPoint3? TangentA { get; set; }

    [JsonPropertyName("tangentB")]
    public IntersectionPoint3? TangentB { get; set; }
}

public sealed class IntersectionSourceRoad
{
    [JsonPropertyName("road")]
    public string Road { get; set; } = "";

    [JsonPropertyName("centerlineId")]
    public string CenterlineId { get; set; } = "";

    [JsonPropertyName("profileName")]
    public string? ProfileName { get; set; }

    [JsonPropertyName("symmetric")]
    public bool Symmetric { get; set; } = true;

    [JsonPropertyName("requiresSideSelection")]
    public bool RequiresSideSelection { get; set; }

    [JsonPropertyName("resolvedSide")]
    public string? ResolvedSide { get; set; }

    [JsonPropertyName("carriagewayEdgeOffset")]
    public double CarriagewayEdgeOffset { get; set; }

    [JsonPropertyName("carriagewaySurfaceOffset")]
    public double CarriagewaySurfaceOffset { get; set; }

    [JsonPropertyName("curbReturnDriverOffset")]
    public double CurbReturnDriverOffset { get; set; }

    [JsonPropertyName("outerEnvelopeOffset")]
    public double OuterEnvelopeOffset { get; set; }

    [JsonPropertyName("armLengthOuterEnvelopeMultiplier")]
    public double ArmLengthOuterEnvelopeMultiplier { get; set; }

    [JsonPropertyName("armLengthCarriagewayMultiplier")]
    public double ArmLengthCarriagewayMultiplier { get; set; }

    [JsonPropertyName("armLengthDiagonalMultiplier")]
    public double ArmLengthDiagonalMultiplier { get; set; }

    [JsonPropertyName("armLengthRadiusMultiplier")]
    public double ArmLengthRadiusMultiplier { get; set; }

    [JsonPropertyName("armLengthMin")]
    public double ArmLengthMin { get; set; }

    [JsonPropertyName("armLengthMax")]
    public double ArmLengthMax { get; set; }

    [JsonPropertyName("selectedParameter")]
    public double SelectedParameter { get; set; }

    [JsonPropertyName("forwardTangent")]
    public IntersectionPoint3 ForwardTangent { get; set; } = new(1.0, 0.0, 0.0);

    public double EffectiveCarriagewaySurfaceOffset =>
        CarriagewaySurfaceOffset > 0.0 ? CarriagewaySurfaceOffset : CarriagewayEdgeOffset;

    public double EffectiveCurbReturnDriverOffset =>
        CurbReturnDriverOffset > 0.0 ? CurbReturnDriverOffset : EffectiveCarriagewaySurfaceOffset;

    public double EffectiveOuterEnvelopeOffset =>
        OuterEnvelopeOffset > 0.0 ? OuterEnvelopeOffset : EffectiveCarriagewaySurfaceOffset;

    public double EffectiveArmLengthOuterEnvelopeMultiplier =>
        ArmLengthOuterEnvelopeMultiplier > 0.0 ? ArmLengthOuterEnvelopeMultiplier : 2.25;

    public double EffectiveArmLengthCarriagewayMultiplier =>
        ArmLengthCarriagewayMultiplier > 0.0 ? ArmLengthCarriagewayMultiplier : 3.5;

    public double EffectiveArmLengthDiagonalMultiplier =>
        ArmLengthDiagonalMultiplier > 0.0 ? ArmLengthDiagonalMultiplier : 0.72;

    public double EffectiveArmLengthRadiusMultiplier =>
        ArmLengthRadiusMultiplier > 0.0 ? ArmLengthRadiusMultiplier : 2.4;

    public double EffectiveArmLengthMin =>
        ArmLengthMin > 0.0 ? ArmLengthMin : 10.0;

    public double EffectiveArmLengthMax =>
        ArmLengthMax > 0.0 ? ArmLengthMax : 48.0;
}

public sealed class IntersectionProvisionalBoundary2D
{
    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("cornerPoints")]
    public List<IntersectionPoint3> CornerPoints { get; set; } = new();
}

public sealed class IntersectionAnalysisGeometry2D
{
    [JsonPropertyName("boundarySegments")]
    public List<IntersectionBoundarySegment> BoundarySegments { get; set; } = new();

    [JsonPropertyName("curbReturnArcs")]
    public List<IntersectionCurbReturnArc> CurbReturnArcs { get; set; } = new();
}

public sealed class IntersectionBoundarySegment
{
    [JsonPropertyName("startPoint")]
    public IntersectionPoint3 StartPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("endPoint")]
    public IntersectionPoint3 EndPoint { get; set; } = new(0.0, 0.0, 0.0);
}

public sealed class IntersectionCurbReturnArc
{
    [JsonPropertyName("cornerOrder")]
    public int? CornerOrder { get; set; }

    [JsonPropertyName("center")]
    public IntersectionPoint3 Center { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("startPoint")]
    public IntersectionPoint3 StartPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("endPoint")]
    public IntersectionPoint3 EndPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("radius")]
    public double Radius { get; set; }
}

public sealed class IntersectionDebugArtifacts
{
    [JsonPropertyName("writeGuides")]
    public bool WriteGuides { get; set; } = true;

    [JsonPropertyName("preserveConstructionArtifacts")]
    public bool PreserveConstructionArtifacts { get; set; }

    [JsonPropertyName("writeApproachEdges")]
    public bool WriteApproachEdges { get; set; }

    [JsonPropertyName("writeApproachPatches")]
    public bool WriteApproachPatches { get; set; }

    [JsonPropertyName("writeDebugDots")]
    public bool WriteDebugDots { get; set; }
}

public sealed class IntersectionRealizationResult
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = IntersectionRealizationSchemas.ResultV1;

    [JsonPropertyName("analysisToken")]
    public string AnalysisToken { get; set; } = "";

    [JsonPropertyName("analysisStage")]
    public string AnalysisStage { get; set; } = "";

    [JsonPropertyName("commitStage")]
    public string CommitStage { get; set; } = "planar-surface-and-guides-write";

    [JsonPropertyName("realizationMode")]
    public string RealizationMode { get; set; } = "planar_surface_with_analysis_guides";

    [JsonPropertyName("targetLayerRoot")]
    public string TargetLayerRoot { get; set; } = "";

    [JsonPropertyName("layerPaths")]
    public IntersectionLayerPaths LayerPaths { get; set; } = new();

    [JsonPropertyName("createdCount")]
    public int CreatedCount { get; set; }

    [JsonPropertyName("createdIds")]
    public List<string> CreatedIds { get; set; } = new();

    [JsonPropertyName("created")]
    public IntersectionCreatedArtifacts Created { get; set; } = new();

    [JsonPropertyName("summaries")]
    public IntersectionRealizationSummaries Summaries { get; set; } = new();

    [JsonPropertyName("fallbackFlags")]
    public List<string> FallbackFlags { get; set; } = new();

    [JsonPropertyName("unresolvedConditions")]
    public JsonElement UnresolvedConditions { get; set; }

    [JsonPropertyName("joinContract")]
    public IntersectionJoinContract? JoinContract { get; set; }
}

public sealed class IntersectionJoinContract
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = IntersectionRealizationSchemas.JoinContractV1;

    [JsonPropertyName("analysisToken")]
    public string AnalysisToken { get; set; } = "";

    [JsonPropertyName("targetLayerRoot")]
    public string TargetLayerRoot { get; set; } = "";

    [JsonPropertyName("realizedBoundaryId")]
    public string? RealizedBoundaryId { get; set; }

    [JsonPropertyName("realizedSurfaceId")]
    public string? RealizedSurfaceId { get; set; }

    [JsonPropertyName("corners")]
    public List<IntersectionJoinCorner> Corners { get; set; } = new();

    [JsonPropertyName("arms")]
    public List<IntersectionJoinArm> Arms { get; set; } = new();
}

public sealed class IntersectionJoinCorner
{
    [JsonPropertyName("cornerId")]
    public string CornerId { get; set; } = "";

    [JsonPropertyName("cornerOrder")]
    public int CornerOrder { get; set; }

    [JsonPropertyName("curbReturnArcId")]
    public string? CurbReturnArcId { get; set; }

    [JsonPropertyName("incomingRoad")]
    public string IncomingRoad { get; set; } = "";

    [JsonPropertyName("incomingSide")]
    public string IncomingSide { get; set; } = "";

    [JsonPropertyName("incomingArmDirection")]
    public string IncomingArmDirection { get; set; } = "";

    [JsonPropertyName("outgoingRoad")]
    public string OutgoingRoad { get; set; } = "";

    [JsonPropertyName("outgoingSide")]
    public string OutgoingSide { get; set; } = "";

    [JsonPropertyName("outgoingArmDirection")]
    public string OutgoingArmDirection { get; set; } = "";

    [JsonPropertyName("incomingJoinPoint")]
    public IntersectionPoint3 IncomingJoinPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("outgoingJoinPoint")]
    public IntersectionPoint3 OutgoingJoinPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("incomingBoundaryParameter")]
    public double IncomingBoundaryParameter { get; set; }

    [JsonPropertyName("outgoingBoundaryParameter")]
    public double OutgoingBoundaryParameter { get; set; }

    [JsonPropertyName("offsetMode")]
    public string OffsetMode { get; set; } = "";

    [JsonPropertyName("outwardReferencePoint")]
    public IntersectionPoint3? OutwardReferencePoint { get; set; }
}

public sealed class IntersectionJoinArm
{
    [JsonPropertyName("road")]
    public string Road { get; set; } = "";

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("armDirection")]
    public string ArmDirection { get; set; } = "";

    [JsonPropertyName("joinPoint")]
    public IntersectionPoint3 JoinPoint { get; set; } = new(0.0, 0.0, 0.0);

    [JsonPropertyName("boundaryParameter")]
    public double BoundaryParameter { get; set; }
}

public sealed class IntersectionLayerPaths
{
    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = "";

    [JsonPropertyName("boundary")]
    public string Boundary { get; set; } = "";

    [JsonPropertyName("curbReturns")]
    public string CurbReturns { get; set; } = "";

    [JsonPropertyName("surface")]
    public string Surface { get; set; } = "";

    [JsonPropertyName("approachEdges")]
    public string? ApproachEdges { get; set; }

    [JsonPropertyName("approachPatches")]
    public string? ApproachPatches { get; set; }
}

public sealed class IntersectionCreatedArtifacts
{
    [JsonPropertyName("realizedSurfaceId")]
    public string? RealizedSurfaceId { get; set; }

    [JsonPropertyName("realizedBoundaryId")]
    public string? RealizedBoundaryId { get; set; }

    [JsonPropertyName("provisionalBoundaryPolygonId")]
    public string? ProvisionalBoundaryPolygonId { get; set; }

    [JsonPropertyName("boundarySegmentIds")]
    public List<string> BoundarySegmentIds { get; set; } = new();

    [JsonPropertyName("curbReturnArcIds")]
    public List<string> CurbReturnArcIds { get; set; } = new();

    [JsonPropertyName("approachEdgeIds")]
    public List<string> ApproachEdgeIds { get; set; } = new();

    [JsonPropertyName("approachPatchIds")]
    public List<string> ApproachPatchIds { get; set; } = new();
}

public sealed class IntersectionRealizationSummaries
{
    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("surfaceArea")]
    public double SurfaceArea { get; set; }

    [JsonPropertyName("boundaryLineLength")]
    public double BoundaryLineLength { get; set; }

    [JsonPropertyName("curbReturnArcLength")]
    public double CurbReturnArcLength { get; set; }

    [JsonPropertyName("approachEdgeLength")]
    public double ApproachEdgeLength { get; set; }

    [JsonPropertyName("approachPatchArea")]
    public double ApproachPatchArea { get; set; }
}

internal sealed class IntersectionPoint3JsonConverter : JsonConverter<IntersectionPoint3>
{
    public override IntersectionPoint3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            var x = reader.GetDouble();
            reader.Read();
            var y = reader.GetDouble();
            reader.Read();
            var z = reader.TokenType == JsonTokenType.EndArray ? 0.0 : reader.GetDouble();

            while (reader.TokenType != JsonTokenType.EndArray)
                reader.Read();

            return new IntersectionPoint3(x, y, z);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            double x = 0.0;
            double y = 0.0;
            double z = 0.0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new IntersectionPoint3(x, y, z);

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected point property name.");

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "x":
                        x = reader.GetDouble();
                        break;
                    case "y":
                        y = reader.GetDouble();
                        break;
                    case "z":
                        z = reader.GetDouble();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        throw new JsonException("Point value must be an array or object.");
    }

    public override void Write(Utf8JsonWriter writer, IntersectionPoint3 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}
