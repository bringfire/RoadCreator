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
/// Interactive single-object placement with random rotation and scale.
/// Converts from Magiccopy.rvb / MagiccopyMesh.rvb (unified via ITerrain).
///
/// Note: VBScript allows free object selection from the viewport. This C# version
/// uses tree templates from the "Stromy databaze" layer instead, which requires
/// objects to be stored in the database first (via RC_TreeDatabaseInsert).
/// Use RC_MagicCopyMulti for manual object selection without the database.
///
/// Algorithm:
///   1. Select terrain
///   2. Collect tree templates from database
///   3. Enter scale variance
///   4. Loop: click to place → random tree, rotation, scale → project to terrain
///   5. Escape to finish
/// </summary>
[System.Runtime.InteropServices.Guid("B1000005-C2D3-E4F5-A6B7-C8D9E0F1A205")]
public class MagicCopyCommand : Command
{
    public override string EnglishName => "RC_MagicCopy";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select terrain
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

        var layers = new LayerManager(doc);
        int treeLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Trees),
            System.Drawing.Color.FromArgb(0, 190, 0));
        var treeAttrs = new ObjectAttributes { LayerIndex = treeLayerIdx };

        var rng = new Random();
        int copyCount = 0;

        // Interactive placement loop
        while (true)
        {
            var getPoint = new GetPoint();
            getPoint.SetCommandPrompt(Strings.ClickToPlace);
            getPoint.AcceptNothing(true);
            var result = getPoint.Get();

            if (result == GetResult.Nothing || result != GetResult.Point)
                break;

            var clickPt = getPoint.Point();

            // Project to terrain
            var projected = terrain.ProjectPointDown(
                new Core.Math.Point3(clickPt.X, clickPt.Y, clickPt.Z + 1000));
            if (projected == null) continue;
            var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

            int treeIdx = RandomPlacementComputer.SelectTreeIndex(trees.Length, rng.NextDouble());
            double rotation = RandomPlacementComputer.ComputeRotationDegrees(rng.NextDouble());
            double scale = RandomPlacementComputer.ComputeScale(scalePercent, rng.NextDouble());

            TreePlacementHelper.PlaceTree(doc, trees[treeIdx], placePt, rotation, scale, treeAttrs);
            copyCount++;
            doc.Views.Redraw();
        }

        RhinoApp.WriteLine(string.Format(Strings.MagicCopyDone, copyCount));
        return Result.Success;
    }

}
