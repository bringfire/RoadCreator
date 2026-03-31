using Rhino.FileIO;

namespace RoadCreator.Rhino.Database;

/// <summary>
/// Reads string-table entries from the external .3dm database file.
/// Caches the string snapshot per file path + last-write-time to avoid
/// repeatedly opening a network .3dm on every profile lookup.
///
/// This is a read-only resolver — it never writes to the external file.
/// Callers decide whether to persist resolved values into the active document.
/// </summary>
internal static class ExternalProfileResolver
{
    private static readonly object Lock = new();
    private static string? _cachedPath;
    private static DateTime _cachedWriteTime;
    private static Dictionary<string, string>? _cachedStrings;

    /// <summary>
    /// Look up a string-table key in the external database.
    /// Returns null if no external database is configured, the file is missing,
    /// or the key is not found.
    /// </summary>
    public static string? GetString(string key)
    {
        var snapshot = GetSnapshot();
        if (snapshot == null)
            return null;

        return snapshot.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// List all keys in the external database that start with the given prefix.
    /// Returns the suffix after the prefix for each matching key.
    /// </summary>
    public static IReadOnlyList<string> ListKeys(string prefix)
    {
        var snapshot = GetSnapshot();
        if (snapshot == null)
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var key in snapshot.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                result.Add(key[prefix.Length..]);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Invalidate the cache. Call when the external database path changes.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (Lock)
        {
            _cachedPath = null;
            _cachedStrings = null;
        }
    }

    private static Dictionary<string, string>? GetSnapshot()
    {
        if (!ExternalDatabase.IsEnabled)
            return null;

        var path = ExternalDatabase.Path!;
        DateTime writeTime;
        try
        {
            writeTime = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return null;
        }

        lock (Lock)
        {
            if (_cachedStrings != null
                && _cachedPath == path
                && _cachedWriteTime == writeTime)
            {
                return _cachedStrings;
            }
        }

        // Read outside the lock — File3dm.Read can be slow on network paths
        Dictionary<string, string>? snapshot = null;
        try
        {
            using var file = File3dm.Read(path);
            if (file != null)
            {
                snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
                int count = file.Strings.Count;
                for (int i = 0; i < count; i++)
                {
                    string k = file.Strings.GetKey(i);
                    string? v = file.Strings.GetValue(k);
                    if (v != null)
                        snapshot[k] = v;
                }
            }
        }
        catch
        {
            return null;
        }

        lock (Lock)
        {
            _cachedPath = path;
            _cachedWriteTime = writeTime;
            _cachedStrings = snapshot;
        }

        return snapshot;
    }
}
