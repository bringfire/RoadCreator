using RoadCreator.Core.Terrain;
using Xunit;

namespace RoadCreator.Core.Tests.Terrain;

public class ContourClassifierTests
{
    // --- Classify ---

    [Theory]
    [InlineData(0, ContourClassifier.ContourType.Main10m)]
    [InlineData(10, ContourClassifier.ContourType.Main10m)]
    [InlineData(20, ContourClassifier.ContourType.Main10m)]
    [InlineData(100, ContourClassifier.ContourType.Main10m)]
    public void Classify_MultiplesOf10_ReturnsMain(int index, ContourClassifier.ContourType expected)
    {
        Assert.Equal(expected, ContourClassifier.Classify(index));
    }

    [Theory]
    [InlineData(5, ContourClassifier.ContourType.Secondary5m)]
    [InlineData(15, ContourClassifier.ContourType.Secondary5m)]
    [InlineData(25, ContourClassifier.ContourType.Secondary5m)]
    public void Classify_MultiplesOf5Not10_ReturnsSecondary(int index, ContourClassifier.ContourType expected)
    {
        Assert.Equal(expected, ContourClassifier.Classify(index));
    }

    [Theory]
    [InlineData(2, ContourClassifier.ContourType.Minor2m)]
    [InlineData(4, ContourClassifier.ContourType.Minor2m)]
    [InlineData(6, ContourClassifier.ContourType.Minor2m)]
    [InlineData(8, ContourClassifier.ContourType.Minor2m)]
    [InlineData(12, ContourClassifier.ContourType.Minor2m)]
    public void Classify_EvenNotMultipleOf5Or10_ReturnsMinor(int index, ContourClassifier.ContourType expected)
    {
        Assert.Equal(expected, ContourClassifier.Classify(index));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(11)]
    public void Classify_OddNotMultipleOf5_ReturnsDiscard(int index)
    {
        Assert.Equal(ContourClassifier.ContourType.Discard, ContourClassifier.Classify(index));
    }

    [Fact]
    public void Classify_FullCycle_MatchesExpectedPattern()
    {
        // Heights 0-9 should produce: Main, Discard, Minor, Discard, Minor, Sec, Minor, Discard, Minor, Discard
        var expected = new[]
        {
            ContourClassifier.ContourType.Main10m,    // 0
            ContourClassifier.ContourType.Discard,     // 1
            ContourClassifier.ContourType.Minor2m,     // 2
            ContourClassifier.ContourType.Discard,     // 3
            ContourClassifier.ContourType.Minor2m,     // 4
            ContourClassifier.ContourType.Secondary5m, // 5
            ContourClassifier.ContourType.Minor2m,     // 6
            ContourClassifier.ContourType.Discard,     // 7
            ContourClassifier.ContourType.Minor2m,     // 8
            ContourClassifier.ContourType.Discard,     // 9
        };

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ContourClassifier.Classify(i));
        }
    }

    // --- ComputeStartElevation ---

    [Fact]
    public void ComputeStartElevation_PositiveElevation_RoundsDownToNearest10()
    {
        Assert.Equal(350.0, ContourClassifier.ComputeStartElevation(357.3));
    }

    [Fact]
    public void ComputeStartElevation_ExactMultipleOf10()
    {
        Assert.Equal(200.0, ContourClassifier.ComputeStartElevation(200.0));
    }

    [Fact]
    public void ComputeStartElevation_NegativeElevation()
    {
        // -15.0 / 10.0 = -1 (int cast), then floored-- => -2 => -20.0
        Assert.Equal(-20.0, ContourClassifier.ComputeStartElevation(-15.0));
    }

    [Fact]
    public void ComputeStartElevation_Zero()
    {
        Assert.Equal(0.0, ContourClassifier.ComputeStartElevation(0.0));
    }

    [Fact]
    public void ComputeStartElevation_SmallPositive()
    {
        Assert.Equal(0.0, ContourClassifier.ComputeStartElevation(5.0));
    }

    [Theory]
    [InlineData(-0.5, -10.0)]
    [InlineData(-3.7, -10.0)]
    [InlineData(-10.0, -20.0)]
    [InlineData(-10.1, -20.0)]
    public void ComputeStartElevation_FractionalNegatives(double input, double expected)
    {
        Assert.Equal(expected, ContourClassifier.ComputeStartElevation(input));
    }

    // --- ComputeLevelCount ---

    [Fact]
    public void ComputeLevelCount_NormalRange()
    {
        // startElev=350, maxElev=400, interval=1 => (400-350)/1 + 1 = 51
        Assert.Equal(51, ContourClassifier.ComputeLevelCount(350, 400, 1.0));
    }

    [Fact]
    public void ComputeLevelCount_MaxBelowStart_ReturnsZero()
    {
        Assert.Equal(0, ContourClassifier.ComputeLevelCount(400, 350));
    }

    [Fact]
    public void ComputeLevelCount_EqualStartAndMax_ReturnsZero()
    {
        Assert.Equal(0, ContourClassifier.ComputeLevelCount(350, 350));
    }

    [Fact]
    public void ComputeLevelCount_CustomInterval()
    {
        // startElev=0, maxElev=100, interval=5 => 100/5 + 1 = 21
        Assert.Equal(21, ContourClassifier.ComputeLevelCount(0, 100, 5.0));
    }

    [Fact]
    public void ComputeLevelCount_ThrowsOnZeroInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContourClassifier.ComputeLevelCount(0, 100, 0.0));
    }

    [Fact]
    public void ComputeLevelCount_ThrowsOnNegativeInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContourClassifier.ComputeLevelCount(0, 100, -1.0));
    }
}
