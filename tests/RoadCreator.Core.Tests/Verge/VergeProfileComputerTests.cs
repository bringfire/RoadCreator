using RoadCreator.Core.Math;
using RoadCreator.Core.Verge;
using Xunit;

namespace RoadCreator.Core.Tests.Verge;

public class VergeProfileComputerTests
{
    // --- Basic profile shape ---

    [Fact]
    public void ComputeVergeProfile_Returns2Points()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(0.5);

        Assert.Equal(2, pts.Length);
    }

    [Fact]
    public void ComputeVergeProfile_StartsAtOrigin()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(0.75);

        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
        Assert.Equal(0, pts[0].Z, 1e-10);
    }

    [Fact]
    public void ComputeVergeProfile_EndX_EqualsWidth()
    {
        double width = 1.5;
        var pts = VergeProfileComputer.ComputeVergeProfile(width);

        Assert.Equal(width, pts[1].X, 1e-10);
    }

    [Fact]
    public void ComputeVergeProfile_DefaultSlope_Is8Percent()
    {
        double width = 1.0;
        var pts = VergeProfileComputer.ComputeVergeProfile(width);

        // Y = -width × 0.08 = -0.08
        Assert.Equal(-0.08, pts[1].Y, 1e-10);
    }

    [Fact]
    public void ComputeVergeProfile_DefaultSlope_MatchesVBScript()
    {
        // VBScript: Rhino.Addline(Array(0, 0, 0), Array(sirka, 0, -sirka * 0.08))
        double width = 0.5;
        var pts = VergeProfileComputer.ComputeVergeProfile(width);

        Assert.Equal(0.5, pts[1].X, 1e-10);
        Assert.Equal(-0.5 * 0.08, pts[1].Y, 1e-10);
        Assert.Equal(0, pts[1].Z, 1e-10);
    }

    [Fact]
    public void ComputeVergeProfile_ZCoordinatesAreZero()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(2.0);

        foreach (var pt in pts)
            Assert.Equal(0, pt.Z, 1e-10);
    }

    // --- Custom slope ---

    [Fact]
    public void ComputeVergeProfile_CustomSlope_AppliedCorrectly()
    {
        double width = 1.0;
        double slope = 0.12; // 12%
        var pts = VergeProfileComputer.ComputeVergeProfile(width, slope);

        Assert.Equal(-width * slope, pts[1].Y, 1e-10);
    }

    [Fact]
    public void ComputeVergeProfile_ZeroSlope_FlatProfile()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(1.0, 0.0);

        Assert.Equal(0, pts[1].Y, 1e-10);
    }

    // --- Profile goes downward ---

    [Fact]
    public void ComputeVergeProfile_EndpointIsBelowOrigin()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(2.0);

        Assert.True(pts[1].Y < 0, "Verge endpoint should be below road edge (negative Y)");
    }

    // --- X is increasing ---

    [Fact]
    public void ComputeVergeProfile_XIsIncreasing()
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(1.0);

        Assert.True(pts[1].X > pts[0].X);
    }

    // --- Width proportional to drop ---

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(5.0)]
    [InlineData(20.0)]
    public void ComputeVergeProfile_DropProportionalToWidth(double width)
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(width);

        double expectedDrop = -width * VergeProfileComputer.DefaultSlope;
        Assert.Equal(expectedDrop, pts[1].Y, 1e-10);
    }

    // --- DefaultSlope constant ---

    [Fact]
    public void DefaultSlope_Is0_08()
    {
        Assert.Equal(0.08, VergeProfileComputer.DefaultSlope, 1e-10);
    }

    // --- Validation ---

    [Fact]
    public void ComputeVergeProfile_ZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VergeProfileComputer.ComputeVergeProfile(0));
    }

    [Fact]
    public void ComputeVergeProfile_NegativeWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VergeProfileComputer.ComputeVergeProfile(-1.0));
    }

    [Fact]
    public void ComputeVergeProfile_NegativeSlope_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VergeProfileComputer.ComputeVergeProfile(1.0, -0.05));
    }

    // --- Boundary values ---

    [Theory]
    [InlineData(0.1)]   // Minimum from VBScript
    [InlineData(20.0)]  // Maximum from VBScript
    public void ComputeVergeProfile_BoundaryWidths_ProduceValidProfile(double width)
    {
        var pts = VergeProfileComputer.ComputeVergeProfile(width);

        Assert.Equal(2, pts.Length);
        Assert.True(pts[1].X > 0);
        Assert.True(pts[1].Y <= 0);
    }
}
