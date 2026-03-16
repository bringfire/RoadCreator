using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Database;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Stores objects in the RoadCreator database layer with a companion base point.
/// Converts from Databazevlozit.rvb.
///
/// Algorithm:
///   1. Ensure RC_Database layer exists
///   2. Check for existing named objects (prevent duplicates)
///   3. Select objects and base point
///   4. Enter a unique name
///   5. Create companion point named "{Name}-point(RoadCreator)"
///   6. Move objects and companion point to RC_Database layer
/// </summary>
[System.Runtime.InteropServices.Guid("A100000A-B2C3-D4E5-F6A7-B8C9D0E1F20A")]
public class DatabaseInsertCommand : Command
{
    public override string EnglishName => "RC_DatabaseInsert";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dbCheck = ExternalDatabase.ValidateConfiguration();
        if (dbCheck != null) return dbCheck.Value;

        var layers = new LayerManager(doc);
        string dbPath = LayerScheme.BuildPath(LayerScheme.Database);

        // Collect existing names to check for duplicates
        HashSet<string> existingNames;
        if (ExternalDatabase.IsEnabled)
        {
            existingNames = new HashSet<string>(
                ExternalDatabase.ListTemplateNames(LayerScheme.Database),
                StringComparer.Ordinal);
        }
        else
        {
            int dbLayerIdx = layers.EnsureLayer(dbPath,
                System.Drawing.Color.FromArgb(0, 0, 0));
            existingNames = CollectExistingNames(doc, dbLayerIdx);
            layers.LockLayer(dbPath);
            layers.SetLayerVisible(dbPath, false);
        }

        // Select objects
        var getObjects = new GetObject();
        getObjects.SetCommandPrompt(Strings.SelectObjectsForDatabase);
        if (getObjects.GetMultiple(1, 0) != GetResult.Object)
            return Result.Cancel;

        var objectIds = new Guid[getObjects.ObjectCount];
        for (int i = 0; i < getObjects.ObjectCount; i++)
            objectIds[i] = getObjects.Object(i).ObjectId;

        // Select base point
        var getBase = new GetPoint();
        getBase.SetCommandPrompt(Strings.SelectDatabaseBasePoint);
        if (getBase.Get() != GetResult.Point)
            return Result.Cancel;
        var basePoint = getBase.Point();

        // Enter name
        var getName = new GetString();
        getName.SetCommandPrompt(Strings.EnterDatabaseObjectName);
        if (getName.Get() != GetResult.String)
            return Result.Cancel;
        string objectName = getName.StringResult().Trim();

        if (string.IsNullOrEmpty(objectName))
            return Result.Cancel;

        // Check for duplicate
        if (existingNames.Contains(objectName))
        {
            RhinoApp.WriteLine(Strings.DatabaseNameExists);
            return Result.Failure;
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            if (ExternalDatabase.IsEnabled)
            {
                // Gather geometry from selected objects
                var geometries = new List<GeometryBase>();
                foreach (var objId in objectIds)
                {
                    var obj = doc.Objects.FindId(objId);
                    if (obj?.Geometry == null) continue;
                    geometries.Add(obj.Geometry.Duplicate());
                }

                if (geometries.Count == 0)
                    return Result.Failure;

                bool ok = ExternalDatabase.InsertTemplate(
                    LayerScheme.Database, objectName, geometries.ToArray(), basePoint, doc);
                if (!ok)
                {
                    RhinoApp.WriteLine("Failed to write to external database.");
                    return Result.Failure;
                }
            }
            else
            {
                int dbLayerIdx = layers.EnsureLayer(dbPath,
                    System.Drawing.Color.FromArgb(0, 0, 0));

                // Unlock database layer for modification
                var dbLayer = doc.Layers[dbLayerIdx];
                dbLayer.IsLocked = false;
                dbLayer.IsVisible = true;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);

                // Create companion point
                var companionName = DatabaseNaming.GetCompanionPointName(objectName);
                var pointId = doc.Objects.AddPoint(basePoint);
                var pointObj = doc.Objects.FindId(pointId);
                if (pointObj != null)
                {
                    var ptAttrs = pointObj.Attributes;
                    ptAttrs.Name = companionName;
                    ptAttrs.LayerIndex = dbLayerIdx;
                    doc.Objects.ModifyAttributes(pointId, ptAttrs, true);
                }

                // Set name and layer for all selected objects
                foreach (var objId in objectIds)
                {
                    var obj = doc.Objects.FindId(objId);
                    if (obj == null) continue;
                    var objAttrs = obj.Attributes;
                    objAttrs.Name = objectName;
                    objAttrs.LayerIndex = dbLayerIdx;
                    doc.Objects.ModifyAttributes(objId, objAttrs, true);
                }

                // Lock and hide database layer
                layers.LockLayer(dbPath);
                layers.SetLayerVisible(dbPath, false);
            }

            RhinoApp.WriteLine(Strings.DatabaseInsertDone);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static HashSet<string> CollectExistingNames(RhinoDoc doc, int dbLayerIdx)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var layer = doc.Layers[dbLayerIdx];
        if (layer == null) return names;
        var layerObjects = doc.Objects.FindByLayer(layer);
        if (layerObjects == null) return names;

        foreach (var obj in layerObjects)
        {
            if (obj.Geometry is PointCloud || obj.Geometry is global::Rhino.Geometry.Point)
                continue;
            var name = obj.Attributes.Name;
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }
}
