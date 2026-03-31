using global::Rhino;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Database;

namespace RoadCreator.Rhino.Footprint;

/// <summary>
/// Persists OffsetProfile and StyleSet definitions inside the .3dm document
/// using RhinoDoc.Strings (a built-in key-value store in every Rhino document).
///
/// Keys:
///   "RC_Profile::{name}"   → serialized OffsetProfile JSON
///   "RC_StyleSet::{name}"  → serialized StyleSet JSON
///
/// Get/List/Delete operate on the active document only.
/// Resolve methods (for "use" paths like footprint generation) read through
/// to the external database. The external database is a shared .3dm whose
/// path is set via RC_SetExternalDatabase.
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

    /// <summary>
    /// Retrieve an offset profile from the active document only.
    /// </summary>
    public static OffsetProfile? GetProfile(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(ProfilePrefix + name);
        return json != null ? FootprintSerializer.DeserializeProfile(json) : null;
    }

    /// <summary>
    /// List offset profile names in the active document only.
    /// </summary>
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

    /// <summary>
    /// Retrieve a style set from the active document only.
    /// </summary>
    public static StyleSet? GetStyleSet(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(StyleSetPrefix + name);
        return json != null ? FootprintSerializer.DeserializeStyleSet(json) : null;
    }

    /// <summary>
    /// List style set names in the active document only.
    /// </summary>
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
    /// Resolve a profile by name or inline JSON with provenance.
    /// Resolution order: active document → external database → inline JSON.
    /// Returns null if no source yields a valid profile.
    /// </summary>
    public static OffsetProfile? ResolveProfile(RhinoDoc doc, string nameOrJson)
    {
        return ResolveProfile(doc, nameOrJson, out _);
    }

    /// <summary>
    /// Resolve a profile by name or inline JSON with provenance.
    /// </summary>
    public static OffsetProfile? ResolveProfile(RhinoDoc doc, string nameOrJson, out string source)
    {
        // 1. Active document
        var docJson = doc.Strings.GetValue(ProfilePrefix + nameOrJson);
        if (docJson != null)
        {
            var profile = FootprintSerializer.DeserializeProfile(docJson);
            if (profile != null) { source = "document"; return profile; }
        }

        // 2. External database
        var extJson = ExternalProfileResolver.GetString(ProfilePrefix + nameOrJson);
        if (extJson != null)
        {
            var profile = FootprintSerializer.DeserializeProfile(extJson);
            if (profile != null) { source = "external_database"; return profile; }
        }

        // 3. Inline JSON
        if (nameOrJson.TrimStart().StartsWith('{'))
        {
            var profile = FootprintSerializer.DeserializeProfile(nameOrJson);
            if (profile != null) { source = "inline"; return profile; }
        }

        source = "none";
        return null;
    }

    /// <summary>
    /// Resolve a style set by name or inline JSON with provenance.
    /// Resolution order: active document → external database → built-in → inline JSON → default.
    /// </summary>
    public static StyleSet ResolveStyleSet(RhinoDoc doc, string? nameOrJson)
    {
        return ResolveStyleSet(doc, nameOrJson, out _);
    }

    /// <summary>
    /// Resolve a style set by name or inline JSON with provenance.
    /// </summary>
    public static StyleSet ResolveStyleSet(RhinoDoc doc, string? nameOrJson, out string source)
    {
        if (string.IsNullOrEmpty(nameOrJson))
        {
            source = "builtin";
            return DefaultStyles.Generic;
        }

        // 1. Active document
        var docJson = doc.Strings.GetValue(StyleSetPrefix + nameOrJson);
        if (docJson != null)
        {
            var styleSet = FootprintSerializer.DeserializeStyleSet(docJson);
            if (styleSet != null) { source = "document"; return styleSet; }
        }

        // 2. External database
        var extJson = ExternalProfileResolver.GetString(StyleSetPrefix + nameOrJson);
        if (extJson != null)
        {
            var styleSet = FootprintSerializer.DeserializeStyleSet(extJson);
            if (styleSet != null) { source = "external_database"; return styleSet; }
        }

        // 3. Built-in defaults
        if (DefaultStyles.All.TryGetValue(nameOrJson, out var builtin))
        {
            source = "builtin";
            return builtin;
        }

        // 4. Inline JSON
        if (nameOrJson.TrimStart().StartsWith('{'))
        {
            var parsed = FootprintSerializer.DeserializeStyleSet(nameOrJson);
            if (parsed != null) { source = "inline"; return parsed; }
        }

        source = "builtin";
        return DefaultStyles.Generic;
    }
}
