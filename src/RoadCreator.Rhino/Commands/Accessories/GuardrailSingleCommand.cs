using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Creates a single-sided metal W-beam guardrail along a road edge.
/// Converts from Svodidlajednostranna.rvb.
///
/// Algorithm:
///   1. Select road edge curve
///   2. Pick direction point (offset side)
///   3. Offset curve by 0.37m toward direction
///   4. Divide offset curve at 4m intervals
///   5. At each point: get tangent, place W-beam profile and post box
///   6. Loft all profiles into guardrail surface
/// </summary>
[System.Runtime.InteropServices.Guid("A1000001-B2C3-D4E5-F6A7-B8C9D0E1F201")]
public class GuardrailSingleCommand : Command
{
    private sealed record GuardrailSingleInputs(
        string RoadName,
        Curve GuideCurve,
        Point3d DirectionPoint);

    public override string EnglishName => "RC_GuardrailSingle";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectGuardrailCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();
        var guideObject = getCurve.Object(0).Object();
        if (guideCurve == null || guideObject == null)
            return Result.Cancel;

        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectGuardrailDirection);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var direction = getDir.Point();

        string roadName = RoadObjectNaming.ParseRoadName(guideObject.Attributes.Name) ?? "";
        return RunCore(doc, new GuardrailSingleInputs(roadName, guideCurve, direction));
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
        getDir.SetCommandPrompt(Strings.SelectGuardrailDirection);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var direction = getDir.Point();

        var guideCurve = RoadCurveResolver.ResolveNearestBoundaryCurve(doc, roadName, direction);
        if (guideCurve == null)
        {
            RhinoApp.WriteLine($"No road boundary curve found for road '{roadName}'.");
            return Result.Failure;
        }

        return RunCore(doc, new GuardrailSingleInputs(roadName, guideCurve, direction));
    }

    private Result RunCore(RhinoDoc doc, GuardrailSingleInputs inputs)
    {

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Offset curve toward direction
            var offsetCurves = inputs.GuideCurve.Offset(inputs.DirectionPoint, Vector3d.ZAxis,
                GuardrailProfileComputer.EdgeOffset, tolerance, CurveOffsetCornerStyle.Sharp);
            if (offsetCurves == null || offsetCurves.Length == 0)
            {
                RhinoApp.WriteLine(Strings.GuardrailLoftFailed);
                return Result.Failure;
            }
            var offsetCurve = offsetCurves[0];

            // Divide offset curve at post spacing.
            // Note: includeEnds=true adds both start and endpoint, which may produce
            // a shorter final segment if curve length isn't an exact multiple of spacing.
            // VBScript DivideCurveEquidistant behaves similarly.
            var divParams = offsetCurve.DivideByLength(
                GuardrailProfileComputer.PostSpacing, true, out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length < 2)
            {
                RhinoApp.WriteLine(Strings.GuardrailLoftFailed);
                return Result.Failure;
            }

            // Layer setup
            var layers = new LayerManager(doc);
            int postLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Guardrail),
                System.Drawing.Color.FromArgb(50, 90, 90));
            int profileLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.GuardrailProfile),
                System.Drawing.Color.FromArgb(250, 250, 250));
            var postAttrs = new ObjectAttributes { LayerIndex = postLayerIdx };
            var profileAttrs = new ObjectAttributes { LayerIndex = profileLayerIdx };

            // Determine side: check which side of the original curve the offset is on
            double sideAngleOffset = DetermineSideOffset(inputs.GuideCurve, offsetCurve, divPoints[0], tolerance);

            // Build profile and post data
            var wbeamPts = GuardrailProfileComputer.GetWBeamProfile();
            var postCorners = GuardrailProfileComputer.GetPostBoxCorners();
            var upperBracket = GuardrailProfileComputer.GetUpperBracket();
            var lowerBracket = GuardrailProfileComputer.GetLowerBracket();

            var profileCurves = new List<Curve>();

            for (int i = 0; i < divPoints.Length; i++)
            {
                var pt = divPoints[i];
                double tangentAngle = GetTangentAngle(offsetCurve, pt, tolerance);
                double rotation = sideAngleOffset + tangentAngle;

                // VBScript: end-cap profiles are copies moved down 1m so they sink into the
                // ground, creating a tapered end. The profile is translated by (0,0,-1) to
                // produce a slope from ground level to the first interior post.
                bool isEndPoint = (i == 0 || i == divPoints.Length - 1);
                var zOffset = isEndPoint ? new Vector3d(0, 0, -1) : Vector3d.Zero;

                // Last profile faces opposite direction
                if (i == divPoints.Length - 1)
                    rotation += 180;

                // Create W-beam profile polyline
                var profilePts = new Point3d[wbeamPts.Length];
                for (int j = 0; j < wbeamPts.Length; j++)
                    profilePts[j] = new Point3d(wbeamPts[j].X, wbeamPts[j].Y, wbeamPts[j].Z);

                var polyline = new Polyline(profilePts);
                var polyCurve = polyline.ToPolylineCurve();

                // Transform: translate to point (with Z offset for end caps), then rotate
                polyCurve.Translate(new Vector3d(pt) + zOffset);
                polyCurve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);

                profileCurves.Add(polyCurve);

                // Place posts at mid-points only (not at first/last)
                if (!isEndPoint)
                {
                    PlacePost(doc, postCorners, upperBracket, lowerBracket,
                        pt, rotation, postAttrs);
                }
            }

            // Loft profiles
            var loft = Brep.CreateFromLoft(profileCurves, Point3d.Unset, Point3d.Unset,
                LoftType.Straight, false);
            if (loft == null || loft.Length == 0)
            {
                RhinoApp.WriteLine(Strings.GuardrailLoftFailed);
                return Result.Failure;
            }

            foreach (var brep in loft)
                doc.Objects.AddBrep(brep, profileAttrs);

            RhinoApp.WriteLine(Strings.GuardrailSingleCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    /// <summary>
    /// Determine the side offset angle (180° or 360°) by checking which test point
    /// is closer to the original curve, matching VBScript behavior.
    /// </summary>
    private static double DetermineSideOffset(Curve originalCurve, Curve offsetCurve,
        Point3d firstPoint, double tolerance)
    {
        offsetCurve.ClosestPoint(firstPoint, out double t);
        var tangent = offsetCurve.TangentAt(t);
        double angle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

        // Create two test points perpendicular to tangent
        var perp = new Vector3d(-tangent.Y, tangent.X, 0);
        perp.Unitize();
        var testA = firstPoint + perp * 0.2;
        var testB = firstPoint - perp * 0.2;

        originalCurve.ClosestPoint(testA, out double tA);
        originalCurve.ClosestPoint(testB, out double tB);
        double distA = testA.DistanceTo(originalCurve.PointAt(tA));
        double distB = testB.DistanceTo(originalCurve.PointAt(tB));

        return distA < distB ? 360.0 : 180.0;
    }

    private static double GetTangentAngle(Curve curve, Point3d pt, double tolerance)
    {
        curve.ClosestPoint(pt, out double t);
        var tangent = curve.TangentAt(t);
        return System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);
    }

    private static void PlacePost(RhinoDoc doc, Core.Math.Point3[] corners,
        (Core.Math.Point3 Start, Core.Math.Point3 End) upper,
        (Core.Math.Point3 Start, Core.Math.Point3 End) lower,
        Point3d position, double rotation, ObjectAttributes attrs)
    {
        var center = GuardrailProfileComputer.PostCenterOffset;
        var transform = Transform.Rotation(
            rotation * System.Math.PI / 180.0, Vector3d.ZAxis, position);

        // Post box
        var boxBrep = Brep.CreateFromBox(new BoundingBox(
            new Point3d(corners[0].X, corners[0].Y, corners[0].Z),
            new Point3d(corners[6].X, corners[6].Y, corners[6].Z)));
        if (boxBrep != null)
        {
            boxBrep.Translate(new Vector3d(position.X - center.X,
                position.Y - center.Y, position.Z));
            boxBrep.Transform(transform);
            doc.Objects.AddBrep(boxBrep, attrs);
        }

        // Upper bracket surface
        var extDir = GuardrailProfileComputer.BracketExtrusionDir;
        AddBracketSurface(doc, upper.Start, upper.End, extDir, position, center, rotation, attrs);

        // Lower bracket surface
        AddBracketSurface(doc, lower.Start, lower.End, extDir, position, center, rotation, attrs);
    }
    private static void AddBracketSurface(RhinoDoc doc,
        Core.Math.Point3 start, Core.Math.Point3 end,
        Core.Math.Vector3 extrusionDir,
        Point3d position, Core.Math.Point3 center, double rotation,
        ObjectAttributes attrs)
    {
        var s = new Point3d(start.X + position.X - center.X,
            start.Y + position.Y - center.Y, start.Z + position.Z);
        var e = new Point3d(end.X + position.X - center.X,
            end.Y + position.Y - center.Y, end.Z + position.Z);

        var transform = Transform.Rotation(
            rotation * System.Math.PI / 180.0, Vector3d.ZAxis, position);
        s.Transform(transform);
        e.Transform(transform);

        var line = new LineCurve(s, e);
        var extVec = new Vector3d(extrusionDir.X, extrusionDir.Y, extrusionDir.Z);
        extVec.Transform(Transform.Rotation(
            rotation * System.Math.PI / 180.0, Vector3d.ZAxis, Point3d.Origin));

        var srf = Surface.CreateExtrusion(line, extVec);
        if (srf != null)
            doc.Objects.AddSurface(srf, attrs);
    }
}
