using RoadCreator.Core.Nature;
using Xunit;

namespace RoadCreator.Core.Tests.Nature;

public class TreeDatabaseNamingTests
{
    // --- Constants ---

    [Fact]
    public void LayerName_IsTreeDatabase()
    {
        Assert.Equal("Tree Database", TreeDatabaseNaming.LayerName);
    }

    [Fact]
    public void TreeNamePrefix_IsTree()
    {
        Assert.Equal("Tree", TreeDatabaseNaming.TreeNamePrefix);
    }

    // --- GetTreeName ---

    [Theory]
    [InlineData(0, "Tree0")]
    [InlineData(1, "Tree1")]
    [InlineData(5, "Tree5")]
    [InlineData(99, "Tree99")]
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
        // "Tree0" + "-point(RoadCreator)"
        Assert.Equal("Tree0-point(RoadCreator)",
            TreeDatabaseNaming.GetTreeCompanionPointName(0));
    }

    [Fact]
    public void GetTreeCompanionPointName_Index5()
    {
        Assert.Equal("Tree5-point(RoadCreator)",
            TreeDatabaseNaming.GetTreeCompanionPointName(5));
    }

    // --- IsTreeName ---

    [Theory]
    [InlineData("Tree0", true)]
    [InlineData("Tree1", true)]
    [InlineData("Tree99", true)]
    [InlineData("Tree", false)]        // No index
    [InlineData("tree0", false)]       // Wrong case
    [InlineData("Strom0", false)]      // Wrong prefix
    [InlineData("Tree0-point(RoadCreator)", false)]  // Companion name
    [InlineData("", false)]
    [InlineData("TreeABC", false)]     // Non-numeric suffix
    public void IsTreeName_CorrectResults(string name, bool expected)
    {
        Assert.Equal(expected, TreeDatabaseNaming.IsTreeName(name));
    }

    // --- ParseTreeIndex ---

    [Theory]
    [InlineData("Tree0", 0)]
    [InlineData("Tree1", 1)]
    [InlineData("Tree42", 42)]
    public void ParseTreeIndex_ValidNames(string name, int expected)
    {
        Assert.Equal(expected, TreeDatabaseNaming.ParseTreeIndex(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Strom0")]
    [InlineData("Tree")]
    [InlineData("TreeABC")]
    public void ParseTreeIndex_InvalidNames_ReturnsMinusOne(string name)
    {
        Assert.Equal(-1, TreeDatabaseNaming.ParseTreeIndex(name));
    }
}
