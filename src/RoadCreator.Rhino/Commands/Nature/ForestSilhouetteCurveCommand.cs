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
/// Forest silhouette along an existing curve with adaptive spacing.
/// Converts from Leskulisakrivka.rvb / LeskulisakrivkaMesh.rvb (unified via ITerrain).
///
/// Algorithm:
///   1. Select guide curve and offset direction
///   2. Select terrain
///   3. Enter spacing (default 5m)
///   4. Create 3 offset curves at adaptive distances (3*i + spacing/0.8)
///   5. Divide each at user spacing
///   6. At each point: ±2m jitter, project to terrain, place random tree
/// </summary>
[System.Runtime.InteropServices.Guid("B1000003-C2D3-E4F5-A6B7-C8D9E0F1A203")]
public class ForestSilhouetteCurveCommand : Command
{
    public override string EnglishName => "RC_ForestSilhouetteCurve";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select guide curve
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();

        // Select direction point (offset side)
        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectDirectionPoint);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var dirPoint = getDir.Point();

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

        // Get spacing
        var getSpacing = new GetNumber();
        getSpacing.SetCommandPrompt(Strings.EnterSpacing);
        getSpacing.SetDefaultNumber(ForestSilhouetteComputer.DefaultAdaptiveSpacing);
        getSpacing.SetLowerLimit(0.5, false);
        getSpacing.SetUpperLimit(100, false);
        if (getSpacing.Get() != GetResult.Number)
            return Result.Cancel;
        double spacing = getSpacing.Number();

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

            double tolerance = doc.ModelAbsoluteTolerance;
            var rng = new Random();
            int placedCount = 0;

            var flatCurve = Curve.ProjectToPlane(guideCurve, Plane.WorldXY);
            if (flatCurve == null) flatCurve = guideCurve;

            var offsets = ForestSilhouetteComputer.GetAdaptiveOffsetDistances(spacing);

            foreach (double dist in offsets)
            {
                var offsetCurves = flatCurve.Offset(
                    new Point3d(dirPoint.X, dirPoint.Y, 0),
                    Vector3d.ZAxis, dist, tolerance, CurveOffsetCornerStyle.Sharp);
                if (offsetCurves == null || offsetCurves.Length == 0) continue;

                var offsetCurve = offsetCurves[0];

                // includeEnds=true: includes both start and endpoint.
                // Adaptive mode: point spacing equals user spacing.
                offsetCurve.DivideByLength(spacing, true, out Point3d[] divPoints);
                if (divPoints == null) continue;

                foreach (var pt in divPoints)
                {
                    var (jx, jy) = ForestSilhouetteComputer.ApplyJitter(
                        pt.X, pt.Y, rng.NextDouble(), rng.NextDouble());

                    var projected = terrain.ProjectPointDown(
                        new Core.Math.Point3(jx, jy, pt.Z + 1000));
                    if (projected == null) continue;
                    var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

                    int treeIdx = RandomPlacementComputer.SelectTreeIndex(trees.Length, rng.NextDouble());
                    double rotation = RandomPlacementComputer.ComputeRotationDegrees(rng.NextDouble());
                    double scale = RandomPlacementComputer.ComputeScale(scalePercent, rng.NextDouble());

                    TreePlacementHelper.PlaceTree(doc, trees[treeIdx], placePt, rotation, scale, treeAttrs);
                    placedCount++;
                }
            }

            RhinoApp.WriteLine(string.Format(Strings.ForestSilhouetteCreated, placedCount));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

}
