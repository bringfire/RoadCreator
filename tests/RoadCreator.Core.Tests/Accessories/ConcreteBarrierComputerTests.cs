using RoadCreator.Core.Accessories;
using Xunit;

namespace RoadCreator.Core.Tests.Accessories;

public class ConcreteBarrierComputerTests
{
    // --- Constants ---

    [Fact]
    public void DefaultPostSpacing_Is2_5()
    {
        Assert.Equal(2.5, ConcreteBarrierComputer.DefaultPostSpacing);
    }

    [Fact]
    public void RodRadius_Is0_05()
    {
        Assert.Equal(0.05, ConcreteBarrierComputer.RodRadius);
    }

    [Fact]
    public void LowerRodHeight_Is0_5()
    {
        Assert.Equal(0.5, ConcreteBarrierComputer.LowerRodHeight);
    }

    [Fact]
    public void UpperRodHeight_Is0_9()
    {
        Assert.Equal(0.9, ConcreteBarrierComputer.UpperRodHeight);
    }

    [Fact]
    public void PostWidth_Is0_35()
    {
        Assert.Equal(0.35, ConcreteBarrierComputer.PostWidth);
    }

    [Fact]
    public void PostDepth_Is0_35()
    {
        Assert.Equal(0.35, ConcreteBarrierComputer.PostDepth);
    }

    [Fact]
    public void PostHeight_Is1_4()
    {
        Assert.Equal(1.4, ConcreteBarrierComputer.PostHeight);
    }

    // --- Default post ---

    [Fact]
    public void GetDefaultPostCorners_Returns8Points()
    {
        var corners = ConcreteBarrierComputer.GetDefaultPostCorners();
        Assert.Equal(8, corners.Length);
    }

    [Fact]
    public void GetDefaultPostCorners_CorrectDimensions()
    {
        var c = ConcreteBarrierComputer.GetDefaultPostCorners();

        // Width: 0.35m
        Assert.Equal(0.35, c[1].X - c[0].X, 1e-10);
        // Depth: 0.35m
        Assert.Equal(0.35, c[2].Y - c[1].Y, 1e-10);
        // Height: from -0.2 to 1.2 = 1.4m
        Assert.Equal(-0.2, c[0].Z, 1e-10);
        Assert.Equal(1.2, c[4].Z, 1e-10);
    }

    [Fact]
    public void GetDefaultPostCorners_MatchesVBScript()
    {
        // VBScript: Hrany(0) = Array(0, 0, -0.2), Hrany(6) = Array(0.35, 0.35, 1.2)
        var c = ConcreteBarrierComputer.GetDefaultPostCorners();
        Assert.Equal(0, c[0].X, 1e-10);
        Assert.Equal(0, c[0].Y, 1e-10);
        Assert.Equal(-0.2, c[0].Z, 1e-10);
        Assert.Equal(0.35, c[6].X, 1e-10);
        Assert.Equal(0.35, c[6].Y, 1e-10);
        Assert.Equal(1.2, c[6].Z, 1e-10);
    }

    [Fact]
    public void DefaultPostCenter_Is0_17_0_17_0()
    {
        var center = ConcreteBarrierComputer.DefaultPostCenter;
        Assert.Equal(0.17, center.X, 1e-10);
        Assert.Equal(0.17, center.Y, 1e-10);
        Assert.Equal(0, center.Z, 1e-10);
    }

    // --- Rod heights validation ---

    [Fact]
    public void LowerRodHeight_IsLessThanUpperRodHeight()
    {
        Assert.True(ConcreteBarrierComputer.LowerRodHeight < ConcreteBarrierComputer.UpperRodHeight);
    }

    [Fact]
    public void RodHeights_WithinPostHeight()
    {
        double postBottom = ConcreteBarrierComputer.GetDefaultPostCorners()[0].Z;
        double postTop = ConcreteBarrierComputer.GetDefaultPostCorners()[4].Z;
        Assert.True(ConcreteBarrierComputer.LowerRodHeight > postBottom);
        Assert.True(ConcreteBarrierComputer.UpperRodHeight < postTop);
    }
}
