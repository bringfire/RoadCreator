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

        // Collect available names — external database or document layers
        string[] nameList;
        if (ExternalDatabase.IsEnabled)
        {
            nameList = ExternalDatabase.ListTemplateNames(LayerScheme.Database);
            if (nameList.Length == 0)
            {
                RhinoApp.WriteLine(Strings.DatabaseEmpty);
                return Result.Failure;
            }
        }
        else
        {
            int dbLayerIdx = doc.Layers.FindByFullPath(dbPath, -1);
            if (dbLayerIdx < 0)
            {
                RhinoApp.WriteLine(Strings.DatabaseEmpty);
                return Result.Failure;
            }

            var dbLayer = doc.Layers[dbLayerIdx];
            dbLayer.IsLocked = false;
            dbLayer.IsVisible = true;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);

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

            layers.LockLayer(dbPath);
            layers.SetLayerVisible(dbPath, false);

            nameList = objectNames.ToArray();
        }

        // Present selection via command-line options
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
            // Get geometries + base point — external or document
            GeometryBase[] geometries;
            Point3d basePoint;

            if (ExternalDatabase.IsEnabled)
            {
                var result = ExternalDatabase.FindObjectsByName(LayerScheme.Database, selectedName);
                if (result == null)
                {
                    RhinoApp.WriteLine(Strings.DatabaseEmpty);
                    return Result.Failure;
                }
                geometries = result.Value.Geometries;
                basePoint = result.Value.BasePoint;
            }
            else
            {
                int dbLayerIdx = doc.Layers.FindByFullPath(dbPath, -1);
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
                    layers.LockLayer(dbPath);
                    layers.SetLayerVisible(dbPath, false);
                    return Result.Failure;
                }

                geometries = sourceObjects
                    .Where(o => o.Geometry != null)
                    .Select(o => o.Geometry!.Duplicate())
                    .ToArray();

                layers.LockLayer(dbPath);
                layers.SetLayerVisible(dbPath, false);
            }

            // Set up target layer
            int targetLayerIdx = layers.EnsureLayer(
                LayerScheme.BuildPath(LayerScheme.DatabaseObjects),
                System.Drawing.Color.FromArgb(0, 0, 0));
            var targetAttrs = new ObjectAttributes { LayerIndex = targetLayerIdx };

            // Copy objects from base to placement point
            var copiedIds = new List<Guid>();
            foreach (var geom in geometries)
            {
                var copy = geom.Duplicate();
                copy.Transform(Transform.Translation(placementPt - basePoint));
                targetAttrs.Name = selectedName + "-Objekt";
                var id = doc.Objects.Add(copy, targetAttrs);
                if (id != Guid.Empty)
                    copiedIds.Add(id);
            }

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
