using RoadCreator.Core.Signs;
using Xunit;

namespace RoadCreator.Core.Tests.Signs;

public class TrafficSignCatalogTests
{
    // --- GetAllSignIds ---

    [Fact]
    public void GetAllSignIds_Returns14Signs()
    {
        var ids = TrafficSignCatalog.GetAllSignIds();
        Assert.Equal(14, ids.Count);
    }

    [Theory]
    [InlineData("A1a")]
    [InlineData("A1b")]
    [InlineData("A2a")]
    [InlineData("A2b")]
    [InlineData("B1")]
    [InlineData("B20")]
    [InlineData("B28")]
    [InlineData("B29")]
    [InlineData("C1")]
    [InlineData("P1")]
    [InlineData("P2")]
    [InlineData("P3")]
    [InlineData("P4")]
    [InlineData("P6")]
    public void GetAllSignIds_ContainsExpectedId(string signId)
    {
        var ids = TrafficSignCatalog.GetAllSignIds();
        Assert.Contains(signId, ids);
    }

    // --- IsKnownSign ---

    [Theory]
    [InlineData("B1", true)]
    [InlineData("A1a", true)]
    [InlineData("P6", true)]
    [InlineData("X99", false)]
    [InlineData("", false)]
    [InlineData("b1", false)] // case-sensitive
    public void IsKnownSign_ReturnsExpected(string signId, bool expected)
    {
        Assert.Equal(expected, TrafficSignCatalog.IsKnownSign(signId));
    }

    // --- TryGetLegacyBasePoint ---

    [Fact]
    public void TryGetLegacyBasePoint_B1_Returns110()
    {
        Assert.True(TrafficSignCatalog.TryGetLegacyBasePoint("B1", out var bp));
        Assert.Equal(1.0, bp.X);
        Assert.Equal(1.0, bp.Y);
        Assert.Equal(0.0, bp.Z);
    }

    [Fact]
    public void TryGetLegacyBasePoint_A1a_Returns220()
    {
        Assert.True(TrafficSignCatalog.TryGetLegacyBasePoint("A1a", out var bp));
        Assert.Equal(2.0, bp.X);
        Assert.Equal(2.0, bp.Y);
        Assert.Equal(0.0, bp.Z);
    }

    [Fact]
    public void TryGetLegacyBasePoint_B28_Returns000()
    {
        Assert.True(TrafficSignCatalog.TryGetLegacyBasePoint("B28", out var bp));
        Assert.Equal(0.0, bp.X);
        Assert.Equal(0.0, bp.Y);
        Assert.Equal(0.0, bp.Z);
    }

    [Fact]
    public void TryGetLegacyBasePoint_P4_Returns410()
    {
        Assert.True(TrafficSignCatalog.TryGetLegacyBasePoint("P4", out var bp));
        Assert.Equal(4.0, bp.X);
        Assert.Equal(1.0, bp.Y);
        Assert.Equal(0.0, bp.Z);
    }

    [Fact]
    public void TryGetLegacyBasePoint_UnknownSign_ReturnsFalse()
    {
        Assert.False(TrafficSignCatalog.TryGetLegacyBasePoint("Z99", out _));
    }

    // --- ComputeSignRotation ---

    [Fact]
    public void ComputeSignRotation_PointingRight_Returns90()
    {
        // atan2(0, 1) = 0° → 0 + 90 = 90
        double rotation = TrafficSignCatalog.ComputeSignRotation(0, 0, 1, 0);
        Assert.Equal(90.0, rotation, 1e-10);
    }

    [Fact]
    public void ComputeSignRotation_PointingUp_Returns180()
    {
        // atan2(1, 0) = 90° → 90 + 90 = 180
        double rotation = TrafficSignCatalog.ComputeSignRotation(0, 0, 0, 1);
        Assert.Equal(180.0, rotation, 1e-10);
    }

    [Fact]
    public void ComputeSignRotation_PointingLeft_Returns270()
    {
        // atan2(0, -1) = 180° → 180 + 90 = 270
        double rotation = TrafficSignCatalog.ComputeSignRotation(0, 0, -1, 0);
        Assert.Equal(270.0, rotation, 1e-10);
    }

    [Fact]
    public void ComputeSignRotation_PointingDown_Returns0()
    {
        // atan2(-1, 0) = -90° → -90 + 90 = 0
        double rotation = TrafficSignCatalog.ComputeSignRotation(0, 0, 0, -1);
        Assert.Equal(0.0, rotation, 1e-10);
    }

    [Fact]
    public void ComputeSignRotation_NonOriginPlacement()
    {
        // From (10,20) pointing right to (15,20): atan2(0,5) = 0° → 0 + 90 = 90
        double rotation = TrafficSignCatalog.ComputeSignRotation(10, 20, 15, 20);
        Assert.Equal(90.0, rotation, 1e-10);
    }

    [Fact]
    public void ComputeSignRotation_45Degrees()
    {
        // atan2(1, 1) = 45° → 45 + 90 = 135
        double rotation = TrafficSignCatalog.ComputeSignRotation(0, 0, 1, 1);
        Assert.Equal(135.0, rotation, 1e-10);
    }

    // --- Constants ---

    [Fact]
    public void PlacementLayerName_IsTrafficSigns()
    {
        Assert.Equal("Traffic Signs", TrafficSignCatalog.PlacementLayerName);
    }

    [Fact]
    public void PlacedSignName_IsTrafficSign()
    {
        Assert.Equal("TrafficSign", TrafficSignCatalog.PlacedSignName);
    }

    [Fact]
    public void RotationOffsetDegrees_Is90()
    {
        Assert.Equal(90.0, TrafficSignCatalog.RotationOffsetDegrees);
    }

    // --- All legacy base points verified against VBScript source ---

    [Theory]
    [InlineData("A1a", 2, 2)]
    [InlineData("A1b", 1, 2)]
    [InlineData("A2a", 3, 2)]
    [InlineData("A2b", 4, 2)]
    [InlineData("B1", 1, 1)]
    [InlineData("B20", 1, 0)]
    [InlineData("B28", 0, 0)]
    [InlineData("B29", 3, 0)]
    [InlineData("C1", 2, 0)]
    [InlineData("P1", 3, 1)]
    [InlineData("P2", 0, 1)]
    [InlineData("P3", 0, 2)]
    [InlineData("P4", 4, 1)]
    [InlineData("P6", 2, 1)]
    public void TryGetLegacyBasePoint_AllSigns_MatchVBScript(string id, double x, double y)
    {
        Assert.True(TrafficSignCatalog.TryGetLegacyBasePoint(id, out var bp));
        Assert.Equal(x, bp.X);
        Assert.Equal(y, bp.Y);
        Assert.Equal(0.0, bp.Z);
    }

    // --- Degenerate rotation case ---

    [Fact]
    public void ComputeSignRotation_CoincidentPoints_Returns90()
    {
        // atan2(0, 0) = 0 in .NET → 0 + 90 = 90
        double rotation = TrafficSignCatalog.ComputeSignRotation(5, 5, 5, 5);
        Assert.Equal(90.0, rotation, 1e-10);
    }
}
