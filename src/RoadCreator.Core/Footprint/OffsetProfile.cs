namespace RoadCreator.Core.Footprint;

/// <summary>
/// Defines the geometry of a road footprint: which offset lines exist and where.
///
/// Intentionally decoupled from presentation (layer, linetype, color) — those
/// live in StyleSet. A single profile can be rendered with different style sets
/// (project A vs project B layer naming, print vs screen, metric vs imperial).
///
/// Baseline: the feature id (or "centerline") from which all offsets are measured.
/// This enables composable profiles — a sidewalk profile can reference
/// "edge_of_pavement" as its baseline and inherit its position automatically.
///
/// Units: "m" (metres) or "ft" (feet). Stored for documentation; callers are
/// responsible for consistent unit usage.
/// </summary>
public sealed record OffsetProfile
{
    public const string SchemaVersion = "roadcreator.offset-profile/v1";

    public string Name { get; init; }
    public string Units { get; init; }
    public string Baseline { get; init; }
    public IReadOnlyList<OffsetFeature> Features { get; init; }

    public OffsetProfile(string name, string units, string baseline,
        IReadOnlyList<OffsetFeature> features)
    {
        Name = name;
        Units = units;
        Baseline = baseline;
        Features = features;
    }

    /// <summary>
    /// Features sorted left-to-right by signed offset.
    /// </summary>
    public IEnumerable<OffsetFeature> OrderedFeatures =>
        Features.OrderBy(f => f.Offset);

    /// <summary>
    /// Resolve the absolute signed offset of a feature from the true centerline,
    /// accounting for a non-centerline baseline.
    ///
    /// Resolution is single-level (non-recursive): the baseline feature's own offset
    /// is used as-is (absolute from the centerline), not itself recursively resolved.
    /// Chained baselines (A → B → C) are not supported; define all offsets from a
    /// single shared baseline or use "centerline" and absolute values instead.
    ///
    /// Self-referential baselines (feature.Id == Baseline) produce offset + offset,
    /// which is numerically valid but semantically incorrect. Validate profiles at
    /// construction time if this matters for your use case.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the baseline feature id is not found in this profile.
    /// </exception>
    public double ResolveAbsoluteOffset(OffsetFeature feature)
    {
        if (string.Equals(Baseline, "centerline", StringComparison.OrdinalIgnoreCase))
            return feature.Offset;

        var baseFeature = Features.FirstOrDefault(
            f => string.Equals(f.Id, Baseline, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Baseline feature '{Baseline}' not found in profile '{Name}'.");

        return baseFeature.Offset + feature.Offset;
    }
}
