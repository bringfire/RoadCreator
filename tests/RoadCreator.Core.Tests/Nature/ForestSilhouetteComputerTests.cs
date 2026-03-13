using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class ForestSilhouetteComputerTests
{
    // --- Constants ---

    [Fact]
    public void RowCount_Is3()
    {
        Assert.Equal(3, ForestSilhouetteComputer.RowCount);
    }

    [Fact]
    public void FixedSpacing_Is5Point5()
    {
        Assert.Equal(5.5, ForestSilhouetteComputer.FixedSpacing);
    }

    [Fact]
    public void JitterRange_Is4()
    {
        Assert.Equal(4.0, ForestSilhouetteComputer.JitterRange);
    }

    // --- GetFixedOffsetDistances ---

    [Fact]
    public void GetFixedOffsetDistances_Returns3Values()
    {
        var distances = ForestSilhouetteComputer.GetFixedOffsetDistances();
        Assert.Equal(3, distances.Length);
    }

    [Fact]
    public void GetFixedOffsetDistances_CorrectValues()
    {
        // VBScript: 3*i + 4 → 4, 7, 10
        var d = ForestSilhouetteComputer.GetFixedOffsetDistances();
        Assert.Equal(4.0, d[0], 1e-10);
        Assert.Equal(7.0, d[1], 1e-10);
        Assert.Equal(10.0, d[2], 1e-10);
    }

    // --- GetAdaptiveOffsetDistances ---

    [Fact]
    public void GetAdaptiveOffsetDistances_Spacing5_CorrectValues()
    {
        // VBScript: 3*i + rozestup/0.8 = 3*i + 6.25
        var d = ForestSilhouetteComputer.GetAdaptiveOffsetDistances(5.0);
        Assert.Equal(3, d.Length);
        Assert.Equal(6.25, d[0], 1e-10);
        Assert.Equal(9.25, d[1], 1e-10);
        Assert.Equal(12.25, d[2], 1e-10);
    }

    [Fact]
    public void GetAdaptiveOffsetDistances_Spacing10_CorrectValues()
    {
        // 3*i + 10/0.8 = 3*i + 12.5
        var d = ForestSilhouetteComputer.GetAdaptiveOffsetDistances(10.0);
        Assert.Equal(12.5, d[0], 1e-10);
        Assert.Equal(15.5, d[1], 1e-10);
        Assert.Equal(18.5, d[2], 1e-10);
    }

    [Fact]
    public void GetAdaptiveOffsetDistances_ZeroSpacing_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForestSilhouetteComputer.GetAdaptiveOffsetDistances(0));
    }

    [Fact]
    public void GetAdaptiveOffsetDistances_NegativeSpacing_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForestSilhouetteComputer.GetAdaptiveOffsetDistances(-5));
    }

    // --- ApplyJitter ---

    [Fact]
    public void ApplyJitter_HalfRandom_NoShift()
    {
        var (jx, jy) = ForestSilhouetteComputer.ApplyJitter(5.0, 10.0, 0.5, 0.5);
        Assert.Equal(5.0, jx, 1e-10);
        Assert.Equal(10.0, jy, 1e-10);
    }

    [Fact]
    public void ApplyJitter_ZeroRandom_ShiftsMinusTwo()
    {
        // JitterRange=4, random=0 → shift = -2
        var (jx, jy) = ForestSilhouetteComputer.ApplyJitter(0.0, 0.0, 0.0, 0.0);
        Assert.Equal(-2.0, jx, 1e-10);
        Assert.Equal(-2.0, jy, 1e-10);
    }

    [Fact]
    public void ApplyJitter_OneRandom_ShiftsPlusTwo()
    {
        var (jx, jy) = ForestSilhouetteComputer.ApplyJitter(0.0, 0.0, 1.0, 1.0);
        Assert.Equal(2.0, jx, 1e-10);
        Assert.Equal(2.0, jy, 1e-10);
    }
}
