using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Computes cubic parabola transition curve points.
/// From PrechodniceKubParabola.rvb.
///
/// Cubic parabola equation (16 sample points):
///   corrFactor = 1 / cos(asin(L / (2*R)))
///   Y = corrFactor * X^3 / (6 * R * L)
///
/// Parameters:
///   Xs = L / 2
///   thetaT = asin(L / (2*R))
///   k = (L/3) * tan(thetaT/2)
///   m = k - R * (1 - cos(thetaT))
///   T = (R + m) * tan(alpha/2) + Xs
/// </summary>
public static class CubicParabolaTransition
{
    private const int SampleCount = 16; // 0..15 inclusive

    /// <summary>
    /// Compute cubic parabola transition curve points in local coordinates.
    /// Origin is at the transition start point, X-axis along the tangent direction.
    /// </summary>
    public static Point3[] ComputePoints(double transitionLength, double radius)
    {
        TransitionUtils.ValidatePositive(transitionLength, nameof(transitionLength));
        TransitionUtils.ValidatePositive(radius, nameof(radius));
        ValidateDomain(transitionLength, radius);

        double L = transitionLength;
        double R = radius;
        double segLength = L / (SampleCount - 1);

        double thetaT = System.Math.Asin(L / (2.0 * R));
        double corrFactor = 1.0 / System.Math.Cos(thetaT);

        var points = new Point3[SampleCount];

        for (int i = 0; i < SampleCount; i++)
        {
            double x = segLength * i;
            double y = corrFactor * (x * x * x) / (6.0 * R * L);
            points[i] = new Point3(x, y, 0);
        }

        return points;
    }

    /// <summary>
    /// Compute the shift parameter m for cubic parabola.
    /// </summary>
    public static double ComputeShift(double L, double R)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        TransitionUtils.ValidatePositive(R, nameof(R));
        ValidateDomain(L, R);

        double thetaT = System.Math.Asin(L / (2.0 * R));
        double k = (L / 3.0) * System.Math.Tan(thetaT / 2.0);
        double m = k - R * (1.0 - System.Math.Cos(thetaT));
        return m;
    }

    /// <summary>
    /// Compute Xs parameter (always L/2 for cubic parabola).
    /// </summary>
    public static double ComputeXs(double L)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        return L / 2.0;
    }

    /// <summary>
    /// Compute the large tangent T.
    /// </summary>
    public static double ComputeLargeTangent(double L, double R, double deflectionAngleDeg)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        TransitionUtils.ValidatePositive(R, nameof(R));
        ValidateDomain(L, R);

        double m = ComputeShift(L, R);
        double Xs = ComputeXs(L);
        double halfAlpha = AngleUtils.ToRadians(deflectionAngleDeg / 2.0);
        double T = (R + m) * System.Math.Tan(halfAlpha) + Xs;
        return T;
    }

    /// <summary>
    /// Mirror a set of points about the X-axis (negate Y) for right-turn curves.
    /// </summary>
    public static Point3[] MirrorY(Point3[] points) => TransitionUtils.MirrorY(points);

    private static void ValidateDomain(double L, double R)
    {
        if (L / (2.0 * R) >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(L),
                $"Transition length L ({L}) must be less than 2*R ({2 * R}) for cubic parabola (asin domain).");
    }
}
