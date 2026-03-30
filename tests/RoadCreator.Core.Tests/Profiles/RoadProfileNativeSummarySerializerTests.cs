using System.Text.Json;
using RoadCreator.Core.Profiles;
using Xunit;

namespace RoadCreator.Core.Tests.Profiles;

public class RoadProfileNativeSummarySerializerTests
{
    [Fact]
    public void Serialize_RecipeProfile_UsesEdgeOfPavementAsCarriagewaySurfaceFallback()
    {
        var build = RoadProfileBuilder.BuildFromRecipe(new ProfileRecipeInput
        {
            Name = "collector_urban",
            LaneWidth = 4.75,
            LanesPerDirection = 1,
            Curb = new CurbRecipe { Height = 0.2, TopWidth = 0.3 },
            Sidewalk = new SidewalkRecipe { Width = 3.0 },
        });

        string json = RoadProfileNativeSummarySerializer.Serialize(build.Profile);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(RoadProfileNativeSummarySerializer.SchemaVersion, root.GetProperty("schema").GetString());
        Assert.True(root.GetProperty("edgeOfPavementOffset").GetDouble() > 0.0);
        Assert.True(root.GetProperty("carriagewaySurfaceOffset").GetDouble() > 0.0);
        Assert.Equal(
            root.GetProperty("edgeOfPavementOffset").GetDouble(),
            root.GetProperty("carriagewaySurfaceOffset").GetDouble());
        Assert.Equal(
            root.GetProperty("carriagewaySurfaceOffset").GetDouble(),
            root.GetProperty("carriagewayEdgeOffset").GetDouble());
    }

    [Fact]
    public void Serialize_ProfileWithoutTotalWidth_DerivesItFromMaxOffset()
    {
        var build = RoadProfileBuilder.BuildFromFeatures(
            "explicit_edges",
            new List<RoadProfileFeatureDefinition>
            {
                new() { Id = "left", Type = RoadProfileFeatureTypes.EdgeOfPavement, Offset = -4.0 },
                new() { Id = "right", Type = RoadProfileFeatureTypes.EdgeOfPavement, Offset = 4.0 },
            });

        Assert.Null(build.Profile.TotalWidth);

        string json = RoadProfileNativeSummarySerializer.Serialize(build.Profile);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(8.0, doc.RootElement.GetProperty("totalWidth").GetDouble());
        Assert.Equal(4.0, doc.RootElement.GetProperty("maxOffset").GetDouble());
    }
}
