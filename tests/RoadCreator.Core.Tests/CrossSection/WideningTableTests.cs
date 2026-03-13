using RoadCreator.Core.CrossSection;
using Xunit;

namespace RoadCreator.Core.Tests.CrossSection;

public class WideningTableTests
{
    // --- ComputeBaseWidening ---

    [Theory]
    [InlineData(300, 0.0)]
    [InlineData(250, 0.0)]
    [InlineData(249, 0.2)]
    [InlineData(200, 0.2)]
    [InlineData(199, 0.25)]
    [InlineData(170, 0.25)]
    [InlineData(169, 0.30)]
    [InlineData(141, 0.30)]
    [InlineData(140, 0.35)]
    [InlineData(125, 0.35)]
    [InlineData(124, 0.40)]
    [InlineData(110, 0.40)]
    [InlineData(109, 0.50)]
    [InlineData(50, 0.50)]
    public void ComputeBaseWidening_KnownRadii(double radius, double expected)
    {
        Assert.Equal(expected, WideningTable.ComputeBaseWidening(radius), 1e-10);
    }

    // --- ComputeWidening with category corrections ---

    [Fact]
    public void ComputeWidening_S65_AddsCorrection()
    {
        // R=100 => base=0.5, S65 adds 0.3 => 0.8
        double w = WideningTable.ComputeWidening(100, RoadCategory.S65);
        Assert.Equal(0.8, w, 1e-10);
    }

    [Fact]
    public void ComputeWidening_S75_AddsCorrection()
    {
        // R=100 => base=0.5, S75 adds 0.05 => 0.55
        double w = WideningTable.ComputeWidening(100, RoadCategory.S75);
        Assert.Equal(0.55, w, 1e-10);
    }

    [Fact]
    public void ComputeWidening_S95_NoCorrection()
    {
        // R=100 => base=0.5, S95 no correction => 0.5
        double w = WideningTable.ComputeWidening(100, RoadCategory.S95);
        Assert.Equal(0.5, w, 1e-10);
    }

    [Fact]
    public void ComputeWidening_NoWideningNeeded_NoCategoryCorrection()
    {
        // R=300 => base=0, no correction even for S65
        double w = WideningTable.ComputeWidening(300, RoadCategory.S65);
        Assert.Equal(0.0, w, 1e-10);
    }

    [Fact]
    public void ComputeWidening_ThrowsOnNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WideningTable.ComputeWidening(-10, RoadCategory.S75));
    }

    [Fact]
    public void ComputeWidening_ZeroRadius()
    {
        // R=0 => base=0.5 (< 110)
        double w = WideningTable.ComputeWidening(0, RoadCategory.S95);
        Assert.Equal(0.5, w, 1e-10);
    }

    // --- Boundary values ---

    [Theory]
    [InlineData(250, 0.0)]
    [InlineData(249.99, 0.2)]
    public void ComputeBaseWidening_BoundaryAt250(double radius, double expected)
    {
        Assert.Equal(expected, WideningTable.ComputeBaseWidening(radius), 1e-10);
    }

    // --- Null category throws ---

    [Fact]
    public void ComputeWidening_NullCategory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WideningTable.ComputeWidening(100, null!));
    }
}
