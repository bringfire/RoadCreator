using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// Retrieves a tree from the tree database, places it with interactive rotation.
/// Converts from StromDatabazevyber.rvb.
///
/// Algorithm:
///   1. Scan "Stromy databaze" layer for named tree objects
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
        var layers = new LayerManager(doc);
        int dbLayerIdx = doc.Layers.FindByFullPath(TreeDatabaseNaming.LayerName, -1);

        if (dbLayerIdx < 0)
        {
            RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
            return Result.Failure;
        }

        // Unlock and show database layer temporarily
        var dbLayer = doc.Layers[dbLayerIdx];
        dbLayer.IsLocked = false;
        dbLayer.IsVisible = true;
        doc.Layers.Modify(dbLayer, dbLayerIdx, true);

        // Collect unique tree names (exclude companion points)
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

        // Lock and hide for selection UI
        layers.LockLayer(TreeDatabaseNaming.LayerName);
        layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);

        // Present selection via command-line options
        var nameList = objectNames.ToArray();
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
            // Unlock database to read objects
            dbLayer = doc.Layers[dbLayerIdx];
            dbLayer.IsLocked = false;
            dbLayer.IsVisible = true;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);

            // Find companion point for base position
            string companionName = DatabaseNaming.GetCompanionPointName(selectedName);
            var companionSettings = new ObjectEnumeratorSettings
            {
                LayerIndexFilter = dbLayerIdx,
                NameFilter = companionName,
            };
            Point3d basePoint = Point3d.Origin;
            foreach (var ptObj in doc.Objects.GetObjectList(companionSettings))
            {
                if (ptObj.Geometry is global::Rhino.Geometry.Point pt)
                {
                    basePoint = pt.Location;
                    break;
                }
            }

            // Find all objects with the selected name
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

            // VBScript: output layer is "Stromy z databaze" (Trees from database)
            int targetLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Trees),
                System.Drawing.Color.FromArgb(0, 190, 0));
            var targetAttrs = new ObjectAttributes { LayerIndex = targetLayerIdx };

            // Copy objects
            var copiedIds = new List<Guid>();
            foreach (var srcObj in sourceObjects)
            {
                if (srcObj.Geometry == null) continue;
                var copy = srcObj.Geometry.Duplicate();
                copy.Transform(Transform.Translation(placementPt - basePoint));
                targetAttrs.Name = selectedName + "Strom";
                var id = doc.Objects.Add(copy, targetAttrs);
                if (id != Guid.Empty)
                    copiedIds.Add(id);
            }

            // Lock and hide database
            layers.LockLayer(TreeDatabaseNaming.LayerName);
            layers.SetLayerVisible(TreeDatabaseNaming.LayerName, false);

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
                // VBScript uses Rhino.Angle which returns [0, 360] counterclockwise.
                // Atan2 returns [-180, 180]. Both produce the same rotation when
                // combined with the +90 offset for typical placement scenarios.
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
