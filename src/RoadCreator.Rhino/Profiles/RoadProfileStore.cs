using global::Rhino;
using RoadCreator.Core.Profiles;
using RoadCreator.Rhino.Database;

namespace RoadCreator.Rhino.Profiles;

/// <summary>
/// Persists RoadProfileDefinition inside the .3dm document using RhinoDoc.Strings.
///
/// Uses a separate keyspace from the OffsetProfile store:
///   "RC_RoadProfile::{name}" → serialized RoadProfileDefinition JSON
///
/// Also writes a native-friendly summary to the same document string table:
///   "RC_RoadProfileSummary::{name}" → summary JSON readable by C++ via
///   CRhinoDoc::GetUserString(). This is a derived cache — the canonical
///   source of truth is always the RC_RoadProfile:: entry.
///
/// Does not collapse with RC_Profile:: — the two keyspaces are independent.
/// </summary>
public static class RoadProfileStore
{
    private const string Prefix = "RC_RoadProfile::";
    private const string SummaryPrefix = "RC_RoadProfileSummary::";
    private const string SummarySection = "RC_RoadProfileSummary";

    /// <summary>
    /// Store a canonical road profile. Returns whether an existing profile was overwritten.
    /// Also writes a native-friendly summary to document user text (C++ readable).
    /// </summary>
    public static bool Store(RhinoDoc doc, RoadProfileDefinition profile)
    {
        string key = Prefix + profile.Name;
        bool existed = doc.Strings.GetValue(key) != null;
        var json = RoadProfileSerializer.Serialize(profile);
        doc.Strings.SetString(key, json);

        WriteSummary(doc, profile);

        return existed;
    }

    /// <summary>
    /// Retrieve a road profile from the active document only.
    /// Lazily backfills the native summary if missing or schema-stale.
    /// </summary>
    public static RoadProfileDefinition? Get(RhinoDoc doc, string name)
    {
        var json = doc.Strings.GetValue(Prefix + name);
        if (json == null) return null;
        var profile = RoadProfileSerializer.Deserialize(json);
        if (profile == null) return null;

        EnsureSummary(doc, profile);
        return profile;
    }

    /// <summary>
    /// Check whether a road profile exists in the active document.
    /// </summary>
    public static bool Exists(RhinoDoc doc, string name)
    {
        return doc.Strings.GetValue(Prefix + name) != null;
    }

    /// <summary>
    /// List road profile names in the active document only.
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

    // ── Resolution (read-through for "use" paths) ─────────────────────────────

    /// <summary>
    /// Resolve a road profile for use (e.g., intersection, road surface, footprint).
    /// Resolution order: active document → external database.
    /// Does NOT persist external database profiles into the active document.
    /// </summary>
    public static RoadProfileDefinition? Resolve(RhinoDoc doc, string name)
    {
        return Resolve(doc, name, out _);
    }

    /// <summary>
    /// Resolve a road profile for use, with provenance.
    /// </summary>
    public static RoadProfileDefinition? Resolve(RhinoDoc doc, string name, out string source)
    {
        // 1. Active document
        var profile = Get(doc, name);
        if (profile != null)
        {
            source = "document";
            return profile;
        }

        // 2. External database
        var extJson = ExternalProfileResolver.GetString(Prefix + name);
        if (extJson != null)
        {
            var extProfile = RoadProfileSerializer.Deserialize(extJson);
            if (extProfile != null)
            {
                source = "external_database";
                return extProfile;
            }
        }

        source = "none";
        return null;
    }

    /// <summary>
    /// Delete a stored road profile. Returns true if it existed.
    /// Also removes the native summary from document user text.
    /// </summary>
    public static bool Delete(RhinoDoc doc, string name)
    {
        string key = Prefix + name;
        bool exists = doc.Strings.GetValue(key) != null;
        if (exists) doc.Strings.Delete(key);
        doc.Strings.Delete(SummaryPrefix + name);
        doc.Strings.Delete(SummarySection, name);
        return exists;
    }

    private static void WriteSummary(RhinoDoc doc, RoadProfileDefinition profile)
    {
        string summaryJson = RoadProfileNativeSummarySerializer.Serialize(profile);
        WriteSummary(doc, profile.Name, summaryJson);
    }

    private static void EnsureSummary(RhinoDoc doc, RoadProfileDefinition profile)
    {
        string desiredSummary = RoadProfileNativeSummarySerializer.Serialize(profile);
        string? flatSummary = doc.Strings.GetValue(SummaryPrefix + profile.Name);
        string? sectionSummary = doc.Strings.GetValue(SummarySection, profile.Name);

        // Content-aware regeneration keeps old same-schema summaries from staying stale
        // after serializer fixes or summary-shape improvements, and ensures both the
        // flat and sectioned key forms stay synchronized.
        if (string.Equals(flatSummary, desiredSummary, StringComparison.Ordinal)
            && string.Equals(sectionSummary, desiredSummary, StringComparison.Ordinal))
            return;

        WriteSummary(doc, profile.Name, desiredSummary);
    }

    private static void WriteSummary(RhinoDoc doc, string name, string summaryJson)
    {
        // Write both flat and sectioned forms. Native code primarily reads the flat
        // key via CRhinoDoc::GetUserString("RC_RoadProfileSummary::name"), while the
        // sectioned form mirrors the established cross-plugin doc.Strings pattern and
        // gives us a second interoperable path if Rhino normalizes the backing store.
        doc.Strings.SetString(SummaryPrefix + name, summaryJson);
        doc.Strings.SetString(SummarySection, name, summaryJson);
    }
}
