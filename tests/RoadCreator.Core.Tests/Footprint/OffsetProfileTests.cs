using RoadCreator.Core.Footprint;
using Xunit;

namespace RoadCreator.Core.Tests.Footprint;

public class OffsetProfileTests
{
    private static OffsetProfile SimpleProfile() => new(
        "test", "m", "centerline",
        new OffsetFeature[]
        {
            new("cl",      0.0,   "centerline",      "centerline"),
            new("ld_l",   -3.75,  "lane_divider",    "lane_divider"),
            new("edge_l", -7.5,   "edge_of_pavement","edge_of_pavement"),
            new("ld_r",    3.75,  "lane_divider",    "lane_divider"),
            new("edge_r",  7.5,   "edge_of_pavement","edge_of_pavement"),
        });

    // ── OrderedFeatures ───────────────────────────────────────────────────────

    [Fact]
    public void OrderedFeatures_ReturnsSortedLeftToRight()
    {
        var profile = SimpleProfile();
        var offsets = profile.OrderedFeatures.Select(f => f.Offset).ToList();
        Assert.Equal(offsets.OrderBy(x => x).ToList(), offsets);
    }

    [Fact]
    public void OrderedFeatures_LeftmostIsNegative()
    {
        var profile = SimpleProfile();
        Assert.True(profile.OrderedFeatures.First().Offset < 0);
    }

    // ── ResolveAbsoluteOffset — centerline baseline ───────────────────────────

    [Theory]
    [InlineData("cl",      0.0)]
    [InlineData("ld_l",   -3.75)]
    [InlineData("edge_r",  7.5)]
    public void ResolveAbsoluteOffset_CenterlineBaseline_ReturnsOffsetDirectly(
        string featureId, double expected)
    {
        var profile = SimpleProfile();
        var feature = profile.Features.First(f => f.Id == featureId);
        Assert.Equal(expected, profile.ResolveAbsoluteOffset(feature), 1e-10);
    }

    // ── ResolveAbsoluteOffset — non-centerline baseline ───────────────────────

    [Fact]
    public void ResolveAbsoluteOffset_NonCenterlineBaseline_AddsBaselineOffset()
    {
        // Sidewalk profile: offsets measured from edge_of_pavement (offset=7.5)
        var sidewalk = new OffsetProfile("sidewalk", "m", "edge_r",
            new OffsetFeature[]
            {
                new("curb",     0.5,  "curb_face", "curb_face"),
                new("sw_edge",  2.5,  "setback",   "setback"),
            });

        // Inherit edge_r from parent profile by injecting it with offset=7.5
        // For standalone use, the baseline feature must exist in the profile.
        // In composable usage the consumer resolves baseline externally.
        // Here we test the simple intra-profile case:
        var intra = new OffsetProfile("sidewalk_intra", "m", "curb_base",
            new OffsetFeature[]
            {
                new("curb_base",  8.0,  "curb_face", "curb_face"),
                new("sw_edge",    2.5,  "setback",   "setback"),   // 8.0 + 2.5 = 10.5
            });

        var swEdge = intra.Features.First(f => f.Id == "sw_edge");
        Assert.Equal(10.5, intra.ResolveAbsoluteOffset(swEdge), 1e-10);
    }

    [Fact]
    public void ResolveAbsoluteOffset_ThrowsWhenBaselineNotFound()
    {
        var profile = new OffsetProfile("bad", "m", "nonexistent",
            new OffsetFeature[] { new("f1", 1.0, "edge_of_pavement", "edge_of_pavement") });

        var feature = profile.Features[0];
        Assert.Throws<InvalidOperationException>(() => profile.ResolveAbsoluteOffset(feature));
    }

    // ── Asymmetry ─────────────────────────────────────────────────────────────

    [Fact]
    public void Profile_CanBeAsymmetric_LeftAndRightHaveDifferentCounts()
    {
        // 1 lane left, 2 lanes right — a passing lane profile
        var profile = new OffsetProfile("2plus1", "m", "centerline",
            new OffsetFeature[]
            {
                new("cl",       0.0,   "centerline",      "centerline"),
                new("edge_l",  -3.75,  "edge_of_pavement","edge_of_pavement"),
                new("ld_r1",    3.75,  "lane_divider",    "lane_divider"),
                new("ld_r2",    7.5,   "lane_divider",    "lane_divider"),
                new("edge_r",  11.25,  "edge_of_pavement","edge_of_pavement"),
            });

        var leftFeatures  = profile.Features.Where(f => f.Offset < 0).ToList();
        var rightFeatures = profile.Features.Where(f => f.Offset > 0).ToList();

        Assert.Equal(1, leftFeatures.Count);
        Assert.Equal(3, rightFeatures.Count);
    }
}
