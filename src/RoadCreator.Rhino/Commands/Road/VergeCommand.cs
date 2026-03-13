using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Verge;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Road;

/// <summary>
/// Creates a road verge (shoulder) surface by sweeping an 8% slope profile along a road edge.
/// Converts from Krajnice.rvb.
///
/// Algorithm:
///   1. Select road edge curve to sweep along
///   2. Pick direction point to determine outward side
///   3. Enter verge width (default 0.5m)
///   4. Build profile line: [0,0] → [width, -width×0.08]
///   5. Orient profile perpendicular to curve at start
///   6. Test direction: flip 180° if profile points toward direction point
///   7. Sweep profile along the edge curve
/// </summary>
[System.Runtime.InteropServices.Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public class VergeCommand : Command
{
    private sealed record VergeInputs(
        string RoadName,
        Curve EdgeCurve,
        Point3d DirectionPoint,
        double VergeWidth);

    public override string EnglishName => "RC_Verge";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        // Select curve to sweep along
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectSweepCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var edge = getCurve.Object(0).Curve();
        var edgeObj = getCurve.Object(0).Object();
        if (edge == null || edgeObj == null)
            return Result.Cancel;

        // Pick direction point (outward side)
        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectDirection);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var directionPt = getDir.Point();

        // Get verge width
        var getWidth = new GetNumber();
        getWidth.SetCommandPrompt(Strings.EnterVergeWidth);
        getWidth.SetDefaultNumber(0.5);
        getWidth.SetLowerLimit(0.1, false);
        getWidth.SetUpperLimit(20.0, false);
        if (getWidth.Get() != GetResult.Number)
            return Result.Cancel;
        double vergeWidth = getWidth.Number();

        // Parse road name from edge object
        string roadName = RoadObjectNaming.ParseRoadName(edgeObj.Attributes.Name) ?? "";

        return RunCore(doc, new VergeInputs(roadName, edge, directionPt, vergeWidth));
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

        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectDirection);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var directionPt = getDir.Point();

        var getWidth = new GetNumber();
        getWidth.SetCommandPrompt(Strings.EnterVergeWidth);
        getWidth.SetDefaultNumber(0.5);
        getWidth.SetLowerLimit(0.1, false);
        getWidth.SetUpperLimit(20.0, false);
        if (getWidth.Get() != GetResult.Number)
            return Result.Cancel;
        double vergeWidth = getWidth.Number();

        var edge = ResolveBoundaryCurve(doc, roadName, directionPt);
        if (edge == null)
        {
            RhinoApp.WriteLine($"No road boundary curve found for road '{roadName}'.");
            return Result.Failure;
        }

        return RunCore(doc, new VergeInputs(roadName, edge, directionPt, vergeWidth));
    }

    private Result RunCore(RhinoDoc doc, VergeInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            var profilePts = VergeProfileComputer.ComputeVergeProfile(inputs.VergeWidth);
            var profileCurve = CreateProfileCurve(profilePts);

            if (!inputs.EdgeCurve.ClosestPoint(inputs.DirectionPoint, out double closestT))
                return Result.Failure;
            var testPt = inputs.EdgeCurve.PointAt(closestT);
            double testAngle = GetTangentAngle(inputs.EdgeCurve, closestT);

            var testProfile = CreateProfileCurve(profilePts);
            OrientProfile(testProfile, testPt, testAngle + 90);

            bool needsFlip = ShouldFlipToward(testProfile, testPt, inputs.DirectionPoint);

            // Place final profile at curve start for sweep
            var startPt = inputs.EdgeCurve.PointAtStart;
            double startAngle = GetTangentAngle(inputs.EdgeCurve, inputs.EdgeCurve.Domain.Min);
            OrientProfile(profileCurve, startPt, startAngle + 90);

            if (needsFlip)
                profileCurve.Rotate(System.Math.PI, Vector3d.ZAxis, startPt);

            // Layer setup
            var layers = new LayerManager(doc);
            string vergePath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName, LayerScheme.Verge);
            int vergeLayerIdx = layers.EnsureLayer(vergePath,
                System.Drawing.Color.FromArgb(0, 0, 0));

            var attrs = new ObjectAttributes { LayerIndex = vergeLayerIdx };

            // Sweep profile along edge
            var sweep = new SweepOneRail();
            var breps = sweep.PerformSweep(inputs.EdgeCurve, new[] { profileCurve });
            if (breps == null || breps.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SweepFailed);
                return Result.Failure;
            }

            foreach (var brep in breps)
                doc.Objects.AddBrep(brep, attrs);

            RhinoApp.WriteLine(Strings.VergeCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static Curve? ResolveBoundaryCurve(RhinoDoc doc, string roadName, Point3d directionPoint)
    {
        foreach (var layerPath in new[]
        {
            LayerScheme.BuildRoadPath(roadName, LayerScheme.Road3D),
            LayerScheme.BuildRoadPath(roadName, "Sections"),
        })
        {
            int layerIdx = doc.Layers.FindByFullPath(layerPath, -1);
            if (layerIdx < 0)
                continue;

            var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (objects == null || objects.Length == 0)
                continue;

            Curve? bestCurve = null;
            double bestDistance = double.MaxValue;

            foreach (var obj in objects)
            {
                if (obj.Geometry is not Curve curve)
                    continue;

                var name = obj.Attributes.Name ?? "";
                if (!name.Contains("boundary", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!curve.ClosestPoint(directionPoint, out double t))
                    continue;

                double distance = curve.PointAt(t).DistanceTo(directionPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCurve = curve;
                }
            }

            if (bestCurve != null)
                return bestCurve;
        }

        return null;
    }

    /// <summary>
    /// Create a Rhino PolylineCurve from Core profile points, remapping Y→Z.
    /// </summary>
    private static PolylineCurve CreateProfileCurve(Core.Math.Point3[] profilePts)
    {
        var rhinoPts = new List<Point3d>();
        foreach (var p in profilePts)
            rhinoPts.Add(new Point3d(p.X, 0, p.Y));

        return new Polyline(rhinoPts).ToPolylineCurve();
    }

    /// <summary>
    /// Get the tangent angle (in degrees, in XY plane) at a parameter on the curve.
    /// </summary>
    private static double GetTangentAngle(Curve curve, double t)
    {
        var tangent = curve.TangentAt(t);
        return System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);
    }

    /// <summary>
    /// Position a profile curve at a point and rotate it to the given angle.
    /// </summary>
    private static void OrientProfile(PolylineCurve curve, Point3d position, double angleDegrees)
    {
        curve.Translate(position - Point3d.Origin);
        curve.Rotate(angleDegrees * System.Math.PI / 180.0, Vector3d.ZAxis, position);
    }

    /// <summary>
    /// Determine if the profile needs to be flipped to point toward the direction point.
    /// VBScript (Krajnice.rvb line 75): if test profile endpoint is closer to direction
    /// than the edge point, the profile already points toward direction → flip 180°
    /// so the sweep profile at start points the same outward direction.
    /// </summary>
    private static bool ShouldFlipToward(PolylineCurve profileCurve, Point3d edgePoint, Point3d directionPt)
    {
        var profileEnd = profileCurve.PointAtEnd;
        double distProfile = profileEnd.DistanceTo(directionPt);
        double distEdge = edgePoint.DistanceTo(directionPt);

        // VBScript: If distance(0) < distance(1) → flip
        // distance(0) = profile endpoint to direction, distance(1) = edge point to direction
        return distProfile < distEdge;
    }

}
