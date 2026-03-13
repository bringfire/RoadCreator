using RoadCreator.Core.Math;

namespace RoadCreator.Core.Terrain;

/// <summary>
/// Computes longitudinal profile data from terrain elevations along a route.
/// From RC2_PodelnyProfil_CZ.rvb.
///
/// Algorithm:
///   1. Sample terrain elevation at regular intervals along the route axis
///   2. Compute reference datum: floor(minElevation / 10) * 10 - 10
///   3. Convert to profile coordinates: X = chainage, Y = (elevation - datum) * exaggeration
///
/// Profile coordinate system:
///   X = chainage (distance along route from start)
///   Y = (elevation - datum) × vertical exaggeration (10:1)
/// </summary>
public static class LongitudinalProfileComputer
{
    /// <summary>
    /// Compute the reference datum (srovnávací hladina) from minimum terrain elevation.
    /// </summary>
    /// <param name="minElevation">Minimum terrain elevation along the route.</param>
    /// <remarks>
    /// Uses Math.Floor for consistent rounding toward negative infinity.
    /// The original VBScript uses `\` (integer division) which truncates toward zero,
    /// producing different results for negative elevations (e.g., -5 => -10 in VBScript vs -20 here).
    /// Math.Floor is intentionally preferred for mathematical consistency.
    /// </remarks>
    public static double ComputeReferenceDatum(double minElevation)
    {
        return (System.Math.Floor(minElevation / 10.0) - 1) * 10.0;
    }

    /// <summary>
    /// Convert terrain elevation to profile Y coordinate.
    /// </summary>
    /// <param name="elevation">Terrain elevation in meters.</param>
    /// <param name="referenceDatum">Reference datum elevation.</param>
    /// <param name="verticalExaggeration">Vertical exaggeration factor (default 10).</param>
    public static double ElevationToProfileY(double elevation, double referenceDatum, double verticalExaggeration = 10.0)
    {
        return (elevation - referenceDatum) * verticalExaggeration;
    }

    /// <summary>
    /// Convert profile Y coordinate back to real elevation.
    /// </summary>
    /// <param name="profileY">Y coordinate in profile space.</param>
    /// <param name="referenceDatum">Reference datum elevation.</param>
    /// <param name="verticalExaggeration">Vertical exaggeration factor (default 10).</param>
    public static double ProfileYToElevation(double profileY, double referenceDatum, double verticalExaggeration = 10.0)
    {
        if (System.Math.Abs(verticalExaggeration) < 1e-10)
            throw new ArgumentException("Vertical exaggeration must not be zero.");
        return referenceDatum + profileY / verticalExaggeration;
    }

    /// <summary>
    /// Compute profile points from terrain elevations at given chainages.
    /// </summary>
    /// <param name="chainages">Distance along route for each sample point.</param>
    /// <param name="elevations">Terrain elevation at each sample point.</param>
    /// <param name="referenceDatum">Reference datum (from ComputeReferenceDatum).</param>
    /// <param name="verticalExaggeration">Vertical exaggeration factor.</param>
    /// <returns>Profile points where X = chainage, Y = exaggerated elevation, Z = 0.</returns>
    public static Point3[] ComputeProfilePoints(
        double[] chainages,
        double[] elevations,
        double referenceDatum,
        double verticalExaggeration = 10.0)
    {
        if (chainages == null)
            throw new ArgumentNullException(nameof(chainages));
        if (elevations == null)
            throw new ArgumentNullException(nameof(elevations));
        if (chainages.Length != elevations.Length)
            throw new ArgumentException(
                $"Chainages ({chainages.Length}) and elevations ({elevations.Length}) must have the same length.");

        var points = new Point3[chainages.Length];
        for (int i = 0; i < chainages.Length; i++)
        {
            double y = ElevationToProfileY(elevations[i], referenceDatum, verticalExaggeration);
            points[i] = new Point3(chainages[i], y, 0);
        }

        return points;
    }

    /// <summary>
    /// Find the minimum elevation in an array.
    /// </summary>
    public static double FindMinElevation(double[] elevations)
    {
        if (elevations == null || elevations.Length == 0)
            throw new ArgumentException("Must have at least one elevation.", nameof(elevations));

        double min = elevations[0];
        for (int i = 1; i < elevations.Length; i++)
        {
            if (elevations[i] < min)
                min = elevations[i];
        }
        return min;
    }
}
