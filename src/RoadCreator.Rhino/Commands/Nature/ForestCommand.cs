using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Layers;
using RoadCreator.Rhino.Terrain;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// Grid-based random forest placement on a surface with terrain projection.
/// Converts from Les.rvb / LesMesh.rvb (unified via ITerrain).
///
/// Note: VBScript has the user draw a polyline boundary and creates a planar surface
/// from it (then deletes both after placement). This C# version expects the user to
/// pre-select an existing planar surface, which is more flexible.
///
/// Algorithm:
///   1. Select planar surface defining the forest area
///   2. Select terrain for vertical projection
///   3. Collect tree templates from "Tree Database" layer
///   4. Divide surface domain into grid cells at user-specified density
///   5. At each cell: jitter ±density/2, project to terrain, place random tree
///      with random rotation (0-360°) and scale variation
/// </summary>
[System.Runtime.InteropServices.Guid("B1000001-C2D3-E4F5-A6B7-C8D9E0F1A201")]
public class ForestCommand : Command
{
    public override string EnglishName => "RC_Forest";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dbCheck = Database.ExternalDatabase.ValidateConfiguration();
        if (dbCheck != null) return dbCheck.Value;

        // Select planar surface for forest area
        var getSurface = new GetObject();
        getSurface.SetCommandPrompt(Strings.SelectForestSurface);
        getSurface.GeometryFilter = ObjectType.Surface | ObjectType.Brep;
        if (getSurface.Get() != GetResult.Object)
            return Result.Cancel;
        var areaSurface = getSurface.Object(0).Brep();
        if (areaSurface == null) return Result.Cancel;

        // Select terrain for projection
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectForestTerrain);
        getTerrain.GeometryFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Mesh;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;
        var terrain = TerrainFactory.FromRhinoObject(getTerrain.Object(0).Object());
        if (terrain == null)
        {
            RhinoApp.WriteLine(Strings.ErrorInvalidTerrain);
            return Result.Failure;
        }

        // Get density
        var getDensity = new GetNumber();
        getDensity.SetCommandPrompt(Strings.EnterDensity);
        getDensity.SetDefaultNumber(ForestGridComputer.DefaultDensity);
        getDensity.SetLowerLimit(ForestGridComputer.MinDensity, false);
        getDensity.SetUpperLimit(ForestGridComputer.MaxDensity, false);
        if (getDensity.Get() != GetResult.Number)
            return Result.Cancel;
        double density = getDensity.Number();

        // Get scale variance
        var getScale = new GetNumber();
        getScale.SetCommandPrompt(Strings.EnterScaleVariance);
        getScale.SetDefaultNumber(20.0);
        getScale.SetLowerLimit(0, true);
        getScale.SetUpperLimit(100, false);
        if (getScale.Get() != GetResult.Number)
            return Result.Cancel;
        double scalePercent = getScale.Number();

        // Collect trees from database
        var trees = TreePlacementHelper.CollectTreeTemplates(doc);
        if (trees == null || trees.Length == 0)
        {
            RhinoApp.WriteLine(Strings.TreeDatabaseEmpty);
            return Result.Failure;
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            var layers = new LayerManager(doc);
            int treeLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Trees),
                System.Drawing.Color.FromArgb(0, 190, 0));
            var treeAttrs = new ObjectAttributes { LayerIndex = treeLayerIdx };

            // Get surface domain
            var face = areaSurface.Faces[0];
            var domainU = face.Domain(0);
            var domainV = face.Domain(1);

            var rng = new Random();
            int placedCount = 0;

            var origins = ForestGridComputer.ComputeGridOrigins(
                domainU.T0, domainU.T1, domainV.T0, domainV.T1, density);

            foreach (var (u, v) in origins)
            {
                // Evaluate surface at grid point
                var surfPt = face.PointAt(u, v);

                // Check if point is on surface
                face.ClosestPoint(surfPt, out double cu, out double cv);
                if (face.IsPointOnFace(cu, cv) == PointFaceRelation.Exterior)
                    continue;

                // Apply random jitter
                double jx = RandomPlacementComputer.ApplyGridJitter(surfPt.X, density, rng.NextDouble());
                double jy = RandomPlacementComputer.ApplyGridJitter(surfPt.Y, density, rng.NextDouble());

                // Project to terrain
                var projected = terrain.ProjectPointDown(
                    new Core.Math.Point3(jx, jy, surfPt.Z + 1000));
                if (projected == null) continue;
                var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

                // Random tree, rotation, scale
                int treeIdx = RandomPlacementComputer.SelectTreeIndex(trees.Length, rng.NextDouble());
                double rotation = RandomPlacementComputer.ComputeRotationDegrees(rng.NextDouble());
                double scale = RandomPlacementComputer.ComputeScale(scalePercent, rng.NextDouble());

                TreePlacementHelper.PlaceTree(doc, trees[treeIdx], placePt, rotation, scale, treeAttrs);
                placedCount++;
            }

            RhinoApp.WriteLine(string.Format(Strings.ForestCreated, placedCount));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

}
