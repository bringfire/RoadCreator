using global::Rhino.Geometry;

namespace RoadCreator.Rhino.Commands.Road;

/// <summary>
/// Shared utilities for 3D road surface generation commands (Road3D, Road3DSection).
/// </summary>
internal static class RoadGeometryHelper
{
    /// <summary>Default radius for straight sections (curvature near zero).</summary>
    public const double DefaultStraightRadius = 500.0;

    /// <summary>
    /// Parse curve direction string to numeric value.
    /// L = left curve (+1), P = right curve (-1), R = straight (0).
    /// LM = transition from left curve to straight (+0.5).
    /// PM = transition from right curve to straight (-0.5).
    /// </summary>
    public static double ParseCurveDirection(string direction)
    {
        return direction switch
        {
            "L" => 1.0,
            "P" => -1.0,
            "LM" => 0.5,
            "PM" => -0.5,
            "R" => 0.0,
            _ => 0.0
        };
    }

    /// <summary>
    /// Get the curve radius at a point on the route.
    /// Returns DefaultStraightRadius if the curvature is near zero.
    /// </summary>
    public static double GetRadiusAtPoint(Curve routeCurve, Point3d point)
    {
        if (!routeCurve.ClosestPoint(point, out double t))
            return DefaultStraightRadius;
        var curvatureVec = routeCurve.CurvatureAt(t);
        if (!curvatureVec.IsValid)
            return DefaultStraightRadius;
        double curvature = curvatureVec.Length;
        if (curvature < 1e-10 || double.IsNaN(curvature) || double.IsInfinity(curvature))
            return DefaultStraightRadius;
        return 1.0 / curvature;
    }

    /// <summary>Station information for cross-section profile generation.</summary>
    public record struct StationInfo(Point3d Point, string Direction, double Chainage);
}
