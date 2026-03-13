using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Computes parabolic vertical curves for road grade transitions.
/// From RC2_Parabolickyoblouk_CZ.rvb / parabolickyoblouk.rvb.
///
/// Key formulas:
///   t = R / 200 * |s1 - s2|       (tangent length, half-length of parabola)
///   ymax = t^2 / (2 * R)           (maximum ordinate / offset from tangent)
///   y(x) = x^2 / (2 * R)           (parabolic offset at distance x from start)
///
/// Where:
///   R = vertical curve radius (meters)
///   s1, s2 = grades in percent (e.g., 3.5 for 3.5%)
///   x = distance from start of parabola (ZZ point)
///
/// Important points:
///   ZZ = start of parabolic curve (chainage = vertex_chainage - t)
///   KZ = end of parabolic curve (chainage = vertex_chainage + t)
///   V  = vertex (intersection of the two grade segments)
/// </summary>
public static class ParabolicVerticalCurve
{
    /// <summary>
    /// Compute the tangent length t (half-length of the parabolic curve).
    /// </summary>
    /// <param name="radius">R — vertical curve radius in meters.</param>
    /// <param name="grade1Percent">s1 — first grade in percent.</param>
    /// <param name="grade2Percent">s2 — second grade in percent.</param>
    public static double ComputeTangentLength(double radius, double grade1Percent, double grade2Percent)
    {
        ValidateRadius(radius);

        return radius / 200.0 * System.Math.Abs(grade1Percent - grade2Percent);
    }

    /// <summary>
    /// Compute the maximum ordinate ymax (peak offset from tangent line).
    /// </summary>
    public static double ComputeMaxOrdinate(double radius, double tangentLength)
    {
        ValidateRadius(radius);

        return tangentLength * tangentLength / (2.0 * radius);
    }

    /// <summary>
    /// Compute the parabolic offset y at distance x from the start of the curve (ZZ).
    /// </summary>
    /// <param name="x">Distance from the start of the curve (0 to 2t).</param>
    /// <param name="radius">R — vertical curve radius in meters.</param>
    public static double ComputeOffset(double x, double radius)
    {
        ValidateRadius(radius);

        return x * x / (2.0 * radius);
    }

    /// <summary>
    /// Determine whether the vertical curve is a sag (concave up) or crest (convex up).
    /// Sag: grade increases (s1 &lt; s2). Crest: grade decreases (s1 &gt; s2).
    /// </summary>
    /// <returns>True if sag curve, false if crest curve.</returns>
    public static bool IsSagCurve(double grade1Percent, double grade2Percent)
    {
        return grade1Percent < grade2Percent;
    }

    /// <summary>
    /// Compute parabolic vertical curve points in profile coordinates.
    /// X axis = chainage (relative to ZZ), Y axis = elevation offset from tangent.
    /// The offset direction is positive for sag curves, negative for crest curves.
    /// </summary>
    /// <param name="radius">R — vertical curve radius in meters.</param>
    /// <param name="grade1Percent">s1 — first grade in percent.</param>
    /// <param name="grade2Percent">s2 — second grade in percent.</param>
    /// <param name="vertexChainage">Chainage of the vertex (grade intersection point).</param>
    /// <param name="vertexElevation">Elevation at the vertex.</param>
    /// <returns>Array of profile points (X = chainage, Y = elevation, Z = 0).</returns>
    public static Point3[] ComputeProfilePoints(
        double radius,
        double grade1Percent,
        double grade2Percent,
        double vertexChainage,
        double vertexElevation)
    {
        ValidateRadius(radius);

        double t = ComputeTangentLength(radius, grade1Percent, grade2Percent);
        if (t < 1e-10)
            return new[] { new Point3(vertexChainage, vertexElevation, 0) };

        bool isSag = IsSagCurve(grade1Percent, grade2Percent);
        double sign = isSag ? 1.0 : -1.0;

        // ZZ and KZ chainages
        double zzChainage = vertexChainage - t;
        double zzElevation = vertexElevation - (grade1Percent / 100.0) * t;

        // Sample points at 1m intervals along the tangent, matching original VBScript
        double fullLength = 2.0 * t;
        int intSamples = (int)fullLength + 1;
        bool needsKzEndpoint = fullLength - (intSamples - 1) > 1e-10;
        var points = new Point3[intSamples + (needsKzEndpoint ? 1 : 0)];

        for (int i = 0; i < intSamples; i++)
        {
            double x = i; // distance from ZZ
            double chainage = zzChainage + x;

            // Elevation on the first tangent at this chainage
            double tangentElevation = zzElevation + (grade1Percent / 100.0) * x;

            // Parabolic offset
            double offset = ComputeOffset(x, radius);

            // Apply offset (sag = up from tangent, crest = down from tangent)
            double elevation = tangentElevation + sign * offset;

            points[i] = new Point3(chainage, elevation, 0);
        }

        // Append exact KZ endpoint if 2t is not an integer
        if (needsKzEndpoint)
        {
            double x = fullLength;
            double chainage = zzChainage + x;
            double tangentElevation = zzElevation + (grade1Percent / 100.0) * x;
            double offset = ComputeOffset(x, radius);
            double elevation = tangentElevation + sign * offset;
            points[^1] = new Point3(chainage, elevation, 0);
        }

        return points;
    }

    /// <summary>
    /// Compute the three important stationing points: ZZ (start), V (vertex), KZ (end).
    /// </summary>
    public static (Point3 ZZ, Point3 V, Point3 KZ) ComputeImportantPoints(
        double radius,
        double grade1Percent,
        double grade2Percent,
        double vertexChainage,
        double vertexElevation)
    {
        ValidateRadius(radius);

        double t = ComputeTangentLength(radius, grade1Percent, grade2Percent);

        double zzChainage = vertexChainage - t;
        double zzElevation = vertexElevation - (grade1Percent / 100.0) * t;

        double kzChainage = vertexChainage + t;
        double kzElevation = vertexElevation + (grade2Percent / 100.0) * t;

        var zz = new Point3(zzChainage, zzElevation, 0);
        var v = new Point3(vertexChainage, vertexElevation, 0);
        var kz = new Point3(kzChainage, kzElevation, 0);

        return (zz, v, kz);
    }

    /// <summary>
    /// Compute the grade (slope in percent) of a segment given two elevation points.
    /// </summary>
    /// <param name="chainage1">Start chainage.</param>
    /// <param name="elevation1">Start elevation.</param>
    /// <param name="chainage2">End chainage.</param>
    /// <param name="elevation2">End elevation.</param>
    public static double ComputeGrade(double chainage1, double elevation1, double chainage2, double elevation2)
    {
        double dx = chainage2 - chainage1;
        if (System.Math.Abs(dx) < 1e-10)
            throw new ArgumentException("Chainage difference is too small to compute grade.");

        return (elevation2 - elevation1) / dx * 100.0;
    }

    private static void ValidateRadius(double radius)
    {
        if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius))
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be a finite positive number.");
    }
}
