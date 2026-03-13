using RoadCreator.Core.Accessories;
using Xunit;

namespace RoadCreator.Core.Tests.Accessories;

public class PoleSpacingComputerTests
{
    // --- Constants ---

    [Fact]
    public void BaseInterval_Is5()
    {
        Assert.Equal(5.0, PoleSpacingComputer.BaseInterval);
    }

    [Fact]
    public void StraightRadius_Is10000()
    {
        Assert.Equal(10000.0, PoleSpacingComputer.StraightRadius);
    }

    // --- GetRequiredSkip ---

    [Theory]
    [InlineData(2000, 10)]   // R > 1250 → 10 intervals (50m)
    [InlineData(1251, 10)]
    [InlineData(1000, 8)]    // R > 850 → 8 intervals (40m)
    [InlineData(851, 8)]
    [InlineData(600, 6)]     // R > 450 → 6 intervals (30m)
    [InlineData(451, 6)]
    [InlineData(300, 4)]     // R > 250 → 4 intervals (20m)
    [InlineData(251, 4)]
    [InlineData(100, 2)]     // R > 50 → 2 intervals (10m)
    [InlineData(51, 2)]
    [InlineData(30, 1)]      // R ≤ 50 → 1 interval (5m)
    [InlineData(10, 1)]
    [InlineData(0, 1)]
    public void GetRequiredSkip_CorrectForRadius(double radius, int expected)
    {
        Assert.Equal(expected, PoleSpacingComputer.GetRequiredSkip(radius));
    }

    // --- Boundary values ---

    [Fact]
    public void GetRequiredSkip_Exactly1250_Returns8()
    {
        // 1250 is NOT > 1250, so falls through to > 850
        Assert.Equal(8, PoleSpacingComputer.GetRequiredSkip(1250));
    }

    [Fact]
    public void GetRequiredSkip_Exactly850_Returns6()
    {
        Assert.Equal(6, PoleSpacingComputer.GetRequiredSkip(850));
    }

    [Fact]
    public void GetRequiredSkip_Exactly450_Returns4()
    {
        Assert.Equal(4, PoleSpacingComputer.GetRequiredSkip(450));
    }

    [Fact]
    public void GetRequiredSkip_Exactly250_Returns2()
    {
        Assert.Equal(2, PoleSpacingComputer.GetRequiredSkip(250));
    }

    [Fact]
    public void GetRequiredSkip_Exactly50_Returns1()
    {
        Assert.Equal(1, PoleSpacingComputer.GetRequiredSkip(50));
    }

    [Fact]
    public void GetRequiredSkip_NegativeRadius_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PoleSpacingComputer.GetRequiredSkip(-1));
    }

    // --- GetSpacingDistance ---

    [Theory]
    [InlineData(2000, 50)]
    [InlineData(1000, 40)]
    [InlineData(600, 30)]
    [InlineData(300, 20)]
    [InlineData(100, 10)]
    [InlineData(30, 5)]
    public void GetSpacingDistance_CorrectForRadius(double radius, double expected)
    {
        Assert.Equal(expected, PoleSpacingComputer.GetSpacingDistance(radius));
    }

    // --- EstimateRadius ---

    [Fact]
    public void EstimateRadius_ZeroDiff_ReturnsStraight()
    {
        Assert.Equal(PoleSpacingComputer.StraightRadius,
            PoleSpacingComputer.EstimateRadius(0));
    }

    [Fact]
    public void EstimateRadius_VerySmallDiff_ReturnsStraight()
    {
        Assert.Equal(PoleSpacingComputer.StraightRadius,
            PoleSpacingComputer.EstimateRadius(0.00005));
    }

    [Fact]
    public void EstimateRadius_NegativeDiff_TreatsAsAbsolute()
    {
        double positive = PoleSpacingComputer.EstimateRadius(5);
        double negative = PoleSpacingComputer.EstimateRadius(-5);
        Assert.Equal(positive, negative, 1e-10);
    }

    [Fact]
    public void EstimateRadius_MatchesVBScriptFormula()
    {
        // VBScript: polomer = 10 / Rhino.ToRadians(rozdil) / 2
        //         = 10 / (rozdil * PI/180) / 2
        //         = 5 / (rozdil * PI/180)
        double angleDiff = 10.0; // degrees
        double expected = 5.0 / (angleDiff * System.Math.PI / 180.0);
        double actual = PoleSpacingComputer.EstimateRadius(angleDiff);
        Assert.Equal(expected, actual, 4);
    }

    [Fact]
    public void EstimateRadius_LargeAngle_SmallRadius()
    {
        double small = PoleSpacingComputer.EstimateRadius(45);
        double large = PoleSpacingComputer.EstimateRadius(5);
        Assert.True(small < large);
    }

    [Fact]
    public void EstimateRadius_SmallAngle_LargeRadius()
    {
        double radius = PoleSpacingComputer.EstimateRadius(0.1);
        Assert.True(radius > 1000);
    }
}
