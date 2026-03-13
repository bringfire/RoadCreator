using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Math;

public class Point3Tests
{
    [Fact]
    public void DistanceTo_SamePoint_IsZero()
    {
        var p = new Point3(3, 4, 5);
        Assert.Equal(0, p.DistanceTo(p), 10);
    }

    [Fact]
    public void DistanceTo_KnownDistance()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(3, 4, 0);
        Assert.Equal(5, a.DistanceTo(b), 10);
    }

    [Fact]
    public void DistanceTo2D_IgnoresZ()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(3, 4, 100);
        Assert.Equal(5, a.DistanceTo2D(b), 10);
    }

    [Fact]
    public void WithZ_ReplacesZ()
    {
        var p = new Point3(1, 2, 3);
        var q = p.WithZ(99);
        Assert.Equal(1, q.X);
        Assert.Equal(2, q.Y);
        Assert.Equal(99, q.Z);
    }

    [Fact]
    public void OperatorPlus_PointPlusVector()
    {
        var p = new Point3(1, 2, 3);
        var v = new Vector3(10, 20, 30);
        var r = p + v;
        Assert.Equal(11, r.X);
        Assert.Equal(22, r.Y);
        Assert.Equal(33, r.Z);
    }

    [Fact]
    public void OperatorMinus_PointMinusPoint_GivesVector()
    {
        var a = new Point3(10, 20, 30);
        var b = new Point3(1, 2, 3);
        var v = a - b;
        Assert.Equal(9, v.X);
        Assert.Equal(18, v.Y);
        Assert.Equal(27, v.Z);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Point3(1.5, 2.5, 3.5);
        var b = new Point3(1.5, 2.5, 3.5);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Point3(1, 2, 3);
        var b = new Point3(1, 2, 4);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
