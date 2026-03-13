namespace RoadCreator.Core.Accessories;

/// <summary>
/// Naming conventions for the RoadCreator object database layer system.
/// Converts from Databazevlozit.rvb / Databazevyber.rvb.
///
/// Objects stored in the database have a companion point named
/// "{ObjectName}-point(RoadCreator)" that stores the object's base position.
/// </summary>
public static class DatabaseNaming
{
    /// <summary>Suffix appended to companion point names.</summary>
    public const string CompanionSuffix = "-point(RoadCreator)";

    /// <summary>
    /// Build the companion point name for a database object.
    /// Example: "Lamp" → "Lamp-point(RoadCreator)"
    /// </summary>
    public static string GetCompanionPointName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            throw new ArgumentException("Object name cannot be empty.", nameof(objectName));
        return objectName + CompanionSuffix;
    }

    /// <summary>
    /// Check if a name follows the companion point naming convention.
    /// </summary>
    public static bool IsCompanionPointName(string name)
    {
        return !string.IsNullOrEmpty(name) && name.EndsWith(CompanionSuffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extract the object name from a companion point name.
    /// Returns null if the name doesn't match the convention.
    /// </summary>
    public static string? ExtractObjectName(string companionName)
    {
        if (!IsCompanionPointName(companionName))
            return null;
        return companionName[..^CompanionSuffix.Length];
    }
}
