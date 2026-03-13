namespace RoadCreator.Core.Math;

/// <summary>
/// Angle conversion and normalization utilities.
/// Replaces the VBScript cosin()/sinus() geometry workaround and Rhino.Angle() calls.
/// </summary>
public static class AngleUtils
{
    public const double Deg2Rad = System.Math.PI / 180.0;
    public const double Rad2Deg = 180.0 / System.Math.PI;

    public static double ToRadians(double degrees) => degrees * Deg2Rad;
    public static double ToDegrees(double radians) => radians * Rad2Deg;

    /// <summary>
    /// Normalize angle to [0, 360) range.
    /// Replaces the VBScript pattern: If angle > 270 Then angle = angle - 360
    /// </summary>
    public static double Normalize360(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0) degrees += 360.0;
        return degrees;
    }

    /// <summary>
    /// Normalize angle to [-180, 180) range.
    /// </summary>
    public static double Normalize180(double degrees)
    {
        degrees = Normalize360(degrees);
        if (degrees >= 180.0) degrees -= 360.0;
        return degrees;
    }

    /// <summary>
    /// Angle in degrees from point a to point b (measured from positive X axis, counterclockwise).
    /// Replaces Rhino.Angle(pt1, pt2) calls used throughout the scripts.
    /// </summary>
    public static double AngleBetweenPoints(Point3 from, Point3 to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double angle = System.Math.Atan2(dy, dx) * Rad2Deg;
        return Normalize360(angle);
    }

    /// <summary>
    /// Determine which side a test point is on relative to a directed line (from -> to).
    /// Returns positive for left, negative for right, zero for collinear.
    /// Replaces the LevaPrava() function from the scripts.
    /// </summary>
    public static double SideOfLine(Point3 from, Point3 to, Point3 test)
    {
        return (to.X - from.X) * (test.Y - from.Y) - (to.Y - from.Y) * (test.X - from.X);
    }

    /// <summary>
    /// Smallest signed angle difference between two angles in degrees.
    /// Handles wrapping correctly.
    /// </summary>
    public static double AngleDifference(double angle1Deg, double angle2Deg)
    {
        double diff = angle2Deg - angle1Deg;
        return Normalize180(diff);
    }
}
