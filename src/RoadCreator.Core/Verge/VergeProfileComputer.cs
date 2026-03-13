using RoadCreator.Core.Math;

namespace RoadCreator.Core.Verge;

/// <summary>
/// Computes road verge (shoulder) cross-section profiles.
/// From Krajnice.rvb — the verge profile construction.
///
/// A verge is the paved or unpaved strip at the road edge that provides
/// structural support and drainage. The profile slopes downward at a fixed
/// gradient (default 8%) from the road edge outward.
///
/// Profile layout:
///   [0, 0] -- [width, -width × slope]
///   (road edge → outer verge edge)
///
/// Coordinate system (local 2D, same as SlopeProfileComputer):
///   X = horizontal distance outward from road edge
///   Y = vertical offset (negative = below road grade)
///   Z = 0 (2D profile, remapped Y→Z by Rhino command)
/// </summary>
public static class VergeProfileComputer
{
    /// <summary>
    /// Default verge cross-slope: 8% (0.08).
    /// From VBScript: -sirka * 0.08 (consistent across all verge scripts).
    /// </summary>
    public const double DefaultSlope = 0.08;

    /// <summary>
    /// Compute the verge profile points starting at the road edge.
    /// </summary>
    /// <param name="width">Verge width in meters (positive).</param>
    /// <param name="slope">Cross-slope as a fraction (e.g., 0.08 = 8%). Defaults to 8%.</param>
    /// <returns>2-point profile: road edge → outer verge edge.</returns>
    public static Point3[] ComputeVergeProfile(double width, double slope = DefaultSlope)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width,
                "Verge width must be positive.");
        if (slope < 0)
            throw new ArgumentOutOfRangeException(nameof(slope), slope,
                "Verge slope must be non-negative.");

        return new[]
        {
            new Point3(0, 0, 0),
            new Point3(width, -width * slope, 0),
        };
    }
}
