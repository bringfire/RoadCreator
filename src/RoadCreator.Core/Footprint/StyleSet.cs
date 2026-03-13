namespace RoadCreator.Core.Footprint;

/// <summary>
/// A named collection of visual styles for road footprint lines.
///
/// Decoupled from OffsetProfile so the same geometry can be rendered with
/// different office, client, or country layer standards. An agent can construct
/// a profile once and swap style sets without touching offset geometry.
///
/// Lookup is case-insensitive on StyleEntry.Id.
/// </summary>
public sealed record StyleSet
{
    public const string SchemaVersion = "roadcreator.style-set/v1";

    public string Name { get; init; }
    public IReadOnlyList<StyleEntry> Styles { get; init; }

    public StyleSet(string name, IReadOnlyList<StyleEntry> styles)
    {
        Name = name;
        Styles = styles;
    }

    /// <summary>
    /// Find a style by id. Returns null if not found.
    /// </summary>
    public StyleEntry? FindStyle(string id) =>
        Styles.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Find a style by id, falling back to a provided default when not found.
    /// Useful so rendering never fails silently.
    /// </summary>
    public StyleEntry FindStyleOrDefault(string id, StyleEntry fallback) =>
        FindStyle(id) ?? fallback;
}
