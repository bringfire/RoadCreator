using global::Rhino;
using RoadCreator.Core.Footprint;

namespace RoadCreator.Rhino.Footprint;

/// <summary>
/// Persists OffsetProfile and StyleSet definitions inside the .3dm document
/// using RhinoDoc.Strings (a built-in key-value store in every Rhino document).
///
/// Keys:
///   "RC_Profile::{name}"   → serialized OffsetProfile JSON
///   "RC_StyleSet::{name}"  → serialized StyleSet JSON
///
/// No external files, no path management — definitions travel with the document.
/// Agents use RC_StoreProfile / RC_StoreStyleSet to populate the store, then
/// reference profiles by name in RC_RoadFootprint.
/// </summary>
public static class FootprintProfileStore
{
    private const string ProfilePrefix = "RC_Profile::";
    private const string StyleSetPrefix = "RC_StyleSet::";

    // ── Profile CRUD ──────────────────────────────────────────────────────────

    public static void StoreProfile(RhinoDoc doc, OffsetProfile profile)
    {
        var json = FootprintSerializer.SerializeProfile(profile);
        doc.Strings.SetString(ProfilePrefix + profile.Name, json);
    }

    public static OffsetProfile? GetProfile(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(ProfilePrefix + name);
        return json != null ? FootprintSerializer.DeserializeProfile(json) : null;
    }

    public static IReadOnlyList<string> ListProfiles(RhinoDoc doc)
    {
        var result = new List<string>();
        int count = doc.Strings.Count;
        for (int i = 0; i < count; i++)
        {
            string key = doc.Strings.GetKey(i);
            if (key.StartsWith(ProfilePrefix, StringComparison.Ordinal))
                result.Add(key[ProfilePrefix.Length..]);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public static bool DeleteProfile(RhinoDoc doc, string name)
    {
        string key = ProfilePrefix + name;
        bool exists = doc.Strings.GetValue(key) != null;
        if (exists) doc.Strings.Delete(key);
        return exists;
    }

    // ── StyleSet CRUD ─────────────────────────────────────────────────────────

    public static void StoreStyleSet(RhinoDoc doc, StyleSet styleSet)
    {
        var json = FootprintSerializer.SerializeStyleSet(styleSet);
        doc.Strings.SetString(StyleSetPrefix + styleSet.Name, json);
    }

    public static StyleSet? GetStyleSet(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(StyleSetPrefix + name);
        return json != null ? FootprintSerializer.DeserializeStyleSet(json) : null;
    }

    public static IReadOnlyList<string> ListStyleSets(RhinoDoc doc)
    {
        var result = new List<string>();
        int count = doc.Strings.Count;
        for (int i = 0; i < count; i++)
        {
            string key = doc.Strings.GetKey(i);
            if (key.StartsWith(StyleSetPrefix, StringComparison.Ordinal))
                result.Add(key[StyleSetPrefix.Length..]);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public static bool DeleteStyleSet(RhinoDoc doc, string name)
    {
        string key = StyleSetPrefix + name;
        bool exists = doc.Strings.GetValue(key) != null;
        if (exists) doc.Strings.Delete(key);
        return exists;
    }

    // ── Resolution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve a profile by name: document store first, then inline JSON fallback.
    /// Returns null if neither source yields a valid profile.
    /// </summary>
    public static OffsetProfile? ResolveProfile(RhinoDoc doc, string nameOrJson)
    {
        // Try as a stored name first
        var stored = GetProfile(doc, nameOrJson);
        if (stored != null) return stored;

        // Try as inline JSON (agent workflow: pass profile directly without storing)
        if (nameOrJson.TrimStart().StartsWith('{'))
            return FootprintSerializer.DeserializeProfile(nameOrJson);

        return null;
    }

    /// <summary>
    /// Resolve a style set by name: document store → built-in defaults → inline JSON.
    /// Falls back to DefaultStyles.Generic so rendering never fails silently.
    /// </summary>
    public static StyleSet ResolveStyleSet(RhinoDoc doc, string? nameOrJson)
    {
        if (string.IsNullOrEmpty(nameOrJson))
            return DefaultStyles.Generic;

        // Document store
        var stored = GetStyleSet(doc, nameOrJson);
        if (stored != null) return stored;

        // Built-in defaults
        if (DefaultStyles.All.TryGetValue(nameOrJson, out var builtin))
            return builtin;

        // Inline JSON
        if (nameOrJson.TrimStart().StartsWith('{'))
        {
            var parsed = FootprintSerializer.DeserializeStyleSet(nameOrJson);
            if (parsed != null) return parsed;
        }

        return DefaultStyles.Generic;
    }
}
