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
/// Retrieves a tree from the tree database, places it with interactive rotation.
/// Converts from StromDatabazevyber.rvb.
///
/// Algorithm:
///   1. Scan "Tree Database" layer for named tree objects
///   2. Present list for selection
///   3. Pick placement point
///   4. Copy tree to placement point
///   5. Pick rotation angle point → rotate copies
/// </summary>
[System.Runtime.InteropServices.Guid("B1000008-C2D3-E4F5-A6B7-C8D9E0F1A208")]
public class TreeDatabaseRetrieveCommand : Command
{
    public override string EnglishName => "RC_TreeDatabaseRetrieve";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dbCheck = Database.ExternalDatabase.ValidateConfiguration();
        if (dbCheck != null) return dbCheck.Value;

        var layers = new LayerManager(doc);

        // Collect available names — external database or document layers
        string[] nameList;
        if (ExternalDatabase.IsEnabled)
        {
            nameList = ExternalDatabase.ListTemplateNames(TreeDatabaseNaming.LayerName);
            if (nameList.Length == 0)
            {
                RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
                return Result.Failure;
            }
        }
        else
        {
            int dbLayerIdx = doc.Layers.FindByFullPath(TreeDatabaseNaming.LayerName, -1);
            if (dbLayerIdx < 0)
            {
                RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
                return Result.Failure;
            }

            var dbLayer = doc.Layers[dbLayerIdx];
            dbLayer.IsLocked = false;
            dbLayer.IsVisible = true;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);

            var layerObjects = doc.Objects.FindByLayer(dbLayer);
            if (layerObjects == null || layerObjects.Length == 0)
            {
                RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
                layers.LockLayer(TreeDatabaseNaming.LayerName);
                layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
                return Result.Failure;
            }

            var objectNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obj in layerObjects)
            {
                if (obj.Geometry is global::Rhino.Geometry.Point) continue;
                var name = obj.Attributes.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (DatabaseNaming.IsCompanionPointName(name)) continue;
                objectNames.Add(name);
            }

            if (objectNames.Count == 0)
            {
                RhinoApp.WriteLine(Strings.TreeDatabaseNoTrees);
                layers.LockLayer(TreeDatabaseNaming.LayerName);
                layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
                return Result.Failure;
            }

            layers.LockLayer(TreeDatabaseNaming.LayerName);
            layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);

            nameList = objectNames.ToArray();
        }

        // Present selection via command-line options
        var getChoice = new GetOption();
        getChoice.SetCommandPrompt(Strings.SelectTreeFromDatabase);
        foreach (var name in nameList)
            getChoice.AddOption(name.Replace(" ", "_"));
        if (getChoice.Get() != GetResult.Option)
            return Result.Cancel;
        int choiceIndex = getChoice.Option().Index - 1;
        if (choiceIndex < 0 || choiceIndex >= nameList.Length)
            return Result.Cancel;
        string selectedName = nameList[choiceIndex];

        // Pick placement point
        var getPoint = new GetPoint();
        getPoint.SetCommandPrompt(Strings.SelectPlacementPoint);
        if (getPoint.Get() != GetResult.Point)
            return Result.Cancel;
        var placementPt = getPoint.Point();

        doc.Views.RedrawEnabled = false;

        try
        {
            // Get geometries + base point — external or document
            GeometryBase[] geometries;
            Point3d basePoint;

            if (ExternalDatabase.IsEnabled)
            {
                var result = ExternalDatabase.FindObjectsByName(
                    TreeDatabaseNaming.LayerName, selectedName);
                if (result == null)
                {
                    RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
                    return Result.Failure;
                }
                geometries = ExternalDatabase.ResolveBlockDefinitions(doc, result.Value.Geometries);
                if (geometries.Length == 0)
                {
                    RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
                    return Result.Failure;
                }
                basePoint = result.Value.BasePoint;
            }
            else
            {
                int dbLayerIdx = doc.Layers.FindByFullPath(TreeDatabaseNaming.LayerName, -1);
                var dbLayer = doc.Layers[dbLayerIdx];
                dbLayer.IsLocked = false;
                dbLayer.IsVisible = true;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);

                string companionName = DatabaseNaming.GetCompanionPointName(selectedName);
                var companionSettings = new ObjectEnumeratorSettings
                {
                    LayerIndexFilter = dbLayerIdx,
                    NameFilter = companionName,
                };
                basePoint = Point3d.Origin;
                foreach (var ptObj in doc.Objects.GetObjectList(companionSettings))
                {
                    if (ptObj.Geometry is global::Rhino.Geometry.Point pt)
                    {
                        basePoint = pt.Location;
                        break;
                    }
                }

                var sourceSettings = new ObjectEnumeratorSettings
                {
                    LayerIndexFilter = dbLayerIdx,
                    NameFilter = selectedName,
                };
                var sourceObjects = doc.Objects.GetObjectList(sourceSettings).ToArray();
                if (sourceObjects.Length == 0)
                {
                    layers.LockLayer(TreeDatabaseNaming.LayerName);
                    layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
                    return Result.Failure;
                }

                geometries = sourceObjects
                    .Where(o => o.Geometry != null)
                    .Select(o => o.Geometry!.Duplicate())
                    .ToArray();

                layers.LockLayer(TreeDatabaseNaming.LayerName);
                layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);
            }

            int targetLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Trees),
                System.Drawing.Color.FromArgb(0, 190, 0));
            var targetAttrs = new ObjectAttributes { LayerIndex = targetLayerIdx };

            // Copy objects
            var copiedIds = new List<Guid>();
            foreach (var geom in geometries)
            {
                var copy = geom.Duplicate();
                copy.Transform(Transform.Translation(placementPt - basePoint));
                targetAttrs.Name = selectedName + "Tree";
                var id = doc.Objects.Add(copy, targetAttrs);
                if (id != Guid.Empty)
                    copiedIds.Add(id);
            }

            // Show placed objects and pick rotation
            doc.Views.RedrawEnabled = true;
            doc.Views.Redraw();

            // Pick rotation angle
            var getRotation = new GetPoint();
            getRotation.SetCommandPrompt(Strings.SelectRotationPoint);
            getRotation.SetBasePoint(placementPt, true);
            if (getRotation.Get() == GetResult.Point)
            {
                var rotationPt = getRotation.Point();
                double angle = System.Math.Atan2(
                    rotationPt.Y - placementPt.Y,
                    rotationPt.X - placementPt.X) * (180.0 / System.Math.PI);
                double rotation = angle + 90;

                foreach (var id in copiedIds)
                {
                    doc.Objects.Transform(id,
                        Transform.Rotation(rotation * System.Math.PI / 180.0,
                            Vector3d.ZAxis, placementPt), true);
                }
            }

            RhinoApp.WriteLine(Strings.TreeDatabaseRetrieveDone);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
