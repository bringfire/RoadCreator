using global::Rhino.FileIO;
using global::Rhino.Geometry;
using global::Rhino.DocObjects;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Commands.Nature;

namespace RoadCreator.Rhino.Database;

/// <summary>
/// Reads and writes template objects from/to an external .3dm file.
/// When enabled, all database commands use this file instead of document layers.
/// </summary>
internal static class ExternalDatabase
{
    private static string? _path;

    /// <summary>
    /// Path to the external .3dm database file. Null = disabled (use document layers).
    /// </summary>
    public static string? Path
    {
        get => _path;
        set => _path = value;
    }

    /// <summary>True if a path is set and the file exists.</summary>
    public static bool IsEnabled => !string.IsNullOrEmpty(_path) && File.Exists(_path);

    /// <summary>
    /// Collect tree templates from the external database file.
    /// Returns null if the file cannot be read or has no trees.
    /// </summary>
    public static TreeTemplate[]? CollectTreeTemplates()
    {
        var templates = CollectTemplatesFromLayer(TreeDatabaseNaming.LayerName);
        if (templates == null || templates.Length == 0)
            return null;
        return templates.Select(t => new TreeTemplate(t.Name, t.Geometries, t.BasePoint)).ToArray();
    }

    /// <summary>
    /// List unique template names on a given layer in the external file.
    /// </summary>
    public static string[] ListTemplateNames(string layerName)
    {
        if (!IsEnabled) return Array.Empty<string>();

        using var file = File3dm.Read(_path!);
        if (file == null) return Array.Empty<string>();

        var objects = GetObjectsOnLayer(file, layerName);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (geometry, attrs) in objects)
        {
            var name = attrs.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (DatabaseNaming.IsCompanionPointName(name)) continue;
            if (geometry is global::Rhino.Geometry.Point) continue;
            names.Add(name);
        }

        return names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Find all objects with a given name on a layer, plus its companion base point.
    /// Returns null if no objects found.
    /// </summary>
    public static (GeometryBase[] Geometries, Point3d BasePoint)? FindObjectsByName(
        string layerName, string name)
    {
        if (!IsEnabled) return null;

        using var file = File3dm.Read(_path!);
        if (file == null) return null;

        var objects = GetObjectsOnLayer(file, layerName);
        var geometries = new List<GeometryBase>();
        Point3d basePoint = Point3d.Origin;
        string companionName = DatabaseNaming.GetCompanionPointName(name);

        foreach (var (geometry, attrs) in objects)
        {
            if (attrs.Name == companionName && geometry is global::Rhino.Geometry.Point pt)
            {
                basePoint = pt.Location;
            }
            else if (attrs.Name == name && geometry is not global::Rhino.Geometry.Point)
            {
                geometries.Add(geometry.Duplicate());
            }
        }

        if (geometries.Count == 0)
            return null;

        return (geometries.ToArray(), basePoint);
    }

    /// <summary>
    /// Insert a template into the external .3dm file.
    /// Creates the target layer if it doesn't exist.
    /// </summary>
    public static bool InsertTemplate(string layerName, string name,
        GeometryBase[] geometries, Point3d basePoint)
    {
        if (string.IsNullOrEmpty(_path)) return false;

        // Read existing or create new
        using var file = File.Exists(_path) ? File3dm.Read(_path) : new File3dm();
        if (file == null) return false;

        // Find or create layer
        int layerIndex = FindLayerIndex(file, layerName);
        if (layerIndex < 0)
        {
            var layer = new Layer { Name = layerName };
            file.AllLayers.Add(layer);
            layerIndex = FindLayerIndex(file, layerName);
        }

        // Check for duplicate names
        foreach (var obj in file.Objects)
        {
            if (obj.Attributes.LayerIndex == layerIndex &&
                obj.Attributes.Name == name &&
                obj.Geometry is not global::Rhino.Geometry.Point)
            {
                return false; // duplicate
            }
        }

        // Add geometry objects
        var attrs = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = name,
        };
        foreach (var geom in geometries)
        {
            file.Objects.Add(geom.Duplicate(), attrs);
        }

        // Add companion point
        var companionAttrs = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = DatabaseNaming.GetCompanionPointName(name),
        };
        file.Objects.AddPoint(basePoint, companionAttrs);

        return file.Write(_path, 8);
    }

    /// <summary>
    /// Collect all templates (name + geometries + base point) from a layer.
    /// </summary>
    internal static DatabaseTemplate[]? CollectTemplatesFromLayer(string layerName)
    {
        if (!IsEnabled) return null;

        using var file = File3dm.Read(_path!);
        if (file == null) return null;

        var objects = GetObjectsOnLayer(file, layerName);
        if (objects.Count == 0)
            return null;

        // Group by name, extract companion points
        var nameToGeometries = new Dictionary<string, List<GeometryBase>>(StringComparer.Ordinal);
        var nameToBase = new Dictionary<string, Point3d>(StringComparer.Ordinal);

        foreach (var (geometry, attrs) in objects)
        {
            var objName = attrs.Name;
            if (string.IsNullOrEmpty(objName)) continue;

            if (DatabaseNaming.IsCompanionPointName(objName) && geometry is global::Rhino.Geometry.Point pt)
            {
                var extracted = DatabaseNaming.ExtractObjectName(objName);
                if (extracted != null)
                    nameToBase[extracted] = pt.Location;
            }
            else if (geometry is not global::Rhino.Geometry.Point)
            {
                if (!nameToGeometries.TryGetValue(objName, out var list))
                {
                    list = new List<GeometryBase>();
                    nameToGeometries[objName] = list;
                }
                list.Add(geometry.Duplicate());
            }
        }

        if (nameToGeometries.Count == 0)
            return null;

        var templates = new List<DatabaseTemplate>();
        foreach (var (name, geomList) in nameToGeometries)
        {
            var bp = nameToBase.TryGetValue(name, out var basePt) ? basePt : Point3d.Origin;
            templates.Add(new DatabaseTemplate(name, geomList.ToArray(), bp));
        }

        return templates.ToArray();
    }

    private static List<(GeometryBase Geometry, ObjectAttributes Attrs)> GetObjectsOnLayer(
        File3dm file, string layerName)
    {
        int layerIndex = FindLayerIndex(file, layerName);
        if (layerIndex < 0) return new();

        var results = new List<(GeometryBase, ObjectAttributes)>();
        foreach (var obj in file.Objects)
        {
            if (obj.Attributes.LayerIndex == layerIndex && obj.Geometry != null)
                results.Add((obj.Geometry, obj.Attributes));
        }
        return results;
    }

    private static int FindLayerIndex(File3dm file, string layerName)
    {
        foreach (var layer in file.AllLayers)
        {
            if (string.Equals(layer.Name, layerName, StringComparison.Ordinal))
                return layer.Index;
        }
        return -1;
    }
}

/// <summary>
/// A template from the database: its geometry and base point.
/// </summary>
internal record DatabaseTemplate(string Name, GeometryBase[] Geometries, Point3d BasePoint);
