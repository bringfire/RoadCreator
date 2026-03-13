using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Alignment;

/// <summary>
/// Creates a tangent polygon (alignment baseline) from user-drawn polyline.
/// Converts from RC2_Tecnovypolygon_CZ.rvb.
///
/// Workflow:
/// 1. User draws polyline defining the alignment
/// 2. Each segment becomes a named line on its own layer
/// 3. Layer hierarchy: RoadCreator::Road_N::Tangent Polygon::Stationing
/// 4. Start point (ZU) is marked with a stationing label
/// </summary>
[System.Runtime.InteropServices.Guid("B3A1C5D7-E9F2-4A6B-8C0D-1E3F5A7B9C2D")]
public class TangentPolygonCommand : Command
{
    public override string EnglishName => "RC_TangentPolygon";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Get polyline from user
        var gp = new GetPoint();
        gp.SetCommandPrompt("Draw tangent polygon (press Enter when done)");
        gp.AcceptNothing(true);

        var points = new List<Point3d>();
        var previewLines = new List<Guid>();

        while (true)
        {
            if (points.Count > 0)
                gp.SetBasePoint(points[^1], true);

            var result = gp.Get();

            if (result == GetResult.Nothing || result == GetResult.Cancel)
                break;

            if (result != GetResult.Point)
                break;

            var pt = gp.Point();
            pt = new Point3d(pt.X, pt.Y, 0); // Flatten to XY
            points.Add(pt);

            if (points.Count > 1)
            {
                var lineId = doc.Objects.AddLine(points[^2], points[^1]);
                previewLines.Add(lineId);
            }
        }

        // Clean up preview lines
        foreach (var id in previewLines)
            doc.Objects.Delete(id, true);

        if (points.Count < 2)
        {
            RhinoApp.WriteLine(Strings.ErrorNoSelection);
            return Result.Cancel;
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            var layers = new LayerManager(doc);

            // Find next available road name
            string roadName = RoadObjectNaming.GetNextRoadName(
                name => doc.Layers.FindByFullPath(LayerScheme.BuildPath(name), -1) >= 0);
            string tpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon);
            string stPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, LayerScheme.Stationing);
            string stPtsPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, LayerScheme.Stationing, LayerScheme.StationingPoints);

            layers.EnsureLayer(tpPath, System.Drawing.Color.Red);
            layers.EnsureLayer(stPath, System.Drawing.Color.Red);
            layers.EnsureLayer(stPtsPath, System.Drawing.Color.Red);
            string legendPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, "Legend");
            layers.EnsureLayer(legendPath, System.Drawing.Color.Red);

            int tpLayerIndex = doc.Layers.FindByFullPath(tpPath, -1);
            int stLayerIndex = doc.Layers.FindByFullPath(stPath, -1);
            int stPtsLayerIndex = doc.Layers.FindByFullPath(stPtsPath, -1);

            // Create line segments
            for (int i = 0; i < points.Count - 1; i++)
            {
                var line = new Line(points[i], points[i + 1]);
                var attrs = new ObjectAttributes
                {
                    LayerIndex = tpLayerIndex,
                    Name = RoadObjectNaming.BuildSegmentName(roadName, i + 1)
                };
                doc.Objects.AddLine(line, attrs);
            }

            // Add start stationing marker (ZU)
            var startAngle = Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
            var startAngleDeg = startAngle * 180.0 / Math.PI;

            // Stationing tick line
            const double TickLength = 33.0; // model-unit length for stationing tick mark
            var tickLine = new Line(Point3d.Origin, new Point3d(TickLength, 0, 0));
            var tickXform = Transform.Rotation(
                (startAngleDeg - 90) * Math.PI / 180.0,
                Vector3d.ZAxis,
                Point3d.Origin);
            tickXform = Transform.Translation(new Vector3d(points[0])) * tickXform;
            var tickCurve = new LineCurve(tickLine);
            tickCurve.Transform(tickXform);

            var tickAttrs = new ObjectAttributes { LayerIndex = stLayerIndex };
            doc.Objects.AddCurve(tickCurve, tickAttrs);

            // ZU label — offset perpendicular to the first tangent direction
            var textAttrs = new ObjectAttributes { LayerIndex = stLayerIndex };
            var perpOffset = new Vector3d(-Math.Sin(startAngle), Math.Cos(startAngle), 0) * 2.0;
            var textPt = new Point3d(points[0].X + perpOffset.X, points[0].Y + perpOffset.Y, 0);
            var textEntity = new TextEntity
            {
                Plane = new Plane(textPt, Vector3d.ZAxis),
                PlainText = "ZU km 0.000000",
                TextHeight = 3.0
            };
            doc.Objects.AddText(textEntity, textAttrs);

            // ZU point object
            var zuAttrs = new ObjectAttributes
            {
                LayerIndex = stPtsLayerIndex,
                Name = RoadObjectNaming.BuildStationingName(
                    roadName, 0, "R", RoadObjectNaming.TypeCodes.Start, 0.0)
            };
            doc.Objects.AddPoint(points[0], zuAttrs);

            // Lock stationing layers
            layers.LockLayer(stPtsPath);
            layers.LockLayer(stPath);
            layers.LockLayer(legendPath);

            RhinoApp.WriteLine($"Tangent polygon created: {roadName} with {points.Count - 1} segments.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
