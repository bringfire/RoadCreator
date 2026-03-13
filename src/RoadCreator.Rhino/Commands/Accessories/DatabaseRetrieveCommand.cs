using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Retrieves a named object from the database, copies it to a placement point
/// with optional rotation.
/// Converts from Databazevyber.rvb.
///
/// Algorithm:
///   1. Scan RC_Database layer for named objects (exclude points)
///   2. Present list to user for selection
///   3. Optionally select terrain surface (for projected placement)
///   4. Pick placement point
///   5. Copy database objects to placement point
///   6. Pick rotation angle point
///   7. Rotate copies around placement point
/// </summary>
[System.Runtime.InteropServices.Guid("A100000B-B2C3-D4E5-F6A7-B8C9D0E1F20B")]
public class DatabaseRetrieveCommand : Command
{
    public override string EnglishName => "RC_DatabaseRetrieve";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var layers = new LayerManager(doc);
        string dbPath = LayerScheme.BuildPath(LayerScheme.Database);
        int dbLayerIdx = doc.Layers.FindByFullPath(dbPath, -1);

        if (dbLayerIdx < 0)
        {
            RhinoApp.WriteLine(Strings.DatabaseEmpty);
            return Result.Failure;
        }

        // Unlock and show database layer temporarily
        var dbLayer = doc.Layers[dbLayerIdx];
        dbLayer.IsLocked = false;
        dbLayer.IsVisible = true;
        doc.Layers.Modify(dbLayer, dbLayerIdx, true);

        // Collect unique object names (exclude points and companion point names)
        var layerObjects = doc.Objects.FindByLayer(dbLayer);
        if (layerObjects == null || layerObjects.Length == 0)
        {
            RhinoApp.WriteLine(Strings.DatabaseEmpty);
            layers.LockLayer(dbPath);
            layers.SetLayerVisible(dbPath, false);
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
            RhinoApp.WriteLine(Strings.DatabaseNoNamedObjects);
            layers.LockLayer(dbPath);
            layers.SetLayerVisible(dbPath, false);
            return Result.Failure;
        }

        // Lock and hide database layer for selection UI
        layers.LockLayer(dbPath);
        layers.SetLayerVisible(dbPath, false);

        // Present selection via command-line options.
        // Rhino GetOption doesn't support spaces in option names, so we replace
        // spaces with underscores for display but use the index to look up the
        // original name from the array.
        var nameList = objectNames.ToArray();
        var getChoice = new GetOption();
        getChoice.SetCommandPrompt("Select object from database");
        foreach (var name in nameList)
            getChoice.AddOption(name.Replace(" ", "_"));
        if (getChoice.Get() != GetResult.Option)
            return Result.Cancel;
        int choiceIndex = getChoice.Option().Index - 1;
        if (choiceIndex < 0 || choiceIndex >= nameList.Length)
            return Result.Cancel;
        string selectedName = nameList[choiceIndex];

        // Optional: select terrain
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectTerrainForPlacement);
        getTerrain.GeometryFilter = ObjectType.Brep | ObjectType.Surface | ObjectType.Mesh;
        getTerrain.AcceptNothing(true);
        var terrainResult = getTerrain.Get();

        // Pick placement point
        Point3d placementPt;
        if (terrainResult == GetResult.Object)
        {
            var getPoint = new GetPoint();
            getPoint.SetCommandPrompt(Strings.SelectPlacementPointOnTerrain);
            if (getPoint.Get() != GetResult.Point)
                return Result.Cancel;
            placementPt = getPoint.Point();
        }
        else
        {
            var getPoint = new GetPoint();
            getPoint.SetCommandPrompt(Strings.SelectPlacementPoint);
            if (getPoint.Get() != GetResult.Point)
                return Result.Cancel;
            placementPt = getPoint.Point();
        }

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
                layers.LockLayer(dbPath);
                layers.SetLayerVisible(dbPath, false);
                return Result.Failure;
            }

            // Set up target layer
            int targetLayerIdx = layers.EnsureLayer(
                LayerScheme.BuildPath(LayerScheme.DatabaseObjects),
                System.Drawing.Color.FromArgb(0, 0, 0));
            var targetAttrs = new ObjectAttributes { LayerIndex = targetLayerIdx };

            // Copy objects from base to placement point
            var copiedIds = new List<Guid>();
            foreach (var srcObj in sourceObjects)
            {
                if (srcObj.Geometry == null) continue;
                var copy = srcObj.Geometry.Duplicate();
                copy.Transform(Transform.Translation(placementPt - basePoint));
                targetAttrs.Name = selectedName + "-Objekt";
                var id = doc.Objects.Add(copy, targetAttrs);
                if (id != Guid.Empty)
                    copiedIds.Add(id);
            }

            // Lock and hide database layer
            layers.LockLayer(dbPath);
            layers.SetLayerVisible(dbPath, false);

            // Re-enable redraw so user can see placed objects and pick rotation
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
                // VBScript: angle(0) + 90
                double rotation = angle + 90;

                foreach (var id in copiedIds)
                {
                    doc.Objects.Transform(id,
                        Transform.Rotation(rotation * System.Math.PI / 180.0,
                            Vector3d.ZAxis, placementPt), true);
                }
            }

            RhinoApp.WriteLine(Strings.DatabaseRetrieveDone);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
