using RoadCreator.Core.Footprint;
using Xunit;

namespace RoadCreator.Core.Tests.Footprint;

public class StyleSetTests
{
    private static StyleSet BuildSet() => new("test",
        new StyleEntry[]
        {
            new("centerline",       "Layer::CL",   "Center",     0.50),
            new("lane_divider",     "Layer::LD",   "Dashed",     0.18),
            new("edge_of_pavement", "Layer::Edge", "Continuous", 0.50),
        });

    // ── FindStyle ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("centerline")]
    [InlineData("CENTERLINE")]
    [InlineData("CenterLine")]
    public void FindStyle_IsCaseInsensitive(string id)
    {
        var set = BuildSet();
        Assert.NotNull(set.FindStyle(id));
    }

    [Fact]
    public void FindStyle_ReturnsCorrectEntry()
    {
        var set = BuildSet();
        var entry = set.FindStyle("lane_divider");

        Assert.NotNull(entry);
        Assert.Equal("Layer::LD", entry!.Layer);
        Assert.Equal("Dashed", entry.Linetype);
        Assert.Equal(0.18, entry.PrintWidthMm, 1e-10);
    }

    [Fact]
    public void FindStyle_ReturnsNull_WhenNotFound()
    {
        var set = BuildSet();
        Assert.Null(set.FindStyle("nonexistent_role"));
    }

    // ── FindStyleOrDefault ────────────────────────────────────────────────────

    [Fact]
    public void FindStyleOrDefault_ReturnsFallback_WhenNotFound()
    {
        var set = BuildSet();
        var fallback = DefaultStyles.Fallback;
        var result = set.FindStyleOrDefault("missing", fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void FindStyleOrDefault_ReturnsMatch_WhenFound()
    {
        var set = BuildSet();
        var fallback = DefaultStyles.Fallback;
        var result = set.FindStyleOrDefault("centerline", fallback);
        Assert.Equal("centerline", result.Id);
    }

    // ── DefaultStyles ─────────────────────────────────────────────────────────

    [Fact]
    public void DefaultStyles_Generic_ContainsAllExpectedRoles()
    {
        var expected = new[]
        {
            "centerline", "lane_divider", "edge_of_pavement",
            "shoulder", "curb_face", "setback", "right_of_way", "utility_corridor"
        };

        foreach (var role in expected)
            Assert.NotNull(DefaultStyles.Generic.FindStyle(role));
    }

    [Fact]
    public void DefaultStyles_All_ContainsGeneric()
    {
        Assert.True(DefaultStyles.All.ContainsKey("default_generic"));
    }

    [Fact]
    public void DefaultStyles_Fallback_HasContinuousLinetype()
    {
        Assert.Equal("Continuous", DefaultStyles.Fallback.Linetype);
    }

    // ── StyleEntry ────────────────────────────────────────────────────────────

    [Fact]
    public void StyleEntry_NullColor_IsNull()
    {
        var entry = new StyleEntry("cl", "L::CL", "Continuous", 0.5, null);
        Assert.Null(entry.Color);
    }

    [Fact]
    public void StyleEntry_WithColor_RetainsColor()
    {
        var entry = new StyleEntry("cl", "L::CL", "Continuous", 0.5, "#FFFF00");
        Assert.Equal("#FFFF00", entry.Color);
    }
}
