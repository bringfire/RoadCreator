using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Math;

public class AngleUtilsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(360, 0)]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(-360, 0)]
    public void Normalize360_ReturnsCorrectRange(double input, double expected)
    {
        Assert.Equal(expected, AngleUtils.Normalize360(input), 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, -180)]
    [InlineData(270, -90)]
    [InlineData(-90, -90)]
    [InlineData(90, 90)]
    public void Normalize180_ReturnsCorrectRange(double input, double expected)
    {
        Assert.Equal(expected, AngleUtils.Normalize180(input), 6);
    }

    [Fact]
    public void AngleBetweenPoints_East()
    {
        var from = new Point3(0, 0, 0);
        var to = new Point3(1, 0, 0);
        Assert.Equal(0, AngleUtils.AngleBetweenPoints(from, to), 6);
    }

    [Fact]
    public void AngleBetweenPoints_North()
    {
        var from = new Point3(0, 0, 0);
        var to = new Point3(0, 1, 0);
        Assert.Equal(90, AngleUtils.AngleBetweenPoints(from, to), 6);
    }

    [Fact]
    public void AngleBetweenPoints_SouthWest()
    {
        var from = new Point3(0, 0, 0);
        var to = new Point3(-1, -1, 0);
        Assert.Equal(225, AngleUtils.AngleBetweenPoints(from, to), 6);
    }

    [Fact]
    public void SideOfLine_LeftIsPositive()
    {
        var from = new Point3(0, 0, 0);
        var to = new Point3(1, 0, 0);
        var left = new Point3(0.5, 1, 0);
        Assert.True(AngleUtils.SideOfLine(from, to, left) > 0);
    }

    [Fact]
    public void SideOfLine_RightIsNegative()
    {
        var from = new Point3(0, 0, 0);
        var to = new Point3(1, 0, 0);
        var right = new Point3(0.5, -1, 0);
        Assert.True(AngleUtils.SideOfLine(from, to, right) < 0);
    }
}
