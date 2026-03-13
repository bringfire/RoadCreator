using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Combines a horizontal alignment (2D plan) with a vertical profile (niveleta)
/// to produce a 3D route centerline.
/// From RC2_VytvoreniTrasy_CZ.rvb.
///
/// Algorithm:
///   1. Divide horizontal alignment into equidistant stations
///   2. For each station, sample elevation from the vertical profile
///   3. Combine horizontal (X, Y) with vertical (Z) to produce 3D points
///
/// The vertical profile is defined in a profile coordinate system:
///   - Profile X = chainage (distance along route from start)
///   - Profile Y = elevation (real-world, not exaggerated)
/// </summary>
public static class Route3DAssembler
{
    /// <summary>
    /// Assemble 3D route points from horizontal alignment points and a vertical profile.
    /// </summary>
    /// <param name="horizontalPoints">2D points along the horizontal alignment (X, Y, Z=0).</param>
    /// <param name="profileElevations">Elevation values at each horizontal point's chainage.
    /// Must be the same length as horizontalPoints.</param>
    /// <returns>3D points with X,Y from horizontal and Z from elevation.</returns>
    public static Point3[] Assemble(Point3[] horizontalPoints, double[] profileElevations)
    {
        if (horizontalPoints == null)
            throw new ArgumentNullException(nameof(horizontalPoints));
        if (profileElevations == null)
            throw new ArgumentNullException(nameof(profileElevations));
        if (horizontalPoints.Length != profileElevations.Length)
            throw new ArgumentException(
                $"Horizontal points ({horizontalPoints.Length}) and profile elevations ({profileElevations.Length}) must have the same count.");

        var result = new Point3[horizontalPoints.Length];
        for (int i = 0; i < horizontalPoints.Length; i++)
        {
            result[i] = new Point3(
                horizontalPoints[i].X,
                horizontalPoints[i].Y,
                profileElevations[i]);
        }

        return result;
    }

    /// <summary>
    /// Sample elevation from a piecewise-linear vertical profile at a given chainage.
    /// The profile is defined by a sorted array of (chainage, elevation) points.
    /// Linearly interpolates between profile points.
    /// </summary>
    /// <param name="profilePoints">Sorted profile points where X = chainage, Y = elevation.</param>
    /// <param name="chainage">The chainage to sample at.</param>
    /// <returns>Interpolated elevation at the given chainage.</returns>
    public static double SampleElevation(Point3[] profilePoints, double chainage)
    {
        if (profilePoints == null || profilePoints.Length == 0)
            throw new ArgumentException("Profile must have at least one point.", nameof(profilePoints));

        // Before first point — extrapolate from first segment
        if (chainage <= profilePoints[0].X)
        {
            if (profilePoints.Length == 1) return profilePoints[0].Y;
            double slope = (profilePoints[1].Y - profilePoints[0].Y) / (profilePoints[1].X - profilePoints[0].X);
            return profilePoints[0].Y + slope * (chainage - profilePoints[0].X);
        }

        // After last point — extrapolate from last segment
        if (chainage >= profilePoints[^1].X)
        {
            if (profilePoints.Length == 1) return profilePoints[0].Y;
            int n = profilePoints.Length;
            double slope = (profilePoints[n - 1].Y - profilePoints[n - 2].Y) / (profilePoints[n - 1].X - profilePoints[n - 2].X);
            return profilePoints[n - 1].Y + slope * (chainage - profilePoints[n - 1].X);
        }

        // Binary search for the enclosing segment
        int lo = 0, hi = profilePoints.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (profilePoints[mid].X <= chainage)
                lo = mid;
            else
                hi = mid;
        }

        // Linear interpolation
        double dx = profilePoints[hi].X - profilePoints[lo].X;
        if (System.Math.Abs(dx) < 1e-10)
            return profilePoints[lo].Y;

        double fraction = (chainage - profilePoints[lo].X) / dx;
        return profilePoints[lo].Y + fraction * (profilePoints[hi].Y - profilePoints[lo].Y);
    }

    /// <summary>
    /// Compute chainages for an array of 2D horizontal points,
    /// measuring cumulative distance from the first point.
    /// </summary>
    public static double[] ComputeChainages(Point3[] horizontalPoints)
    {
        if (horizontalPoints == null || horizontalPoints.Length == 0)
            throw new ArgumentException("Must have at least one point.", nameof(horizontalPoints));

        var chainages = new double[horizontalPoints.Length];
        chainages[0] = 0;
        for (int i = 1; i < horizontalPoints.Length; i++)
        {
            chainages[i] = chainages[i - 1] + horizontalPoints[i].DistanceTo2D(horizontalPoints[i - 1]);
        }

        return chainages;
    }
}
