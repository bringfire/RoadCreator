using RoadCreator.Core.Profiles;
using Xunit;

namespace RoadCreator.Core.Tests.Profiles;

public class ProfileCrossSectionBuilderTests
{
    /// <summary>Helper: build a minimal realizable profile with pavement surface.</summary>
    private static RoadProfileDefinition MakeProfile(
        double leftOffset = -3.5,
        double rightOffset = 3.5,
        double? crossfallStraight = null,
        double? crossfallCurve = null,
        double? surfaceCrossfall = null,
        string leftType = "edge_of_pavement",
        string rightType = "edge_of_pavement")
    {
        return new RoadProfileDefinition
        {
            Name = "test",
            Baseline = "centerline",
            Features = new()
            {
                new() { Id = "left", Type = leftType, Offset = leftOffset },
                new() { Id = "right", Type = rightType, Offset = rightOffset },
            },
            Surfaces = new()
            {
                new()
                {
                    Id = "carriageway",
                    Between = new() { "left", "right" },
                    Type = ProfileSurfaceTypes.Pavement,
                    Crossfall = surfaceCrossfall,
                },
            },
            CrossSectionDefaults = (crossfallStraight != null || crossfallCurve != null)
                ? new RoadProfileCrossSectionDefaults
                {
                    CrossfallStraight = crossfallStraight,
                    CrossfallCurve = crossfallCurve,
                }
                : null,
        };
    }

    // ── Test 1: Straight, no crossfall -> flat ───────────────────────────

    [Fact]
    public void Straight_NoCrossfall_ProducesFlatSection()
    {
        var profile = MakeProfile();
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0);

        Assert.Equal(3, pts.Length);
        Assert.Equal(-3.5, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
        Assert.Equal(0, pts[1].X, 1e-10);
        Assert.Equal(0, pts[1].Y, 1e-10);
        Assert.Equal(3.5, pts[2].X, 1e-10);
        Assert.Equal(0, pts[2].Y, 1e-10);
    }

    // ── Test 2: Straight with CrossSectionDefaults ───────────────────────

    [Fact]
    public void Straight_WithDefaults_ProducesSymmetricSlope()
    {
        var profile = MakeProfile(crossfallStraight: 2.5);

        // curveDirection=0 means straight: M=1, Z=0
        // zLeft = ((1*(-2.5)) + (-1 * pmax * 0)) / 100 * 3.5 = -2.5/100 * 3.5 = -0.0875
        // zRight = ((1*(-2.5)) + (+1 * pmax * 0)) / 100 * 3.5 = -2.5/100 * 3.5 = -0.0875
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0);

        Assert.Equal(-0.0875, pts[0].Y, 1e-10); // left edge
        Assert.Equal(-0.0875, pts[2].Y, 1e-10); // right edge (symmetric in straight)
    }

    // ── Test 3: Left curve (curveDirection=+1) -> asymmetric banking ────

    [Fact]
    public void LeftCurve_ProducesAsymmetricBanking()
    {
        var profile = MakeProfile(crossfallStraight: 2.5, crossfallCurve: 4.0);

        // curveDirection=+1 (left curve, matching CrossSectionComputer convention): M=0, Z=+1
        // zLeft  = ((0*(-2.5)) + (-1 * 4.0 * +1)) / 100 * 3.5 = (-4.0)/100 * 3.5 = -0.14
        // zRight = ((0*(-2.5)) + (+1 * 4.0 * +1)) / 100 * 3.5 = (4.0)/100 * 3.5 = 0.14
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, +1.0);

        Assert.Equal(-0.14, pts[0].Y, 1e-10);  // left edge lowered
        Assert.Equal(0.14, pts[2].Y, 1e-10);   // right edge raised
    }

    // ── Test 4: Right curve (curveDirection=-1) -> opposite banking ──────

    [Fact]
    public void RightCurve_ProducesOppositeBanking()
    {
        var profile = MakeProfile(crossfallStraight: 2.5, crossfallCurve: 4.0);

        // curveDirection=-1 (right curve, matching CrossSectionComputer convention): M=0, Z=-1
        // zLeft  = ((0*(-2.5)) + (-1 * 4.0 * -1)) / 100 * 3.5 = (4.0)/100 * 3.5 = 0.14
        // zRight = ((0*(-2.5)) + (+1 * 4.0 * -1)) / 100 * 3.5 = (-4.0)/100 * 3.5 = -0.14
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, -1.0);

        Assert.Equal(0.14, pts[0].Y, 1e-10);   // left edge raised
        Assert.Equal(-0.14, pts[2].Y, 1e-10);  // right edge lowered
    }

    // ── Test 5: Surface-level crossfall fallback ─────────────────────────

    [Fact]
    public void SurfaceCrossfall_UsedWhenNoDefaults()
    {
        var profile = MakeProfile(surfaceCrossfall: 3.0);

        // curveDirection=0: M=1, Z=0
        // p=3.0, pmax=3.0 (surface crossfall used for both)
        // zLeft = ((1*(-3.0)) + (-1 * 3.0 * 0)) / 100 * 3.5 = -3.0/100 * 3.5 = -0.105
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0);

        Assert.Equal(-0.105, pts[0].Y, 1e-10);
    }

    // ── Test 6: Override precedence ──────────────────────────────────────

    [Fact]
    public void Overrides_TakePrecedenceOverDefaults()
    {
        var profile = MakeProfile(crossfallStraight: 2.5, crossfallCurve: 4.0);

        // Override with 5.0 straight, 6.0 curve
        // curveDirection=0: M=1, Z=0
        // zLeft = ((1*(-5.0)) + 0) / 100 * 3.5 = -0.175
        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, 5.0, 6.0, 0);

        Assert.Equal(-0.175, pts[0].Y, 1e-10);
        Assert.Equal(-0.175, pts[2].Y, 1e-10);
    }

    // ── Test 7: Multiple pavement surfaces -> error ──────────────────────

    [Fact]
    public void MultiplePavementSurfaces_ThrowsError()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "l1", Type = "edge_of_pavement", Offset = -3.5 },
                new() { Id = "r1", Type = "edge_of_pavement", Offset = 3.5 },
                new() { Id = "l2", Type = "edge_of_pavement", Offset = -7.0 },
                new() { Id = "r2", Type = "edge_of_pavement", Offset = 7.0 },
            },
            Surfaces = new()
            {
                new() { Id = "pave1", Between = new() { "l1", "r1" }, Type = "pavement" },
                new() { Id = "pave2", Between = new() { "l2", "r2" }, Type = "pavement" },
            },
        };

        var ex = Assert.Throws<System.ArgumentException>(() =>
            ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0));

        Assert.Contains("exactly one", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 8: No pavement surface, semantic fallback works ─────────────

    [Fact]
    public void NoPavementSurface_SemanticFallback_Works()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "eop_l", Type = "edge_of_pavement", Offset = -4.0 },
                new() { Id = "eop_r", Type = "edge_of_pavement", Offset = 4.0 },
                new() { Id = "sw", Type = "sidewalk_outer", Offset = -6.0 },
            },
        };

        var pts = ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0);

        // Should use edge_of_pavement features, not sidewalk
        Assert.Equal(-4.0, pts[0].X, 1e-10);
        Assert.Equal(4.0, pts[2].X, 1e-10);
    }

    // ── Test 9: No pavement surface, no semantic boundaries -> error ─────

    [Fact]
    public void NoPavementSurface_NoSemanticBoundaries_ThrowsError()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "sw_l", Type = "sidewalk_outer", Offset = -6.0 },
                new() { Id = "sw_r", Type = "sidewalk_outer", Offset = 6.0 },
            },
        };

        var ex = Assert.Throws<System.ArgumentException>(() =>
            ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0));

        Assert.Contains("cannot determine", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 10: Same-sign boundaries -> error ───────────────────────────

    [Fact]
    public void SameSignBoundaries_ThrowsError()
    {
        var profile = new RoadProfileDefinition
        {
            Name = "test",
            Features = new()
            {
                new() { Id = "a", Type = "edge_of_pavement", Offset = 2.0 },
                new() { Id = "b", Type = "edge_of_pavement", Offset = 5.0 },
            },
            Surfaces = new()
            {
                new() { Id = "pave", Between = new() { "a", "b" }, Type = "pavement" },
            },
        };

        // footprintReady will fail (no negative offset)
        var ex = Assert.Throws<System.ArgumentException>(() =>
            ProfileCrossSectionBuilder.ComputeSectionPoints(profile, null, null, 0));

        Assert.Contains("footprint-ready", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
