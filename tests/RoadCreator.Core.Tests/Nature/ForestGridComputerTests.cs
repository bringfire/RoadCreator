using RoadCreator.Core.Math;
using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class ForestGridComputerTests
{
    // --- Constants ---

    [Fact]
    public void DefaultDensity_Is6()
    {
        Assert.Equal(6.0, ForestGridComputer.DefaultDensity);
    }

    [Fact]
    public void MinDensity_Is1()
    {
        Assert.Equal(1.0, ForestGridComputer.MinDensity);
    }

    [Fact]
    public void MaxDensity_Is200()
    {
        Assert.Equal(200.0, ForestGridComputer.MaxDensity);
    }

    // --- ComputeGridOrigins ---

    [Fact]
    public void ComputeGridOrigins_1x1_SinglePoint()
    {
        var origins = ForestGridComputer.ComputeGridOrigins(0, 0.5, 0, 0.5, 1.0);
        Assert.Single(origins);
        Assert.Equal(0.0, origins[0].U);
        Assert.Equal(0.0, origins[0].V);
    }

    [Fact]
    public void ComputeGridOrigins_10x10_Density5_CorrectCount()
    {
        // 10/5 = 2 cols, 10/5 = 2 rows → (2+1)*(2+1) = 9 points
        var origins = ForestGridComputer.ComputeGridOrigins(0, 10, 0, 10, 5.0);
        Assert.Equal(9, origins.Length);
    }

    [Fact]
    public void ComputeGridOrigins_CorrectCoordinates()
    {
        var origins = ForestGridComputer.ComputeGridOrigins(0, 6, 0, 6, 3.0);
        // 6/3 = 2 cols, 2 rows → (2+1)*(2+1) = 9 points
        Assert.Equal(9, origins.Length);

        // First column (u=0): v=0, 3, 6
        Assert.Contains(origins, o => o.U == 0 && o.V == 0);
        Assert.Contains(origins, o => o.U == 0 && o.V == 3);
        Assert.Contains(origins, o => o.U == 0 && o.V == 6);

        // Last column (u=6): v=0, 3, 6
        Assert.Contains(origins, o => o.U == 6 && o.V == 0);
        Assert.Contains(origins, o => o.U == 6 && o.V == 6);
    }

    [Fact]
    public void ComputeGridOrigins_NonZeroStart()
    {
        var origins = ForestGridComputer.ComputeGridOrigins(10, 16, 20, 26, 3.0);
        Assert.Equal(9, origins.Length);
        Assert.Contains(origins, o => o.U == 10 && o.V == 20);
        Assert.Contains(origins, o => o.U == 16 && o.V == 26);
    }

    [Fact]
    public void ComputeGridOrigins_ZeroDensity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForestGridComputer.ComputeGridOrigins(0, 10, 0, 10, 0));
    }

    [Fact]
    public void ComputeGridOrigins_NegativeDensity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForestGridComputer.ComputeGridOrigins(0, 10, 0, 10, -1));
    }

    // --- ApplyJitter ---

    [Fact]
    public void ApplyJitter_HalfRandom_NoShift()
    {
        var pt = new Point3(5, 10, 0);
        var result = ForestGridComputer.ApplyJitter(pt, 6.0, 0.5, 0.5);
        Assert.Equal(5.0, result.X, 1e-10);
        Assert.Equal(10.0, result.Y, 1e-10);
        Assert.Equal(0.0, result.Z, 1e-10);
    }

    [Fact]
    public void ApplyJitter_PreservesZ()
    {
        var pt = new Point3(0, 0, 42.0);
        var result = ForestGridComputer.ApplyJitter(pt, 6.0, 0.3, 0.7);
        Assert.Equal(42.0, result.Z, 1e-10);
    }

    [Fact]
    public void ApplyJitter_ExtremeRandom_CorrectBounds()
    {
        var pt = new Point3(0, 0, 0);
        var low = ForestGridComputer.ApplyJitter(pt, 6.0, 0.0, 0.0);
        var high = ForestGridComputer.ApplyJitter(pt, 6.0, 1.0, 1.0);
        Assert.Equal(-3.0, low.X, 1e-10);
        Assert.Equal(-3.0, low.Y, 1e-10);
        Assert.Equal(3.0, high.X, 1e-10);
        Assert.Equal(3.0, high.Y, 1e-10);
    }
}
