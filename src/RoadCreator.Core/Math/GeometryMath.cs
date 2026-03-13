namespace RoadCreator.Core.Math;

/// <summary>
/// Common geometry operations used across road design algorithms.
/// </summary>
public static class GeometryMath
{
    /// <summary>
    /// Compute a perpendicular offset point given a position and a tangent direction.
    /// Offset to the left when looking in the tangent direction (positive offset).
    /// </summary>
    public static Point3 PerpendicularOffset(Point3 origin, double tangentAngleDeg, double offset)
    {
        double perpAngle = AngleUtils.ToRadians(tangentAngleDeg + 90.0);
        return new Point3(
            origin.X + offset * System.Math.Cos(perpAngle),
            origin.Y + offset * System.Math.Sin(perpAngle),
            origin.Z
        );
    }

    /// <summary>
    /// Compute a point offset along a direction from an origin.
    /// </summary>
    public static Point3 OffsetAlongDirection(Point3 origin, double angleDeg, double distance)
    {
        double rad = AngleUtils.ToRadians(angleDeg);
        return new Point3(
            origin.X + distance * System.Math.Cos(rad),
            origin.Y + distance * System.Math.Sin(rad),
            origin.Z
        );
    }

    /// <summary>
    /// Create a cross-section profile point with vertical offset (for crossfall/superelevation).
    /// Given a horizontal offset from centerline and a slope percentage, returns the 3D point.
    /// </summary>
    public static Point3 CrossSectionPoint(
        Point3 centerline, double tangentAngleDeg, double horizontalOffset, double slopePercent)
    {
        var pt = PerpendicularOffset(centerline, tangentAngleDeg, horizontalOffset);
        double dz = System.Math.Abs(horizontalOffset) * slopePercent / 100.0;
        return new Point3(pt.X, pt.Y, centerline.Z - dz);
    }

    /// <summary>
    /// Interpolate linearly between two points.
    /// </summary>
    public static Point3 Lerp(Point3 a, Point3 b, double t)
    {
        return new Point3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t
        );
    }

    /// <summary>
    /// Compute the 2D intersection point of two lines defined by point+direction.
    /// Returns null if lines are parallel.
    /// </summary>
    public static Point3? LineLineIntersection2D(
        Point3 p1, double angle1Deg,
        Point3 p2, double angle2Deg)
    {
        double a1 = AngleUtils.ToRadians(angle1Deg);
        double a2 = AngleUtils.ToRadians(angle2Deg);

        double d1x = System.Math.Cos(a1);
        double d1y = System.Math.Sin(a1);
        double d2x = System.Math.Cos(a2);
        double d2y = System.Math.Sin(a2);

        double denom = d1x * d2y - d1y * d2x;
        if (System.Math.Abs(denom) < 1e-12) return null;

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double t = (dx * d2y - dy * d2x) / denom;

        return new Point3(
            p1.X + t * d1x,
            p1.Y + t * d1y,
            0
        );
    }
}
