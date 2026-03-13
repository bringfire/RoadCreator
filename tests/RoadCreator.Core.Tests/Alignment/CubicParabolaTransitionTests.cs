using RoadCreator.Core.Alignment;
using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Alignment;

public class CubicParabolaTransitionTests
{
    [Fact]
    public void ComputePoints_Returns16Points()
    {
        var points = CubicParabolaTransition.ComputePoints(60, 200);
        Assert.Equal(16, points.Length);
    }

    [Fact]
    public void ComputePoints_FirstPointIsOrigin()
    {
        var points = CubicParabolaTransition.ComputePoints(60, 200);
        Assert.Equal(0, points[0].X);
        Assert.Equal(0, points[0].Y);
        Assert.Equal(0, points[0].Z);
    }

    [Fact]
    public void ComputePoints_LastPointXEqualsL()
    {
        double L = 60;
        var points = CubicParabolaTransition.ComputePoints(L, 200);

        Assert.Equal(L, points[^1].X, 1e-10);
    }

    [Fact]
    public void ComputePoints_YValuesAreNonNegativeAndIncreasing()
    {
        var points = CubicParabolaTransition.ComputePoints(60, 200);

        for (int i = 1; i < points.Length; i++)
        {
            Assert.True(points[i].Y >= 0, $"Y[{i}] = {points[i].Y} should be >= 0");
            Assert.True(points[i].Y >= points[i - 1].Y, $"Y[{i}] should be >= Y[{i - 1}]");
        }
    }

    [Fact]
    public void ComputePoints_XValuesAreEvenlySpaced()
    {
        double L = 60;
        var points = CubicParabolaTransition.ComputePoints(L, 200);
        double expectedSpacing = L / 15.0;

        for (int i = 0; i < points.Length; i++)
        {
            Assert.Equal(expectedSpacing * i, points[i].X, 1e-10);
        }
    }

    [Fact]
    public void ComputePoints_YFollowsCubicLaw()
    {
        double L = 60;
        double R = 200;
        var points = CubicParabolaTransition.ComputePoints(L, R);

        double thetaT = System.Math.Asin(L / (2.0 * R));
        double corrFactor = 1.0 / System.Math.Cos(thetaT);

        double x = points[^1].X;
        double expectedY = corrFactor * x * x * x / (6.0 * R * L);
        Assert.Equal(expectedY, points[^1].Y, 1e-10);
    }

    [Fact]
    public void ComputePoints_AllZValuesAreZero()
    {
        var points = CubicParabolaTransition.ComputePoints(60, 200);
        foreach (var pt in points)
            Assert.Equal(0, pt.Z);
    }

    [Theory]
    [InlineData(60, 200)]
    [InlineData(80, 300)]
    [InlineData(40, 100)]
    public void ComputeShift_IsSmall(double L, double R)
    {
        double m = CubicParabolaTransition.ComputeShift(L, R);
        Assert.True(System.Math.Abs(m) < R * 0.1, $"Shift |m|={System.Math.Abs(m)} should be small relative to R={R}");
    }

    [Fact]
    public void ComputeShift_KnownValues()
    {
        // For L=60, R=200:
        // thetaT = asin(0.15) ≈ 0.15057 rad
        // k = (60/3) * tan(0.15057/2) ≈ 20 * 0.07537 ≈ 1.5074
        // m = 1.5074 - 200*(1 - cos(0.15057)) ≈ 1.5074 - 2.262 ≈ -0.754
        double m = CubicParabolaTransition.ComputeShift(60, 200);
        Assert.InRange(m, -1.0, -0.5);
    }

    [Fact]
    public void ComputeXs_ReturnsHalfL()
    {
        Assert.Equal(30.0, CubicParabolaTransition.ComputeXs(60));
        Assert.Equal(50.0, CubicParabolaTransition.ComputeXs(100));
        Assert.Equal(20.0, CubicParabolaTransition.ComputeXs(40));
    }

    [Fact]
    public void ComputeLargeTangent_IsPositive()
    {
        double T = CubicParabolaTransition.ComputeLargeTangent(60, 200, 30);
        Assert.True(T > 0, $"T={T} should be positive");
    }

    [Fact]
    public void ComputeLargeTangent_IncreasesWithDeflectionAngle()
    {
        double T1 = CubicParabolaTransition.ComputeLargeTangent(60, 200, 20);
        double T2 = CubicParabolaTransition.ComputeLargeTangent(60, 200, 40);
        Assert.True(T2 > T1, $"T at 40deg ({T2}) should be > T at 20deg ({T1})");
    }

    [Fact]
    public void ComputeLargeTangent_IncreasesWithRadius()
    {
        double T1 = CubicParabolaTransition.ComputeLargeTangent(60, 100, 30);
        double T2 = CubicParabolaTransition.ComputeLargeTangent(60, 300, 30);
        Assert.True(T2 > T1, $"T at R=300 ({T2}) should be > T at R=100 ({T1})");
    }

    [Fact]
    public void MirrorY_NegatesYCoordinates()
    {
        var original = new Point3[] { new(0, 0, 0), new(10, 3, 0), new(20, 8, 0) };
        var mirrored = CubicParabolaTransition.MirrorY(original);

        Assert.Equal(original.Length, mirrored.Length);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i].X, mirrored[i].X);
            Assert.Equal(-original[i].Y, mirrored[i].Y);
            Assert.Equal(original[i].Z, mirrored[i].Z);
        }
    }

    [Fact]
    public void MirrorY_DoesNotModifyOriginal()
    {
        var original = new Point3[] { new(10, 5, 0), new(20, 12, 0) };
        var origY = original[0].Y;

        CubicParabolaTransition.MirrorY(original);

        Assert.Equal(origY, original[0].Y);
    }

    // C2/S4: Domain validation — L/(2R) >= 1 must throw, not produce NaN
    [Fact]
    public void ComputePoints_ThrowsWhenLOverTwoRGreaterOrEqualToOne()
    {
        // L=200, R=80 => L/(2R) = 1.25 > 1
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputePoints(200, 80));
    }

    [Fact]
    public void ComputeShift_ThrowsWhenLOverTwoRGreaterOrEqualToOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputeShift(200, 80));
    }

    [Fact]
    public void ComputeLargeTangent_ThrowsWhenLOverTwoRGreaterOrEqualToOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputeLargeTangent(200, 80, 30));
    }

    // C1: Input validation — negative/zero values
    [Theory]
    [InlineData(0, 200)]
    [InlineData(-10, 200)]
    [InlineData(60, 0)]
    [InlineData(60, -100)]
    public void ComputePoints_ThrowsOnInvalidInput(double L, double R)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputePoints(L, R));
    }

    [Fact]
    public void ComputeXs_ThrowsOnZeroL()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputeXs(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CubicParabolaTransition.ComputeXs(-5));
    }

    [Theory]
    [InlineData(60, 200)]
    [InlineData(80, 300)]
    public void ClothoidAndParabola_ShiftAbsValuesAreSameOrder(double L, double R)
    {
        double clothoidM = ClothoidTransition.ComputeShift(L, R);
        double parabolaM = CubicParabolaTransition.ComputeShift(L, R);

        double absRatio = System.Math.Abs(parabolaM) / System.Math.Abs(clothoidM);
        Assert.InRange(absRatio, 0.1, 10.0);
    }
}
