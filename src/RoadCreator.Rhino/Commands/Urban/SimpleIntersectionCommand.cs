using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Urban;

/// <summary>
/// Creates a simple multi-arm intersection with fillet curves between arms.
/// Converts from Krizovatkajednoducha.rvb.
///
/// Algorithm:
///   1. Pick intersection center point
///   2. Enter number of arms (default 3)
///   3. For each arm: pick direction point (counter-clockwise order)
///   4. For each arm: enter right/left lane widths, offset arm axis
///   5. Between adjacent arms: create fillet curves
///      - Fillet right edge of arm P with left edge of arm P+1
///      - Last arm fillets with first arm (wraps around)
///   6. Create profile lines between fillet endpoints
///   7. Create planar surface from all boundary curves
/// </summary>
[System.Runtime.InteropServices.Guid("B8C9D0E1-F2A3-4567-8901-BCDEF0123456")]
public class SimpleIntersectionCommand : Command
{
    public override string EnglishName => "RC_SimpleIntersection";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select center point
        var getCenter = new GetPoint();
        getCenter.SetCommandPrompt(Strings.SelectIntersectionCenter);
        if (getCenter.Get() != GetResult.Point)
            return Result.Cancel;
        var center = getCenter.Point();

        // Enter number of arms
        var getArms = new GetInteger();
        getArms.SetCommandPrompt(Strings.EnterNumberOfArms);
        getArms.SetDefaultInteger(3);
        getArms.SetLowerLimit(2, false);
        getArms.SetUpperLimit(8, false);
        if (getArms.Get() != GetResult.Number)
            return Result.Cancel;
        int armCount = getArms.Number();

        // Collect arm direction points (counter-clockwise)
        var armDirections = new Point3d[armCount];
        for (int i = 0; i < armCount; i++)
        {
            var getArm = new GetPoint();
            getArm.SetCommandPrompt(string.Format(Strings.EnterArmDirection, i + 1));
            getArm.SetBasePoint(center, false);
            if (getArm.Get() != GetResult.Point)
                return Result.Cancel;
            armDirections[i] = getArm.Point();
        }

        // Collect lane widths for each arm
        var rightWidths = new double[armCount];
        var leftWidths = new double[armCount];
        for (int i = 0; i < armCount; i++)
        {
            var getRight = new GetNumber();
            getRight.SetCommandPrompt(string.Format(Strings.EnterArmRightWidth, i + 1));
            getRight.SetDefaultNumber(3.75);
            getRight.SetLowerLimit(2.0, false);
            if (getRight.Get() != GetResult.Number)
                return Result.Cancel;
            rightWidths[i] = getRight.Number();

            var getLeft = new GetNumber();
            getLeft.SetCommandPrompt(string.Format(Strings.EnterArmLeftWidth, i + 1));
            getLeft.SetDefaultNumber(3.75);
            getLeft.SetLowerLimit(2.0, false);
            if (getLeft.Get() != GetResult.Number)
                return Result.Cancel;
            leftWidths[i] = getLeft.Number();
        }

        // Collect fillet radii between adjacent arms
        var filletRadii = new double[armCount];
        for (int i = 0; i < armCount; i++)
        {
            int nextArm = (i + 1) % armCount;
            var getRadius = new GetNumber();
            getRadius.SetCommandPrompt(string.Format(Strings.EnterArmFilletRadius, i + 1, nextArm + 1));
            getRadius.SetDefaultNumber(i == armCount - 1 ? 9.0 : 8.0);
            getRadius.SetLowerLimit(1.0, false);
            if (getRadius.Get() != GetResult.Number)
                return Result.Cancel;
            filletRadii[i] = getRadius.Number();
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Layer setup
            var layers = new LayerManager(doc);
            string intersectionPath = LayerScheme.BuildPath(LayerScheme.Intersection);
            int intersectionLayerIdx = layers.EnsureLayer(intersectionPath,
                System.Drawing.Color.FromArgb(0, 0, 0));
            var attrs = new ObjectAttributes { LayerIndex = intersectionLayerIdx };

            // Create arm axis lines from center outward
            var axisLines = new LineCurve[armCount];
            for (int i = 0; i < armCount; i++)
                axisLines[i] = new LineCurve(center, armDirections[i]);

            // Offset each arm axis to get right and left edge curves
            var rightEdges = new Curve[armCount];
            var leftEdges = new Curve[armCount];

            for (int i = 0; i < armCount; i++)
            {
                var axisDir = armDirections[i] - center;
                axisDir.Unitize();

                // Right side: rotate arm direction point +90° around center
                var rightPt = new Point3d(
                    center.X - axisDir.Y * 50, center.Y + axisDir.X * 50, center.Z);
                var rightOffsets = axisLines[i].Offset(rightPt, Vector3d.ZAxis,
                    rightWidths[i], tolerance, CurveOffsetCornerStyle.Sharp);
                if (rightOffsets != null && rightOffsets.Length > 0)
                    rightEdges[i] = rightOffsets[0];

                // Left side: rotate arm direction point -90° around center
                var leftPt = new Point3d(
                    center.X + axisDir.Y * 50, center.Y - axisDir.X * 50, center.Z);
                var leftOffsets = axisLines[i].Offset(leftPt, Vector3d.ZAxis,
                    leftWidths[i], tolerance, CurveOffsetCornerStyle.Sharp);
                if (leftOffsets != null && leftOffsets.Length > 0)
                    leftEdges[i] = leftOffsets[0];
            }

            // Create fillets between adjacent arms and build boundary curves
            var allBoundaryCurves = new List<Curve>();

            for (int i = 0; i < armCount; i++)
            {
                int nextArm = (i + 1) % armCount;

                if (rightEdges[i] == null || leftEdges[nextArm] == null)
                    continue;

                // VBScript: fillet right edge of arm P with left edge of arm P+1
                var rightMid = rightEdges[i].PointAt(rightEdges[i].Domain.Mid);
                var leftMid = leftEdges[nextArm].PointAt(leftEdges[nextArm].Domain.Mid);

                var filletResult = Curve.CreateFilletCurves(
                    rightEdges[i], rightMid,
                    leftEdges[nextArm], leftMid,
                    filletRadii[i], true, true, true, tolerance, tolerance);

                if (filletResult != null && filletResult.Length >= 3)
                {
                    // Fillet result: [trimmedRight, trimmedLeft, filletArc]
                    // Join them into a single boundary curve
                    var joined = Curve.JoinCurves(filletResult, tolerance);
                    if (joined != null && joined.Length > 0)
                    {
                        allBoundaryCurves.Add(joined[0]);
                        doc.Objects.AddCurve(joined[0], attrs);
                    }
                    else
                    {
                        foreach (var fc in filletResult)
                        {
                            allBoundaryCurves.Add(fc);
                            doc.Objects.AddCurve(fc, attrs);
                        }
                    }
                }
                else
                {
                    // Fillet failed — join curves directly (VBScript fallback)
                    RhinoApp.WriteLine(string.Format(Strings.IntersectionFilletFailed, i + 1, nextArm + 1));
                    var joined = Curve.JoinCurves(
                        new[] { rightEdges[i], leftEdges[nextArm] }, tolerance);
                    if (joined != null && joined.Length > 0)
                    {
                        allBoundaryCurves.Add(joined[0]);
                        doc.Objects.AddCurve(joined[0], attrs);
                    }
                }
            }

            // Create profile lines connecting endpoints of adjacent boundary curves
            int boundaryCount = allBoundaryCurves.Count;
            for (int i = 0; i < boundaryCount; i++)
            {
                int next = (i + 1) % boundaryCount;
                var endPt = allBoundaryCurves[i].PointAtEnd;
                var startPt = allBoundaryCurves[next].PointAtStart;

                if (endPt.DistanceTo(startPt) > tolerance)
                {
                    var profileLine = new LineCurve(endPt, startPt);
                    allBoundaryCurves.Add(profileLine);
                    doc.Objects.AddCurve(profileLine, attrs);
                }
            }

            // Create planar surface from all boundary curves
            var planarSurfaces = Brep.CreatePlanarBreps(allBoundaryCurves, tolerance);
            if (planarSurfaces != null)
            {
                foreach (var srf in planarSurfaces)
                    doc.Objects.AddBrep(srf, attrs);
            }

            // Add scaled axis lines (half-length) for reference
            for (int i = 0; i < armCount; i++)
            {
                var midPt = new Point3d(
                    (center.X + armDirections[i].X) / 2,
                    (center.Y + armDirections[i].Y) / 2,
                    (center.Z + armDirections[i].Z) / 2);
                var halfAxis = new LineCurve(center, midPt);
                doc.Objects.AddCurve(halfAxis, attrs);
            }

            RhinoApp.WriteLine(Strings.IntersectionCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
