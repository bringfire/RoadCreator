namespace RoadCreator.Core.Footprint;

/// <summary>
/// Built-in style sets shipped with RoadCreator.
///
/// These are starting points. Projects override via RC_StoreStyleSet or by
/// passing inline JSON to RC_RoadFootprint. No Czech-specific or country-specific
/// assumptions are baked in — role names are semantic ("edge_of_pavement"),
/// not prescriptive ("ČSN_73_6101_edge").
///
/// Layer hierarchy: RoadCreator::Markings::* for lines on the paved surface,
///                  RoadCreator::Reference::* for planning/reference lines.
/// </summary>
public static class DefaultStyles
{
    /// <summary>
    /// Generic international style set suitable for any road type or country.
    /// </summary>
    public static readonly StyleSet Generic = new("default_generic",
        new StyleEntry[]
        {
            new("centerline",       "RoadCreator::Markings::Centerline",    "Center",     0.50),
            new("lane_divider",     "RoadCreator::Markings::LaneDivider",   "Dashed",     0.18),
            new("edge_of_pavement", "RoadCreator::Markings::EdgeLine",      "Continuous", 0.50),
            new("shoulder",         "RoadCreator::Markings::Shoulder",      "Continuous", 0.25),
            new("curb_face",        "RoadCreator::Markings::Curb",          "Continuous", 0.50),
            new("setback",          "RoadCreator::Reference::Setback",       "Dashed",     0.18),
            new("right_of_way",     "RoadCreator::Reference::RightOfWay",    "Dashed",     0.18),
            new("utility_corridor", "RoadCreator::Reference::Utility",       "Center",     0.18),
        });

    /// <summary>
    /// Fallback style used when a feature's StyleRef is not found in the active StyleSet.
    /// Ensures rendering never fails silently.
    /// </summary>
    public static readonly StyleEntry Fallback =
        new("_fallback", "RoadCreator::Markings::Unknown", "Continuous", 0.18);

    /// <summary>
    /// All built-in style sets indexed by name (case-insensitive).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, StyleSet> All =
        new Dictionary<string, StyleSet>(StringComparer.OrdinalIgnoreCase)
        {
            [Generic.Name] = Generic,
        };
}
