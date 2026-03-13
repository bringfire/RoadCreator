namespace RoadCreator.Core.Urban;

/// <summary>
/// Computes roundabout lane widths based on Czech ČSN standards.
/// From Okruznikrizovatka.rvb — lane width is diameter-dependent.
///
/// Czech standard lane width table (outer diameter → lane width):
///   ≥40m → 4.5m
///   ≥35m → 5.0m
///   ≥32m → 5.25m
///   ≥30m → 5.5m
///   ≥28m → 6.0m
///   ≥25m → 6.5m
///   &lt;25m → user-specified (no standard, requires manual entry)
///
/// Apron ring (prstenec): 1m wide, only for diameter ≥ 25m.
/// Central island raised 0.75m above road surface.
/// </summary>
public static class RoundaboutLaneComputer
{
    /// <summary>
    /// Minimum outer diameter for standard lane width lookup (25m).
    /// Below this, lane width must be specified manually.
    /// </summary>
    public const double MinStandardDiameter = 25.0;

    /// <summary>
    /// Apron ring width (prstenec) in meters. Only applies when diameter ≥ 25m.
    /// </summary>
    public const double ApronWidth = 1.0;

    /// <summary>
    /// Central island raise height in meters.
    /// From VBScript: MoveObject pomplocha(0), Array(0,0,0), Array(0,0,0.75).
    /// </summary>
    public const double IslandHeight = 0.75;

    // Threshold table: (minDiameter, laneWidth) in descending order
    private static readonly (double MinDiameter, double LaneWidth)[] StandardWidths =
    {
        (40.0, 4.5),
        (35.0, 5.0),
        (32.0, 5.25),
        (30.0, 5.5),
        (28.0, 6.0),
        (25.0, 6.5),
    };

    /// <summary>
    /// Get the standard lane width for a given outer diameter.
    /// Returns null if diameter is below the standard minimum (25m),
    /// meaning the user must specify the width manually.
    /// </summary>
    public static double? GetStandardLaneWidth(double outerDiameter)
    {
        if (outerDiameter <= 0)
            throw new ArgumentOutOfRangeException(nameof(outerDiameter), outerDiameter,
                "Outer diameter must be positive.");

        foreach (var (minDia, width) in StandardWidths)
        {
            if (outerDiameter >= minDia)
                return width;
        }

        return null; // Below 25m, no standard
    }

    /// <summary>
    /// Whether the roundabout should have an apron ring (prstenec).
    /// Czech standard: apron ring for diameter ≥ 25m.
    /// </summary>
    public static bool HasApron(double outerDiameter)
    {
        return outerDiameter >= MinStandardDiameter;
    }

    /// <summary>
    /// Compute the inner circle radius (edge of travel lane).
    /// </summary>
    public static double ComputeInnerRadius(double outerRadius, double laneWidth)
    {
        if (outerRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(outerRadius), outerRadius,
                "Outer radius must be positive.");
        if (laneWidth <= 0 || laneWidth >= outerRadius)
            throw new ArgumentOutOfRangeException(nameof(laneWidth), laneWidth,
                "Lane width must be positive and less than outer radius.");

        return outerRadius - laneWidth;
    }

    /// <summary>
    /// Compute the apron inner radius (island outer edge).
    /// Only valid when diameter ≥ 25m.
    /// </summary>
    public static double ComputeApronInnerRadius(double outerRadius, double laneWidth)
    {
        double innerRadius = ComputeInnerRadius(outerRadius, laneWidth);
        return innerRadius - ApronWidth;
    }
}
