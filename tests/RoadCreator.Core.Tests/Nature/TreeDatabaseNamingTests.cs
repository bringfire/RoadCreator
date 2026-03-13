using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class TreeDatabaseNamingTests
{
    // --- Constants ---

    [Fact]
    public void LayerName_IsStromyDatabaze()
    {
        Assert.Equal("Stromy databaze", TreeDatabaseNaming.LayerName);
    }

    [Fact]
    public void TreeNamePrefix_IsStrom()
    {
        Assert.Equal("Strom", TreeDatabaseNaming.TreeNamePrefix);
    }

    // --- GetTreeName ---

    [Theory]
    [InlineData(0, "Strom0")]
    [InlineData(1, "Strom1")]
    [InlineData(5, "Strom5")]
    [InlineData(99, "Strom99")]
    public void GetTreeName_CorrectFormat(int index, string expected)
    {
        Assert.Equal(expected, TreeDatabaseNaming.GetTreeName(index));
    }

    [Fact]
    public void GetTreeName_NegativeIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TreeDatabaseNaming.GetTreeName(-1));
    }

    // --- GetTreeCompanionPointName ---

    [Fact]
    public void GetTreeCompanionPointName_CorrectFormat()
    {
        // "Strom0" + "-point(RoadCreator)"
        Assert.Equal("Strom0-point(RoadCreator)",
            TreeDatabaseNaming.GetTreeCompanionPointName(0));
    }

    [Fact]
    public void GetTreeCompanionPointName_Index5()
    {
        Assert.Equal("Strom5-point(RoadCreator)",
            TreeDatabaseNaming.GetTreeCompanionPointName(5));
    }

    // --- IsTreeName ---

    [Theory]
    [InlineData("Strom0", true)]
    [InlineData("Strom1", true)]
    [InlineData("Strom99", true)]
    [InlineData("Strom", false)]       // No index
    [InlineData("strom0", false)]      // Wrong case
    [InlineData("Tree0", false)]       // Wrong prefix
    [InlineData("Strom0-point(RoadCreator)", false)]  // Companion name
    [InlineData("", false)]
    [InlineData("StromABC", false)]    // Non-numeric suffix
    public void IsTreeName_CorrectResults(string name, bool expected)
    {
        Assert.Equal(expected, TreeDatabaseNaming.IsTreeName(name));
    }

    // --- ParseTreeIndex ---

    [Theory]
    [InlineData("Strom0", 0)]
    [InlineData("Strom1", 1)]
    [InlineData("Strom42", 42)]
    public void ParseTreeIndex_ValidNames(string name, int expected)
    {
        Assert.Equal(expected, TreeDatabaseNaming.ParseTreeIndex(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tree0")]
    [InlineData("Strom")]
    [InlineData("StromABC")]
    public void ParseTreeIndex_InvalidNames_ReturnsMinusOne(string name)
    {
        Assert.Equal(-1, TreeDatabaseNaming.ParseTreeIndex(name));
    }
}
