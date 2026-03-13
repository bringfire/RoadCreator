namespace RoadCreator.Core.Footprint;

/// <summary>
/// Visual appearance definition for one category of footprint line.
/// Referenced by OffsetFeature.StyleRef → StyleSet lookup.
///
/// Layer:        Full Rhino layer path, e.g. "RoadCreator::Markings::LaneDivider".
///               The layer will be created automatically if it does not exist.
/// Linetype:     Rhino linetype name: "Continuous", "Dashed", "Center", "DashDot".
///               Falls back to layer linetype if the name is not found in the document.
/// PrintWidthMm: Pen weight for printed output (mm). 0 = use layer default.
/// Color:        Optional object-level color override. Null = use layer color.
///               Accepts hex "#RRGGBB" or named colours ("red", "yellow", etc.).
/// </summary>
public sealed record StyleEntry(
    string Id,
    string Layer,
    string Linetype,
    double PrintWidthMm,
    string? Color = null
);
