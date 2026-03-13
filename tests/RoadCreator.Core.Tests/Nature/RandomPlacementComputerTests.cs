using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class RandomPlacementComputerTests
{
    // --- ComputeScale ---

    [Fact]
    public void ComputeScale_ZeroVariance_Always1()
    {
        // Scale=0 → no variation, always 1.0
        Assert.Equal(1.01, RandomPlacementComputer.ComputeScale(0, 0.0), 1e-10);
        Assert.Equal(1.01, RandomPlacementComputer.ComputeScale(0, 0.5), 1e-10);
        Assert.Equal(1.01, RandomPlacementComputer.ComputeScale(0, 0.99), 1e-10);
    }

    [Fact]
    public void ComputeScale_20Percent_RangeCheck()
    {
        // VBScript: (((20) * Rnd + 1) - (20 / 2)) / 100 + 1
        // Rnd=0: ((0+1)-10)/100+1 = -9/100+1 = 0.91
        // Rnd=1: ((20+1)-10)/100+1 = 11/100+1 = 1.11
        double min = RandomPlacementComputer.ComputeScale(20, 0.0);
        double max = RandomPlacementComputer.ComputeScale(20, 1.0);
        Assert.Equal(0.91, min, 1e-10);
        Assert.Equal(1.11, max, 1e-10);
    }

    [Fact]
    public void ComputeScale_20Percent_MidpointNear1()
    {
        double mid = RandomPlacementComputer.ComputeScale(20, 0.5);
        Assert.Equal(1.01, mid, 1e-10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void ComputeScale_AlwaysPositive(double scalePercent)
    {
        for (double r = 0; r <= 1.0; r += 0.1)
            Assert.True(RandomPlacementComputer.ComputeScale(scalePercent, r) > 0);
    }

    [Fact]
    public void ComputeScale_100Percent_ExtremeRange()
    {
        // Scale=100: range [0.51, 1.51]
        // Rnd=0: ((0+1)-50)/100+1 = -49/100+1 = 0.51
        // Rnd=1: ((100+1)-50)/100+1 = 51/100+1 = 1.51
        double min = RandomPlacementComputer.ComputeScale(100, 0.0);
        double max = RandomPlacementComputer.ComputeScale(100, 1.0);
        Assert.Equal(0.51, min, 1e-10);
        Assert.Equal(1.51, max, 1e-10);
    }

    // --- ComputeRotationDegrees ---

    [Fact]
    public void ComputeRotation_Zero_Returns0()
    {
        Assert.Equal(0.0, RandomPlacementComputer.ComputeRotationDegrees(0.0));
    }

    [Fact]
    public void ComputeRotation_One_Returns360()
    {
        Assert.Equal(360.0, RandomPlacementComputer.ComputeRotationDegrees(1.0));
    }

    [Fact]
    public void ComputeRotation_Half_Returns180()
    {
        Assert.Equal(180.0, RandomPlacementComputer.ComputeRotationDegrees(0.5));
    }

    // --- SelectTreeIndex ---

    [Fact]
    public void SelectTreeIndex_SingleTree_AlwaysZero()
    {
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(1, 0.0));
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(1, 0.5));
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(1, 0.99));
    }

    [Fact]
    public void SelectTreeIndex_TwoTrees_CorrectDistribution()
    {
        // VBScript: ((2-1) * Rnd) \ 1 = floor(Rnd)
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(2, 0.0));
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(2, 0.49));
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(2, 0.99));
    }

    [Fact]
    public void SelectTreeIndex_FiveTrees_FullRange()
    {
        // VBScript: ((5-1) * Rnd) \ 1 = floor(4 * Rnd)
        Assert.Equal(0, RandomPlacementComputer.SelectTreeIndex(5, 0.0));
        Assert.Equal(1, RandomPlacementComputer.SelectTreeIndex(5, 0.3));
        Assert.Equal(2, RandomPlacementComputer.SelectTreeIndex(5, 0.6));
        Assert.Equal(3, RandomPlacementComputer.SelectTreeIndex(5, 0.9));
    }

    [Fact]
    public void SelectTreeIndex_ZeroCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RandomPlacementComputer.SelectTreeIndex(0, 0.5));
    }

    [Fact]
    public void SelectTreeIndex_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RandomPlacementComputer.SelectTreeIndex(-1, 0.5));
    }

    // --- ApplyGridJitter ---

    [Fact]
    public void ApplyGridJitter_ZeroRandom_ShiftsNegativeHalf()
    {
        // random=0 → coord + 0 - cellSize/2 = coord - cellSize/2
        Assert.Equal(7.0, RandomPlacementComputer.ApplyGridJitter(10.0, 6.0, 0.0), 1e-10);
    }

    [Fact]
    public void ApplyGridJitter_OneRandom_ShiftsPositiveHalf()
    {
        // random=1 → coord + cellSize - cellSize/2 = coord + cellSize/2
        Assert.Equal(13.0, RandomPlacementComputer.ApplyGridJitter(10.0, 6.0, 1.0), 1e-10);
    }

    [Fact]
    public void ApplyGridJitter_HalfRandom_NoShift()
    {
        // random=0.5 → coord + cellSize*0.5 - cellSize/2 = coord
        Assert.Equal(10.0, RandomPlacementComputer.ApplyGridJitter(10.0, 6.0, 0.5), 1e-10);
    }

    // --- ApplyFixedJitter ---

    [Fact]
    public void ApplyFixedJitter_ForestRange_CorrectBounds()
    {
        // Forest silhouette: jitterRange=4, so ±2 units
        double min = RandomPlacementComputer.ApplyFixedJitter(0.0, 4.0, 0.0);
        double max = RandomPlacementComputer.ApplyFixedJitter(0.0, 4.0, 1.0);
        Assert.Equal(-2.0, min, 1e-10);
        Assert.Equal(2.0, max, 1e-10);
    }

    [Fact]
    public void ApplyFixedJitter_HalfRandom_NoShift()
    {
        Assert.Equal(5.0, RandomPlacementComputer.ApplyFixedJitter(5.0, 4.0, 0.5), 1e-10);
    }
}
