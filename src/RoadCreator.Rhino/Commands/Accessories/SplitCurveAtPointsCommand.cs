using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Splits a curve at one or two user-picked points.
/// Converts from Splitkrivkabody.rvb.
///
/// Algorithm:
///   1. Select curve
///   2. Pick first split point on curve
///   3. Optionally pick second split point
///   4. Split curve at the parameter(s) corresponding to the picked point(s)
///   5. Delete original, add split segments
/// </summary>
[System.Runtime.InteropServices.Guid("A1000009-B2C3-D4E5-F6A7-B8C9D0E1F209")]
public class SplitCurveAtPointsCommand : Command
{
    public override string EnglishName => "RC_SplitCurveAtPoints";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectCurveToSplit);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var curveRef = getCurve.Object(0);
        var curve = curveRef.Curve();
        var curveId = curveRef.ObjectId;

        // First split point
        var getPoint1 = new GetPoint();
        getPoint1.SetCommandPrompt(Strings.SelectFirstSplitPoint);
        getPoint1.Constrain(curve, false);
        if (getPoint1.Get() != GetResult.Point)
            return Result.Cancel;
        var splitPt1 = getPoint1.Point();

        // Second split point (optional)
        var getPoint2 = new GetPoint();
        getPoint2.SetCommandPrompt(Strings.SelectSecondSplitPoint);
        getPoint2.Constrain(curve, false);
        getPoint2.AcceptNothing(true);
        var pt2Result = getPoint2.Get();
        Point3d? splitPt2 = pt2Result == GetResult.Point ? getPoint2.Point() : null;

        doc.Views.RedrawEnabled = false;

        try
        {
            // Find curve parameters for split points
            var splitParams = new List<double>();

            curve.ClosestPoint(splitPt1, out double t1);
            splitParams.Add(t1);

            if (splitPt2.HasValue)
            {
                curve.ClosestPoint(splitPt2.Value, out double t2);
                splitParams.Add(t2);
            }

            // Sort parameters
            splitParams.Sort();

            // Split curve
            var segments = curve.Split(splitParams);
            if (segments == null || segments.Length < 2)
            {
                RhinoApp.WriteLine(Strings.CurveSplitFailed);
                return Result.Failure;
            }

            // Get original attributes
            var origObj = doc.Objects.FindId(curveId);
            var attrs = origObj?.Attributes ?? new ObjectAttributes();

            // Delete original and add segments
            doc.Objects.Delete(curveId, true);
            foreach (var seg in segments)
                doc.Objects.AddCurve(seg, attrs);

            RhinoApp.WriteLine(Strings.CurveSplit);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
