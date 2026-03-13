using RoadCreator.Core.Math;
using RoadCreator.Core.Urban;
using Xunit;

namespace RoadCreator.Core.Tests.Urban;

public class CrossingStripComputerTests
{
    // --- Constants ---

    [Fact]
    public void DefaultWidth_Is4()
    {
        Assert.Equal(4.0, CrossingStripComputer.DefaultWidth);
    }

    [Fact]
    public void StripeWidth_Is0_5()
    {
        Assert.Equal(0.5, CrossingStripComputer.StripeWidth);
    }

    [Fact]
    public void StripeSpacing_Is1()
    {
        Assert.Equal(1.0, CrossingStripComputer.StripeSpacing);
    }

    // --- ComputeStripeCount ---

    [Theory]
    [InlineData(7.0, 7)]
    [InlineData(7.5, 7)]
    [InlineData(7.9, 7)]
    [InlineData(8.0, 8)]
    [InlineData(1.0, 1)]
    [InlineData(0.5, 0)]
    public void ComputeStripeCount_TruncatesToInt(double length, int expected)
    {
        Assert.Equal(expected, CrossingStripComputer.ComputeStripeCount(length));
    }

    [Fact]
    public void ComputeStripeCount_MatchesVBScript()
    {
        // VBScript: Do While i < Rhino.Distance(startpoint, endpoint)
        // with i incrementing by 1, so count = floor(distance)
        Assert.Equal(7, CrossingStripComputer.ComputeStripeCount(7.5));
    }

    [Fact]
    public void ComputeStripeCount_ZeroLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossingStripComputer.ComputeStripeCount(0));
    }

    [Fact]
    public void ComputeStripeCount_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossingStripComputer.ComputeStripeCount(-1));
    }

    // --- ComputeCrossingAngle ---

    [Fact]
    public void ComputeCrossingAngle_East_Is0()
    {
        var start = new Point3(0, 0, 0);
        var end = new Point3(10, 0, 0);
        Assert.Equal(0, CrossingStripComputer.ComputeCrossingAngle(start, end), 1e-10);
    }

    [Fact]
    public void ComputeCrossingAngle_North_Is90()
    {
        var start = new Point3(0, 0, 0);
        var end = new Point3(0, 10, 0);
        Assert.Equal(90, CrossingStripComputer.ComputeCrossingAngle(start, end), 1e-10);
    }

    [Fact]
    public void ComputeCrossingAngle_West_Is180()
    {
        var start = new Point3(0, 0, 0);
        var end = new Point3(-10, 0, 0);
        Assert.Equal(180, CrossingStripComputer.ComputeCrossingAngle(start, end), 1e-10);
    }

    [Fact]
    public void ComputeCrossingAngle_South_IsNeg90()
    {
        var start = new Point3(0, 0, 0);
        var end = new Point3(0, -10, 0);
        Assert.Equal(-90, CrossingStripComputer.ComputeCrossingAngle(start, end), 1e-10);
    }

    [Fact]
    public void ComputeCrossingAngle_45Degrees()
    {
        var start = new Point3(0, 0, 0);
        var end = new Point3(10, 10, 0);
        Assert.Equal(45, CrossingStripComputer.ComputeCrossingAngle(start, end), 1e-10);
    }

    // --- ComputeCrossingRectangle ---

    [Fact]
    public void ComputeCrossingRectangle_Returns4Corners()
    {
        var corners = CrossingStripComputer.ComputeCrossingRectangle(4.0, 7.0);
        Assert.Equal(4, corners.Length);
    }

    [Fact]
    public void ComputeCrossingRectangle_CenteredOnOrigin()
    {
        var corners = CrossingStripComputer.ComputeCrossingRectangle(4.0, 8.0);

        // Average of all corners should be near origin
        double avgX = (corners[0].X + corners[1].X + corners[2].X + corners[3].X) / 4;
        double avgY = (corners[0].Y + corners[1].Y + corners[2].Y + corners[3].Y) / 4;
        Assert.Equal(0, avgX, 1e-10);
        Assert.Equal(0, avgY, 1e-10);
    }

    [Fact]
    public void ComputeCrossingRectangle_CorrectDimensions()
    {
        double width = 4.0;
        double length = 8.0;
        var corners = CrossingStripComputer.ComputeCrossingRectangle(width, length);

        // Width in X direction
        double actualWidth = corners[1].X - corners[0].X;
        Assert.Equal(width, actualWidth, 1e-10);

        // Length in Y direction
        double actualLength = corners[2].Y - corners[1].Y;
        Assert.Equal(length, actualLength, 1e-10);
    }

    [Fact]
    public void ComputeCrossingRectangle_ZCoordinatesAreZero()
    {
        var corners = CrossingStripComputer.ComputeCrossingRectangle(4.0, 7.0);
        foreach (var pt in corners)
            Assert.Equal(0, pt.Z, 1e-10);
    }

    [Fact]
    public void ComputeCrossingRectangle_ZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossingStripComputer.ComputeCrossingRectangle(0, 7));
    }

    [Fact]
    public void ComputeCrossingRectangle_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossingStripComputer.ComputeCrossingRectangle(4, -1));
    }
}
