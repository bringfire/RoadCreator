using RoadCreator.Core.Alignment;
using Xunit;

namespace RoadCreator.Core.Tests.Alignment;

public class RoadObjectNamingTests
{
    // --- ParseRoadName ---

    [Theory]
    [InlineData("Road_1 1", "Road_1")]
    [InlineData("Road_1 2 R ZP 45.500000", "Road_1")]
    [InlineData("Road_1 LongProfile 420", "Road_1")]
    [InlineData("Road_1 3D_route", "Road_1")]
    [InlineData("Road_12", "Road_12")]
    public void ParseRoadName_ExtractsFirstToken(string input, string expected)
    {
        Assert.Equal(expected, RoadObjectNaming.ParseRoadName(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ParseRoadName_NullOrEmpty_ReturnsNull(string? input)
    {
        Assert.Null(RoadObjectNaming.ParseRoadName(input));
    }

    // --- FormatChainage ---

    [Theory]
    [InlineData(0.0, "0.000000")]
    [InlineData(70.0, "70.000000")]
    [InlineData(123.456789, "123.456789")]
    [InlineData(1500.1, "1500.100000")]
    public void FormatChainage_SixDecimalPlaces(double value, string expected)
    {
        Assert.Equal(expected, RoadObjectNaming.FormatChainage(value));
    }

    [Fact]
    public void FormatChainage_CultureInvariant_UsesDotSeparator()
    {
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            // Czech locale uses comma as decimal separator
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("cs-CZ");
            Assert.Equal("45.500000", RoadObjectNaming.FormatChainage(45.5));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // --- BuildSegmentName ---

    [Fact]
    public void BuildSegmentName_FormatsCorrectly()
    {
        Assert.Equal("Road_1 2", RoadObjectNaming.BuildSegmentName("Road_1", 2));
    }

    // --- BuildStationingName ---

    [Fact]
    public void BuildStationingName_FormatsCorrectly()
    {
        string result = RoadObjectNaming.BuildStationingName("Road_1", 1, "R", "ZP", 45.5);
        Assert.Equal("Road_1 1 R ZP 45.500000", result);
    }

    [Fact]
    public void BuildStationingName_StartPoint()
    {
        string result = RoadObjectNaming.BuildStationingName("Road_1", 0, "R", "ZU", 0);
        Assert.Equal("Road_1 0 R ZU 0.000000", result);
    }

    // --- BuildRoute3DName ---

    [Fact]
    public void BuildRoute3DName_FormatsCorrectly()
    {
        Assert.Equal("Road_1 3D_route", RoadObjectNaming.BuildRoute3DName("Road_1"));
    }

    // --- BuildLongProfileName ---

    [Fact]
    public void BuildLongProfileName_FormatsCorrectly()
    {
        Assert.Equal("Road_1 LongProfile 420", RoadObjectNaming.BuildLongProfileName("Road_1", 420));
    }

    // --- TryParseLongProfileDatum ---

    [Theory]
    [InlineData("Road_1 LongProfile 420", true, 420.0)]
    [InlineData("Road_1 LongProfile 350", true, 350.0)]
    [InlineData("Road_1 Podélný_profil 420", true, 420.0)] // Czech name
    public void TryParseLongProfileDatum_ValidNames(string name, bool expectedResult, double expectedDatum)
    {
        bool result = RoadObjectNaming.TryParseLongProfileDatum(name, out double datum);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedDatum, datum, 1e-10);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Road_1")]
    [InlineData("Road_1 SomethingElse 420")]
    [InlineData("Road_1 LongProfile")]
    public void TryParseLongProfileDatum_InvalidNames_ReturnsFalse(string? name)
    {
        Assert.False(RoadObjectNaming.TryParseLongProfileDatum(name, out _));
    }

    // --- BuildLongProfileName / TryParseLongProfileDatum round-trip ---

    [Theory]
    [InlineData("Road_1", 420.0)]
    [InlineData("Road_5", 350.0)]
    [InlineData("Road_12", 0.0)]
    public void BuildLongProfileName_RoundTrips_WithTryParse(string roadName, double datum)
    {
        string name = RoadObjectNaming.BuildLongProfileName(roadName, datum);
        bool parsed = RoadObjectNaming.TryParseLongProfileDatum(name, out double parsedDatum);
        Assert.True(parsed);
        Assert.Equal(datum, parsedDatum, 1e-10);
    }

    // --- GetNextRoadName ---

    [Fact]
    public void GetNextRoadName_NoExisting_ReturnsRoad1()
    {
        string result = RoadObjectNaming.GetNextRoadName(_ => false);
        Assert.Equal("Road_1", result);
    }

    [Fact]
    public void GetNextRoadName_FirstTwoExist_ReturnsRoad3()
    {
        string result = RoadObjectNaming.GetNextRoadName(name =>
            name == "Road_1" || name == "Road_2");
        Assert.Equal("Road_3", result);
    }

    // --- TypeCodes constants ---

    [Fact]
    public void TypeCodes_MatchVBScriptConvention()
    {
        Assert.Equal("ZU", RoadObjectNaming.TypeCodes.Start);
        Assert.Equal("KU", RoadObjectNaming.TypeCodes.End);
        Assert.Equal("ZP", RoadObjectNaming.TypeCodes.TransitionStart);
        Assert.Equal("PO", RoadObjectNaming.TypeCodes.ArcStart);
        Assert.Equal("OP", RoadObjectNaming.TypeCodes.ArcEnd);
        Assert.Equal("KP", RoadObjectNaming.TypeCodes.TransitionEnd);
        Assert.Equal("ZZ", RoadObjectNaming.TypeCodes.ParabolicStart);
        Assert.Equal("V", RoadObjectNaming.TypeCodes.ParabolicVertex);
        Assert.Equal("KZ", RoadObjectNaming.TypeCodes.ParabolicEnd);
    }

    // --- Constants ---

    [Fact]
    public void RoadPrefix_IsRoad()
    {
        Assert.Equal("Road_", RoadObjectNaming.RoadPrefix);
    }

    [Fact]
    public void Route3DSuffix_Is3Droute()
    {
        Assert.Equal("3D_route", RoadObjectNaming.Route3DSuffix);
    }
}
