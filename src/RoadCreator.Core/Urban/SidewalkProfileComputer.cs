using RoadCreator.Core.Math;

namespace RoadCreator.Core.Urban;

/// <summary>
/// Computes sidewalk cross-section profile including curb geometry.
/// From Chodnik.rvb — sidewalk with raised curb.
///
/// Profile layout (looking from road toward sidewalk):
///   [0, 0]  →  road edge (curb base)
///   [0, CurbHeight]  →  curb top at road edge
///   [CurbTopWidth, CurbHeight]  →  curb top outer edge
///   [CurbTopWidth + sidewalkWidth, CurbHeight]  →  sidewalk outer edge
///
/// Coordinate system (local 2D):
///   X = horizontal distance from road edge
///   Y = vertical offset (positive = above road)
///   Z = 0 (2D profile, remapped by Rhino command)
/// </summary>
public static class SidewalkProfileComputer
{
    /// <summary>
    /// Standard curb height: 0.2m (200mm).
    /// From VBScript: CopyObject(krivka(0), Array(0,0,0), Array(0,0,0.2)).
    /// </summary>
    public const double CurbHeight = 0.2;

    /// <summary>
    /// Curb top width: 0.3m (300mm).
    /// From VBScript: OffsetCurve(krivka(1), stred, 0.3).
    /// </summary>
    public const double CurbTopWidth = 0.3;

    /// <summary>
    /// Compute the curb profile points (3 curves for loft).
    /// Returns the profile as 3 key elevations:
    ///   [0] = road edge base (0, 0)
    ///   [1] = curb top at road edge (0, CurbHeight)
    ///   [2] = curb top outer edge (CurbTopWidth, CurbHeight)
    /// </summary>
    public static Point3[] ComputeCurbProfile()
    {
        return new[]
        {
            new Point3(0, 0, 0),
            new Point3(0, CurbHeight, 0),
            new Point3(CurbTopWidth, CurbHeight, 0),
        };
    }

    /// <summary>
    /// Compute the full sidewalk profile including curb.
    /// Returns 4 points from road edge to sidewalk outer edge.
    /// </summary>
    /// <param name="sidewalkWidth">Sidewalk width in meters (positive, not including curb).</param>
    public static Point3[] ComputeFullProfile(double sidewalkWidth)
    {
        if (sidewalkWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(sidewalkWidth), sidewalkWidth,
                "Sidewalk width must be positive.");

        return new[]
        {
            new Point3(0, 0, 0),
            new Point3(0, CurbHeight, 0),
            new Point3(CurbTopWidth, CurbHeight, 0),
            new Point3(CurbTopWidth + sidewalkWidth, CurbHeight, 0),
        };
    }
}
