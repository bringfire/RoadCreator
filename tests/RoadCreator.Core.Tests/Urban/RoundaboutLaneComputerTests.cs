using RoadCreator.Core.Urban;
using Xunit;

namespace RoadCreator.Core.Tests.Urban;

public class RoundaboutLaneComputerTests
{
    // --- Standard lane width table (Czech ČSN) ---

    [Theory]
    [InlineData(40.0, 4.5)]
    [InlineData(45.0, 4.5)]
    [InlineData(50.0, 4.5)]
    public void GetStandardLaneWidth_Diameter40Plus_Returns4_5(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    [Theory]
    [InlineData(35.0, 5.0)]
    [InlineData(39.9, 5.0)]
    public void GetStandardLaneWidth_Diameter35To40_Returns5_0(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    [Theory]
    [InlineData(32.0, 5.25)]
    [InlineData(34.9, 5.25)]
    public void GetStandardLaneWidth_Diameter32To35_Returns5_25(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    [Theory]
    [InlineData(30.0, 5.5)]
    [InlineData(31.9, 5.5)]
    public void GetStandardLaneWidth_Diameter30To32_Returns5_5(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    [Theory]
    [InlineData(28.0, 6.0)]
    [InlineData(29.9, 6.0)]
    public void GetStandardLaneWidth_Diameter28To30_Returns6_0(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    [Theory]
    [InlineData(25.0, 6.5)]
    [InlineData(27.9, 6.5)]
    public void GetStandardLaneWidth_Diameter25To28_Returns6_5(double diameter, double expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    // --- Below standard minimum ---

    [Theory]
    [InlineData(14.0)]
    [InlineData(20.0)]
    [InlineData(24.9)]
    public void GetStandardLaneWidth_BelowMinimum_ReturnsNull(double diameter)
    {
        Assert.Null(RoundaboutLaneComputer.GetStandardLaneWidth(diameter));
    }

    // --- Exact boundary values ---

    [Fact]
    public void GetStandardLaneWidth_Exactly25_Returns6_5()
    {
        Assert.Equal(6.5, RoundaboutLaneComputer.GetStandardLaneWidth(25.0));
    }

    [Fact]
    public void GetStandardLaneWidth_Exactly28_Returns6_0()
    {
        Assert.Equal(6.0, RoundaboutLaneComputer.GetStandardLaneWidth(28.0));
    }

    [Fact]
    public void GetStandardLaneWidth_Exactly40_Returns4_5()
    {
        Assert.Equal(4.5, RoundaboutLaneComputer.GetStandardLaneWidth(40.0));
    }

    // --- Validation ---

    [Fact]
    public void GetStandardLaneWidth_ZeroDiameter_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoundaboutLaneComputer.GetStandardLaneWidth(0));
    }

    [Fact]
    public void GetStandardLaneWidth_NegativeDiameter_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoundaboutLaneComputer.GetStandardLaneWidth(-10));
    }

    // --- HasApron ---

    [Theory]
    [InlineData(25.0, true)]
    [InlineData(30.0, true)]
    [InlineData(50.0, true)]
    [InlineData(24.9, false)]
    [InlineData(14.0, false)]
    public void HasApron_CorrectForDiameter(double diameter, bool expected)
    {
        Assert.Equal(expected, RoundaboutLaneComputer.HasApron(diameter));
    }

    // --- ComputeInnerRadius ---

    [Fact]
    public void ComputeInnerRadius_SubtractsLaneWidth()
    {
        double outerR = 15.0;
        double laneW = 5.0;
        Assert.Equal(10.0, RoundaboutLaneComputer.ComputeInnerRadius(outerR, laneW), 1e-10);
    }

    [Fact]
    public void ComputeInnerRadius_ZeroOuterRadius_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoundaboutLaneComputer.ComputeInnerRadius(0, 5));
    }

    [Fact]
    public void ComputeInnerRadius_LaneWidthExceedsRadius_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoundaboutLaneComputer.ComputeInnerRadius(5, 5));
    }

    // --- ComputeApronInnerRadius ---

    [Fact]
    public void ComputeApronInnerRadius_SubtractsApronWidth()
    {
        double outerR = 15.0;
        double laneW = 5.0;
        double innerR = 10.0;
        double expected = innerR - RoundaboutLaneComputer.ApronWidth; // 9.0
        Assert.Equal(expected, RoundaboutLaneComputer.ComputeApronInnerRadius(outerR, laneW), 1e-10);
    }

    // --- Constants ---

    [Fact]
    public void MinStandardDiameter_Is25()
    {
        Assert.Equal(25.0, RoundaboutLaneComputer.MinStandardDiameter);
    }

    [Fact]
    public void ApronWidth_Is1()
    {
        Assert.Equal(1.0, RoundaboutLaneComputer.ApronWidth);
    }

    [Fact]
    public void IslandHeight_Is0_75()
    {
        Assert.Equal(0.75, RoundaboutLaneComputer.IslandHeight);
    }

    // --- VBScript equivalence ---

    [Fact]
    public void LaneWidth_MatchesVBScript_ForDiameter30()
    {
        // VBScript: If vnejsiprumer >= 30 Then sirkapruhu = 5.5
        Assert.Equal(5.5, RoundaboutLaneComputer.GetStandardLaneWidth(30));
    }

    [Fact]
    public void LaneWidth_MatchesVBScript_ForDiameter25()
    {
        // VBScript: else (>= 25) sirkapruhu = 6.5
        Assert.Equal(6.5, RoundaboutLaneComputer.GetStandardLaneWidth(25));
    }
}
