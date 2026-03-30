using RoadCreator.Core.Profiles;
using Xunit;

namespace RoadCreator.Core.Tests.Profiles;

public class RoadProfileSerializerTests
{
    private const string ArterialWithBikeLanesJson = """
        {
          "name": "arterial_with_bike_lanes",
          "version": 1,
          "description": "Urban arterial road with separated bike lanes on both sides. 15m carriageway, 1m buffers, 3m bike lanes, 3.5m shoulders to ROW.",
          "source": {
            "project": "Riyadh",
            "file": "civil_cad_export",
            "extractedAt": "2026-03-10T11:00:00Z",
            "centerlineId": "b63d031f-9244-4ef7-a790-2f24a646f68a",
            "chainage": 62.87
          },
          "symmetric": true,
          "totalWidth": 30.0,
          "features": [
            {
              "type": "carriageway_edge",
              "offset": 7.5,
              "width": 7.5,
              "label": "Curb line"
            },
            {
              "type": "bike_lane_inner",
              "offset": 8.5,
              "width": 1.0,
              "label": "Buffer (curb to bike lane)"
            },
            {
              "type": "bike_lane_outer",
              "offset": 11.5,
              "width": 3.0,
              "label": "Bike lane"
            },
            {
              "type": "row",
              "offset": 15.0,
              "width": 3.5,
              "label": "Shoulder to right-of-way"
            }
          ],
          "layerMap": {
            "CPCNTRAV": "centerline",
            "CPCURBNL": "carriageway_edge",
            "MIC_bike": "bike_lane",
            "APBLROW": "row"
          },
          "tags": ["arterial", "bike_lanes", "urban", "separated_bike", "riyadh"]
        }
        """;

    private const string CollectorOneSideBikeJson = """
        {
          "name": "collector_one_side_bike",
          "version": 1,
          "description": "Two-lane collector road with a separated bike lane on one side. 7.6m carriageway, 1m buffer, 3m bike lane, 3.5m shoulder to ROW on the bike side. Which side gets the bike lane is determined at application time.",
          "source": {
            "project": "Riyadh",
            "file": "civil_cad_export",
            "extractedAt": "2026-03-10T11:10:00Z",
            "centerlineId": "cb5d7020-c8bb-4878-ab61-0dc7951b5464",
            "chainage": 15.27
          },
          "symmetric": false,
          "totalWidth": 15.1,
          "features": [
            {
              "type": "carriageway_edge",
              "offset": 3.8,
              "width": 3.8,
              "label": "Curb line"
            },
            {
              "type": "bike_lane_inner",
              "offset": 4.8,
              "width": 1.0,
              "label": "Buffer (curb to bike lane)",
              "bilateral": false
            },
            {
              "type": "bike_lane_outer",
              "offset": 7.8,
              "width": 3.0,
              "label": "Bike lane",
              "bilateral": false
            },
            {
              "type": "row",
              "offset": 11.3,
              "width": 3.5,
              "label": "Shoulder to right-of-way",
              "bilateral": false
            }
          ],
          "layerMap": {
            "CPCNTRAV": "centerline",
            "CPCURBNL": "carriageway_edge",
            "MIC_bike": "bike_lane",
            "APBLROW": "row"
          },
          "tags": ["collector", "two_lane", "bike_lane", "asymmetric", "one_side_bike", "riyadh"]
        }
        """;

    [Fact]
    public void Deserialize_CurrentNativeShape_ParsesArterialProfile()
    {
        var profile = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson);

        Assert.NotNull(profile);
        Assert.Equal("arterial_with_bike_lanes", profile!.Name);
        Assert.Equal(RoadProfileSchemas.DefinitionV1, profile.Schema);
        Assert.Equal("m", profile.Units);
        Assert.True(profile.Symmetric);
        Assert.Equal(30.0, profile.TotalWidth);
        Assert.Equal(4, profile.Features.Count);
        Assert.Equal("carriageway_edge-0", profile.Features[0].Id);
        Assert.Equal("row-0", profile.Features[3].Id);
        Assert.Contains(
            RoadProfileBoundaryRoles.CarriagewaySurface,
            profile.Features[0].BoundaryRoles);
        Assert.Contains(
            RoadProfileBoundaryRoles.OuterEnvelope,
            profile.Features[3].BoundaryRoles);
    }

    [Fact]
    public void Deserialize_CurrentNativeShape_ParsesAsymmetricProfile()
    {
        var profile = RoadProfileSerializer.Deserialize(CollectorOneSideBikeJson);

        Assert.NotNull(profile);
        Assert.False(profile!.Symmetric);
        Assert.True(profile.HasUnilateralFeatures);
        Assert.True(profile.Features[0].EligibleForIntersectionTopology);
        Assert.False(profile.Features[1].Bilateral);
        Assert.Equal("bike_lane_inner-0", profile.Features[1].Id);
    }

    [Fact]
    public void SummaryBuilder_MatchesCurrentIntersectionOffsets()
    {
        var arterial = RoadProfileSummaryBuilder.Build(
            RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!);
        var collector = RoadProfileSummaryBuilder.Build(
            RoadProfileSerializer.Deserialize(CollectorOneSideBikeJson)!);

        Assert.Equal(15.0, arterial.OuterEnvelopeOffset, 1e-10);
        Assert.Equal(7.5, arterial.CarriagewaySurfaceOffset, 1e-10);
        Assert.Equal(7.5, arterial.CurbReturnDriverOffset, 1e-10);
        Assert.Equal(3.8, collector.CarriagewaySurfaceOffset, 1e-10);
        Assert.Equal(3.8, collector.CurbReturnDriverOffset, 1e-10);
        Assert.Equal(11.3, collector.OuterEnvelopeOffset, 1e-10);
        Assert.True(collector.RequiresSideSelection);
    }

    [Fact]
    public void Serialize_RoundTripsCanonicalFields()
    {
        var profile = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!;

        var json = RoadProfileSerializer.Serialize(profile);
        var restored = RoadProfileSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Contains(RoadProfileSchemas.DefinitionV1, json);
        Assert.Equal(profile.Name, restored!.Name);
        Assert.Equal(profile.Features.Count, restored.Features.Count);
        Assert.Equal(profile.Features[0].Id, restored.Features[0].Id);
        Assert.Contains(
            RoadProfileBoundaryRoles.CarriagewaySurface,
            restored.Features[0].BoundaryRoles);
    }

    [Fact]
    public void Serialize_RoundTripsIntersectionArmLengthDefaults()
    {
        var source = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!;
        var profile = new RoadProfileDefinition
        {
            Schema = source.Schema,
            Name = source.Name,
            Version = source.Version,
            Units = source.Units,
            Description = source.Description,
            Symmetric = source.Symmetric,
            TotalWidth = source.TotalWidth,
            Source = source.Source,
            Features = source.Features,
            LayerMap = source.LayerMap,
            Tags = source.Tags,
            CrossSectionDefaults = source.CrossSectionDefaults,
            IntersectionDefaults = new RoadProfileIntersectionDefaults
            {
                ArmLengthOuterEnvelopeMultiplier = 2.25,
                ArmLengthCarriagewayMultiplier = 3.5,
                ArmLengthDiagonalMultiplier = 0.72,
                ArmLengthRadiusMultiplier = 2.4,
                ArmLengthMin = 10.0,
                ArmLengthMax = 48.0,
            }
        };

        var json = RoadProfileSerializer.Serialize(profile);
        var restored = RoadProfileSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.IntersectionDefaults);
        Assert.Equal(2.25, restored.IntersectionDefaults!.ArmLengthOuterEnvelopeMultiplier!.Value, 10);
        Assert.Equal(48.0, restored.IntersectionDefaults!.ArmLengthMax!.Value, 10);
        Assert.Contains("\"intersectionDefaults\"", json);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForInvalidSchema()
    {
        var result = RoadProfileSerializer.Deserialize(
            "{\"schema\":\"other.tool/v1\",\"name\":\"test\",\"features\":[{\"type\":\"carriageway_edge\",\"offset\":1.0,\"width\":1.0}]}");

        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForMissingName()
    {
        var result = RoadProfileSerializer.Deserialize(
            "{\"features\":[{\"type\":\"carriageway_edge\",\"offset\":1.0,\"width\":1.0}]}");

        Assert.Null(result);
    }

    [Fact]
    public void CompactSerializer_ProducesShortKeyJson()
    {
        var profile = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!;

        var json = RoadProfileCompactSerializer.Serialize(profile);

        Assert.Contains("\"s\":\"roadcreator.road-profile.compact/v1\"", json);
        Assert.Contains("\"n\":\"arterial_with_bike_lanes\"", json);
        Assert.Contains("\"sym\":true", json);
        Assert.Contains("\"f\":[[", json);
        Assert.DoesNotContain("\"features\"", json);
        Assert.DoesNotContain("\"description\"", json);
    }

    [Fact]
    public void CompactSerializer_RoundTripsThroughCanonicalModel()
    {
        var profile = RoadProfileSerializer.Deserialize(CollectorOneSideBikeJson)!;

        var json = RoadProfileCompactSerializer.Serialize(profile);
        var restored = RoadProfileCompactSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(profile.Name, restored!.Name);
        Assert.Equal(profile.Symmetric, restored.Symmetric);
        Assert.Equal(profile.Features.Count, restored.Features.Count);
        Assert.Equal(profile.Features[0].Type, restored.Features[0].Type);
        Assert.Equal(profile.Features[0].Offset, restored.Features[0].Offset, 1e-10);
        Assert.Contains(
            RoadProfileBoundaryRoles.CurbReturnDriver,
            restored.Features[0].BoundaryRoles);
        Assert.False(restored.Features[1].Bilateral);
    }

    [Fact]
    public void CompactSerializer_RoundTripsIntersectionArmLengthDefaults()
    {
        var source = RoadProfileSerializer.Deserialize(CollectorOneSideBikeJson)!;
        var profile = new RoadProfileDefinition
        {
            Schema = source.Schema,
            Name = source.Name,
            Version = source.Version,
            Units = source.Units,
            Description = source.Description,
            Symmetric = source.Symmetric,
            TotalWidth = source.TotalWidth,
            Source = source.Source,
            Features = source.Features,
            LayerMap = source.LayerMap,
            Tags = source.Tags,
            CrossSectionDefaults = source.CrossSectionDefaults,
            IntersectionDefaults = new RoadProfileIntersectionDefaults
            {
                ArmLengthOuterEnvelopeMultiplier = 2.25,
                ArmLengthCarriagewayMultiplier = 3.5,
                ArmLengthDiagonalMultiplier = 0.72,
                ArmLengthRadiusMultiplier = 2.4,
                ArmLengthMin = 10.0,
                ArmLengthMax = 48.0,
            }
        };

        var json = RoadProfileCompactSerializer.Serialize(profile);
        var restored = RoadProfileCompactSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.IntersectionDefaults);
        Assert.Equal(0.72, restored.IntersectionDefaults!.ArmLengthDiagonalMultiplier!.Value, 10);
        Assert.Equal(10.0, restored.IntersectionDefaults!.ArmLengthMin!.Value, 10);
        Assert.Contains("\"ix\":{", json);
        Assert.Contains("\"ao\":2.25", json);
        Assert.Contains("\"mx\":48", json);
    }

    [Fact]
    public void CompactSerializer_UsesRoleCodes_NotVerboseRoleNames()
    {
        var profile = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!;

        var json = RoadProfileCompactSerializer.Serialize(profile);

        Assert.Contains("cs|cr|it", json);
        Assert.Contains("oe", json);
        Assert.DoesNotContain(RoadProfileBoundaryRoles.CarriagewaySurface, json);
        Assert.DoesNotContain(RoadProfileBoundaryRoles.CurbReturnDriver, json);
    }

    [Fact]
    public void CompactSerializer_DeserializeRejectsWrongSchema()
    {
        var result = RoadProfileCompactSerializer.Deserialize(
            "{\"s\":\"other.tool/v1\",\"n\":\"x\",\"f\":[[\"a\",\"carriageway_edge\",1,1,true,\"cs|cr|it\"]]}");

        Assert.Null(result);
    }

    // ── Builder-shape compatibility tests ────────────────────────────────

    private const string BuilderShapeJson = """
        {
          "schema": "roadcreator.road-profile/v1",
          "name": "urban_two_lane",
          "units": "m",
          "symmetric": true,
          "baseline": "centerline",
          "features": [
            { "id": "cl", "role": "centerline_reference", "offset": 0.0, "styleRef": "centerline" },
            { "id": "eop_l", "role": "edge_of_pavement", "offset": -3.5, "side": "left", "styleRef": "edge" },
            { "id": "eop_r", "role": "edge_of_pavement", "offset": 3.5, "side": "right", "styleRef": "edge" },
            { "id": "curb_l", "type": "curb_face", "offset": -3.8, "side": "left", "styleRef": "curb" },
            { "id": "curb_r", "type": "curb_face", "offset": 3.8, "side": "right", "styleRef": "curb" },
            { "id": "sw_l", "role": "sidewalk_outer", "offset": -5.8, "side": "left", "styleRef": "sidewalk" },
            { "id": "sw_r", "role": "sidewalk_outer", "offset": 5.8, "side": "right", "styleRef": "sidewalk" }
          ],
          "surfaces": [
            { "id": "carriageway", "between": ["eop_l", "eop_r"], "type": "pavement" },
            { "id": "sw_left", "between": ["curb_l", "sw_l"], "type": "sidewalk", "crossfall": 0.02 }
          ],
          "elements": [
            { "id": "curb_left", "type": "curb", "at": "curb_l", "side": "left", "height": 0.2, "topWidth": 0.3 },
            { "id": "guardrail_right", "type": "guardrail", "at": "eop_r", "side": "right", "postSpacing": 4.0 }
          ]
        }
        """;

    [Fact]
    public void Deserialize_BuilderShape_ParsesFeaturesSurfacesElements()
    {
        var profile = RoadProfileSerializer.Deserialize(BuilderShapeJson);

        Assert.NotNull(profile);
        Assert.Equal("urban_two_lane", profile!.Name);
        Assert.Equal("centerline", profile.Baseline);
        Assert.Equal(7, profile.Features.Count);

        // "role" alias resolves to Type
        Assert.Equal("centerline_reference", profile.Features[0].Type);
        Assert.Equal("edge_of_pavement", profile.Features[1].Type);

        // StyleRef and Side are preserved
        Assert.Equal("centerline", profile.Features[0].StyleRef);
        Assert.Equal("left", profile.Features[1].Side);
        Assert.Equal("right", profile.Features[2].Side);
        Assert.Null(profile.Features[0].Side); // centerline has no side

        // Surfaces
        Assert.Equal(2, profile.Surfaces.Count);
        Assert.Equal("carriageway", profile.Surfaces[0].Id);
        Assert.Equal(new List<string> { "eop_l", "eop_r" }, profile.Surfaces[0].Between);
        Assert.Equal("pavement", profile.Surfaces[0].Type);
        Assert.Null(profile.Surfaces[0].Crossfall);
        Assert.Equal(0.02, profile.Surfaces[1].Crossfall);

        // Elements
        Assert.Equal(2, profile.Elements.Count);
        Assert.Equal("curb", profile.Elements[0].Type);
        Assert.Equal("curb_l", profile.Elements[0].At);
        Assert.Equal(0.2, profile.Elements[0].Height);
        Assert.Equal(0.3, profile.Elements[0].TopWidth);
        Assert.Equal("guardrail", profile.Elements[1].Type);
        Assert.Equal(4.0, profile.Elements[1].PostSpacing);
    }

    [Fact]
    public void Deserialize_BuilderShape_PreservesExistingSummarySemantics()
    {
        var profile = RoadProfileSerializer.Deserialize(BuilderShapeJson)!;
        var summary = RoadProfileSummaryBuilder.Build(profile);

        // Summary still works — edge_of_pavement resolves to EdgeOfPavement offset
        Assert.Equal(3.5, summary.EdgeOfPavementOffset, 1e-10);
        Assert.Equal(3.8, summary.CurbReturnDriverOffset, 1e-10);
        Assert.Equal(5.8, summary.OuterEnvelopeOffset, 1e-10);
    }

    [Fact]
    public void Serialize_BuilderShape_RoundTripsSurfacesAndElements()
    {
        var profile = RoadProfileSerializer.Deserialize(BuilderShapeJson)!;
        var json = RoadProfileSerializer.Serialize(profile);
        var restored = RoadProfileSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Surfaces.Count);
        Assert.Equal(2, restored.Elements.Count);
        Assert.Equal("pavement", restored.Surfaces[0].Type);
        Assert.Equal("curb", restored.Elements[0].Type);
        Assert.Equal(0.2, restored.Elements[0].Height);
        Assert.Equal("edge", restored.Features[1].StyleRef);
        Assert.Equal("left", restored.Features[1].Side);
    }

    [Fact]
    public void Deserialize_ExistingNativeShape_StillWorksWithNewFields()
    {
        // Existing JSON has no surfaces, elements, baseline, styleRef, side
        // Must still parse correctly with empty defaults
        var profile = RoadProfileSerializer.Deserialize(ArterialWithBikeLanesJson)!;

        Assert.Equal("centerline", profile.Baseline);
        Assert.Empty(profile.Surfaces);
        Assert.Empty(profile.Elements);
        Assert.Null(profile.Features[0].StyleRef);
        Assert.Null(profile.Features[0].Side);
    }

    [Fact]
    public void Deserialize_DesignVocabulary_NormalizesToCanonicalTypes()
    {
        // Design doc uses: centerline, sidewalk_back, right_of_way
        // These must normalize to: centerline_reference, sidewalk_outer, row
        var json = """
            {
              "name": "design_vocab_test",
              "features": [
                { "id": "cl", "role": "centerline", "offset": 0.0 },
                { "id": "eop", "role": "edge_of_pavement", "offset": 3.5 },
                { "id": "sw", "role": "sidewalk_back", "offset": 5.8 },
                { "id": "row", "role": "right_of_way", "offset": 10.0 },
                { "id": "sh", "role": "shoulder_edge", "offset": 4.5 },
                { "id": "ld", "role": "lane_divider", "offset": 0.0 }
              ]
            }
            """;

        var profile = RoadProfileSerializer.Deserialize(json)!;

        Assert.Equal(RoadProfileFeatureTypes.CenterlineReference, profile.Features[0].Type);
        Assert.Equal(RoadProfileFeatureTypes.EdgeOfPavement, profile.Features[1].Type);
        Assert.Equal(RoadProfileFeatureTypes.SidewalkOuter, profile.Features[2].Type);
        Assert.Equal(RoadProfileFeatureTypes.RightOfWay, profile.Features[3].Type);
        Assert.Equal(RoadProfileFeatureTypes.ShoulderEdge, profile.Features[4].Type);
        Assert.Equal(RoadProfileFeatureTypes.LaneDivider, profile.Features[5].Type);

        // Boundary role inference still works through normalized types
        var summary = RoadProfileSummaryBuilder.Build(profile);
        Assert.Equal(3.5, summary.EdgeOfPavementOffset, 1e-10);
        Assert.Equal(10.0, summary.OuterEnvelopeOffset, 1e-10);
    }
}
