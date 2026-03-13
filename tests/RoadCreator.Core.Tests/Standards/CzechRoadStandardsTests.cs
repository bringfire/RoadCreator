using RoadCreator.Core.Standards;
using Xunit;

namespace RoadCreator.Core.Tests.Standards;

public class CzechRoadStandardsTests
{
    [Theory]
    [InlineData(300, "S 7.5", 0)]     // R >= 250, no widening
    [InlineData(250, "S 7.5", 0)]     // R == 250, boundary
    [InlineData(200, "S 7.5", 0.2)]   // 200 <= R < 250
    [InlineData(170, "S 7.5", 0.25)]  // 170 <= R < 200
    [InlineData(141, "S 7.5", 0.30)]  // 141 <= R < 170
    [InlineData(125, "S 7.5", 0.35)]  // 125 <= R < 141
    [InlineData(110, "S 7.5", 0.4)]   // 110 <= R < 125
    [InlineData(100, "S 7.5", 0.55)]  // R < 110 + 0.05 adjustment for S 7.5
    [InlineData(100, "S 6.5", 0.8)]   // R < 110 + 0.3 adjustment for S 6.5
    [InlineData(100, "S 9.5", 0.5)]   // R < 110, no adjustment
    public void GetWidening_ReturnsCorrectValues(double radius, string category, double expected)
    {
        Assert.Equal(expected, CzechRoadStandards.GetWidening(radius, category), 6);
    }

    [Theory]
    [InlineData(40, 4.5)]
    [InlineData(35, 5.0)]
    [InlineData(32, 5.25)]
    [InlineData(30, 5.5)]
    [InlineData(28, 6.0)]
    [InlineData(25, 6.5)]
    [InlineData(20, 0)]
    public void GetRoundaboutLaneWidth_ReturnsCorrectValues(double diameter, double expected)
    {
        Assert.Equal(expected, CzechRoadStandards.GetRoundaboutLaneWidth(diameter));
    }

    [Fact]
    public void Categories_ContainExpectedCount()
    {
        Assert.Equal(10, CzechRoadStandards.Categories.Length);
    }

    [Theory]
    [InlineData("S 6.5", 2.75)]    // 1 lane × 2.75
    [InlineData("S 7.5", 3.00)]    // 1 lane × 3.00
    [InlineData("S 9.5", 3.50)]    // 1 lane × 3.50
    [InlineData("S 11.5", 3.50)]   // 1 lane × 3.50
    [InlineData("S 20.75", 10.50)] // 2 lanes × 5.25
    [InlineData("S 24.5", 10.50)]  // 2 lanes × 5.25
    [InlineData("D 25.5", 7.50)]   // 2 lanes × 3.75
    [InlineData("D 27.5", 7.50)]   // 2 lanes × 3.75
    [InlineData("D 33.5", 11.25)]  // 3 lanes × 3.75
    [InlineData("D 4/8", 7.50)]    // 2 lanes × 3.75
    public void HalfWidth_ComputedCorrectly(string name, double expectedHalfWidth)
    {
        var cat = CzechRoadStandards.Categories.First(c => c.Name == name);
        Assert.Equal(expectedHalfWidth, cat.HalfWidth, 6);
    }
}
