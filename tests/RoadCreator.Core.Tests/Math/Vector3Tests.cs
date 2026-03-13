using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Math;

public class Vector3Tests
{
    [Fact]
    public void Length_UnitX_IsOne()
    {
        Assert.Equal(1, Vector3.UnitX.Length, 10);
    }

    [Fact]
    public void Length_KnownVector()
    {
        var v = new Vector3(3, 4, 0);
        Assert.Equal(5, v.Length, 10);
    }

    [Fact]
    public void Normalized_ReturnsUnitLength()
    {
        var v = new Vector3(3, 4, 0);
        var n = v.Normalized;
        Assert.Equal(1, n.Length, 10);
        Assert.Equal(0.6, n.X, 10);
        Assert.Equal(0.8, n.Y, 10);
    }

    [Fact]
    public void Normalized_ZeroVector_ReturnsZero()
    {
        var v = Vector3.Zero;
        var n = v.Normalized;
        Assert.Equal(0, n.Length, 10);
    }

    [Fact]
    public void Dot_Perpendicular_IsZero()
    {
        Assert.Equal(0, Vector3.Dot(Vector3.UnitX, Vector3.UnitY), 10);
    }

    [Fact]
    public void Dot_Parallel_IsProduct()
    {
        var a = new Vector3(2, 0, 0);
        var b = new Vector3(3, 0, 0);
        Assert.Equal(6, Vector3.Dot(a, b), 10);
    }

    [Fact]
    public void Cross_XY_GivesZ()
    {
        var result = Vector3.Cross(Vector3.UnitX, Vector3.UnitY);
        Assert.Equal(0, result.X, 10);
        Assert.Equal(0, result.Y, 10);
        Assert.Equal(1, result.Z, 10);
    }

    [Fact]
    public void Cross_Parallel_GivesZero()
    {
        var a = new Vector3(2, 0, 0);
        var b = new Vector3(5, 0, 0);
        var result = Vector3.Cross(a, b);
        Assert.Equal(0, result.Length, 10);
    }

    [Fact]
    public void OperatorMultiply_ScalesVector()
    {
        var v = new Vector3(1, 2, 3);
        var r = v * 3;
        Assert.Equal(3, r.X);
        Assert.Equal(6, r.Y);
        Assert.Equal(9, r.Z);
    }

    [Fact]
    public void OperatorNegate_FlipsDirection()
    {
        var v = new Vector3(1, -2, 3);
        var r = -v;
        Assert.Equal(-1, r.X);
        Assert.Equal(2, r.Y);
        Assert.Equal(-3, r.Z);
    }
}
