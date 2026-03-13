using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Computes clothoid (Euler spiral) transition curve points.
/// 5-term parametric expansion from RC2_PrechodniceKlotoida_CZ.rvb.
///
/// Clothoid equations (26 sample points):
///   A = L * R  (clothoid parameter)
///   For each sample point at distance l along curve:
///     r = A / l  (local radius)
///     X = l - l^3/(40*r^2) + l^5/(3456*r^4)
///     Y = l^2/(6*r) - l^4/(336*r^3) + l^6/(42240*r^5)
///
/// Large tangent parameters:
///   Xs = L/2 - L^3/(240*R^2)
///   m  = L^2/(24*R) - L^4/(2688*R^3)
///   T  = (R + m) * tan(alpha/2) + Xs
/// </summary>
public static class ClothoidTransition
{
    private const int SampleCount = 26; // 0..25 inclusive

    /// <summary>
    /// Compute clothoid transition curve points in local coordinates.
    /// Origin is at the transition start point, X-axis along the tangent direction.
    /// </summary>
    /// <param name="transitionLength">L - total transition length in meters.</param>
    /// <param name="radius">R - radius of the connecting circular arc.</param>
    /// <returns>Array of 26 points defining the clothoid curve.</returns>
    public static Point3[] ComputePoints(double transitionLength, double radius)
    {
        TransitionUtils.ValidatePositive(transitionLength, nameof(transitionLength));
        TransitionUtils.ValidatePositive(radius, nameof(radius));

        double L = transitionLength;
        double R = radius;
        double A = L * R;
        double segLength = L / (SampleCount - 1);

        var points = new Point3[SampleCount];
        points[0] = Point3.Origin;

        for (int i = 1; i < SampleCount; i++)
        {
            double l = segLength * i;
            double r = A / l;
            double r2 = r * r;
            double r3 = r2 * r;
            double r4 = r2 * r2;
            double r5 = r4 * r;
            double l2 = l * l;
            double l3 = l2 * l;
            double l4 = l2 * l2;
            double l5 = l4 * l;
            double l6 = l3 * l3;

            double x = l - l3 / (40.0 * r2) + l5 / (3456.0 * r4);
            double y = l2 / (6.0 * r) - l4 / (336.0 * r3) + l6 / (42240.0 * r5);

            points[i] = new Point3(x, y, 0);
        }

        return points;
    }

    /// <summary>
    /// Compute the shift parameter m.
    /// </summary>
    public static double ComputeShift(double L, double R)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        TransitionUtils.ValidatePositive(R, nameof(R));

        double L2 = L * L;
        double L4 = L2 * L2;
        double R3 = R * R * R;
        return L2 / (24.0 * R) - L4 / (2688.0 * R3);
    }

    /// <summary>
    /// Compute the Xs parameter (projection of transition endpoint onto tangent).
    /// </summary>
    public static double ComputeXs(double L, double R)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        TransitionUtils.ValidatePositive(R, nameof(R));

        double L3 = L * L * L;
        return L / 2.0 - L3 / (240.0 * R * R);
    }

    /// <summary>
    /// Compute the large tangent T (distance from vertex to transition start point).
    /// </summary>
    /// <param name="L">Transition length.</param>
    /// <param name="R">Arc radius.</param>
    /// <param name="deflectionAngleDeg">Deflection angle alpha between tangents in degrees.</param>
    public static double ComputeLargeTangent(double L, double R, double deflectionAngleDeg)
    {
        TransitionUtils.ValidatePositive(L, nameof(L));
        TransitionUtils.ValidatePositive(R, nameof(R));

        double m = ComputeShift(L, R);
        double Xs = ComputeXs(L, R);
        double halfAlpha = AngleUtils.ToRadians(deflectionAngleDeg / 2.0);
        double T = (R + m) * System.Math.Tan(halfAlpha) + Xs;
        return T;
    }

    /// <summary>
    /// Mirror a set of points about the X-axis (negate Y) for right-turn curves.
    /// </summary>
    public static Point3[] MirrorY(Point3[] points) => TransitionUtils.MirrorY(points);
}
