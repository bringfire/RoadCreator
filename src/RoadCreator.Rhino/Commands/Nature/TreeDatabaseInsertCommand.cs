using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Database;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// Stores tree objects in the tree database layer ("Tree Database")
/// with a companion base point.
/// Converts from StromDatabazevlozit.rvb.
///
/// Same algorithm as RC_DatabaseInsert but uses the tree-specific layer
/// and naming conventions from <see cref="TreeDatabaseNaming"/>.
/// </summary>
[System.Runtime.InteropServices.Guid("B1000007-C2D3-E4F5-A6B7-C8D9E0F1A207")]
public class TreeDatabaseInsertCommand : Command
{
    public override string EnglishName => "RC_TreeDatabaseInsert";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var layers = new LayerManager(doc);

        // Collect existing names to check for duplicates
        HashSet<string> existingNames;
        if (ExternalDatabase.IsEnabled)
        {
            existingNames = new HashSet<string>(
                ExternalDatabase.ListTemplateNames(TreeDatabaseNaming.LayerName),
                StringComparer.Ordinal);
        }
        else
        {
            int dbLayerIdx = layers.EnsureLayer(TreeDatabaseNaming.LayerName,
                System.Drawing.Color.FromArgb(0, 0, 0));
            existingNames = CollectExistingNames(doc, dbLayerIdx);
            layers.LockLayer(TreeDatabaseNaming.LayerName);
            layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
        }

        // Select objects
        var getObjects = new GetObject();
        getObjects.SetCommandPrompt(Strings.SelectTreesForDatabase);
        if (getObjects.GetMultiple(1, 0) != GetResult.Object)
            return Result.Cancel;

        var objectIds = new Guid[getObjects.ObjectCount];
        for (int i = 0; i < getObjects.ObjectCount; i++)
            objectIds[i] = getObjects.Object(i).ObjectId;

        // Select base point
        var getBase = new GetPoint();
        getBase.SetCommandPrompt(Strings.SelectTreeDatabaseBasePoint);
        if (getBase.Get() != GetResult.Point)
            return Result.Cancel;
        var basePoint = getBase.Point();

        // Enter name
        var getName = new GetString();
        getName.SetCommandPrompt(Strings.EnterTreeName);
        if (getName.Get() != GetResult.String)
            return Result.Cancel;
        string treeName = getName.StringResult().Trim();

        if (string.IsNullOrEmpty(treeName))
            return Result.Cancel;

        // Check for duplicate
        if (existingNames.Contains(treeName))
        {
            RhinoApp.WriteLine(Strings.TreeDatabaseNameExists);
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
                    TreeDatabaseNaming.LayerName, treeName, geometries.ToArray(), basePoint, doc);
                if (!ok)
                {
                    RhinoApp.WriteLine("Failed to write to external database.");
                    return Result.Failure;
                }
            }
            else
            {
                int dbLayerIdx = layers.EnsureLayer(TreeDatabaseNaming.LayerName,
                    System.Drawing.Color.FromArgb(0, 0, 0));

                // Unlock database layer for modification
                var dbLayer = doc.Layers[dbLayerIdx];
                dbLayer.IsLocked = false;
                dbLayer.IsVisible = true;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);

                // Create companion point
                var companionName = DatabaseNaming.GetCompanionPointName(treeName);
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
                    objAttrs.Name = treeName;
                    objAttrs.LayerIndex = dbLayerIdx;
                    doc.Objects.ModifyAttributes(objId, objAttrs, true);
                }

                // Lock and hide database layer
                layers.LockLayer(TreeDatabaseNaming.LayerName);
                layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
            }

            RhinoApp.WriteLine(Strings.TreeDatabaseInsertDone);
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
            if (string.IsNullOrEmpty(name)) continue;
            if (DatabaseNaming.IsCompanionPointName(name)) continue;
            names.Add(name);
        }

        return names;
    }
}
