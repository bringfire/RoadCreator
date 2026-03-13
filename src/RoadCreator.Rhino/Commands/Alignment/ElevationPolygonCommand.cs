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
/// Creates an elevation polygon (vertical tangent polygon) from user-drawn polyline.
/// Converts from Road2D/Vyskovypolygon.rvb and RC2_Niveleta_CZ.rvb.
///
/// The vertical profile uses a 2D coordinate system:
///   X = chainage (horizontal distance along route)
///   Y = elevation × VerticalExaggeration (10:1 by default)
///
/// Workflow:
/// 1. User selects a road (or creates standalone profile)
/// 2. User draws polyline in profile space
/// 3. Each segment gets slope/length labels
/// 4. Segments are named for later use by ParabolicCurve command
/// </summary>
[System.Runtime.InteropServices.Guid("A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D")]
public class ElevationPolygonCommand : Command
{
    public override string EnglishName => "RC_ElevationPolygon";

    // Label positioning offsets (matching original VBScript layout)
    private const double LabelXOffset = -10.0;
    private const double LabelDistanceYOffset = 18.0;
    private const double LabelSlopeYOffset = 13.0;
    private const double LabelTextHeight = 3.0;
    private const double VerticalRefLineHeight = 83.0;

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Get polyline from user
        var gp = new GetPoint();
        gp.SetCommandPrompt("Draw elevation polygon (press Enter when done)");
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

            // RC2: Route selection dialog (auto-detect available roads)
            string roadName = "";
            var (routeChoice, selectedRoad) = RouteDiscovery.PromptRouteSelection(doc, "Standalone");
            if (routeChoice == RoutePromptResult.RoadSelected)
                roadName = selectedRoad;

            string glPath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.GradeLine);
            int glLayerIdx = layers.EnsureLayer(glPath, System.Drawing.Color.Red);

            string stPath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.GradeLine, LayerScheme.Stationing);
            int stLayerIdx = layers.EnsureLayer(stPath, System.Drawing.Color.Red);

            // Create grade line segments
            for (int i = 0; i < points.Count - 1; i++)
            {
                var line = new Line(points[i], points[i + 1]);
                var attrs = new ObjectAttributes
                {
                    LayerIndex = glLayerIdx,
                    Name = $"{roadName} grade {i + 1}"
                };
                doc.Objects.AddLine(line, attrs);

                // Compute grade and length (Y is exaggerated)
                double dx = points[i + 1].X - points[i].X;
                double realDy = (points[i + 1].Y - points[i].Y) / ProfileConstants.VerticalExaggeration;
                double grade = (realDy / dx) * 100.0;
                double length = dx;

                // Add labels at segment midpoint
                double midX = (points[i].X + points[i + 1].X) / 2.0;
                double midY = (points[i].Y + points[i + 1].Y) / 2.0;

                var labelAttrs = new ObjectAttributes { LayerIndex = stLayerIdx };
                var lengthText = new TextEntity
                {
                    Plane = new Plane(new Point3d(midX + LabelXOffset, midY + LabelDistanceYOffset, 0), Vector3d.ZAxis),
                    PlainText = $"d={length:F1} m",
                    TextHeight = LabelTextHeight
                };
                doc.Objects.AddText(lengthText, labelAttrs);

                var slopeText = new TextEntity
                {
                    Plane = new Plane(new Point3d(midX + LabelXOffset, midY + LabelSlopeYOffset, 0), Vector3d.ZAxis),
                    PlainText = $"s={grade:F2} %",
                    TextHeight = LabelTextHeight
                };
                doc.Objects.AddText(slopeText, labelAttrs);
            }

            // Add start/end vertical reference lines
            var startLineAttrs = new ObjectAttributes { LayerIndex = stLayerIdx };
            doc.Objects.AddLine(
                new Line(points[0], new Point3d(points[0].X, points[0].Y + VerticalRefLineHeight, 0)),
                startLineAttrs);
            doc.Objects.AddLine(
                new Line(points[^1], new Point3d(points[^1].X, points[^1].Y + VerticalRefLineHeight, 0)),
                startLineAttrs);

            // Lock stationing layer
            layers.LockLayer(stPath);

            RhinoApp.WriteLine($"Elevation polygon created with {points.Count - 1} grade segments.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
