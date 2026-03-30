using global::Rhino;
using RoadCreator.Core.Profiles;

namespace RoadCreator.Rhino.Profiles;

/// <summary>
/// Persists RoadProfileDefinition inside the .3dm document using RhinoDoc.Strings.
///
/// Uses a separate keyspace from the OffsetProfile store:
///   "RC_RoadProfile::{name}" → serialized RoadProfileDefinition JSON
///
/// Does not collapse with RC_Profile:: — the two keyspaces are independent.
/// </summary>
public static class RoadProfileStore
{
    private const string Prefix = "RC_RoadProfile::";

    /// <summary>
    /// Store a canonical road profile. Returns whether an existing profile was overwritten.
    /// </summary>
    public static bool Store(RhinoDoc doc, RoadProfileDefinition profile)
    {
        string key = Prefix + profile.Name;
        bool existed = doc.Strings.GetValue(key) != null;
        var json = RoadProfileSerializer.Serialize(profile);
        doc.Strings.SetString(key, json);
        return existed;
    }

    /// <summary>
    /// Retrieve a stored road profile by name. Returns null if not found.
    /// </summary>
    public static RoadProfileDefinition? Get(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(Prefix + name);
        return json != null ? RoadProfileSerializer.Deserialize(json) : null;
    }

    /// <summary>
    /// Check whether a road profile exists in the document.
    /// </summary>
    public static bool Exists(RhinoDoc doc, string name)
    {
        return doc.Strings.GetValue(Prefix + name) != null;
    }

    /// <summary>
    /// List all stored road profile names.
    /// </summary>
    public static IReadOnlyList<string> List(RhinoDoc doc)
    {
        var result = new List<string>();
        int count = doc.Strings.Count;
        for (int i = 0; i < count; i++)
        {
            string key = doc.Strings.GetKey(i);
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                result.Add(key[Prefix.Length..]);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Delete a stored road profile. Returns true if it existed.
    /// </summary>
    public static bool Delete(RhinoDoc doc, string name)
    {
        string key = Prefix + name;
        bool exists = doc.Strings.GetValue(key) != null;
        if (exists) doc.Strings.Delete(key);
        return exists;
    }
}
