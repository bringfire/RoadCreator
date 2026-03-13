using RoadCreator.Core.Accessories;
using Xunit;

namespace RoadCreator.Core.Tests.Accessories;

public class DatabaseNamingTests
{
    // --- GetCompanionPointName ---

    [Fact]
    public void GetCompanionPointName_AppendsCorrectSuffix()
    {
        Assert.Equal("Lamp-point(RoadCreator)",
            DatabaseNaming.GetCompanionPointName("Lamp"));
    }

    [Fact]
    public void GetCompanionPointName_WithSpaces()
    {
        Assert.Equal("Road Sign-point(RoadCreator)",
            DatabaseNaming.GetCompanionPointName("Road Sign"));
    }

    [Fact]
    public void GetCompanionPointName_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DatabaseNaming.GetCompanionPointName(""));
    }

    [Fact]
    public void GetCompanionPointName_WhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DatabaseNaming.GetCompanionPointName("   "));
    }

    // --- IsCompanionPointName ---

    [Fact]
    public void IsCompanionPointName_ValidName_ReturnsTrue()
    {
        Assert.True(DatabaseNaming.IsCompanionPointName("Lamp-point(RoadCreator)"));
    }

    [Fact]
    public void IsCompanionPointName_RegularName_ReturnsFalse()
    {
        Assert.False(DatabaseNaming.IsCompanionPointName("Lamp"));
    }

    [Fact]
    public void IsCompanionPointName_Empty_ReturnsFalse()
    {
        Assert.False(DatabaseNaming.IsCompanionPointName(""));
    }

    [Fact]
    public void IsCompanionPointName_Null_ReturnsFalse()
    {
        Assert.False(DatabaseNaming.IsCompanionPointName(null!));
    }

    [Fact]
    public void IsCompanionPointName_PartialMatch_ReturnsFalse()
    {
        Assert.False(DatabaseNaming.IsCompanionPointName("-point(RoadCreator"));
    }

    // --- ExtractObjectName ---

    [Fact]
    public void ExtractObjectName_ValidCompanion_ReturnsName()
    {
        Assert.Equal("Lamp", DatabaseNaming.ExtractObjectName("Lamp-point(RoadCreator)"));
    }

    [Fact]
    public void ExtractObjectName_WithSpaces_ReturnsName()
    {
        Assert.Equal("Road Sign",
            DatabaseNaming.ExtractObjectName("Road Sign-point(RoadCreator)"));
    }

    [Fact]
    public void ExtractObjectName_InvalidFormat_ReturnsNull()
    {
        Assert.Null(DatabaseNaming.ExtractObjectName("Lamp"));
    }

    [Fact]
    public void ExtractObjectName_Empty_ReturnsNull()
    {
        Assert.Null(DatabaseNaming.ExtractObjectName(""));
    }

    // --- Roundtrip ---

    [Fact]
    public void Roundtrip_CreateThenExtract()
    {
        string name = "MyObject";
        string companion = DatabaseNaming.GetCompanionPointName(name);
        Assert.True(DatabaseNaming.IsCompanionPointName(companion));
        Assert.Equal(name, DatabaseNaming.ExtractObjectName(companion));
    }

    // --- VBScript compatibility ---

    [Fact]
    public void CompanionSuffix_MatchesVBScript()
    {
        // VBScript: CStr(Vyber) & "-point(RoadCreator)"
        Assert.Equal("-point(RoadCreator)", DatabaseNaming.CompanionSuffix);
    }

    [Fact]
    public void PoleName_MatchesVBScript()
    {
        // VBScript uses "Silnicnisloupek(RoadCreator)" as the pole object name
        // The companion would be "Silnicnisloupek(RoadCreator)-point(RoadCreator)"
        var companion = DatabaseNaming.GetCompanionPointName("Silnicnisloupek(RoadCreator)");
        Assert.Equal("Silnicnisloupek(RoadCreator)-point(RoadCreator)", companion);
    }
}
