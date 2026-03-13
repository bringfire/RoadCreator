using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Rhino;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Creates a double-sided metal W-beam guardrail along a road center line.
/// Converts from svodidlaoboustrany.rvb.
///
/// Algorithm:
///   1. Select center curve
///   2. Divide curve at 4m intervals
///   3. At each point: get tangent, place W-beam profiles on both sides + post box
///   4. Loft both profile sets into two guardrail surfaces
/// </summary>
[System.Runtime.InteropServices.Guid("A1000002-B2C3-D4E5-F6A7-B8C9D0E1F202")]
public class GuardrailDoubleCommand : Command
{
    private sealed record GuardrailDoubleInputs(
        string RoadName,
        Curve GuideCurve);

    public override string EnglishName => "RC_GuardrailDouble";

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

        string roadName = RoadCreator.Core.Alignment.RoadObjectNaming.ParseRoadName(
            guideObject.Attributes.Name) ?? "";
        return RunCore(doc, new GuardrailDoubleInputs(roadName, guideCurve));
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

        var guideCurve = RouteDiscovery.FindRoute3DCurve(doc, roadName)
            ?? RouteDiscovery.FindFullAxisCurve(doc, roadName);
        if (guideCurve == null)
        {
            RhinoApp.WriteLine($"No guide curve found for road '{roadName}'.");
            return Result.Failure;
        }

        return RunCore(doc, new GuardrailDoubleInputs(roadName, guideCurve));
    }

    private Result RunCore(RhinoDoc doc, GuardrailDoubleInputs inputs)
    {

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Divide curve at post spacing (no offset for double-sided).
            // includeEnds=true: includes both start and endpoint (may produce shorter final segment).
            inputs.GuideCurve.DivideByLength(
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

            var wbeamPts = GuardrailProfileComputer.GetWBeamProfile();

            var profileCurvesFront = new List<Curve>();
            var profileCurvesBack = new List<Curve>();

            for (int i = 0; i < divPoints.Length; i++)
            {
                var pt = divPoints[i];
                inputs.GuideCurve.ClosestPoint(pt, out double t);
                var tangent = inputs.GuideCurve.TangentAt(t);
                double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

                bool isEndPoint = (i == 0 || i == divPoints.Length - 1);
                var zOffset = isEndPoint ? new Vector3d(0, 0, -1) : Vector3d.Zero;

                // Front profile rotation
                double frontRotation = 180 + tangentAngle;
                // Last profile faces opposite
                if (i == divPoints.Length - 1)
                    frontRotation = tangentAngle; // 360 + alfa ≡ alfa

                // Back profile (mirrored: 180° rotated copy of front)
                double backRotation = frontRotation;

                // Create front profile
                profileCurvesFront.Add(CreateProfile(wbeamPts, pt, frontRotation, zOffset));

                // Create back profile (VBScript mirrors by rotating template 180°)
                profileCurvesBack.Add(CreateMirroredProfile(wbeamPts, pt, backRotation, zOffset));

                // Place posts at mid-points only
                if (!isEndPoint)
                {
                    PlaceDoublePost(doc, pt, tangentAngle, postAttrs);
                }
            }

            // Loft front profiles
            var frontLoft = Brep.CreateFromLoft(profileCurvesFront, Point3d.Unset, Point3d.Unset,
                LoftType.Straight, false);
            if (frontLoft != null)
                foreach (var brep in frontLoft)
                    doc.Objects.AddBrep(brep, profileAttrs);

            // Loft back profiles
            var backLoft = Brep.CreateFromLoft(profileCurvesBack, Point3d.Unset, Point3d.Unset,
                LoftType.Straight, false);
            if (backLoft != null)
                foreach (var brep in backLoft)
                    doc.Objects.AddBrep(brep, profileAttrs);

            if ((frontLoft == null || frontLoft.Length == 0) &&
                (backLoft == null || backLoft.Length == 0))
            {
                RhinoApp.WriteLine(Strings.GuardrailLoftFailed);
                return Result.Failure;
            }

            RhinoApp.WriteLine(Strings.GuardrailDoubleCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static Curve CreateProfile(Core.Math.Point3[] wbeamPts, Point3d pt,
        double rotation, Vector3d zOffset)
    {
        var pts = new Point3d[wbeamPts.Length];
        for (int j = 0; j < wbeamPts.Length; j++)
            pts[j] = new Point3d(wbeamPts[j].X, wbeamPts[j].Y, wbeamPts[j].Z);

        var polyline = new Polyline(pts);
        var curve = polyline.ToPolylineCurve();
        curve.Translate(new Vector3d(pt) + zOffset);
        curve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);
        return curve;
    }

    private static Curve CreateMirroredProfile(Core.Math.Point3[] wbeamPts, Point3d pt,
        double rotation, Vector3d zOffset)
    {
        // Mirror the W-beam: negate Y values and reverse point order so that
        // the seam point direction matches the front profile, preventing loft twist.
        var pts = new Point3d[wbeamPts.Length];
        for (int j = 0; j < wbeamPts.Length; j++)
        {
            int srcIdx = wbeamPts.Length - 1 - j;
            pts[j] = new Point3d(wbeamPts[srcIdx].X, -wbeamPts[srcIdx].Y, wbeamPts[srcIdx].Z);
        }

        var polyline = new Polyline(pts);
        var curve = polyline.ToPolylineCurve();
        curve.Translate(new Vector3d(pt) + zOffset);
        curve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);
        return curve;
    }

    private static void PlaceDoublePost(RhinoDoc doc, Point3d position,
        double tangentAngle, ObjectAttributes attrs)
    {
        var corners = GuardrailProfileComputer.GetPostBoxCorners();
        var center = GuardrailProfileComputer.PostCenterOffset;

        var boxBrep = Brep.CreateFromBox(new BoundingBox(
            new Point3d(corners[0].X, corners[0].Y, corners[0].Z),
            new Point3d(corners[6].X, corners[6].Y, corners[6].Z)));
        if (boxBrep != null)
        {
            double rotation = 180 + tangentAngle;
            boxBrep.Translate(new Vector3d(position.X - center.X,
                position.Y - center.Y, position.Z));
            boxBrep.Transform(Transform.Rotation(
                rotation * System.Math.PI / 180.0, Vector3d.ZAxis, position));
            doc.Objects.AddBrep(boxBrep, attrs);
        }
    }
}
