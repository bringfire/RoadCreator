using RoadCreator.Core.Profiles;
using Xunit;

namespace RoadCreator.Core.Tests.Profiles;

public class RoadProfileBuilderTests
{
    // ── Mode A: Explicit features ────────────────────────────────────────

    [Fact]
    public void BuildFromFeatures_MinimalInput_ProducesValidProfile()
    {
        var result = RoadProfileBuilder.BuildFromFeatures(
            "test_road",
            new List<RoadProfileFeatureDefinition>
            {
                new() { Id = "left", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "right", Type = "edge_of_pavement", Offset = 3.5 },
            });

        Assert.Equal("test_road", result.Profile.Name);
        Assert.Equal(RoadProfileSchemas.DefinitionV1, result.Profile.Schema);
        Assert.Equal("centerline", result.Profile.Baseline);
        Assert.Equal(2, result.Profile.Features.Count);
        Assert.True(result.Validation.StructuralValid);
        Assert.True(result.Validation.FootprintReady);
        Assert.Equal("features", result.ExpandedFrom);
    }

    [Fact]
    public void BuildFromFeatures_InfersSideFromOffsetSign()
    {
        var result = RoadProfileBuilder.BuildFromFeatures(
            "test",
            new List<RoadProfileFeatureDefinition>
            {
                new() { Id = "cl", Type = "centerline_reference", Offset = 0.0 },
                new() { Id = "l", Type = "edge_of_pavement", Offset = -4.0 },
                new() { Id = "r", Type = "edge_of_pavement", Offset = 4.0 },
            });

        Assert.Null(result.Profile.Features[0].Side); // zero offset
        Assert.Equal("left", result.Profile.Features[1].Side);
        Assert.Equal("right", result.Profile.Features[2].Side);
    }

    [Fact]
    public void BuildFromFeatures_WithSurfacesAndElements_RealizationReady()
    {
        var result = RoadProfileBuilder.BuildFromFeatures(
            "full",
            new List<RoadProfileFeatureDefinition>
            {
                new() { Id = "eop_l", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "eop_r", Type = "edge_of_pavement", Offset = 3.5 },
                new() { Id = "curb_l", Type = "curb_face", Offset = -3.8 },
            },
            surfaces: new List<ProfileSurface>
            {
                new() { Id = "carriageway", Between = new() { "eop_l", "eop_r" }, Type = "pavement" },
            },
            elements: new List<ProfileElement>
            {
                new() { Id = "curb", Type = "curb", At = "curb_l", Height = 0.2, TopWidth = 0.3 },
            });

        Assert.True(result.Validation.RealizationReady);
        Assert.Equal(1, result.Profile.Surfaces.Count);
        Assert.Equal(1, result.Profile.Elements.Count);
    }

    // ── Mode B: Semantic recipe ──────────────────────────────────────────

    [Fact]
    public void BuildFromRecipe_LanesOnly_ProducesMinimalProfile()
    {
        var result = RoadProfileBuilder.BuildFromRecipe(new ProfileRecipeInput
        {
            Name = "simple",
            LaneWidth = 3.5,
        });

        Assert.True(result.Validation.StructuralValid);
        Assert.True(result.Validation.FootprintReady);
        Assert.True(result.Validation.RealizationReady); // has carriageway surface
        Assert.Equal("recipe", result.ExpandedFrom);
        Assert.Contains(result.Profile.Features, f => f.Id == "eop_l" && f.Offset == -3.5);
        Assert.Contains(result.Profile.Features, f => f.Id == "eop_r" && f.Offset == 3.5);
        Assert.Contains(result.Profile.Surfaces, s => s.Id == "carriageway");
    }

    [Fact]
    public void BuildFromRecipe_MultiLane_ExpandsCorrectly()
    {
        var result = RoadProfileBuilder.BuildFromRecipe(new ProfileRecipeInput
        {
            Name = "multi",
            LaneWidth = 3.5,
            LanesPerDirection = 2,
        });

        // Edge of pavement at ±(3.5 * 2) = ±7.0
        Assert.Contains(result.Profile.Features, f => f.Id == "eop_l" && f.Offset == -7.0);
        Assert.Contains(result.Profile.Features, f => f.Id == "eop_r" && f.Offset == 7.0);
    }

    [Fact]
    public void BuildFromRecipe_FullUrbanStreet_ProducesAllComponents()
    {
        var result = RoadProfileBuilder.BuildFromRecipe(new ProfileRecipeInput
        {
            Name = "urban",
            LaneWidth = 3.5,
            Curb = new CurbRecipe { Height = 0.2, TopWidth = 0.3 },
            Sidewalk = new SidewalkRecipe { Width = 2.0 },
        });

        // Should have: cl, eop_l, eop_r, curb_l, curb_r, sw_l, sw_r = 7 features
        Assert.Equal(7, result.Profile.Features.Count);

        // Curb at ±(3.5 + 0.3) = ±3.8
        Assert.Contains(result.Profile.Features, f => f.Id == "curb_l" && f.Offset == -3.8);

        // Sidewalk at ±(3.8 + 2.0) = ±5.8
        Assert.Contains(result.Profile.Features, f => f.Id == "sw_l" && f.Offset == -5.8);

        // Surfaces: carriageway + sw_left + sw_right
        Assert.Equal(3, result.Profile.Surfaces.Count);

        // Elements: curb_left + curb_right
        Assert.Equal(2, result.Profile.Elements.Count);

        Assert.True(result.Validation.RealizationReady);
    }

    [Fact]
    public void BuildFromRecipe_WithGuardrail_AddsElements()
    {
        var result = RoadProfileBuilder.BuildFromRecipe(new ProfileRecipeInput
        {
            Name = "guardrail_test",
            LaneWidth = 4.0,
            Guardrail = new GuardrailRecipe { PostSpacing = 4.0 },
        });

        Assert.Contains(result.Profile.Elements, e =>
            e.Type == "guardrail" && e.At == "eop_l");
        Assert.Contains(result.Profile.Elements, e =>
            e.Type == "guardrail" && e.At == "eop_r");
    }

    // ── Mode C: Observations ─────────────────────────────────────────────

    [Fact]
    public void BuildFromObservations_ConvertsToSignedOffsets()
    {
        var result = RoadProfileBuilder.BuildFromObservations(
            "captured",
            new List<ProfileObservation>
            {
                new() { CurveId = "c1", Side = "left", Offset = 3.5, Role = "edge_of_pavement", StyleRef = "edge" },
                new() { CurveId = "c2", Side = "right", Offset = 3.5, Role = "edge_of_pavement", StyleRef = "edge" },
            });

        Assert.Equal("observations", result.ExpandedFrom);
        Assert.Equal(-3.5, result.Profile.Features[0].Offset);
        Assert.Equal(3.5, result.Profile.Features[1].Offset);
        Assert.NotNull(result.FeatureProvenance);
        Assert.Equal(2, result.FeatureProvenance!.Count);
        Assert.Equal("c1", result.FeatureProvenance[0].CurveId);
    }

    [Fact]
    public void BuildFromObservations_NormalizesDesignVocabulary()
    {
        var result = RoadProfileBuilder.BuildFromObservations(
            "vocab_test",
            new List<ProfileObservation>
            {
                new() { Side = "left", Offset = 3.5, Role = "sidewalk_back" },
                new() { Side = "right", Offset = 10.0, Role = "right_of_way" },
            });

        Assert.Equal(RoadProfileFeatureTypes.SidewalkOuter, result.Profile.Features[0].Type);
        Assert.Equal(RoadProfileFeatureTypes.RightOfWay, result.Profile.Features[1].Type);
    }

    [Fact]
    public void BuildFromObservations_AutoGeneratesIds()
    {
        var result = RoadProfileBuilder.BuildFromObservations(
            "auto_id",
            new List<ProfileObservation>
            {
                new() { Side = "left", Offset = 3.5, Role = "edge_of_pavement" },
                new() { Side = "right", Offset = 3.5, Role = "edge_of_pavement" },
            });

        Assert.Equal("edge_of_pavement-0", result.Profile.Features[0].Id);
        Assert.Equal("edge_of_pavement-1", result.Profile.Features[1].Id);
    }

    [Fact]
    public void BuildFromObservations_PreservesProvenance()
    {
        var result = RoadProfileBuilder.BuildFromObservations(
            "prov_test",
            new List<ProfileObservation>
            {
                new() {
                    CurveId = "abc-123",
                    Layer = "RoadCreator::Road_1::Edge",
                    ObjectName = "Left Edge",
                    Side = "left",
                    Offset = 3.5,
                    Role = "edge_of_pavement",
                },
            });

        Assert.Single(result.FeatureProvenance!);
        var prov = result.FeatureProvenance[0];
        Assert.Equal("abc-123", prov.CurveId);
        Assert.Equal("RoadCreator::Road_1::Edge", prov.Layer);
        Assert.Equal("Left Edge", prov.ObjectName);
    }
}

public class RoadProfileValidatorTests
{
    [Fact]
    public void Validate_EmptyName_StructuralFailure()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "",
            Features = new() { new() { Id = "a", Type = "edge_of_pavement", Offset = 3.5 } },
        };

        var result = RoadProfileValidator.Validate(profile);

        Assert.False(result.StructuralValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validate_DuplicateFeatureIds_StructuralFailure()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "dup", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "dup", Type = "edge_of_pavement", Offset = 3.5 },
            },
        };

        var result = RoadProfileValidator.Validate(profile);

        Assert.False(result.StructuralValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate"));
    }

    [Fact]
    public void Validate_SurfaceReferencesUnknownFeature_Error()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new() { new() { Id = "a", Type = "edge_of_pavement", Offset = -3.5 } },
            Surfaces = new() { new() { Id = "s", Between = new() { "a", "nonexistent" }, Type = "pavement" } },
        };

        var result = RoadProfileValidator.Validate(profile);

        Assert.False(result.StructuralValid);
        Assert.Contains(result.Errors, e => e.Contains("nonexistent"));
    }

    [Fact]
    public void Validate_CurbWithoutHeight_Error()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "l", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "r", Type = "edge_of_pavement", Offset = 3.5 },
            },
            Elements = new() { new() { Id = "c", Type = "curb", At = "l" } },
        };

        var result = RoadProfileValidator.Validate(profile);

        Assert.False(result.StructuralValid);
        Assert.Contains(result.Errors, e => e.Contains("height"));
    }

    [Fact]
    public void Validate_FootprintReady_RequiresBothSides()
    {
        var oneSideOnly = new RoadProfileDefinition
        {
            Name = "test",
            Features = new() { new() { Id = "r", Type = "edge_of_pavement", Offset = 3.5 } },
        };

        var result = RoadProfileValidator.Validate(oneSideOnly);

        Assert.True(result.StructuralValid);
        Assert.False(result.FootprintReady);
    }

    [Fact]
    public void Validate_RealizationReady_RequiresSurfaces()
    {
        var noSurfaces = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "l", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "r", Type = "edge_of_pavement", Offset = 3.5 },
            },
        };

        var result = RoadProfileValidator.Validate(noSurfaces);

        Assert.True(result.FootprintReady);
        Assert.False(result.RealizationReady);
        Assert.Contains(result.Warnings, w => w.Contains("2D-only"));
    }
}

public class RoadProfileProjectorTests
{
    [Fact]
    public void Project_ProducesOffsetProfileFromFeatures()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "l", Type = "edge_of_pavement", Offset = -3.5, StyleRef = "edge" },
                new() { Id = "r", Type = "edge_of_pavement", Offset = 3.5, StyleRef = "edge" },
            },
            Surfaces = new() { new() { Id = "s", Between = new() { "l", "r" }, Type = "pavement" } },
            Elements = new() { new() { Id = "e", Type = "curb", At = "l", Height = 0.2, TopWidth = 0.3 } },
        };

        var result = RoadProfileProjector.Project(profile);

        Assert.Equal("test", result.OffsetProfile.Name);
        Assert.Equal(2, result.OffsetProfile.Features.Count);
        Assert.Equal("edge_of_pavement", result.OffsetProfile.Features[0].Role);
        Assert.Equal("edge", result.OffsetProfile.Features[0].StyleRef);
        Assert.Equal(-3.5, result.OffsetProfile.Features[0].Offset);
        Assert.Equal(1, result.DroppedSurfaces);
        Assert.Equal(1, result.DroppedElements);
    }

    [Fact]
    public void Project_MissingStyleRef_DefaultsToEmpty()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "a", Type = "edge_of_pavement", Offset = -3.5 },
            },
        };

        var result = RoadProfileProjector.Project(profile);

        Assert.Equal("", result.OffsetProfile.Features[0].StyleRef);
    }
}
