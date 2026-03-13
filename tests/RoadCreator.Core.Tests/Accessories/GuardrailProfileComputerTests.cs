using RoadCreator.Core.Accessories;
using Xunit;

namespace RoadCreator.Core.Tests.Accessories;

public class GuardrailProfileComputerTests
{
    // --- Constants ---

    [Fact]
    public void EdgeOffset_Is0_37()
    {
        Assert.Equal(0.37, GuardrailProfileComputer.EdgeOffset);
    }

    [Fact]
    public void PostSpacing_Is4()
    {
        Assert.Equal(4.0, GuardrailProfileComputer.PostSpacing);
    }

    // --- W-beam profile ---

    [Fact]
    public void GetWBeamProfile_Returns8Points()
    {
        var profile = GuardrailProfileComputer.GetWBeamProfile();
        Assert.Equal(8, profile.Length);
    }

    [Fact]
    public void GetWBeamProfile_AllPointsInYZPlane()
    {
        var profile = GuardrailProfileComputer.GetWBeamProfile();
        foreach (var pt in profile)
            Assert.Equal(0, pt.X, 1e-10);
    }

    [Fact]
    public void GetWBeamProfile_SymmetricWShape()
    {
        // The W-beam has two "dips" forming the characteristic W shape
        var profile = GuardrailProfileComputer.GetWBeamProfile();

        // First dip: points 0-3 (Y from -0.27 to -0.22)
        Assert.True(profile[0].Z < profile[3].Z); // rises
        // Second dip: points 4-7 (Y from -0.22 to -0.27)
        Assert.True(profile[4].Z < profile[7].Z); // rises

        // Middle section is higher
        Assert.True(profile[3].Z < profile[4].Z);
    }

    [Fact]
    public void GetWBeamProfile_MatchesVBScriptTransformedValues()
    {
        // VBScript raw profile after translate(-0.22,0,0.72) + rotate 90° around Z
        var profile = GuardrailProfileComputer.GetWBeamProfile();

        Assert.Equal(-0.27, profile[0].Y, 2);
        Assert.Equal(0.56, profile[0].Z, 2);
        Assert.Equal(-0.32, profile[1].Y, 2);
        Assert.Equal(0.64, profile[2].Z, 2);
        Assert.Equal(-0.22, profile[3].Y, 2);
        Assert.Equal(0.88, profile[7].Z, 2);
    }

    // --- Post box ---

    [Fact]
    public void GetPostBoxCorners_Returns8Points()
    {
        var corners = GuardrailProfileComputer.GetPostBoxCorners();
        Assert.Equal(8, corners.Length);
    }

    [Fact]
    public void GetPostBoxCorners_CorrectDimensions()
    {
        var c = GuardrailProfileComputer.GetPostBoxCorners();

        // Width in X: 0.10m
        Assert.Equal(0.10, c[1].X - c[0].X, 1e-10);
        // Depth in Y: 0.14m
        Assert.Equal(0.14, c[2].Y - c[1].Y, 1e-10);
        // Height in Z: from -0.4 to 0.89 = 1.29m
        Assert.Equal(-0.4, c[0].Z, 1e-10);
        Assert.Equal(0.89, c[4].Z, 1e-10);
    }

    [Fact]
    public void PostCenterOffset_IsCorrect()
    {
        var center = GuardrailProfileComputer.PostCenterOffset;
        Assert.Equal(0.05, center.X, 1e-10);
        Assert.Equal(0.07, center.Y, 1e-10);
        Assert.Equal(0, center.Z, 1e-10);
    }

    // --- Brackets ---

    [Fact]
    public void GetUpperBracket_CorrectHeights()
    {
        var (start, end) = GuardrailProfileComputer.GetUpperBracket();
        Assert.Equal(0.86, start.Z, 1e-10);
        Assert.Equal(0.82, end.Z, 1e-10);
        Assert.Equal(-0.22, end.Y, 1e-10);
    }

    [Fact]
    public void GetLowerBracket_CorrectHeights()
    {
        var (start, end) = GuardrailProfileComputer.GetLowerBracket();
        Assert.Equal(0.50, start.Z, 1e-10);
        Assert.Equal(0.60, end.Z, 1e-10);
    }

    [Fact]
    public void BracketExtrusionDir_Is0_1InX()
    {
        Assert.Equal(0.1, GuardrailProfileComputer.BracketExtrusionDir.X, 1e-10);
        Assert.Equal(0, GuardrailProfileComputer.BracketExtrusionDir.Y, 1e-10);
        Assert.Equal(0, GuardrailProfileComputer.BracketExtrusionDir.Z, 1e-10);
    }
}
