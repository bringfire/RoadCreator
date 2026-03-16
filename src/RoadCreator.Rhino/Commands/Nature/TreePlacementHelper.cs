using global::Rhino;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Database;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// A tree template from the database: its geometry and base point.
/// </summary>
internal record TreeTemplate(string Name, GeometryBase[] Geometries, Point3d BasePoint);

/// <summary>
/// Shared utilities for tree placement across forest, silhouette, and magic copy commands.
/// </summary>
internal static class TreePlacementHelper
{
    /// <summary>
    /// Copy and transform tree template objects to a placement point with rotation and scale.
    /// </summary>
    internal static void PlaceTree(RhinoDoc doc, TreeTemplate tree,
        Point3d placePt, double rotationDeg, double scale, ObjectAttributes attrs)
    {
        var moveXform = Transform.Translation(placePt - tree.BasePoint);
        var rotXform = Transform.Rotation(rotationDeg * System.Math.PI / 180.0,
            Vector3d.ZAxis, placePt);
        var scaleXform = Transform.Scale(placePt, scale);
        var xform = scaleXform * rotXform * moveXform;

        foreach (var geom in tree.Geometries)
        {
            var copy = geom.Duplicate();
            copy.Transform(xform);
            doc.Objects.Add(copy, attrs);
        }
    }

    /// <summary>
    /// Collect tree templates from the external database or the "Tree Database" layer.
    /// </summary>
    internal static TreeTemplate[]? CollectTreeTemplates(RhinoDoc doc)
    {
        // External database takes priority
        if (ExternalDatabase.IsEnabled)
            return ExternalDatabase.CollectTreeTemplates();

        // Fall back to document layers
        string dbLayerName = TreeDatabaseNaming.LayerName;
        int dbLayerIdx = doc.Layers.FindByFullPath(dbLayerName, -1);
        if (dbLayerIdx < 0)
            dbLayerIdx = doc.Layers.FindByFullPath(
                LayerScheme.BuildPath(LayerScheme.TreeDatabase), -1);
        if (dbLayerIdx < 0) return null;

        var dbLayer = doc.Layers[dbLayerIdx];
        bool wasLocked = dbLayer.IsLocked;
        bool wasHidden = !dbLayer.IsVisible;
        dbLayer.IsLocked = false;
        dbLayer.IsVisible = true;
        doc.Layers.Modify(dbLayer, dbLayerIdx, true);

        try
        {
            var layerObjects = doc.Objects.FindByLayer(dbLayer);
            if (layerObjects == null || layerObjects.Length == 0) return null;

            var nameToGeometries = new Dictionary<string, List<GeometryBase>>(StringComparer.Ordinal);
            var nameToBase = new Dictionary<string, Point3d>(StringComparer.Ordinal);

            foreach (var obj in layerObjects)
            {
                var name = obj.Attributes.Name;
                if (string.IsNullOrEmpty(name)) continue;

                if (Core.Accessories.DatabaseNaming.IsCompanionPointName(name))
                {
                    if (obj.Geometry is global::Rhino.Geometry.Point pt)
                    {
                        var objName = Core.Accessories.DatabaseNaming.ExtractObjectName(name);
                        if (objName != null)
                            nameToBase[objName] = pt.Location;
                    }
                    continue;
                }

                if (!nameToGeometries.TryGetValue(name, out var list))
                {
                    list = new List<GeometryBase>();
                    nameToGeometries[name] = list;
                }
                list.Add(obj.Geometry.Duplicate());
            }

            var templates = new List<TreeTemplate>();
            foreach (var kvp in nameToGeometries)
            {
                var basePt = nameToBase.TryGetValue(kvp.Key, out var bp) ? bp : Point3d.Origin;
                templates.Add(new TreeTemplate(kvp.Key, kvp.Value.ToArray(), basePt));
            }

            return templates.Count > 0 ? templates.ToArray() : null;
        }
        finally
        {
            dbLayer = doc.Layers[dbLayerIdx];
            if (wasLocked) dbLayer.IsLocked = true;
            if (wasHidden) dbLayer.IsVisible = false;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);
        }
    }
}
