using RoadCreator.Core.Math;
using RoadCreator.Core.Slopes;
using Xunit;

namespace RoadCreator.Core.Tests.Slopes;

public class SlopeProfileComputerTests
{
    // --- No-ditch profile ---

    [Fact]
    public void NoDitch_Returns2Points()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            fillSlopeRatio: 1.75, cutSlopeRatio: 1.75,
            includeDitch: false, ditchDepth: 0, ditchWidth: 0);

        Assert.Equal(2, pts.Length);
    }

    [Fact]
    public void NoDitch_StartsAtOrigin()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, false, 0, 0);

        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
        Assert.Equal(0, pts[0].Z, 1e-10);
    }

    [Fact]
    public void NoDitch_TerminalPoint_MatchesFormula()
    {
        double cutRatio = 2.0;
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, cutRatio, false, 0, 0);

        // Terminal: X = 10 × cutRatio, Y = 10
        Assert.Equal(10.0 * cutRatio, pts[1].X, 1e-10);
        Assert.Equal(10.0, pts[1].Y, 1e-10);
        Assert.Equal(0, pts[1].Z, 1e-10);
    }

    [Fact]
    public void NoDitch_DifferentCutRatios_ChangeSlopeAngle()
    {
        var pts1 = SlopeProfileComputer.ComputeSlopeProfile(1.75, 1.5, false, 0, 0);
        var pts2 = SlopeProfileComputer.ComputeSlopeProfile(1.75, 2.5, false, 0, 0);

        // Steeper ratio (1.5) → smaller X at same height
        Assert.True(pts1[1].X < pts2[1].X);
        // Same terminal height
        Assert.Equal(pts1[1].Y, pts2[1].Y, 1e-10);
    }

    // --- Ditch profile ---

    [Fact]
    public void WithDitch_Returns4Points()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.4, 0.5);

        Assert.Equal(4, pts.Length);
    }

    [Fact]
    public void WithDitch_StartsAtOrigin()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.4, 0.5);

        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
    }

    [Fact]
    public void WithDitch_DitchInner_MatchesFormula()
    {
        double fillRatio = 1.75;
        double depth = 0.4;
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            fillRatio, 1.75, true, depth, 0.5);

        // Ditch inner: X = depth × fillRatio, Y = -depth
        Assert.Equal(depth * fillRatio, pts[1].X, 1e-10);
        Assert.Equal(-depth, pts[1].Y, 1e-10);
    }

    [Fact]
    public void WithDitch_DitchOuter_MatchesFormula()
    {
        double fillRatio = 1.75;
        double depth = 0.4;
        double width = 0.5;
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            fillRatio, 1.75, true, depth, width);

        // Ditch outer: X = depth × fillRatio + width, Y = -depth
        double expectedX = depth * fillRatio + width;
        Assert.Equal(expectedX, pts[2].X, 1e-10);
        Assert.Equal(-depth, pts[2].Y, 1e-10);
    }

    [Fact]
    public void WithDitch_Terminal_MatchesFormula()
    {
        double fillRatio = 1.75;
        double cutRatio = 2.0;
        double depth = 0.4;
        double width = 0.5;
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            fillRatio, cutRatio, true, depth, width);

        // Terminal: X = depth×fillRatio + width + 12×cutRatio, Y = 12
        double expectedX = depth * fillRatio + width + 12.0 * cutRatio;
        Assert.Equal(expectedX, pts[3].X, 1e-10);
        Assert.Equal(12.0, pts[3].Y, 1e-10);
    }

    [Fact]
    public void WithDitch_DitchBottomIsFlat()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.5, 0.8);

        // Inner and outer ditch points have same Y (flat bottom)
        Assert.Equal(pts[1].Y, pts[2].Y, 1e-10);
    }

    [Fact]
    public void WithDitch_AllZCoordinatesAreZero()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.4, 0.5);

        foreach (var pt in pts)
            Assert.Equal(0, pt.Z, 1e-10);
    }

    // --- X coordinates are monotonically increasing ---

    [Fact]
    public void NoDitch_XIsIncreasing()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, false, 0, 0);

        for (int i = 1; i < pts.Length; i++)
            Assert.True(pts[i].X > pts[i - 1].X,
                $"Point {i} X={pts[i].X} not greater than point {i - 1} X={pts[i - 1].X}");
    }

    [Fact]
    public void WithDitch_XIsIncreasing()
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.4, 0.5);

        for (int i = 1; i < pts.Length; i++)
            Assert.True(pts[i].X > pts[i - 1].X,
                $"Point {i} X={pts[i].X} not greater than point {i - 1} X={pts[i - 1].X}");
    }

    // --- Embankment profile ---

    [Fact]
    public void EmbankmentProfile_Returns2Points()
    {
        var pts = SlopeProfileComputer.ComputeEmbankmentProfile(1.75);

        Assert.Equal(2, pts.Length);
    }

    [Fact]
    public void EmbankmentProfile_StartsAtOrigin()
    {
        var pts = SlopeProfileComputer.ComputeEmbankmentProfile(1.75);

        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
        Assert.Equal(0, pts[0].Z, 1e-10);
    }

    [Fact]
    public void EmbankmentProfile_TerminalMatchesFormula()
    {
        double cutRatio = 2.0;
        var pts = SlopeProfileComputer.ComputeEmbankmentProfile(cutRatio);

        // Terminal: X = 10 × cutRatio, Y = 10
        Assert.Equal(10.0 * cutRatio, pts[1].X, 1e-10);
        Assert.Equal(10.0, pts[1].Y, 1e-10);
    }

    [Fact]
    public void EmbankmentProfile_MatchesNoDitchProfile()
    {
        double cutRatio = 1.75;
        var embPts = SlopeProfileComputer.ComputeEmbankmentProfile(cutRatio);
        var slopePts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, cutRatio, false, 0, 0);

        // Embankment profile should be identical to no-ditch slope profile
        Assert.Equal(slopePts.Length, embPts.Length);
        for (int i = 0; i < embPts.Length; i++)
        {
            Assert.Equal(slopePts[i].X, embPts[i].X, 1e-10);
            Assert.Equal(slopePts[i].Y, embPts[i].Y, 1e-10);
            Assert.Equal(slopePts[i].Z, embPts[i].Z, 1e-10);
        }
    }

    // --- Validation ---

    [Fact]
    public void ComputeSlopeProfile_NegativeFillRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(-1.0, 1.75, false, 0, 0));
    }

    [Fact]
    public void ComputeSlopeProfile_ZeroFillRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(0, 1.75, false, 0, 0));
    }

    [Fact]
    public void ComputeSlopeProfile_NegativeCutRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, -1.0, false, 0, 0));
    }

    [Fact]
    public void ComputeSlopeProfile_ZeroCutRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, 0, false, 0, 0));
    }

    [Fact]
    public void ComputeSlopeProfile_DitchWithZeroDepth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, 1.75, true, 0, 0.5));
    }

    [Fact]
    public void ComputeSlopeProfile_DitchWithNegativeDepth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, 1.75, true, -0.4, 0.5));
    }

    [Fact]
    public void ComputeSlopeProfile_DitchWithZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, 1.75, true, 0.4, 0));
    }

    [Fact]
    public void ComputeSlopeProfile_DitchWithNegativeWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeSlopeProfile(1.75, 1.75, true, 0.4, -0.5));
    }

    [Fact]
    public void ComputeEmbankmentProfile_ZeroRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeEmbankmentProfile(0));
    }

    [Fact]
    public void ComputeEmbankmentProfile_NegativeRatio_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlopeProfileComputer.ComputeEmbankmentProfile(-1.0));
    }

    // --- Boundary/edge values ---

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void NoDitch_BoundaryRatios_ProduceValidProfile(double ratio)
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(ratio, ratio, false, 0, 0);

        Assert.Equal(2, pts.Length);
        Assert.True(pts[1].X > 0);
        Assert.True(pts[1].Y > 0);
    }

    [Theory]
    [InlineData(0.3, 0.2)]  // Minimum ditch dimensions
    [InlineData(2.0, 2.0)]  // Maximum ditch dimensions
    public void WithDitch_BoundaryDimensions_ProduceValidProfile(double depth, double width)
    {
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, depth, width);

        Assert.Equal(4, pts.Length);
        // All X increasing
        for (int i = 1; i < pts.Length; i++)
            Assert.True(pts[i].X > pts[i - 1].X);
    }

    // --- Specific numeric verification (VBScript equivalence) ---

    [Fact]
    public void WithDitch_DefaultValues_MatchVBScript()
    {
        // Default values from VBScript: fill=1.75, cut=1.75, depth=0.4, width=0.5
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, true, 0.4, 0.5);

        // Point 0: [0, 0, 0]
        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);

        // Point 1: [0.4 × 1.75, -0.4, 0] = [0.7, -0.4, 0]
        Assert.Equal(0.7, pts[1].X, 1e-10);
        Assert.Equal(-0.4, pts[1].Y, 1e-10);

        // Point 2: [0.7 + 0.5, -0.4, 0] = [1.2, -0.4, 0]
        Assert.Equal(1.2, pts[2].X, 1e-10);
        Assert.Equal(-0.4, pts[2].Y, 1e-10);

        // Point 3: [1.2 + 12 × 1.75, 12, 0] = [22.2, 12, 0]
        Assert.Equal(22.2, pts[3].X, 1e-10);
        Assert.Equal(12.0, pts[3].Y, 1e-10);
    }

    [Fact]
    public void NoDitch_DefaultValues_MatchVBScript()
    {
        // Default: cut=1.75
        var pts = SlopeProfileComputer.ComputeSlopeProfile(
            1.75, 1.75, false, 0, 0);

        // Point 0: [0, 0, 0]
        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);

        // Point 1: [10 × 1.75, 10, 0] = [17.5, 10, 0]
        Assert.Equal(17.5, pts[1].X, 1e-10);
        Assert.Equal(10.0, pts[1].Y, 1e-10);
    }
}
