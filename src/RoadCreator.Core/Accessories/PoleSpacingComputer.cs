namespace RoadCreator.Core.Accessories;

/// <summary>
/// Adaptive road pole spacing based on curve radius.
/// Converts from silnicnisloupkyjednostranny.rvb / silnicnisloupkyoboustranny.rvb.
///
/// The curve is divided at 5m base intervals. At each interval, the local
/// radius is estimated from the angle change between consecutive segments.
/// Poles are placed only when enough base intervals have passed since the
/// last pole, depending on the local radius.
///
/// Radius thresholds (Czech road standards):
///   R &gt; 1250m → every 50m (10 intervals)
///   R &gt; 850m  → every 40m (8 intervals)
///   R &gt; 450m  → every 30m (6 intervals)
///   R &gt; 250m  → every 20m (4 intervals)
///   R &gt; 50m   → every 10m (2 intervals)
///   R ≤ 50m   → every 5m  (1 interval)
/// </summary>
public static class PoleSpacingComputer
{
    /// <summary>Base division interval along the curve (meters).</summary>
    public const double BaseInterval = 5.0;

    /// <summary>Default radius for straight segments / endpoints.</summary>
    public const double StraightRadius = 10000.0;

    /// <summary>Minimum angular difference to consider a curve (degrees).</summary>
    public const double MinAngleDifference = 0.0001;

    /// <summary>
    /// Radius-to-skip-count table. Ordered from largest to smallest radius.
    /// Each entry: (minRadius, requiredSkip) — place a pole when
    /// (currentIndex - lastPlacedIndex) >= requiredSkip.
    /// </summary>
    private static readonly (double MinRadius, int RequiredSkip)[] SpacingTable =
    {
        (1250.0, 10),  // 50m
        (850.0, 8),    // 40m
        (450.0, 6),    // 30m
        (250.0, 4),    // 20m
        (50.0, 2),     // 10m
        (0.0, 1),      // 5m
    };

    /// <summary>
    /// Get the required number of base intervals between poles for a given radius.
    /// </summary>
    public static int GetRequiredSkip(double radius)
    {
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));

        foreach (var (minRadius, skip) in SpacingTable)
        {
            if (radius > minRadius)
                return skip;
        }

        return 1;
    }

    /// <summary>
    /// Get the effective spacing distance for a given radius (meters).
    /// </summary>
    public static double GetSpacingDistance(double radius)
    {
        return GetRequiredSkip(radius) * BaseInterval;
    }

    /// <summary>
    /// Estimate the local radius of curvature from the angle change between
    /// three consecutive points spaced at the base interval.
    /// VBScript formula: polomer = 10 / toRadians(angleDiff) / 2
    ///                 = 5 / toRadians(angleDiff)
    /// </summary>
    /// <param name="angleDiffDegrees">Absolute angle difference in degrees.</param>
    public static double EstimateRadius(double angleDiffDegrees)
    {
        double absDiff = System.Math.Abs(angleDiffDegrees);
        if (absDiff < MinAngleDifference)
            return StraightRadius;

        double radians = absDiff * System.Math.PI / 180.0;
        // VBScript: polomer = 10 / Rhino.ToRadians(rozdil) / 2
        return BaseInterval * 2.0 / radians / 2.0;
    }
}
