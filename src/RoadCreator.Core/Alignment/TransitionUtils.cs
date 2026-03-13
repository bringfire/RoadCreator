using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Shared utilities for transition curve computations.
/// </summary>
public static class TransitionUtils
{
    /// <summary>
    /// Mirror a set of points about the X-axis (negate Y) for right-turn curves.
    /// </summary>
    public static Point3[] MirrorY(Point3[] points)
    {
        var mirrored = new Point3[points.Length];
        for (int i = 0; i < points.Length; i++)
            mirrored[i] = new Point3(points[i].X, -points[i].Y, points[i].Z);
        return mirrored;
    }

    internal static void ValidatePositive(double value, string name)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
    }
}
