namespace RoadCreator.Core.Nature;

/// <summary>
/// Naming conventions for the tree/vegetation database layer.
/// Converts from StromDatabazevlozit.rvb / StromDatabazevyber.rvb.
///
/// The tree database uses the same companion-point convention as the
/// general database (<see cref="Accessories.DatabaseNaming"/>), but stores
/// objects on a separate standalone layer ("Tree Database").
///
/// Tree objects are named "Tree0", "Tree1", etc.
/// </summary>
public static class TreeDatabaseNaming
{
    /// <summary>Layer name for the tree database.</summary>
    public const string LayerName = "Tree Database";

    /// <summary>Prefix for tree object names.</summary>
    public const string TreeNamePrefix = "Tree";

    /// <summary>
    /// Build a tree object name from its index.
    /// Example: 0 → "Tree0", 5 → "Tree5"
    /// </summary>
    public static string GetTreeName(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        return TreeNamePrefix + index;
    }

    /// <summary>
    /// Build the companion point name for a tree.
    /// Example: "Tree0" → "Tree0-point(RoadCreator)"
    /// </summary>
    public static string GetTreeCompanionPointName(int index)
    {
        return Accessories.DatabaseNaming.GetCompanionPointName(GetTreeName(index));
    }

    /// <summary>
    /// Check if a name matches a tree object naming pattern.
    /// </summary>
    public static bool IsTreeName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith(TreeNamePrefix, StringComparison.Ordinal))
            return false;
        return int.TryParse(name[TreeNamePrefix.Length..], out _);
    }

    /// <summary>
    /// Extract the tree index from a tree name. Returns -1 if invalid.
    /// </summary>
    public static int ParseTreeIndex(string name)
    {
        if (!IsTreeName(name))
            return -1;
        return int.Parse(name[TreeNamePrefix.Length..]);
    }
}
