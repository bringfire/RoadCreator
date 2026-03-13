using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Math;

public class GeometryMathTests
{
    [Fact]
    public void PerpendicularOffset_East_OffsetsNorth()
    {
        var origin = new Point3(10, 10, 0);
        var result = GeometryMath.PerpendicularOffset(origin, 0, 5);
        Assert.Equal(10, result.X, 6);
        Assert.Equal(15, result.Y, 6);
    }

    [Fact]
    public void PerpendicularOffset_North_OffsetsWest()
    {
        var origin = new Point3(10, 10, 0);
        var result = GeometryMath.PerpendicularOffset(origin, 90, 5);
        Assert.Equal(5, result.X, 6);
        Assert.Equal(10, result.Y, 6);
    }

    [Fact]
    public void PerpendicularOffset_NegativeOffset_OffsetsRight()
    {
        var origin = new Point3(0, 0, 0);
        var result = GeometryMath.PerpendicularOffset(origin, 0, -5);
        Assert.Equal(0, result.X, 6);
        Assert.Equal(-5, result.Y, 6);
    }

    [Fact]
    public void CrossSectionPoint_FlatSlope_NoZChange()
    {
        var center = new Point3(0, 0, 100);
        var result = GeometryMath.CrossSectionPoint(center, 0, 3.5, 0);
        Assert.Equal(100, result.Z, 6);
    }

    [Fact]
    public void CrossSectionPoint_TwoPercentSlope()
    {
        var center = new Point3(0, 0, 100);
        var result = GeometryMath.CrossSectionPoint(center, 0, 3.5, 2.0);
        // dz = 3.5 * 2.0 / 100 = 0.07
        Assert.Equal(99.93, result.Z, 6);
    }

    [Fact]
    public void Lerp_AtZero_ReturnsA()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 20, 30);
        var result = GeometryMath.Lerp(a, b, 0);
        Assert.Equal(a, result);
    }

    [Fact]
    public void Lerp_AtOne_ReturnsB()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 20, 30);
        var result = GeometryMath.Lerp(a, b, 1);
        Assert.Equal(b, result);
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 20, 30);
        var result = GeometryMath.Lerp(a, b, 0.5);
        Assert.Equal(5, result.X, 6);
        Assert.Equal(10, result.Y, 6);
        Assert.Equal(15, result.Z, 6);
    }

    [Fact]
    public void LineLineIntersection2D_Perpendicular()
    {
        var result = GeometryMath.LineLineIntersection2D(
            new Point3(0, 0, 0), 0,    // horizontal line through origin
            new Point3(5, -5, 0), 90);  // vertical line through x=5
        Assert.NotNull(result);
        Assert.Equal(5, result.Value.X, 6);
        Assert.Equal(0, result.Value.Y, 6);
    }

    [Fact]
    public void LineLineIntersection2D_Parallel_ReturnsNull()
    {
        var result = GeometryMath.LineLineIntersection2D(
            new Point3(0, 0, 0), 0,
            new Point3(0, 5, 0), 0);
        Assert.Null(result);
    }

    [Fact]
    public void LineLineIntersection2D_45Degrees()
    {
        var result = GeometryMath.LineLineIntersection2D(
            new Point3(0, 0, 0), 45,
            new Point3(10, 0, 0), 135);
        Assert.NotNull(result);
        Assert.Equal(5, result.Value.X, 4);
        Assert.Equal(5, result.Value.Y, 4);
    }
}
