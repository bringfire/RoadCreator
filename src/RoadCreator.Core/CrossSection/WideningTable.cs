namespace RoadCreator.Core.CrossSection;

/// <summary>
/// Radius-dependent lane widening table from Czech ČSN 73 6101.
/// From RC2_3DSilnice_CZ.rvb — the widening lookup block.
///
/// Widening values (in meters) by curve radius:
///   R >= 250      -> 0.0
///   200 <= R < 250 -> 0.2
///   170 <= R < 200 -> 0.25
///   141 <= R < 170 -> 0.30
///   125 <= R < 141 -> 0.35
///   110 <= R < 125 -> 0.40
///   R < 110       -> 0.50
///
/// Additional corrections by road category:
///   S 6.5: +0.30m
///   S 7.5: +0.05m
/// </summary>
public static class WideningTable
{
    /// <summary>
    /// Compute the widening value for a given curve radius and road category.
    /// </summary>
    /// <param name="radius">Curve radius in meters. Use a large value (e.g., 500) for straight sections.</param>
    /// <param name="category">Road category for category-specific corrections.</param>
    /// <returns>Widening in meters (applied to each side of the road).</returns>
    public static double ComputeWidening(double radius, RoadCategory category)
    {
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be non-negative.");
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        double widening = ComputeBaseWidening(radius);

        // Category-specific corrections (from VBScript)
        if (widening > 0)
        {
            if (category == RoadCategory.S65)
                widening += 0.3;
            else if (category == RoadCategory.S75)
                widening += 0.05;
        }

        return widening;
    }

    /// <summary>
    /// Compute the base widening without category corrections.
    /// </summary>
    public static double ComputeBaseWidening(double radius)
    {
        if (radius >= 250) return 0.0;
        if (radius >= 200) return 0.2;
        if (radius >= 170) return 0.25;
        if (radius >= 141) return 0.30;
        if (radius >= 125) return 0.35;
        if (radius >= 110) return 0.40;
        return 0.50;
    }
}
