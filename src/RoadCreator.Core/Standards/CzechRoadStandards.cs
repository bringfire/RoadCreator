namespace RoadCreator.Core.Standards;

/// <summary>
/// Czech road design standards (CSN) - road categories, widths, and widening rules.
/// Data extracted from Silniceprofildatabaze.rvb and Okruznikrizovatka.rvb.
/// </summary>
public static class CzechRoadStandards
{
    /// <summary>
    /// Road category definitions per Czech standards.
    /// LanesPerDirection and HalfWidth derived from the source script profile arrays.
    /// </summary>
    public static readonly RoadCategory[] Categories =
    {
        //                  Name        LaneW  Median  Dual   Lanes/Dir
        new("S 6.5",        2.75,  0,      false, 1),   // 2.75m single lane each way
        new("S 7.5",        3.00,  0,      false, 1),   // 3.00m single lane each way
        new("S 9.5",        3.50,  0,      false, 1),   // 3.50m single lane each way
        new("S 11.5",       3.50,  0,      false, 1),   // 3.50m + 0.50m shoulder = 4.0m half, but profile is 1 lane + shoulders
        new("S 20.75",      5.25,  1.25,   false, 2),   // 2 lanes per direction (2×2.625 ≈ 5.25m half)
        new("S 24.5",       5.25,  3.00,   false, 2),   // 2 lanes per direction + wider median
        new("D 25.5",       3.75,  3.00,   true,  2),   // dual: 2 lanes × 3.75m = 7.50m half
        new("D 27.5",       3.75,  3.00,   true,  2),   // dual: 2 lanes × 3.75m + wider shoulders
        new("D 33.5",       3.75,  4.00,   true,  3),   // dual: 3 lanes × 3.75m = 11.25m half
        new("D 4/8",        3.75,  3.00,   true,  2),   // dual: 2+2 lanes
    };

    /// <summary>
    /// Compute lane widening based on curve radius.
    /// From Silniceprofildatabaze.rvb lines 199-220.
    /// </summary>
    public static double GetWidening(double radius, string categoryName)
    {
        if (radius >= 250 || radius <= 0) return 0;

        double baseWidening;
        if (radius < 110) baseWidening = 0.5;
        else if (radius < 125) baseWidening = 0.4;
        else if (radius < 141) baseWidening = 0.35;
        else if (radius < 170) baseWidening = 0.30;
        else if (radius < 200) baseWidening = 0.25;
        else baseWidening = 0.2;

        // Adjustments for narrow road categories
        if (categoryName == "S 6.5" && radius < 110)
            baseWidening += 0.3;
        else if (categoryName == "S 7.5" && radius < 110)
            baseWidening += 0.05;

        return baseWidening;
    }

    /// <summary>
    /// Roundabout lane width based on outer diameter.
    /// From Okruznikrizovatka.rvb lines 38-68.
    /// </summary>
    public static double GetRoundaboutLaneWidth(double outerDiameter)
    {
        if (outerDiameter >= 40) return 4.5;
        if (outerDiameter >= 35) return 5.0;
        if (outerDiameter >= 32) return 5.25;
        if (outerDiameter >= 30) return 5.5;
        if (outerDiameter >= 28) return 6.0;
        if (outerDiameter >= 25) return 6.5;
        return 0; // Must be user-specified for small roundabouts
    }
}

/// <summary>
/// A Czech road category definition.
/// </summary>
public record RoadCategory(
    string Name,
    double LaneWidth,
    double MedianWidth,
    bool IsDualCarriageway,
    int LanesPerDirection
)
{
    /// <summary>
    /// Total half-width of the road (one direction, excluding median).
    /// </summary>
    public double HalfWidth => LaneWidth * LanesPerDirection;
}
