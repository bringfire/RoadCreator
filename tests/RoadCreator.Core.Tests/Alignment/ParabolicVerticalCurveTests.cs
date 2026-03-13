using RoadCreator.Core.Alignment;
using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Alignment;

public class ParabolicVerticalCurveTests
{
    // --- ComputeTangentLength ---

    [Fact]
    public void ComputeTangentLength_KnownValues()
    {
        // t = R / 200 * |s1 - s2|
        // R=2000, s1=3, s2=-2 => t = 2000/200 * 5 = 50
        double t = ParabolicVerticalCurve.ComputeTangentLength(2000, 3.0, -2.0);
        Assert.Equal(50.0, t, 1e-10);
    }

    [Fact]
    public void ComputeTangentLength_SymmetricGrades()
    {
        // |3 - (-3)| = 6, |(-3) - 3| = 6 => same result
        double t1 = ParabolicVerticalCurve.ComputeTangentLength(1000, 3.0, -3.0);
        double t2 = ParabolicVerticalCurve.ComputeTangentLength(1000, -3.0, 3.0);
        Assert.Equal(t1, t2, 1e-10);
    }

    [Fact]
    public void ComputeTangentLength_ZeroGradeDifference()
    {
        double t = ParabolicVerticalCurve.ComputeTangentLength(5000, 2.0, 2.0);
        Assert.Equal(0.0, t, 1e-10);
    }

    [Fact]
    public void ComputeTangentLength_ThrowsOnNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeTangentLength(-100, 3.0, -2.0));
    }

    [Fact]
    public void ComputeTangentLength_ThrowsOnZeroRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeTangentLength(0, 3.0, -2.0));
    }

    // --- ComputeMaxOrdinate ---

    [Fact]
    public void ComputeMaxOrdinate_KnownValues()
    {
        // ymax = t^2 / (2R) = 50^2 / (2*2000) = 2500/4000 = 0.625
        double ymax = ParabolicVerticalCurve.ComputeMaxOrdinate(2000, 50);
        Assert.Equal(0.625, ymax, 1e-10);
    }

    [Fact]
    public void ComputeMaxOrdinate_ZeroTangent()
    {
        double ymax = ParabolicVerticalCurve.ComputeMaxOrdinate(1000, 0);
        Assert.Equal(0.0, ymax, 1e-10);
    }

    [Fact]
    public void ComputeMaxOrdinate_ThrowsOnNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeMaxOrdinate(-500, 25));
    }

    // --- ComputeOffset ---

    [Fact]
    public void ComputeOffset_AtZero()
    {
        double y = ParabolicVerticalCurve.ComputeOffset(0, 2000);
        Assert.Equal(0.0, y, 1e-10);
    }

    [Fact]
    public void ComputeOffset_KnownValues()
    {
        // y = x^2 / (2R) = 100^2 / (2*2000) = 10000/4000 = 2.5
        double y = ParabolicVerticalCurve.ComputeOffset(100, 2000);
        Assert.Equal(2.5, y, 1e-10);
    }

    [Fact]
    public void ComputeOffset_Increases()
    {
        double y1 = ParabolicVerticalCurve.ComputeOffset(10, 1000);
        double y2 = ParabolicVerticalCurve.ComputeOffset(20, 1000);
        Assert.True(y2 > y1);
    }

    [Fact]
    public void ComputeOffset_ThrowsOnZeroRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeOffset(10, 0));
    }

    [Fact]
    public void ComputeOffset_NegativeX_ReturnsPositive()
    {
        // x^2 is always positive, so negative x produces the same offset as positive x
        double yPos = ParabolicVerticalCurve.ComputeOffset(10, 2000);
        double yNeg = ParabolicVerticalCurve.ComputeOffset(-10, 2000);
        Assert.Equal(yPos, yNeg, 1e-10);
    }

    [Fact]
    public void ComputeTangentLength_ThrowsOnNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeTangentLength(double.NaN, 3.0, -2.0));
    }

    [Fact]
    public void ComputeOffset_ThrowsOnInfinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeOffset(10, double.PositiveInfinity));
    }

    // --- IsSagCurve ---

    [Fact]
    public void IsSagCurve_IncreasingGrade()
    {
        // s1 < s2 => sag
        Assert.True(ParabolicVerticalCurve.IsSagCurve(-2.0, 3.0));
    }

    [Fact]
    public void IsSagCurve_DecreasingGrade()
    {
        // s1 > s2 => crest
        Assert.False(ParabolicVerticalCurve.IsSagCurve(3.0, -2.0));
    }

    [Fact]
    public void IsSagCurve_EqualGrades()
    {
        // s1 == s2 => not sag
        Assert.False(ParabolicVerticalCurve.IsSagCurve(2.0, 2.0));
    }

    // --- ComputeProfilePoints ---

    [Fact]
    public void ComputeProfilePoints_ReturnsCorrectCount()
    {
        // t = 2000/200 * |3 - (-2)| = 50
        // sampleCount = 2*50 + 1 = 101
        var points = ParabolicVerticalCurve.ComputeProfilePoints(2000, 3.0, -2.0, 500, 200);
        Assert.Equal(101, points.Length);
    }

    [Fact]
    public void ComputeProfilePoints_FirstPointAtZZ()
    {
        double R = 2000, s1 = 3.0, s2 = -2.0;
        double vCh = 500, vEl = 200;
        double t = ParabolicVerticalCurve.ComputeTangentLength(R, s1, s2); // 50

        var points = ParabolicVerticalCurve.ComputeProfilePoints(R, s1, s2, vCh, vEl);

        // ZZ chainage = 500 - 50 = 450
        Assert.Equal(450.0, points[0].X, 1e-10);
    }

    [Fact]
    public void ComputeProfilePoints_LastPointNearKZ()
    {
        double R = 2000, s1 = 3.0, s2 = -2.0;
        double vCh = 500, vEl = 200;
        double t = ParabolicVerticalCurve.ComputeTangentLength(R, s1, s2); // 50

        var points = ParabolicVerticalCurve.ComputeProfilePoints(R, s1, s2, vCh, vEl);

        // KZ chainage = 500 + 50 = 550
        // Last sample at x = 100 => chainage = 450 + 100 = 550
        Assert.Equal(550.0, points[^1].X, 1e-10);
    }

    [Fact]
    public void ComputeProfilePoints_ZeroGradeDifference_SinglePoint()
    {
        var points = ParabolicVerticalCurve.ComputeProfilePoints(2000, 2.0, 2.0, 500, 200);
        Assert.Single(points);
        Assert.Equal(500, points[0].X, 1e-10);
        Assert.Equal(200, points[0].Y, 1e-10);
    }

    [Fact]
    public void ComputeProfilePoints_CrestCurveOffsetIsDown()
    {
        // Crest: s1 > s2 => offset is negative (curve below tangent)
        var points = ParabolicVerticalCurve.ComputeProfilePoints(2000, 3.0, -2.0, 500, 200);

        // At midpoint (x = t = 50), the tangent elevation and parabola elevation:
        // tangent at x=50: zzElev + s1/100 * 50
        // parabola: tangent - offset (crest)
        // The midpoint should be below the tangent line
        int mid = points.Length / 2;
        double zzElev = 200 - (3.0 / 100.0) * 50; // 200 - 1.5 = 198.5
        double tangentAtMid = zzElev + (3.0 / 100.0) * 50; // 198.5 + 1.5 = 200
        Assert.True(points[mid].Y < tangentAtMid);
    }

    [Fact]
    public void ComputeProfilePoints_SagCurveOffsetIsUp()
    {
        // Sag: s1 < s2 => offset is positive (curve above tangent)
        var points = ParabolicVerticalCurve.ComputeProfilePoints(2000, -2.0, 3.0, 500, 200);

        int mid = points.Length / 2;
        double zzElev = 200 - (-2.0 / 100.0) * 50; // 200 + 1.0 = 201
        double tangentAtMid = zzElev + (-2.0 / 100.0) * 50; // 201 - 1.0 = 200
        Assert.True(points[mid].Y > tangentAtMid);
    }

    [Fact]
    public void ComputeProfilePoints_IncludesKzEndpointForNonIntegerLength()
    {
        // t = 1500/200 * |2.5 - (-1.0)| = 7.5 * 3.5 = 26.25
        // fullLength = 52.5, intSamples = 53, needsKzEndpoint = true => 54 points
        var points = ParabolicVerticalCurve.ComputeProfilePoints(1500, 2.5, -1.0, 300, 150);
        double t = ParabolicVerticalCurve.ComputeTangentLength(1500, 2.5, -1.0);
        double zzChainage = 300 - t;
        double kzChainage = 300 + t;

        Assert.Equal(54, points.Length);
        Assert.Equal(zzChainage, points[0].X, 1e-10);
        Assert.Equal(kzChainage, points[^1].X, 1e-10);
    }

    [Fact]
    public void ComputeProfilePoints_AllZAreZero()
    {
        var points = ParabolicVerticalCurve.ComputeProfilePoints(2000, 3.0, -2.0, 500, 200);
        foreach (var pt in points)
            Assert.Equal(0, pt.Z);
    }

    [Fact]
    public void ComputeProfilePoints_ThrowsOnNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeProfilePoints(-1000, 3.0, -2.0, 500, 200));
    }

    // --- ComputeImportantPoints ---

    [Fact]
    public void ComputeImportantPoints_KnownValues()
    {
        double R = 2000, s1 = 3.0, s2 = -2.0;
        double vCh = 500, vEl = 200;

        var (zz, v, kz) = ParabolicVerticalCurve.ComputeImportantPoints(R, s1, s2, vCh, vEl);

        Assert.Equal(450.0, zz.X, 1e-10);
        Assert.Equal(500.0, v.X, 1e-10);
        Assert.Equal(550.0, kz.X, 1e-10);
        Assert.Equal(vEl, v.Y, 1e-10);

        // ZZ elevation = vertex - s1/100 * t = 200 - 0.03 * 50 = 198.5
        Assert.Equal(198.5, zz.Y, 1e-10);

        // KZ elevation = vertex + s2/100 * t = 200 + (-0.02) * 50 = 199
        Assert.Equal(199.0, kz.Y, 1e-10);
    }

    [Fact]
    public void ComputeImportantPoints_ThrowsOnZeroRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicVerticalCurve.ComputeImportantPoints(0, 3.0, -2.0, 500, 200));
    }

    // --- ComputeGrade ---

    [Fact]
    public void ComputeGrade_Flat()
    {
        double grade = ParabolicVerticalCurve.ComputeGrade(0, 100, 100, 100);
        Assert.Equal(0.0, grade, 1e-10);
    }

    [Fact]
    public void ComputeGrade_Uphill()
    {
        // Rise 3m over 100m = 3%
        double grade = ParabolicVerticalCurve.ComputeGrade(0, 100, 100, 103);
        Assert.Equal(3.0, grade, 1e-10);
    }

    [Fact]
    public void ComputeGrade_Downhill()
    {
        // Fall 2m over 100m = -2%
        double grade = ParabolicVerticalCurve.ComputeGrade(0, 100, 100, 98);
        Assert.Equal(-2.0, grade, 1e-10);
    }

    [Fact]
    public void ComputeGrade_ThrowsOnZeroDx()
    {
        Assert.Throws<ArgumentException>(() =>
            ParabolicVerticalCurve.ComputeGrade(50, 100, 50, 105));
    }
}
