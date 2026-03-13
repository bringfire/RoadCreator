using RoadCreator.Core.CrossSection;
using Xunit;

namespace RoadCreator.Core.Tests.CrossSection;

public class RoadCategoryTests
{
    // --- Category properties ---

    [Fact]
    public void S65_HasCorrectValues()
    {
        Assert.Equal("S 6.5", RoadCategory.S65.Code);
        Assert.Equal(2.75, RoadCategory.S65.HalfWidth);
        Assert.Equal(0.0, RoadCategory.S65.MedianWidth);
        Assert.False(RoadCategory.S65.IsDivided);
    }

    [Fact]
    public void S75_HasCorrectValues()
    {
        Assert.Equal(3.25, RoadCategory.S75.HalfWidth);
        Assert.Equal(0.0, RoadCategory.S75.MedianWidth);
    }

    [Fact]
    public void S95_HasCorrectValues()
    {
        Assert.Equal(4.25, RoadCategory.S95.HalfWidth);
    }

    [Fact]
    public void S115_HasCorrectValues()
    {
        Assert.Equal(5.25, RoadCategory.S115.HalfWidth);
    }

    [Fact]
    public void S2075_IsDivided()
    {
        Assert.Equal(10.25, RoadCategory.S2075.HalfWidth);
        Assert.Equal(1.25, RoadCategory.S2075.MedianWidth);
        Assert.True(RoadCategory.S2075.IsDivided);
    }

    [Fact]
    public void D48_HasCorrectValues()
    {
        Assert.Equal("D 4/8", RoadCategory.D48.Code);
        Assert.Equal(8.0, RoadCategory.D48.HalfWidth);
        Assert.Equal(4.0, RoadCategory.D48.MedianWidth);
        Assert.True(RoadCategory.D48.IsDivided);
    }

    [Fact]
    public void AllCategories_Has10Entries()
    {
        Assert.Equal(10, RoadCategory.All.Count);
    }

    // --- FromCode ---

    [Fact]
    public void FromCode_ValidCode_ReturnsCategory()
    {
        var cat = RoadCategory.FromCode("S 6.5");
        Assert.NotNull(cat);
        Assert.Equal(2.75, cat!.HalfWidth);
    }

    [Fact]
    public void FromCode_CaseInsensitive()
    {
        var cat = RoadCategory.FromCode("d 4/8");
        Assert.NotNull(cat);
        Assert.Equal(8.0, cat!.HalfWidth);
    }

    [Fact]
    public void FromCode_InvalidCode_ReturnsNull()
    {
        Assert.Null(RoadCategory.FromCode("X 99"));
    }

    [Fact]
    public void FromCode_EmptyString_ReturnsNull()
    {
        Assert.Null(RoadCategory.FromCode(""));
    }

    [Fact]
    public void FromCode_NullString_ReturnsNull()
    {
        Assert.Null(RoadCategory.FromCode(null!));
    }

    // --- ToString ---

    [Fact]
    public void ToString_ReturnsCode()
    {
        Assert.Equal("S 7.5", RoadCategory.S75.ToString());
    }

    // --- All divided categories have median > 0 ---

    [Fact]
    public void UndividedCategories_HaveZeroMedian()
    {
        Assert.False(RoadCategory.S65.IsDivided);
        Assert.False(RoadCategory.S75.IsDivided);
        Assert.False(RoadCategory.S95.IsDivided);
        Assert.False(RoadCategory.S115.IsDivided);
    }

    [Fact]
    public void DividedCategories_HavePositiveMedian()
    {
        Assert.True(RoadCategory.S2075.IsDivided);
        Assert.True(RoadCategory.S245.IsDivided);
        Assert.True(RoadCategory.D255.IsDivided);
        Assert.True(RoadCategory.D275.IsDivided);
        Assert.True(RoadCategory.D335.IsDivided);
        Assert.True(RoadCategory.D48.IsDivided);
    }

    // --- Total width consistency ---

    [Theory]
    [InlineData("S 6.5", 5.5)]   // 2.75 × 2 + 0
    [InlineData("S 7.5", 6.5)]   // 3.25 × 2 + 0
    [InlineData("S 9.5", 8.5)]   // 4.25 × 2 + 0
    [InlineData("S 11.5", 10.5)] // 5.25 × 2 + 0
    public void UndividedRoads_TotalWidth_MatchesCategoryApprox(string code, double expectedPavedWidth)
    {
        var cat = RoadCategory.FromCode(code)!;
        double totalWidth = 2 * cat.HalfWidth + cat.MedianWidth;
        Assert.Equal(expectedPavedWidth, totalWidth, 1e-10);
    }
}
