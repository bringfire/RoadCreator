namespace RoadCreator.Core.Terrain;

/// <summary>
/// Classifies contour lines by elevation interval.
/// From RC2_Vrstevnice_CZ.rvb.
///
/// Classification rules (matching original VBScript):
///   - Every 10m: Main contour (Hlavní vrstevnice)
///   - Every 5m (not 10m): Secondary contour (5m)
///   - Every 2m (not 5m, not 10m): Minor contour (2m)
///   - Odd meters: Discarded
/// </summary>
public static class ContourClassifier
{
    public enum ContourType
    {
        Discard,
        Minor2m,
        Secondary5m,
        Main10m
    }

    /// <summary>
    /// Classify a contour by its height index (meters above the start elevation).
    /// </summary>
    /// <param name="heightIndex">Integer height in meters above the lowest contour level.</param>
    public static ContourType Classify(int heightIndex)
    {
        if (heightIndex % 10 == 0)
            return ContourType.Main10m;
        if (heightIndex % 5 == 0)
            return ContourType.Secondary5m;
        if (heightIndex % 2 == 0)
            return ContourType.Minor2m;
        return ContourType.Discard;
    }

    /// <summary>
    /// Compute the start elevation for contour generation.
    /// Rounds down to the nearest 10m below the minimum terrain elevation.
    /// </summary>
    /// <param name="minElevation">Minimum terrain elevation in meters.</param>
    public static double ComputeStartElevation(double minElevation)
    {
        // (int) truncates toward zero; for negative values we subtract 1 to ensure
        // the result is always at or below the minimum elevation.
        // This matches the VBScript behavior using the `\` operator.
        int floored = (int)(minElevation / 10.0);
        if (minElevation < 0)
            floored--;
        return floored * 10.0;
    }

    /// <summary>
    /// Compute the number of contour levels to generate.
    /// </summary>
    /// <param name="startElevation">Start elevation (from ComputeStartElevation).</param>
    /// <param name="maxElevation">Maximum terrain elevation.</param>
    /// <param name="interval">Contour interval in meters (default 1m).</param>
    public static int ComputeLevelCount(double startElevation, double maxElevation, double interval = 1.0)
    {
        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be positive.");
        if (maxElevation <= startElevation)
            return 0;

        return (int)((maxElevation - startElevation) / interval) + 1;
    }
}
