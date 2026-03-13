using System.Collections.ObjectModel;

namespace RoadCreator.Core.CrossSection;

/// <summary>
/// Czech road category definitions from ČSN 73 6101.
/// From RC2_3DSilnice_CZ.rvb — Select Case typSilnice.
///
/// Naming: S = silnice (road), D = dálnice (motorway)
/// Number = total road width in meters (e.g., S 6.5 = 6.5m wide road)
///
/// Each category defines:
///   - HalfWidth: distance from centerline to road edge (one side)
///   - MedianWidth: central median strip width (0 for undivided roads)
///
/// The total paved width = 2 × HalfWidth + MedianWidth
/// </summary>
public sealed class RoadCategory
{
    public string Code { get; }
    public double HalfWidth { get; }
    public double MedianWidth { get; }

    private RoadCategory(string code, double halfWidth, double medianWidth)
    {
        Code = code;
        HalfWidth = halfWidth;
        MedianWidth = medianWidth;
    }

    // Undivided roads (silnice)
    public static readonly RoadCategory S65 = new("S 6.5", 2.75, 0);
    public static readonly RoadCategory S75 = new("S 7.5", 3.25, 0);
    public static readonly RoadCategory S95 = new("S 9.5", 4.25, 0);
    public static readonly RoadCategory S115 = new("S 11.5", 5.25, 0);

    // Divided roads (silnice with median)
    public static readonly RoadCategory S2075 = new("S 20.75", 10.25, 1.25);
    public static readonly RoadCategory S245 = new("S 24.5", 10.75, 3.0);

    // Motorways (dálnice)
    public static readonly RoadCategory D255 = new("D 25.5", 11.25, 3.0);
    public static readonly RoadCategory D275 = new("D 27.5", 12.0, 3.5);
    public static readonly RoadCategory D335 = new("D 33.5", 15.0, 3.5);
    public static readonly RoadCategory D48 = new("D 4/8", 8.0, 4.0);

    /// <summary>
    /// All available road categories (immutable).
    /// </summary>
    public static readonly ReadOnlyCollection<RoadCategory> All = Array.AsReadOnly(new[]
    {
        S65, S75, S95, S115, S2075, S245, D255, D275, D335, D48
    });

    /// <summary>
    /// Whether this is a divided road (has a median strip).
    /// </summary>
    public bool IsDivided => MedianWidth > 0;

    /// <summary>
    /// Look up a category by its code string.
    /// Returns null if not found.
    /// </summary>
    public static RoadCategory? FromCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        foreach (var cat in All)
        {
            if (string.Equals(cat.Code, code, StringComparison.OrdinalIgnoreCase))
                return cat;
        }
        return null;
    }

    public override string ToString() => Code;
}
