using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Signs;
using RoadCreator.Rhino.Database;
using RoadCreator.Rhino.Layers;
using RoadCreator.Rhino.Terrain;

namespace RoadCreator.Rhino.Commands.Signs;

/// <summary>
/// Place a traffic sign from the database onto terrain with rotation.
/// Consolidates 14 identical Dopravniznacky/*.rvb scripts into one parameterized command.
///
/// Algorithm (matches VBScript exactly):
///   1. Select terrain (surface or mesh)
///   2. Pick placement point
///   3. Project point down to terrain
///   4. User selects sign from available signs in the database
///   5. Unlock "RC_Database" layer, copy sign objects from base point to projected point
///   6. Move copies to "Traffic Signs" layer
///   7. User picks rotation direction point
///   8. Rotate copies by atan2(angle) + 90°
///   9. Name copies "TrafficSign"
///
/// Deviation from VBScript:
///   S1: The original had 14 separate scripts, each hardcoding a sign ID and base point.
///       This command lets the user pick any sign from the database.
///   S2: Uses companion points for base position when available, falling back to
///       legacy hardcoded offsets from TrafficSignCatalog.
///   S3: Uses ITerrain abstraction instead of separate Surface/Mesh code paths.
///   S4: VBScript projects from the user point in +Z direction. This version projects
///       downward from +1000m above, which is more robust when the user clicks above
///       the terrain. Matches the ProjectPointDown pattern used by other commands.
///   S5: VBScript searches for objects globally by name (Rhino.ObjectsByName). This
///       version scopes the search to the database layer only, preventing accidental
///       pickup of identically-named objects on other layers.
/// </summary>
[System.Runtime.InteropServices.Guid("B2000001-C2D3-E4F5-A6B7-C8D9E0F1A301")]
public class TrafficSignCommand : Command
{
    public override string EnglishName => "RC_TrafficSign";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // 1. Select terrain
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectTerrain);
        getTerrain.GeometryFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Mesh;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;
        var terrain = TerrainFactory.FromRhinoObject(getTerrain.Object(0).Object());
        if (terrain == null)
        {
            RhinoApp.WriteLine(Strings.ErrorInvalidTerrain);
            return Result.Failure;
        }

        // 2. Pick placement point
        var getPoint = new GetPoint();
        getPoint.SetCommandPrompt(Strings.SelectSignPlacementPoint);
        if (getPoint.Get() != GetResult.Point)
            return Result.Cancel;
        var userPoint = getPoint.Point();

        // 3. Project to terrain
        var projected = terrain.ProjectPointDown(
            new Core.Math.Point3(userPoint.X, userPoint.Y, userPoint.Z + 1000));
        if (projected == null)
        {
            RhinoApp.WriteLine(Strings.ErrorInvalidTerrain);
            return Result.Failure;
        }
        var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

        // 4. Find available signs — external database or document layers
        var layers = new LayerManager(doc);
        string dbPath = LayerScheme.BuildPath(LayerScheme.Database);

        string[] nameList;
        bool usingExternal = ExternalDatabase.IsEnabled;
        int dbLayerIdx = -1;
        bool wasLocked = false;
        bool wasHidden = false;

        if (usingExternal)
        {
            nameList = ExternalDatabase.ListTemplateNames(LayerScheme.Database);
            if (nameList.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SignDatabaseEmpty);
                return Result.Failure;
            }
        }
        else
        {
            dbLayerIdx = doc.Layers.FindByFullPath(dbPath, -1);
            if (dbLayerIdx < 0)
            {
                RhinoApp.WriteLine(Strings.SignDatabaseNotFound);
                return Result.Failure;
            }

            var dbLayer = doc.Layers[dbLayerIdx];
            wasLocked = dbLayer.IsLocked;
            wasHidden = !dbLayer.IsVisible;
            dbLayer.IsLocked = false;
            dbLayer.IsVisible = true;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);

            var layerObjects = doc.Objects.FindByLayer(dbLayer);
            if (layerObjects == null || layerObjects.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SignDatabaseEmpty);
                dbLayer = doc.Layers[dbLayerIdx];
                if (wasLocked) dbLayer.IsLocked = true;
                if (wasHidden) dbLayer.IsVisible = false;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);
                return Result.Failure;
            }

            var signNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obj in layerObjects)
            {
                var name = obj.Attributes.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (DatabaseNaming.IsCompanionPointName(name)) continue;
                if (obj.Geometry is global::Rhino.Geometry.Point) continue;
                signNames.Add(name);
            }

            if (signNames.Count == 0)
            {
                RhinoApp.WriteLine(Strings.SignDatabaseEmpty);
                dbLayer = doc.Layers[dbLayerIdx];
                if (wasLocked) dbLayer.IsLocked = true;
                if (wasHidden) dbLayer.IsVisible = false;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);
                return Result.Failure;
            }

            dbLayer = doc.Layers[dbLayerIdx];
            dbLayer.IsLocked = true;
            dbLayer.IsVisible = false;
            doc.Layers.Modify(dbLayer, dbLayerIdx, true);

            nameList = signNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        // 5. User selects sign
        var getChoice = new GetOption();
        getChoice.SetCommandPrompt(Strings.SelectSignFromDatabase);
        foreach (var name in nameList)
            getChoice.AddOption(name.Replace(" ", "_"));
        if (getChoice.Get() != GetResult.Option)
            return Result.Cancel;
        int choiceIndex = getChoice.Option().Index - 1;
        if (choiceIndex < 0 || choiceIndex >= nameList.Length)
            return Result.Cancel;
        string selectedSign = nameList[choiceIndex];

        try
        {
            // Get geometries + base point — external or document
            GeometryBase[] geometries;
            Point3d basePoint;

            if (usingExternal)
            {
                var result = ExternalDatabase.FindObjectsByName(LayerScheme.Database, selectedSign);
                if (result == null)
                {
                    RhinoApp.WriteLine(Strings.SignDatabaseEmpty);
                    return Result.Failure;
                }
                geometries = ExternalDatabase.ResolveBlockDefinitions(doc, result.Value.Geometries);
                basePoint = result.Value.BasePoint;

                // If no companion point in external file, try legacy catalog
                if (basePoint == Point3d.Origin &&
                    TrafficSignCatalog.TryGetLegacyBasePoint(selectedSign, out var legacy))
                    basePoint = new Point3d(legacy.X, legacy.Y, legacy.Z);
            }
            else
            {
                var dbLayer = doc.Layers[dbLayerIdx];
                dbLayer.IsLocked = false;
                dbLayer.IsVisible = true;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);

                var layerObjects = doc.Objects.FindByLayer(dbLayer);
                bool foundCompanion = false;
                basePoint = Point3d.Origin;
                string companionName = DatabaseNaming.GetCompanionPointName(selectedSign);
                foreach (var obj in layerObjects!)
                {
                    if (obj.Attributes.Name == companionName && obj.Geometry is global::Rhino.Geometry.Point pt)
                    {
                        basePoint = pt.Location;
                        foundCompanion = true;
                        break;
                    }
                }

                if (!foundCompanion)
                {
                    if (TrafficSignCatalog.TryGetLegacyBasePoint(selectedSign, out var legacy))
                        basePoint = new Point3d(legacy.X, legacy.Y, legacy.Z);
                }

                geometries = layerObjects!
                    .Where(o => o.Attributes.Name == selectedSign && o.Geometry != null)
                    .Select(o => o.Geometry!.Duplicate())
                    .ToArray();
            }

            if (geometries.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SignDatabaseEmpty);
                return Result.Failure;
            }

            doc.Views.RedrawEnabled = false;

            // 6. Copy sign objects to placement point
            int signLayerIdx = layers.EnsureLayer(
                TrafficSignCatalog.PlacementLayerName,
                System.Drawing.Color.FromArgb(0, 0, 0));
            var signAttrs = new ObjectAttributes
            {
                LayerIndex = signLayerIdx,
                Name = TrafficSignCatalog.PlacedSignName,
            };

            var copiedIds = new List<Guid>();
            foreach (var geom in geometries)
            {
                var copy = geom.Duplicate();
                copy.Transform(Transform.Translation(placePt - basePoint));
                var id = doc.Objects.Add(copy, signAttrs);
                if (id != Guid.Empty)
                    copiedIds.Add(id);
            }

            // Re-enable redraw so user can see placed sign and pick rotation
            doc.Views.RedrawEnabled = true;
            doc.Views.Redraw();

            // 7. Pick rotation direction
            var getRotation = new GetPoint();
            getRotation.SetCommandPrompt(Strings.SelectSignRotationPoint);
            getRotation.SetBasePoint(placePt, true);
            if (getRotation.Get() == GetResult.Point)
            {
                var rotPt = getRotation.Point();
                double rotation = TrafficSignCatalog.ComputeSignRotation(
                    placePt.X, placePt.Y, rotPt.X, rotPt.Y);

                foreach (var id in copiedIds)
                {
                    doc.Objects.Transform(id,
                        Transform.Rotation(rotation * System.Math.PI / 180.0,
                            Vector3d.ZAxis, placePt), true);
                }
            }

            RhinoApp.WriteLine(Strings.TrafficSignPlaced);
        }
        finally
        {
            // Restore database layer state (only relevant for document layers)
            if (!usingExternal && dbLayerIdx >= 0)
            {
                var dbLayer = doc.Layers[dbLayerIdx];
                if (wasLocked) dbLayer.IsLocked = true;
                if (wasHidden) dbLayer.IsVisible = false;
                doc.Layers.Modify(dbLayer, dbLayerIdx, true);
            }
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
