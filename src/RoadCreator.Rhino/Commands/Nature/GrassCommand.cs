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
/// Grass patch placement along road edges with terrain projection.
/// Converts from Trava.rvb / TravaMesh.rvb (unified via ITerrain).
///
/// Algorithm:
///   1. Select road edge curve and offset direction
///   2. Select terrain
///   3. Create 2 offset curves at 2m and 4m
///   4. Divide each at 0.8m intervals
///   5. At each point: place 4m horizontal line, rotate randomly 0-360°
///   6. Project line to terrain
///   7. Extrude 0.5m vertically → grass surface
/// </summary>
[System.Runtime.InteropServices.Guid("B1000004-C2D3-E4F5-A6B7-C8D9E0F1A204")]
public class GrassCommand : Command
{
    public override string EnglishName => "RC_Grass";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select road edge curve
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectGrassEdgeCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var edgeCurve = getCurve.Object(0).Curve();

        // Select direction point
        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectGrassDirection);
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

        doc.Views.RedrawEnabled = false;

        try
        {
            var layers = new LayerManager(doc);
            int grassLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Grass),
                System.Drawing.Color.FromArgb(0, 200, 0));
            var grassAttrs = new ObjectAttributes { LayerIndex = grassLayerIdx };

            double tolerance = doc.ModelAbsoluteTolerance;
            var rng = new Random();
            int patchCount = 0;

            var flatCurve = Curve.ProjectToPlane(edgeCurve, Plane.WorldXY);
            if (flatCurve == null) flatCurve = edgeCurve;

            var offsets = GrassComputer.GetOffsetDistances();
            var (lineStart, lineEnd) = GrassComputer.GetBaseLineEndpoints();
            var extVec = GrassComputer.GetExtrusionVector();

            foreach (double dist in offsets)
            {
                var offsetCurves = flatCurve.Offset(
                    new Point3d(dirPoint.X, dirPoint.Y, 0),
                    Vector3d.ZAxis, dist, tolerance, CurveOffsetCornerStyle.Sharp);
                if (offsetCurves == null || offsetCurves.Length == 0) continue;

                var offsetCurve = offsetCurves[0];

                // includeEnds=true: includes both start and endpoint.
                offsetCurve.DivideByLength(GrassComputer.PointSpacing, true, out Point3d[] divPoints);
                if (divPoints == null) continue;

                foreach (var pt in divPoints)
                {
                    // Create base line at point
                    var p1 = new Point3d(pt.X + lineStart.X, pt.Y + lineStart.Y, pt.Z);
                    var p2 = new Point3d(pt.X + lineEnd.X, pt.Y + lineEnd.Y, pt.Z);
                    var baseLine = new LineCurve(p1, p2);

                    // Random rotation
                    double rotation = RandomPlacementComputer.ComputeRotationDegrees(rng.NextDouble());
                    baseLine.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);

                    // Project endpoints to terrain. VBScript projects the full curve,
                    // but for a 4m line the endpoint-only approach is acceptable.
                    var proj1 = terrain.ProjectPointDown(
                        new Core.Math.Point3(baseLine.PointAtStart.X, baseLine.PointAtStart.Y,
                            baseLine.PointAtStart.Z + 1000));
                    var proj2 = terrain.ProjectPointDown(
                        new Core.Math.Point3(baseLine.PointAtEnd.X, baseLine.PointAtEnd.Y,
                            baseLine.PointAtEnd.Z + 1000));
                    if (proj1 == null || proj2 == null) continue;

                    var projLine = new LineCurve(
                        new Point3d(proj1.Value.X, proj1.Value.Y, proj1.Value.Z),
                        new Point3d(proj2.Value.X, proj2.Value.Y, proj2.Value.Z));

                    // Extrude 0.5m vertically
                    var extDir = new Vector3d(extVec.X, extVec.Y, extVec.Z);
                    var surface = Surface.CreateExtrusion(projLine, extDir);
                    if (surface != null)
                    {
                        doc.Objects.AddSurface(surface, grassAttrs);
                        patchCount++;
                    }
                }
            }

            RhinoApp.WriteLine(string.Format(Strings.GrassCreated, patchCount));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
