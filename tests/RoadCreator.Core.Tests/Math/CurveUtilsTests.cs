using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Math;

public class CurveUtilsTests
{
    [Fact]
    public void PolylineLength_StraightLine()
    {
        var points = new List<Point3>
        {
            new(0, 0, 0),
            new(3, 0, 0),
            new(3, 4, 0),
        };
        Assert.Equal(7.0, CurveUtils.PolylineLength(points), 6);
    }

    [Fact]
    public void DividePolylineEquidistant_StraightLine()
    {
        var points = new List<Point3>
        {
            new(0, 0, 0),
            new(10, 0, 0),
        };
        var result = CurveUtils.DividePolylineEquidistant(points, 2.0);
        Assert.Equal(6, result.Count); // 0, 2, 4, 6, 8, 10
        Assert.Equal(0, result[0].X, 6);
        Assert.Equal(2, result[1].X, 6);
        Assert.Equal(10, result[5].X, 6);
    }

    [Fact]
    public void EstimateRadius_StraightLine_ReturnsLarge()
    {
        var p0 = new Point3(0, 0, 0);
        var p1 = new Point3(1, 0, 0);
        var p2 = new Point3(2, 0, 0);
        Assert.Equal(double.MaxValue, CurveUtils.EstimateRadius(p0, p1, p2));
    }

    [Fact]
    public void EstimateRadius_QuarterCircle()
    {
        // Points on a circle of radius 10
        double r = 10;
        var p0 = new Point3(r, 0, 0);
        var p1 = new Point3(r * System.Math.Cos(System.Math.PI / 4), r * System.Math.Sin(System.Math.PI / 4), 0);
        var p2 = new Point3(0, r, 0);
        double estimated = CurveUtils.EstimateRadius(p0, p1, p2);
        Assert.Equal(r, estimated, 3);
    }

    [Fact]
    public void ComputeTangentAngles_StraightEast()
    {
        var points = new List<Point3>
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(2, 0, 0),
        };
        var angles = CurveUtils.ComputeTangentAngles(points);
        Assert.Equal(3, angles.Count);
        Assert.Equal(0, angles[0], 6);
        Assert.Equal(0, angles[1], 6);
        Assert.Equal(0, angles[2], 6);
    }
}
