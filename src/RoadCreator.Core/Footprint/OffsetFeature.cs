namespace RoadCreator.Core.Footprint;

/// <summary>
/// A single offset line within a road footprint profile.
///
/// Offset convention (signed, in profile units):
///   negative = left of baseline (relative to curve direction)
///   positive = right of baseline
///   zero     = the baseline itself (typically the centerline)
///
/// Role is the semantic identity of this line — what it represents in
/// the road cross-section. Common roles:
///   "centerline", "lane_divider", "edge_of_pavement",
///   "shoulder", "curb_face", "setback", "right_of_way", "utility_corridor"
///
/// StyleRef references a StyleEntry.Id in the active StyleSet, decoupling
/// geometry from presentation. The same role (e.g. "edge_of_pavement") may
/// map to different layers in different office standards.
/// </summary>
public sealed record OffsetFeature(
    string Id,
    double Offset,
    string Role,
    string StyleRef
);
