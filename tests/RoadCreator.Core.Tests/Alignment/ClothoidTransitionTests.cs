using RoadCreator.Core.Alignment;
using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Alignment;

public class ClothoidTransitionTests
{
    [Fact]
    public void ComputePoints_Returns26Points()
    {
        var points = ClothoidTransition.ComputePoints(70, 150);
        Assert.Equal(26, points.Length);
    }

    [Fact]
    public void ComputePoints_FirstPointIsOrigin()
    {
        var points = ClothoidTransition.ComputePoints(70, 150);
        Assert.Equal(0, points[0].X);
        Assert.Equal(0, points[0].Y);
        Assert.Equal(0, points[0].Z);
    }

    [Fact]
    public void ComputePoints_LastPointXApproximatesL()
    {
        double L = 70;
        double R = 150;
        var points = ClothoidTransition.ComputePoints(L, R);

        Assert.True(points[^1].X > L * 0.95, $"Last X={points[^1].X} should be close to L={L}");
        Assert.True(points[^1].X <= L, $"Last X={points[^1].X} should not exceed L={L}");
    }

    [Fact]
    public void ComputePoints_YValuesArePositiveAndIncreasing()
    {
        var points = ClothoidTransition.ComputePoints(70, 150);

        for (int i = 1; i < points.Length; i++)
        {
            Assert.True(points[i].Y >= 0, $"Y[{i}] = {points[i].Y} should be >= 0");
            Assert.True(points[i].Y > points[i - 1].Y, $"Y[{i}] = {points[i].Y} should be > Y[{i - 1}] = {points[i - 1].Y}");
        }
    }

    [Fact]
    public void ComputePoints_XValuesAreIncreasing()
    {
        var points = ClothoidTransition.ComputePoints(70, 150);

        for (int i = 1; i < points.Length; i++)
        {
            Assert.True(points[i].X > points[i - 1].X, $"X[{i}] = {points[i].X} should be > X[{i - 1}] = {points[i - 1].X}");
        }
    }

    [Fact]
    public void ComputePoints_AllZValuesAreZero()
    {
        var points = ClothoidTransition.ComputePoints(70, 150);
        foreach (var pt in points)
            Assert.Equal(0, pt.Z);
    }

    [Theory]
    [InlineData(70, 150)]
    [InlineData(100, 300)]
    [InlineData(40, 80)]
    public void ComputeShift_IsPositive(double L, double R)
    {
        double m = ClothoidTransition.ComputeShift(L, R);
        Assert.True(m > 0, $"Shift m={m} should be positive for L={L}, R={R}");
    }

    [Fact]
    public void ComputeShift_KnownValues()
    {
        // m = L^2/(24*R) - L^4/(2688*R^3)
        // For L=70, R=150: m ≈ 1.3587
        double m = ClothoidTransition.ComputeShift(70, 150);
        Assert.InRange(m, 1.35, 1.37);
    }

    [Fact]
    public void ComputeXs_KnownValues()
    {
        // Xs = L/2 - L^3/(240*R^2)
        // For L=70, R=150: Xs ≈ 34.9365
        double Xs = ClothoidTransition.ComputeXs(70, 150);
        Assert.InRange(Xs, 34.9, 35.0);
    }

    [Theory]
    [InlineData(70, 150)]
    [InlineData(100, 300)]
    public void ComputeXs_IsLessThanHalfL(double L, double R)
    {
        double Xs = ClothoidTransition.ComputeXs(L, R);
        Assert.True(Xs < L / 2.0, $"Xs={Xs} should be less than L/2={L / 2.0}");
        Assert.True(Xs > 0, $"Xs={Xs} should be positive");
    }

    [Fact]
    public void ComputeLargeTangent_IsPositive()
    {
        double T = ClothoidTransition.ComputeLargeTangent(70, 150, 30);
        Assert.True(T > 0, $"T={T} should be positive");
    }

    [Fact]
    public void ComputeLargeTangent_IncreasesWithDeflectionAngle()
    {
        double T1 = ClothoidTransition.ComputeLargeTangent(70, 150, 20);
        double T2 = ClothoidTransition.ComputeLargeTangent(70, 150, 40);
        Assert.True(T2 > T1, $"T at 40deg ({T2}) should be > T at 20deg ({T1})");
    }

    [Fact]
    public void ComputeLargeTangent_IncreasesWithRadius()
    {
        double T1 = ClothoidTransition.ComputeLargeTangent(70, 100, 30);
        double T2 = ClothoidTransition.ComputeLargeTangent(70, 200, 30);
        Assert.True(T2 > T1, $"T at R=200 ({T2}) should be > T at R=100 ({T1})");
    }

    [Fact]
    public void MirrorY_NegatesYCoordinates()
    {
        var original = new Point3[] { new(0, 0, 0), new(10, 5, 0), new(20, 12, 0) };
        var mirrored = ClothoidTransition.MirrorY(original);

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

        ClothoidTransition.MirrorY(original);

        Assert.Equal(origY, original[0].Y);
    }

    [Theory]
    [InlineData(50, 100)]
    [InlineData(70, 150)]
    [InlineData(100, 200)]
    [InlineData(120, 500)]
    public void ComputePoints_EndpointYMatchesRadiusGeometry(double L, double R)
    {
        var points = ClothoidTransition.ComputePoints(L, R);
        double lastY = points[^1].Y;
        double approxY = (L * L) / (6.0 * R);
        Assert.InRange(lastY, approxY * 0.8, approxY * 1.2);
    }

    // C1: Input validation tests
    [Theory]
    [InlineData(0, 150)]
    [InlineData(-10, 150)]
    [InlineData(70, 0)]
    [InlineData(70, -100)]
    public void ComputePoints_ThrowsOnInvalidInput(double L, double R)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothoidTransition.ComputePoints(L, R));
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(70, -1)]
    public void ComputeShift_ThrowsOnInvalidInput(double L, double R)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothoidTransition.ComputeShift(L, R));
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(70, -1)]
    public void ComputeXs_ThrowsOnInvalidInput(double L, double R)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothoidTransition.ComputeXs(L, R));
    }

    [Fact]
    public void ComputeLargeTangent_ThrowsOnInvalidInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothoidTransition.ComputeLargeTangent(0, 150, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClothoidTransition.ComputeLargeTangent(70, -1, 30));
    }

    // S7: Large L/R ratio test — series expansion loses accuracy
    [Fact]
    public void ComputePoints_LargeLRRatio_StillProducesMonotonicCurve()
    {
        // L/R = 0.8 is near the practical limit of the 5-term expansion
        var points = ClothoidTransition.ComputePoints(160, 200);

        // X values should still be monotonically increasing
        for (int i = 1; i < points.Length; i++)
            Assert.True(points[i].X > points[i - 1].X, $"X[{i}] should be > X[{i-1}] for large L/R");

        // Y values should still be monotonically increasing
        for (int i = 1; i < points.Length; i++)
            Assert.True(points[i].Y > points[i - 1].Y, $"Y[{i}] should be > Y[{i-1}] for large L/R");
    }
}
