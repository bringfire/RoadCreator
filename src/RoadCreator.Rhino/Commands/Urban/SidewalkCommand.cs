using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Urban;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Urban;

/// <summary>
/// Creates a sidewalk with raised curb along a road edge curve.
/// Converts from Chodnik.rvb.
///
/// Algorithm:
///   1. Select curb edge curve
///   2. Pick direction point (sidewalk side)
///   3. Enter sidewalk width (default 5m)
///   4. Copy edge curve up by curb height (0.2m)
///   5. Offset raised curve by curb top width (0.3m) toward sidewalk
///   6. Offset again by sidewalk width for outer edge
///   7. Loft the 3 curb curves for curb surface
///   8. Loft the 2 sidewalk edge curves for sidewalk surface
/// </summary>
[System.Runtime.InteropServices.Guid("C3D4E5F6-A7B8-9012-CDEF-123456789012")]
public class SidewalkCommand : Command
{
    public override string EnglishName => "RC_Sidewalk";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select curb edge curve
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectCurbEdge);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var edgeCurve = getCurve.Object(0).Curve();

        // Pick sidewalk side
        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectSidewalkSide);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var sidewalkSide = getDir.Point();

        // Enter sidewalk width
        var getWidth = new GetNumber();
        getWidth.SetCommandPrompt(Strings.EnterSidewalkWidth);
        getWidth.SetDefaultNumber(5.0);
        getWidth.SetLowerLimit(0.5, false);
        getWidth.SetUpperLimit(30.0, false);
        if (getWidth.Get() != GetResult.Number)
            return Result.Cancel;
        double sidewalkWidth = getWidth.Number();

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Step 1: Copy edge curve up by curb height
            var raisedCurve = edgeCurve.DuplicateCurve();
            raisedCurve.Translate(new Vector3d(0, 0, SidewalkProfileComputer.CurbHeight));

            // Step 2: Offset raised curve by curb top width toward sidewalk
            var curbTopCurves = raisedCurve.Offset(
                sidewalkSide, Vector3d.ZAxis, SidewalkProfileComputer.CurbTopWidth,
                tolerance, CurveOffsetCornerStyle.Sharp);
            if (curbTopCurves == null || curbTopCurves.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SidewalkLoftFailed);
                return Result.Failure;
            }
            var curbTopCurve = curbTopCurves[0];

            // Step 3: Offset curb top by sidewalk width for outer edge
            // Note: VBScript bug fixed — original used hardcoded 4m instead of user-specified width.
            var sidewalkOuterCurves = curbTopCurve.Offset(
                sidewalkSide, Vector3d.ZAxis, sidewalkWidth,
                tolerance, CurveOffsetCornerStyle.Sharp);
            if (sidewalkOuterCurves == null || sidewalkOuterCurves.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SidewalkLoftFailed);
                return Result.Failure;
            }
            var sidewalkOuterCurve = sidewalkOuterCurves[0];

            // Layer setup
            var layers = new LayerManager(doc);

            // Step 4: Loft the 3 curb curves (base, raised, offset)
            string curbPath = LayerScheme.BuildPath(LayerScheme.Sidewalk);
            int curbLayerIdx = layers.EnsureLayer(curbPath,
                System.Drawing.Color.FromArgb(128, 128, 128));
            var curbAttrs = new ObjectAttributes { LayerIndex = curbLayerIdx };

            var curbLoft = Brep.CreateFromLoft(
                new[] { edgeCurve, raisedCurve, curbTopCurve },
                Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (curbLoft == null || curbLoft.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SidewalkLoftFailed);
                return Result.Failure;
            }
            foreach (var brep in curbLoft)
                doc.Objects.AddBrep(brep, curbAttrs);

            // Step 5: Loft the 2 sidewalk curves (curb top → outer edge)
            var sidewalkLoft = Brep.CreateFromLoft(
                new[] { curbTopCurve, sidewalkOuterCurve },
                Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (sidewalkLoft == null || sidewalkLoft.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SidewalkLoftFailed);
                return Result.Failure;
            }
            foreach (var brep in sidewalkLoft)
                doc.Objects.AddBrep(brep, curbAttrs);

            RhinoApp.WriteLine(Strings.SidewalkCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
