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
/// Assembles a 3D route centerline from a horizontal alignment curve and a grade line (niveleta).
/// Converts from RC2_VytvoreniTrasy_CZ.rvb / RoadCreator2 3D route creation.
///
/// RC2 enhancements over gen-1:
///   - Auto-selects road from available roads (route selection dialog)
///   - Auto-detects axis from Tangent Polygon layer (last segment or joined curve)
///   - Auto-detects grade line (niveleta) from Grade Line layer
///   - Auto-detects reference elevation from Longitudinal Profile datum point
///   - Projects important points (ZP, PO, OP, KP, ZU, KU) from Stationing Points to 3D
///   - Completeness check: warns if 3D Route already exists
///   - Creates "3D Important Points" sublayer for projected stationing
///   - Falls back to manual selection if auto-detection fails
///
/// Algorithm:
///   1. Select road (auto-detect or user picks)
///   2. Auto-find axis, grade line, datum (with manual fallback)
///   3. Check if 3D route already exists (completeness guard)
///   4. Divide horizontal alignment curve into stations every 2 meters
///   5. For each station, sample elevation from grade line via vertical line intersection
///   6. Combine horizontal (X, Y) with vertical (Z) to produce 3D points
///   7. Create interpolated 3D curve through the points
///   8. Project important points from Stationing Points to 3D curve
/// </summary>
[System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8B9C-0D1E-2F3A4B5C6D7E")]
public class Assemble3DRouteCommand : Command
{
    private sealed record Assemble3DRouteInputs(
        string? RoadName,
        Curve HorizontalCurve,
        Curve GradeLine,
        double ReferenceElevation);

    public override string EnglishName => "RC_Assemble3DRoute";

    private const double StationSpacing = 2.0;

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        // --- Step 1: Select road ---
        string? roadName = null;
        Curve? hCurve = null;
        Curve? gradeLine = null;
        double referenceElevation = 0;

        var (choice, selectedRoad) = RouteDiscovery.PromptRouteSelection(doc, "Manual");
        if (choice == RoutePromptResult.Cancelled)
            return Result.Cancel;
        if (choice == RoutePromptResult.RoadSelected)
        {
            roadName = selectedRoad;

            // Auto-detect axis
            hCurve = RouteDiscovery.FindFullAxisCurve(doc, roadName);
            if (hCurve == null)
            {
                RhinoApp.WriteLine(string.Format(Strings.AxisNotFound, roadName));
                return Result.Failure;
            }

            // Auto-detect grade line
            gradeLine = RouteDiscovery.FindGradeLine(doc, roadName);
            if (gradeLine == null)
                RhinoApp.WriteLine(string.Format(Strings.GradeLineNotFound, roadName));

            // Auto-detect reference elevation
            var datum = RouteDiscovery.FindProfileDatum(doc, roadName);
            if (datum.HasValue)
            {
                referenceElevation = datum.Value;
                RhinoApp.WriteLine(string.Format(Strings.DatumAutoDetected, referenceElevation));
            }

            // Completeness check
            if (RouteDiscovery.HasRoute3D(doc, roadName))
            {
                RhinoApp.WriteLine(string.Format(Strings.Route3DAlreadyExists, roadName));
                return Result.Failure;
            }
        }
        // NoRoads or Alternative → fall through to manual selection

        // --- Step 2: Manual selection fallback ---
        if (hCurve == null)
        {
            var getH = new GetObject();
            getH.SetCommandPrompt(Strings.SelectHorizontalAlignment);
            getH.GeometryFilter = ObjectType.Curve;
            if (getH.Get() != GetResult.Object)
                return Result.Cancel;
            hCurve = getH.Object(0).Curve();
            var hObject = getH.Object(0).Object();
            if (hCurve == null || hObject == null)
                return Result.Cancel;
            if (roadName == null)
                roadName = RoadObjectNaming.ParseRoadName(hObject.Attributes.Name) ?? "";
        }

        if (gradeLine == null)
        {
            var getG = new GetObject();
            getG.SetCommandPrompt(Strings.SelectGradeLine);
            getG.GeometryFilter = ObjectType.Curve;
            if (getG.Get() != GetResult.Object)
                return Result.Cancel;
            gradeLine = getG.Object(0).Curve();
            if (gradeLine == null)
                return Result.Cancel;
        }

        if (referenceElevation == 0)
        {
            var getElev = new GetNumber();
            getElev.SetCommandPrompt(Strings.EnterReferenceElevation);
            getElev.SetDefaultNumber(0);
            if (getElev.Get() != GetResult.Number)
                return Result.Cancel;
            referenceElevation = getElev.Number();
        }

        return RunCore(doc, new Assemble3DRouteInputs(
            roadName,
            hCurve,
            gradeLine,
            referenceElevation));
    }

    private Result RunScripted(RhinoDoc doc)
    {
        var getRoad = new GetString();
        getRoad.SetCommandPrompt("Road name");
        if (getRoad.Get() != GetResult.String)
            return Result.Cancel;

        string roadName = getRoad.StringResult().Trim();
        if (string.IsNullOrEmpty(roadName))
        {
            RhinoApp.WriteLine("Road name is required in scripted mode.");
            return Result.Failure;
        }

        var hCurve = RouteDiscovery.FindFullAxisCurve(doc, roadName);
        if (hCurve == null)
        {
            RhinoApp.WriteLine(string.Format(Strings.AxisNotFound, roadName));
            return Result.Failure;
        }

        var gradeLine = RouteDiscovery.FindGradeLine(doc, roadName);
        if (gradeLine == null)
        {
            RhinoApp.WriteLine(string.Format(Strings.GradeLineNotFound, roadName));
            return Result.Failure;
        }

        if (RouteDiscovery.HasRoute3D(doc, roadName))
        {
            RhinoApp.WriteLine(string.Format(Strings.Route3DAlreadyExists, roadName));
            return Result.Failure;
        }

        double referenceElevation = 0;
        var datum = RouteDiscovery.FindProfileDatum(doc, roadName);
        if (datum.HasValue)
        {
            referenceElevation = datum.Value;
            RhinoApp.WriteLine(string.Format(Strings.DatumAutoDetected, referenceElevation));
        }
        else
        {
            var getElev = new GetNumber();
            getElev.SetCommandPrompt(Strings.EnterReferenceElevation);
            getElev.SetDefaultNumber(0);
            if (getElev.Get() != GetResult.Number)
                return Result.Cancel;
            referenceElevation = getElev.Number();
        }

        return RunCore(doc, new Assemble3DRouteInputs(
            roadName,
            hCurve,
            gradeLine,
            referenceElevation));
    }

    private Result RunCore(RhinoDoc doc, Assemble3DRouteInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            // --- Step 3: Divide horizontal curve and sample elevations ---
            double curveLength = inputs.HorizontalCurve.GetLength();
            if (curveLength < StationSpacing)
            {
                RhinoApp.WriteLine("Horizontal alignment curve is too short.");
                return Result.Failure;
            }

            int stationCount = (int)(curveLength / StationSpacing) + 1;
            var route3DPoints = new List<Point3d>();

            for (int i = 0; i < stationCount; i++)
            {
                double dist = i * StationSpacing;
                if (dist > curveLength)
                    dist = curveLength;

                if (!inputs.HorizontalCurve.LengthParameter(dist, out double param))
                    continue;
                var hPoint = inputs.HorizontalCurve.PointAt(param);

                double elevation = RouteDiscovery.SampleProfileElevation(inputs.GradeLine, dist,
                    inputs.ReferenceElevation, doc.ModelAbsoluteTolerance) ?? inputs.ReferenceElevation;

                route3DPoints.Add(new Point3d(hPoint.X, hPoint.Y, elevation));
            }

            // Add last point if not already included
            if ((stationCount - 1) * StationSpacing < curveLength - 1e-10)
            {
                if (inputs.HorizontalCurve.LengthParameter(curveLength, out double lastParam))
                {
                    var lastHPoint = inputs.HorizontalCurve.PointAt(lastParam);
                    double lastElev = RouteDiscovery.SampleProfileElevation(inputs.GradeLine, curveLength,
                        inputs.ReferenceElevation, doc.ModelAbsoluteTolerance) ?? inputs.ReferenceElevation;
                    route3DPoints.Add(new Point3d(lastHPoint.X, lastHPoint.Y, lastElev));
                }
            }

            if (route3DPoints.Count < 2)
            {
                RhinoApp.WriteLine("Could not generate enough 3D route points.");
                return Result.Failure;
            }

            // --- Step 4: Create 3D curve ---
            var route3D = Curve.CreateInterpolatedCurve(route3DPoints, 3);

            var layers = new LayerManager(doc);
            string routePath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName, LayerScheme.Route3D);
            int routeLayerIdx = layers.EnsureLayer(routePath, System.Drawing.Color.Blue);

            var routeAttrs = new ObjectAttributes
            {
                LayerIndex = routeLayerIdx,
                Name = RoadObjectNaming.BuildRoute3DName(inputs.RoadName ?? "")
            };
            doc.Objects.AddCurve(route3D, routeAttrs);

            // --- Step 5: Project important points to 3D ---
            int importantPointCount = 0;
            if (!string.IsNullOrEmpty(inputs.RoadName))
            {
                var stationingPts = RouteDiscovery.FindStationingPoints(doc, inputs.RoadName);
                if (stationingPts.Length > 0)
                {
                    string impPtsPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName,
                        LayerScheme.Route3D, LayerScheme.ImportantPoints);
                    int impPtsLayerIdx = layers.EnsureLayer(impPtsPath,
                        System.Drawing.Color.FromArgb(0, 0, 255));

                    foreach (var (name, location) in stationingPts)
                    {
                        // Find the closest point on the 3D route curve
                        if (!route3D.ClosestPoint(new Point3d(location.X, location.Y, 0),
                                out double t))
                            continue;

                        var pt3D = route3D.PointAt(t);
                        var ptAttrs = new ObjectAttributes
                        {
                            LayerIndex = impPtsLayerIdx,
                            Name = name + " 3D"
                        };
                        doc.Objects.AddPoint(pt3D, ptAttrs);
                        importantPointCount++;
                    }
                }
            }

            // --- Step 6: Add station points ---
            string ptPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName, LayerScheme.Route3D, LayerScheme.RoutePoints);
            int ptLayerIdx = layers.EnsureLayer(ptPath, System.Drawing.Color.LightBlue);

            var ptAttrs2 = new ObjectAttributes { LayerIndex = ptLayerIdx };
            foreach (var pt in route3DPoints)
                doc.Objects.AddPoint(pt, ptAttrs2);

            RhinoApp.WriteLine(string.Format(Strings.Route3DCreatedWithPoints,
                route3DPoints.Count, curveLength, importantPointCount));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
