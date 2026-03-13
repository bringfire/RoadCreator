using RoadCreator.Core.Accessories;
using Xunit;

namespace RoadCreator.Core.Tests.Accessories;

public class DeltaBlokProfileComputerTests
{
    // --- Block spacing ---

    [Fact]
    public void BlockSpacing_Is4()
    {
        Assert.Equal(4.0, DeltaBlokProfileComputer.BlockSpacing);
    }

    // --- Main profiles ---

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetMainProfile_Returns8Points(DeltaBlokVariant variant)
    {
        var profile = DeltaBlokProfileComputer.GetMainProfile(variant);
        Assert.Equal(8, profile.Length);
    }

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetMainProfile_AllInYZPlane(DeltaBlokVariant variant)
    {
        var profile = DeltaBlokProfileComputer.GetMainProfile(variant);
        foreach (var pt in profile)
            Assert.Equal(0, pt.X, 1e-10);
    }

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetMainProfile_Symmetric(DeltaBlokVariant variant)
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(variant);
        // Profile is symmetric: p[0].Y = -p[7].Y, p[1].Y = -p[6].Y, etc.
        Assert.Equal(-p[0].Y, p[7].Y, 1e-10);
        Assert.Equal(-p[1].Y, p[6].Y, 1e-10);
        Assert.Equal(-p[2].Y, p[5].Y, 1e-10);
        Assert.Equal(-p[3].Y, p[4].Y, 1e-10);
        // Z values also symmetric
        Assert.Equal(p[0].Z, p[7].Z, 1e-10);
        Assert.Equal(p[1].Z, p[6].Z, 1e-10);
        Assert.Equal(p[2].Z, p[5].Z, 1e-10);
        Assert.Equal(p[3].Z, p[4].Z, 1e-10);
    }

    [Fact]
    public void GetMainProfile_Blok80_CorrectHeight()
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok80);
        Assert.Equal(0.80, p[3].Z, 1e-10); // Top height = 80cm
    }

    [Fact]
    public void GetMainProfile_Blok100S_CorrectHeight()
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok100S);
        Assert.Equal(1.00, p[3].Z, 1e-10); // Top height = 100cm
    }

    [Fact]
    public void GetMainProfile_Blok100_CorrectHeight()
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok100);
        Assert.Equal(1.00, p[3].Z, 1e-10);
    }

    [Fact]
    public void GetMainProfile_Blok120_CorrectHeight()
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok120);
        Assert.Equal(1.20, p[3].Z, 1e-10); // Top height = 120cm
    }

    [Fact]
    public void GetMainProfile_Blok80_VBScriptValues()
    {
        var p = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok80);
        Assert.Equal(-0.31, p[0].Y, 1e-10);
        Assert.Equal(-0.05, p[0].Z, 1e-10);
        Assert.Equal(-0.29, p[1].Y, 1e-10);
        Assert.Equal(0.11, p[1].Z, 1e-10);
    }

    // --- End-cap profiles ---

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetEndCapProfile_Returns8Points(DeltaBlokVariant variant)
    {
        var profile = DeltaBlokProfileComputer.GetEndCapProfile(variant);
        Assert.Equal(8, profile.Length);
    }

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetEndCapProfile_TopCappedAtShoulderHeight(DeltaBlokVariant variant)
    {
        var main = DeltaBlokProfileComputer.GetMainProfile(variant);
        var endCap = DeltaBlokProfileComputer.GetEndCapProfile(variant);

        double shoulderZ = main[2].Z;
        // Points 3,4 should have Z capped at shoulder height
        Assert.Equal(shoulderZ, endCap[3].Z, 1e-10);
        Assert.Equal(shoulderZ, endCap[4].Z, 1e-10);
    }

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetEndCapProfile_BasePointsUnchanged(DeltaBlokVariant variant)
    {
        var main = DeltaBlokProfileComputer.GetMainProfile(variant);
        var endCap = DeltaBlokProfileComputer.GetEndCapProfile(variant);

        Assert.Equal(main[0].Y, endCap[0].Y, 1e-10);
        Assert.Equal(main[0].Z, endCap[0].Z, 1e-10);
        Assert.Equal(main[7].Y, endCap[7].Y, 1e-10);
        Assert.Equal(main[7].Z, endCap[7].Z, 1e-10);
    }

    // --- Full end-cap profiles ---

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetFullEndCapProfile_Returns8Points(DeltaBlokVariant variant)
    {
        var profile = DeltaBlokProfileComputer.GetFullEndCapProfile(variant);
        Assert.Equal(8, profile.Length);
    }

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetFullEndCapProfile_NearlyFlat(DeltaBlokVariant variant)
    {
        var main = DeltaBlokProfileComputer.GetMainProfile(variant);
        var fullEnd = DeltaBlokProfileComputer.GetFullEndCapProfile(variant);

        // Points 2,3,4,5 should all be at approximately the height of point 1 or 6
        double baseZ = main[1].Z;
        Assert.Equal(baseZ, fullEnd[2].Z, 1e-10);
        Assert.Equal(baseZ, fullEnd[3].Z, 1e-10);
        Assert.Equal(main[6].Z, fullEnd[4].Z, 1e-10);
        Assert.Equal(main[6].Z, fullEnd[5].Z, 1e-10);
    }

    // --- Transition distance ---

    [Theory]
    [InlineData(DeltaBlokVariant.Blok80)]
    [InlineData(DeltaBlokVariant.Blok100S)]
    [InlineData(DeltaBlokVariant.Blok100)]
    [InlineData(DeltaBlokVariant.Blok120)]
    public void GetTransitionDistance_PositiveAndLessThanBlockSpacing(DeltaBlokVariant variant)
    {
        double dist = DeltaBlokProfileComputer.GetTransitionDistance(variant);
        Assert.True(dist > 0);
        Assert.True(dist < DeltaBlokProfileComputer.BlockSpacing);
    }

    [Fact]
    public void GetTransitionDistance_Blok80_MatchesVBScript()
    {
        // VBScript: 4 * ((0.29 - 0.11) / (0.80 - 0.11)) = 4 * 0.18/0.69
        double expected = 4.0 * (0.18 / 0.69);
        double actual = DeltaBlokProfileComputer.GetTransitionDistance(DeltaBlokVariant.Blok80);
        Assert.Equal(expected, actual, 4);
    }

    [Fact]
    public void GetTransitionDistance_Blok120_MatchesVBScript()
    {
        // VBScript: 4 * ((0.39 - 0.08) / (1.2 - 0.08)) = 4 * 0.31/1.12
        double expected = 4.0 * (0.31 / 1.12);
        double actual = DeltaBlokProfileComputer.GetTransitionDistance(DeltaBlokVariant.Blok120);
        Assert.Equal(expected, actual, 4);
    }

    // --- EndCap coordinate-level assertions ---

    [Fact]
    public void GetEndCapProfile_Blok80_CoordinateValues()
    {
        var m = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok80);
        var e = DeltaBlokProfileComputer.GetEndCapProfile(DeltaBlokVariant.Blok80);

        // Points 0,1 unchanged from main
        Assert.Equal(m[0].Y, e[0].Y, 1e-10);
        Assert.Equal(m[0].Z, e[0].Z, 1e-10);
        Assert.Equal(m[1].Y, e[1].Y, 1e-10);
        Assert.Equal(m[1].Z, e[1].Z, 1e-10);

        // Point 2: Y shifted -0.01 from main[2], Z = main[2].Z (shoulder)
        Assert.Equal(m[2].Y - 0.01, e[2].Y, 1e-10);
        Assert.Equal(m[2].Z, e[2].Z, 1e-10);

        // Point 3: Y = main[2].Y, Z capped at shoulder (main[2].Z)
        Assert.Equal(m[2].Y, e[3].Y, 1e-10);
        Assert.Equal(0.29, e[3].Z, 1e-10);

        // Point 4: Y = main[4].Y, Z capped at shoulder
        Assert.Equal(m[4].Y, e[4].Y, 1e-10);
        Assert.Equal(0.29, e[4].Z, 1e-10);

        // Point 5: Y shifted +0.01 from main[4], Z = shoulder
        Assert.Equal(m[4].Y + 0.01, e[5].Y, 1e-10);
        Assert.Equal(m[2].Z, e[5].Z, 1e-10);

        // Points 6,7 unchanged
        Assert.Equal(m[6].Y, e[6].Y, 1e-10);
        Assert.Equal(m[7].Y, e[7].Y, 1e-10);
    }

    // --- FullEndCap coordinate-level assertions ---

    [Fact]
    public void GetFullEndCapProfile_Blok80_CoordinateValues()
    {
        var m = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok80);
        var f = DeltaBlokProfileComputer.GetFullEndCapProfile(DeltaBlokVariant.Blok80);

        // Points 0,1 unchanged
        Assert.Equal(m[0].Y, f[0].Y, 1e-10);
        Assert.Equal(m[1].Y, f[1].Y, 1e-10);

        // Point 2: Y = main[1].Y + 0.005, Z = main[1].Z
        Assert.Equal(m[1].Y + 0.005, f[2].Y, 1e-10);
        Assert.Equal(m[1].Z, f[2].Z, 1e-10);

        // Point 3: Y = main[1].Y + 0.01, Z = main[1].Z
        Assert.Equal(m[1].Y + 0.01, f[3].Y, 1e-10);
        Assert.Equal(m[1].Z, f[3].Z, 1e-10);

        // Point 4: Y = main[6].Y - 0.01, Z = main[6].Z
        Assert.Equal(m[6].Y - 0.01, f[4].Y, 1e-10);
        Assert.Equal(m[6].Z, f[4].Z, 1e-10);

        // Point 5: Y = main[6].Y - 0.005, Z = main[6].Z
        Assert.Equal(m[6].Y - 0.005, f[5].Y, 1e-10);
        Assert.Equal(m[6].Z, f[5].Z, 1e-10);

        // Points 6,7 unchanged
        Assert.Equal(m[6].Y, f[6].Y, 1e-10);
        Assert.Equal(m[7].Y, f[7].Y, 1e-10);
    }

    [Fact]
    public void GetFullEndCapProfile_Blok120_CoordinateValues()
    {
        var m = DeltaBlokProfileComputer.GetMainProfile(DeltaBlokVariant.Blok120);
        var f = DeltaBlokProfileComputer.GetFullEndCapProfile(DeltaBlokVariant.Blok120);

        // All intermediate points (2-5) should be near base height (m[1].Z / m[6].Z)
        Assert.Equal(0.08, f[2].Z, 1e-10);
        Assert.Equal(0.08, f[3].Z, 1e-10);
        Assert.Equal(0.08, f[4].Z, 1e-10);
        Assert.Equal(0.08, f[5].Z, 1e-10);
    }

    // --- Invalid variant ---

    [Fact]
    public void GetMainProfile_InvalidVariant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeltaBlokProfileComputer.GetMainProfile((DeltaBlokVariant)99));
    }
}
