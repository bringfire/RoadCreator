using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class GrassComputerTests
{
    // --- Constants ---

    [Fact]
    public void BandCount_Is2()
    {
        Assert.Equal(2, GrassComputer.BandCount);
    }

    [Fact]
    public void BaseLineHalfLength_Is2()
    {
        Assert.Equal(2.0, GrassComputer.BaseLineHalfLength);
    }

    [Fact]
    public void PointSpacing_Is0Point8()
    {
        Assert.Equal(0.8, GrassComputer.PointSpacing);
    }

    [Fact]
    public void ExtrusionHeight_Is0Point5()
    {
        Assert.Equal(0.5, GrassComputer.ExtrusionHeight);
    }

    // --- GetOffsetDistances ---

    [Fact]
    public void GetOffsetDistances_Returns2Values()
    {
        var distances = GrassComputer.GetOffsetDistances();
        Assert.Equal(2, distances.Length);
    }

    [Fact]
    public void GetOffsetDistances_CorrectValues()
    {
        // VBScript: 2*i + 2 → 2, 4
        var d = GrassComputer.GetOffsetDistances();
        Assert.Equal(2.0, d[0], 1e-10);
        Assert.Equal(4.0, d[1], 1e-10);
    }

    // --- GetBaseLineEndpoints ---

    [Fact]
    public void GetBaseLineEndpoints_CorrectCoordinates()
    {
        var (start, end) = GrassComputer.GetBaseLineEndpoints();
        // VBScript: AddLine((-2,0,0), (2,0,0))
        Assert.Equal(-2.0, start.X, 1e-10);
        Assert.Equal(0.0, start.Y, 1e-10);
        Assert.Equal(0.0, start.Z, 1e-10);
        Assert.Equal(2.0, end.X, 1e-10);
        Assert.Equal(0.0, end.Y, 1e-10);
        Assert.Equal(0.0, end.Z, 1e-10);
    }

    [Fact]
    public void GetBaseLineEndpoints_Length4()
    {
        var (start, end) = GrassComputer.GetBaseLineEndpoints();
        double length = start.DistanceTo(end);
        Assert.Equal(4.0, length, 1e-10);
    }

    // --- GetExtrusionVector ---

    [Fact]
    public void GetExtrusionVector_Vertical0Point5()
    {
        var v = GrassComputer.GetExtrusionVector();
        Assert.Equal(0.0, v.X, 1e-10);
        Assert.Equal(0.0, v.Y, 1e-10);
        Assert.Equal(0.5, v.Z, 1e-10);
    }
}
