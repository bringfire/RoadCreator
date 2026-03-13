using RoadCreator.Core.Footprint;
using Xunit;

namespace RoadCreator.Core.Tests.Footprint;

public class FootprintSerializerTests
{
    private static OffsetProfile SampleProfile() => new(
        "arterial_2plus1", "m", "centerline",
        new OffsetFeature[]
        {
            new("cl",      0.0,   "centerline",      "centerline"),
            new("ld_l1",  -3.5,   "lane_divider",    "lane_divider"),
            new("edge_l", -5.0,   "edge_of_pavement","edge_of_pavement"),
            new("set_l",  -7.0,   "setback",         "setback"),
            new("ld_r1",   3.5,   "lane_divider",    "lane_divider"),
            new("ld_r2",   7.0,   "lane_divider",    "lane_divider"),
            new("edge_r",  10.5,  "edge_of_pavement","edge_of_pavement"),
            new("set_r",   12.5,  "setback",         "setback"),
        });

    private static StyleSet SampleStyleSet() => new("test_styles",
        new StyleEntry[]
        {
            new("centerline",       "RoadCreator::Markings::Centerline",   "Center",     0.50),
            new("lane_divider",     "RoadCreator::Markings::LaneDivider",  "Dashed",     0.18),
            new("edge_of_pavement", "RoadCreator::Markings::EdgeLine",     "Continuous", 0.50),
            new("setback",          "RoadCreator::Reference::Setback",      "Dashed",     0.18, "#FF0000"),
        });

    // ── Profile round-trip ────────────────────────────────────────────────────

    [Fact]
    public void SerializeProfile_RoundTrips_Name()
    {
        var profile = SampleProfile();
        var json = FootprintSerializer.SerializeProfile(profile);
        var restored = FootprintSerializer.DeserializeProfile(json);

        Assert.NotNull(restored);
        Assert.Equal(profile.Name, restored!.Name);
    }

    [Fact]
    public void SerializeProfile_RoundTrips_AllFields()
    {
        var profile = SampleProfile();
        var json = FootprintSerializer.SerializeProfile(profile);
        var restored = FootprintSerializer.DeserializeProfile(json)!;

        Assert.Equal(profile.Units, restored.Units);
        Assert.Equal(profile.Baseline, restored.Baseline);
        Assert.Equal(profile.Features.Count, restored.Features.Count);

        for (int i = 0; i < profile.Features.Count; i++)
        {
            Assert.Equal(profile.Features[i].Id, restored.Features[i].Id);
            Assert.Equal(profile.Features[i].Offset, restored.Features[i].Offset, 1e-10);
            Assert.Equal(profile.Features[i].Role, restored.Features[i].Role);
            Assert.Equal(profile.Features[i].StyleRef, restored.Features[i].StyleRef);
        }
    }

    [Fact]
    public void SerializeProfile_ProducesSchemaField()
    {
        var json = FootprintSerializer.SerializeProfile(SampleProfile());
        Assert.Contains("roadcreator.offset-profile/v1", json);
    }

    [Fact]
    public void DeserializeProfile_ReturnsNull_ForInvalidJson()
    {
        var result = FootprintSerializer.DeserializeProfile("not json at all {{{}}}");
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeProfile_ReturnsNull_ForEmptyString()
    {
        var result = FootprintSerializer.DeserializeProfile("");
        Assert.Null(result);
    }

    // ── StyleSet round-trip ───────────────────────────────────────────────────

    [Fact]
    public void SerializeStyleSet_RoundTrips_AllFields()
    {
        var styleSet = SampleStyleSet();
        var json = FootprintSerializer.SerializeStyleSet(styleSet);
        var restored = FootprintSerializer.DeserializeStyleSet(json)!;

        Assert.Equal(styleSet.Name, restored.Name);
        Assert.Equal(styleSet.Styles.Count, restored.Styles.Count);

        for (int i = 0; i < styleSet.Styles.Count; i++)
        {
            var orig = styleSet.Styles[i];
            var rest = restored.Styles[i];
            Assert.Equal(orig.Id, rest.Id);
            Assert.Equal(orig.Layer, rest.Layer);
            Assert.Equal(orig.Linetype, rest.Linetype);
            Assert.Equal(orig.PrintWidthMm, rest.PrintWidthMm, 1e-10);
            Assert.Equal(orig.Color, rest.Color);
        }
    }

    [Fact]
    public void SerializeStyleSet_ProducesSchemaField()
    {
        var json = FootprintSerializer.SerializeStyleSet(SampleStyleSet());
        Assert.Contains("roadcreator.style-set/v1", json);
    }

    [Fact]
    public void SerializeStyleSet_OmitsNullColor()
    {
        var styleSet = new StyleSet("no_color",
            new[] { new StyleEntry("cl", "Layer::CL", "Center", 0.5, null) });
        var json = FootprintSerializer.SerializeStyleSet(styleSet);
        // null Color should be omitted from JSON
        Assert.DoesNotContain("\"color\"", json);
    }

    [Fact]
    public void SerializeStyleSet_IncludesColor_WhenPresent()
    {
        var styleSet = new StyleSet("with_color",
            new[] { new StyleEntry("cl", "Layer::CL", "Center", 0.5, "#FF0000") });
        var json = FootprintSerializer.SerializeStyleSet(styleSet);
        Assert.Contains("#FF0000", json);
    }

    [Fact]
    public void DeserializeStyleSet_ReturnsNull_ForInvalidJson()
    {
        var result = FootprintSerializer.DeserializeStyleSet("{invalid}");
        Assert.Null(result);
    }

    // ── Schema validation ─────────────────────────────────────────────────────

    [Fact]
    public void DeserializeProfile_ReturnsNull_ForEmptyObject()
    {
        // {} has no name — must be rejected
        var result = FootprintSerializer.DeserializeProfile("{}");
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeProfile_ReturnsNull_ForWrongSchema()
    {
        var json = "{\"schema\":\"other.tool/v1\",\"name\":\"test\",\"features\":[]}";
        var result = FootprintSerializer.DeserializeProfile(json);
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeProfile_Accepts_CorrectSchema()
    {
        var json = "{\"schema\":\"roadcreator.offset-profile/v1\",\"name\":\"test\",\"features\":[]}";
        var result = FootprintSerializer.DeserializeProfile(json);
        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
    }

    [Fact]
    public void DeserializeProfile_Accepts_FutureMinorVersion()
    {
        // prefix match allows forward-compatible v2
        var json = "{\"schema\":\"roadcreator.offset-profile/v2\",\"name\":\"test\",\"features\":[]}";
        var result = FootprintSerializer.DeserializeProfile(json);
        Assert.NotNull(result);
    }

    [Fact]
    public void DeserializeStyleSet_ReturnsNull_ForEmptyObject()
    {
        var result = FootprintSerializer.DeserializeStyleSet("{}");
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeStyleSet_ReturnsNull_ForWrongSchema()
    {
        var json = "{\"schema\":\"other.tool/v1\",\"name\":\"test\",\"styles\":[]}";
        var result = FootprintSerializer.DeserializeStyleSet(json);
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeStyleSet_Accepts_CorrectSchema()
    {
        var json = "{\"schema\":\"roadcreator.style-set/v1\",\"name\":\"test\",\"styles\":[]}";
        var result = FootprintSerializer.DeserializeStyleSet(json);
        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
    }
}
