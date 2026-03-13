using RoadCreator.Core.Math;
using RoadCreator.Core.Terrain;
using Xunit;

namespace RoadCreator.Core.Tests.Terrain;

public class LongitudinalProfileComputerTests
{
    // --- ComputeReferenceDatum ---

    [Fact]
    public void ComputeReferenceDatum_TypicalElevation()
    {
        // minElev=357.5 => floor(357.5/10) = 35, (35-1)*10 = 340
        Assert.Equal(340.0, LongitudinalProfileComputer.ComputeReferenceDatum(357.5));
    }

    [Fact]
    public void ComputeReferenceDatum_ExactMultipleOf10()
    {
        // minElev=200 => floor(200/10) = 20, (20-1)*10 = 190
        Assert.Equal(190.0, LongitudinalProfileComputer.ComputeReferenceDatum(200.0));
    }

    [Fact]
    public void ComputeReferenceDatum_SmallElevation()
    {
        // minElev=15 => floor(15/10) = 1, (1-1)*10 = 0
        Assert.Equal(0.0, LongitudinalProfileComputer.ComputeReferenceDatum(15.0));
    }

    [Fact]
    public void ComputeReferenceDatum_NegativeElevation()
    {
        // minElev=-5 => floor(-5/10) = floor(-0.5) = -1, (-1-1)*10 = -20
        Assert.Equal(-20.0, LongitudinalProfileComputer.ComputeReferenceDatum(-5.0));
    }

    [Fact]
    public void ComputeReferenceDatum_Zero()
    {
        // minElev=0 => floor(0/10) = 0, (0-1)*10 = -10
        Assert.Equal(-10.0, LongitudinalProfileComputer.ComputeReferenceDatum(0.0));
    }

    // --- ElevationToProfileY ---

    [Fact]
    public void ElevationToProfileY_DefaultExaggeration()
    {
        // elevation=360, datum=340, exaggeration=10 => (360-340)*10 = 200
        Assert.Equal(200.0, LongitudinalProfileComputer.ElevationToProfileY(360.0, 340.0));
    }

    [Fact]
    public void ElevationToProfileY_CustomExaggeration()
    {
        Assert.Equal(40.0, LongitudinalProfileComputer.ElevationToProfileY(360.0, 340.0, 2.0));
    }

    [Fact]
    public void ElevationToProfileY_AtDatum_ReturnsZero()
    {
        Assert.Equal(0.0, LongitudinalProfileComputer.ElevationToProfileY(340.0, 340.0));
    }

    // --- ProfileYToElevation ---

    [Fact]
    public void ProfileYToElevation_Roundtrip()
    {
        double elevation = 365.7;
        double datum = 340.0;
        double profileY = LongitudinalProfileComputer.ElevationToProfileY(elevation, datum);
        double recovered = LongitudinalProfileComputer.ProfileYToElevation(profileY, datum);
        Assert.Equal(elevation, recovered, 1e-10);
    }

    [Fact]
    public void ProfileYToElevation_ZeroY_ReturnsDatum()
    {
        Assert.Equal(340.0, LongitudinalProfileComputer.ProfileYToElevation(0.0, 340.0));
    }

    [Fact]
    public void ProfileYToElevation_ThrowsOnZeroExaggeration()
    {
        Assert.Throws<ArgumentException>(() =>
            LongitudinalProfileComputer.ProfileYToElevation(100.0, 340.0, 0.0));
    }

    // --- ComputeProfilePoints ---

    [Fact]
    public void ComputeProfilePoints_SimpleCase()
    {
        var chainages = new[] { 0.0, 10.0, 20.0 };
        var elevations = new[] { 350.0, 355.0, 360.0 };
        double datum = 340.0;

        var points = LongitudinalProfileComputer.ComputeProfilePoints(chainages, elevations, datum);

        Assert.Equal(3, points.Length);
        // X = chainage, Y = (elev - datum) * 10
        Assert.Equal(0.0, points[0].X, 1e-10);
        Assert.Equal(100.0, points[0].Y, 1e-10);  // (350-340)*10
        Assert.Equal(10.0, points[1].X, 1e-10);
        Assert.Equal(150.0, points[1].Y, 1e-10);  // (355-340)*10
        Assert.Equal(20.0, points[2].X, 1e-10);
        Assert.Equal(200.0, points[2].Y, 1e-10);  // (360-340)*10
    }

    [Fact]
    public void ComputeProfilePoints_ZIsAlwaysZero()
    {
        var chainages = new[] { 0.0, 50.0, 100.0 };
        var elevations = new[] { 400.0, 410.0, 420.0 };

        var points = LongitudinalProfileComputer.ComputeProfilePoints(chainages, elevations, 390.0);

        foreach (var pt in points)
            Assert.Equal(0.0, pt.Z, 1e-10);
    }

    [Fact]
    public void ComputeProfilePoints_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LongitudinalProfileComputer.ComputeProfilePoints(null!, new[] { 1.0 }, 0.0));
        Assert.Throws<ArgumentNullException>(() =>
            LongitudinalProfileComputer.ComputeProfilePoints(new[] { 1.0 }, null!, 0.0));
    }

    [Fact]
    public void ComputeProfilePoints_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            LongitudinalProfileComputer.ComputeProfilePoints(
                new[] { 0.0, 10.0 },
                new[] { 350.0 },
                340.0));
    }

    [Fact]
    public void ComputeProfilePoints_CustomExaggeration()
    {
        var chainages = new[] { 0.0, 10.0 };
        var elevations = new[] { 350.0, 360.0 };
        double datum = 340.0;

        var points = LongitudinalProfileComputer.ComputeProfilePoints(chainages, elevations, datum, 5.0);

        Assert.Equal(50.0, points[0].Y, 1e-10);  // (350-340)*5
        Assert.Equal(100.0, points[1].Y, 1e-10);  // (360-340)*5
    }

    // --- FindMinElevation ---

    [Fact]
    public void FindMinElevation_NormalArray()
    {
        Assert.Equal(340.0, LongitudinalProfileComputer.FindMinElevation(
            new[] { 360.0, 355.0, 340.0, 370.0 }));
    }

    [Fact]
    public void FindMinElevation_SingleElement()
    {
        Assert.Equal(500.0, LongitudinalProfileComputer.FindMinElevation(new[] { 500.0 }));
    }

    [Fact]
    public void FindMinElevation_AllEqual()
    {
        Assert.Equal(350.0, LongitudinalProfileComputer.FindMinElevation(
            new[] { 350.0, 350.0, 350.0 }));
    }

    [Fact]
    public void FindMinElevation_NegativeValues()
    {
        Assert.Equal(-20.0, LongitudinalProfileComputer.FindMinElevation(
            new[] { 10.0, -5.0, -20.0, 0.0 }));
    }

    [Fact]
    public void FindMinElevation_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            LongitudinalProfileComputer.FindMinElevation(Array.Empty<double>()));
    }

    [Fact]
    public void FindMinElevation_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() =>
            LongitudinalProfileComputer.FindMinElevation(null!));
    }

    [Fact]
    public void ComputeReferenceDatum_NegativeFractional_UsesFloorNotTruncate()
    {
        // Math.Floor(-0.5) = -1, so (-1-1)*10 = -20
        // VBScript would give -10 (truncates toward zero), but we intentionally use Floor
        Assert.Equal(-20.0, LongitudinalProfileComputer.ComputeReferenceDatum(-5.0));
    }

    [Fact]
    public void FindMinElevation_WithNaN_NaNIsNotMin()
    {
        // NaN comparisons return false, so NaN should not replace a valid min
        var elevations = new[] { 350.0, double.NaN, 340.0 };
        double min = LongitudinalProfileComputer.FindMinElevation(elevations);
        Assert.Equal(340.0, min);
    }

    // --- Integration: Full pipeline ---

    [Fact]
    public void FullPipeline_ComputeDatumAndProfilePoints()
    {
        var chainages = new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0 };
        var elevations = new[] { 357.0, 358.5, 360.0, 359.0, 361.0, 362.5 };

        double minElev = LongitudinalProfileComputer.FindMinElevation(elevations);
        Assert.Equal(357.0, minElev);

        double datum = LongitudinalProfileComputer.ComputeReferenceDatum(minElev);
        // floor(357/10) = 35, (35-1)*10 = 340
        Assert.Equal(340.0, datum);

        var points = LongitudinalProfileComputer.ComputeProfilePoints(chainages, elevations, datum);
        Assert.Equal(6, points.Length);

        // First point: chainage=0, Y=(357-340)*10=170
        Assert.Equal(0.0, points[0].X, 1e-10);
        Assert.Equal(170.0, points[0].Y, 1e-10);

        // Last point: chainage=10, Y=(362.5-340)*10=225
        Assert.Equal(10.0, points[5].X, 1e-10);
        Assert.Equal(225.0, points[5].Y, 1e-10);
    }
}
