using RoadCreator.Core.Math;
using RoadCreator.Core.Urban;
using Xunit;

namespace RoadCreator.Core.Tests.Urban;

public class SidewalkProfileComputerTests
{
    // --- Constants ---

    [Fact]
    public void CurbHeight_Is0_2()
    {
        Assert.Equal(0.2, SidewalkProfileComputer.CurbHeight, 1e-10);
    }

    [Fact]
    public void CurbTopWidth_Is0_3()
    {
        Assert.Equal(0.3, SidewalkProfileComputer.CurbTopWidth, 1e-10);
    }

    // --- ComputeCurbProfile ---

    [Fact]
    public void ComputeCurbProfile_Returns3Points()
    {
        var pts = SidewalkProfileComputer.ComputeCurbProfile();
        Assert.Equal(3, pts.Length);
    }

    [Fact]
    public void ComputeCurbProfile_StartsAtOrigin()
    {
        var pts = SidewalkProfileComputer.ComputeCurbProfile();
        Assert.Equal(0, pts[0].X, 1e-10);
        Assert.Equal(0, pts[0].Y, 1e-10);
        Assert.Equal(0, pts[0].Z, 1e-10);
    }

    [Fact]
    public void ComputeCurbProfile_SecondPointAtCurbHeight()
    {
        var pts = SidewalkProfileComputer.ComputeCurbProfile();
        Assert.Equal(0, pts[1].X, 1e-10);
        Assert.Equal(SidewalkProfileComputer.CurbHeight, pts[1].Y, 1e-10);
    }

    [Fact]
    public void ComputeCurbProfile_ThirdPointAtCurbTopEdge()
    {
        var pts = SidewalkProfileComputer.ComputeCurbProfile();
        Assert.Equal(SidewalkProfileComputer.CurbTopWidth, pts[2].X, 1e-10);
        Assert.Equal(SidewalkProfileComputer.CurbHeight, pts[2].Y, 1e-10);
    }

    [Fact]
    public void ComputeCurbProfile_MatchesVBScript()
    {
        // VBScript: CopyObject up 0.2m, then OffsetCurve 0.3m
        var pts = SidewalkProfileComputer.ComputeCurbProfile();
        Assert.Equal(0.2, pts[1].Y, 1e-10);
        Assert.Equal(0.3, pts[2].X, 1e-10);
        Assert.Equal(0.2, pts[2].Y, 1e-10);
    }

    // --- ComputeFullProfile ---

    [Fact]
    public void ComputeFullProfile_Returns4Points()
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(5.0);
        Assert.Equal(4, pts.Length);
    }

    [Fact]
    public void ComputeFullProfile_LastPointAtOuterEdge()
    {
        double width = 5.0;
        var pts = SidewalkProfileComputer.ComputeFullProfile(width);

        double expectedX = SidewalkProfileComputer.CurbTopWidth + width;
        Assert.Equal(expectedX, pts[3].X, 1e-10);
        Assert.Equal(SidewalkProfileComputer.CurbHeight, pts[3].Y, 1e-10);
    }

    [Fact]
    public void ComputeFullProfile_SidewalkIsFlat()
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(3.0);
        // Points 2 and 3 should be at the same height
        Assert.Equal(pts[2].Y, pts[3].Y, 1e-10);
    }

    [Fact]
    public void ComputeFullProfile_ZCoordinatesAreZero()
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(4.0);
        foreach (var pt in pts)
            Assert.Equal(0, pt.Z, 1e-10);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    [InlineData(10.0)]
    [InlineData(30.0)]
    public void ComputeFullProfile_WidthProportional(double width)
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(width);
        double expectedX = SidewalkProfileComputer.CurbTopWidth + width;
        Assert.Equal(expectedX, pts[3].X, 1e-10);
    }

    // --- Validation ---

    [Fact]
    public void ComputeFullProfile_ZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SidewalkProfileComputer.ComputeFullProfile(0));
    }

    [Fact]
    public void ComputeFullProfile_NegativeWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SidewalkProfileComputer.ComputeFullProfile(-1.0));
    }

    // --- Profile ordering ---

    [Fact]
    public void ComputeFullProfile_XIsNonDecreasing()
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(5.0);
        for (int i = 1; i < pts.Length; i++)
            Assert.True(pts[i].X >= pts[i - 1].X);
    }

    [Fact]
    public void ComputeFullProfile_YIsNonDecreasing()
    {
        var pts = SidewalkProfileComputer.ComputeFullProfile(5.0);
        for (int i = 1; i < pts.Length; i++)
            Assert.True(pts[i].Y >= pts[i - 1].Y);
    }
}
