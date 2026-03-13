using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Copies objects along a curve at regular spacing, rotating each copy to match the curve tangent.
/// Converts from kopirovaniobjektupodelkrivky.rvb.
///
/// Algorithm:
///   1. Select guide curve
///   2. Select objects to copy
///   3. Pick base point of objects
///   4. Enter spacing (default 5m)
///   5. Divide curve at spacing intervals
///   6. At each point: copy objects, rotate by tangent angle
/// </summary>
[System.Runtime.InteropServices.Guid("A1000008-B2C3-D4E5-F6A7-B8C9D0E1F208")]
public class CopyAlongCurveCommand : Command
{
    public override string EnglishName => "RC_CopyAlongCurve";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectCurveForCopy);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();

        var getObjects = new GetObject();
        getObjects.SetCommandPrompt(Strings.SelectObjectsToCopy);
        getObjects.EnablePreSelect(false, true);
        if (getObjects.GetMultiple(1, 0) != GetResult.Object)
            return Result.Cancel;

        var sourceIds = new Guid[getObjects.ObjectCount];
        for (int i = 0; i < getObjects.ObjectCount; i++)
            sourceIds[i] = getObjects.Object(i).ObjectId;

        var getBase = new GetPoint();
        getBase.SetCommandPrompt(Strings.SelectCopyBasePoint);
        if (getBase.Get() != GetResult.Point)
            return Result.Cancel;
        var basePoint = getBase.Point();

        var getSpacing = new GetNumber();
        getSpacing.SetCommandPrompt(Strings.EnterCopySpacing);
        getSpacing.SetDefaultNumber(5.0);
        getSpacing.SetLowerLimit(1.0, false);
        getSpacing.SetUpperLimit(1000.0, false);
        if (getSpacing.Get() != GetResult.Number)
            return Result.Cancel;
        double spacing = getSpacing.Number();

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // includeEnds=true: includes both start and endpoint (may produce shorter final segment).
            guideCurve.DivideByLength(spacing, true, out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length == 0)
                return Result.Failure;

            for (int i = 0; i < divPoints.Length; i++)
            {
                var pt = divPoints[i];

                // Get tangent angle at curve point.
                // Note: VBScript uses chord angle (point-to-point) which approximates a bisector
                // at interior points. We use the curve tangent, which is equivalent for smooth
                // curves but may differ slightly at sharp bends.
                guideCurve.ClosestPoint(pt, out double t);
                var tangent = guideCurve.TangentAt(t);
                double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

                // Rotation: first/last use 180+alfa/360+alfa, mid uses 180+alfa
                double rotation;
                if (i == 0)
                    rotation = 180 + tangentAngle;
                else if (i == divPoints.Length - 1)
                    rotation = tangentAngle; // 360 + alfa ≡ alfa
                else
                    rotation = 180 + tangentAngle;

                // Copy each source object
                foreach (var srcId in sourceIds)
                {
                    var srcObj = doc.Objects.FindId(srcId);
                    if (srcObj?.Geometry == null) continue;

                    var copy = srcObj.Geometry.Duplicate();
                    var moveXform = Transform.Translation(pt - basePoint);
                    var rotateXform = Transform.Rotation(
                        rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);
                    copy.Transform(rotateXform * moveXform);
                    doc.Objects.Add(copy, srcObj.Attributes);
                }
            }

            RhinoApp.WriteLine(Strings.CopyAlongCurveCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
