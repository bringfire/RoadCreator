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
/// Creates a roundabout with configurable arms, fillets, and Czech standard lane widths.
/// Converts from Okruznikrizovatka.rvb.
///
/// Algorithm:
///   1. Pick center point
///   2. Enter outer diameter (14–50m)
///   3. Enter number of arms (2–6)
///   4. Lane width from Czech ČSN table (or manual if diameter &lt; 25m)
///   5. Create outer, inner, and apron circles
///   6. For each arm: pick direction point (counter-clockwise)
///   7. For each arm: get entry/exit widths and fillet radii
///      - Offset arm axis to get entry/exit edge curves
///      - Fillet entry/exit edges against outer circle
///   8. For each arm: create arc + planar surface from fillet curves
///   9. Create annular road surface, apron ring, raised island
/// </summary>
[System.Runtime.InteropServices.Guid("A7B8C9D0-E1F2-3456-7890-ABCDEF012345")]
public class RoundaboutCommand : Command
{
    /// <summary>
    /// Extension beyond outer circle for arm axis line intersection.
    /// From VBScript: (vnejsiprumer / 2) + 20.
    /// </summary>
    private const double AxisExtension = 20.0;

    public override string EnglishName => "RC_Roundabout";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select center point
        var getCenter = new GetPoint();
        getCenter.SetCommandPrompt(Strings.SelectRoundaboutCenter);
        if (getCenter.Get() != GetResult.Point)
            return Result.Cancel;
        var center = getCenter.Point();

        // Enter outer diameter
        var getDiameter = new GetNumber();
        getDiameter.SetCommandPrompt(Strings.EnterOuterDiameter);
        getDiameter.SetDefaultNumber(25);
        getDiameter.SetLowerLimit(14.0, false);
        getDiameter.SetUpperLimit(50.0, false);
        if (getDiameter.Get() != GetResult.Number)
            return Result.Cancel;
        double outerDiameter = getDiameter.Number();
        double outerRadius = outerDiameter / 2.0;

        // Enter number of arms
        var getArms = new GetInteger();
        getArms.SetCommandPrompt(Strings.EnterNumberOfArms);
        getArms.SetDefaultInteger(4);
        getArms.SetLowerLimit(2, false);
        getArms.SetUpperLimit(6, false);
        if (getArms.Get() != GetResult.Number)
            return Result.Cancel;
        int armCount = getArms.Number();

        // Determine lane width from Czech standards
        double? standardWidth = RoundaboutLaneComputer.GetStandardLaneWidth(outerDiameter);
        double laneWidth;
        if (standardWidth.HasValue)
        {
            laneWidth = standardWidth.Value;
        }
        else
        {
            var getLaneWidth = new GetNumber();
            getLaneWidth.SetCommandPrompt(Strings.EnterLaneWidth);
            getLaneWidth.SetDefaultNumber(5.0);
            getLaneWidth.SetLowerLimit(4.0, false);
            getLaneWidth.SetUpperLimit(outerRadius, false);
            if (getLaneWidth.Get() != GetResult.Number)
                return Result.Cancel;
            laneWidth = getLaneWidth.Number();
        }

        double innerRadius = RoundaboutLaneComputer.ComputeInnerRadius(outerRadius, laneWidth);
        bool hasApron = RoundaboutLaneComputer.HasApron(outerDiameter);
        double apronInnerRadius = hasApron
            ? RoundaboutLaneComputer.ComputeApronInnerRadius(outerRadius, laneWidth)
            : 0;

        // Collect arm directions (counter-clockwise order)
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

        // Collect entry/exit widths and fillet radii for each arm
        var entryWidths = new double[armCount];
        var exitWidths = new double[armCount];
        var entryRadii = new double[armCount];
        var exitRadii = new double[armCount];

        for (int i = 0; i < armCount; i++)
        {
            var getEntry = new GetNumber();
            getEntry.SetCommandPrompt(string.Format(Strings.EnterEntryWidth + " (arm {0})", i + 1));
            getEntry.SetDefaultNumber(3.0);
            getEntry.SetLowerLimit(2.0, false);
            if (getEntry.Get() != GetResult.Number)
                return Result.Cancel;
            entryWidths[i] = getEntry.Number();

            var getEntryR = new GetNumber();
            getEntryR.SetCommandPrompt(string.Format(Strings.EnterEntryRadius + " (arm {0})", i + 1));
            getEntryR.SetDefaultNumber(8.0);
            getEntryR.SetLowerLimit(6.0, false);
            getEntryR.SetUpperLimit(17.0, false);
            if (getEntryR.Get() != GetResult.Number)
                return Result.Cancel;
            entryRadii[i] = getEntryR.Number();

            var getExit = new GetNumber();
            getExit.SetCommandPrompt(string.Format(Strings.EnterExitWidth + " (arm {0})", i + 1));
            getExit.SetDefaultNumber(3.0);
            getExit.SetLowerLimit(2.0, false);
            if (getExit.Get() != GetResult.Number)
                return Result.Cancel;
            exitWidths[i] = getExit.Number();

            var getExitR = new GetNumber();
            getExitR.SetCommandPrompt(string.Format(Strings.EnterExitRadius + " (arm {0})", i + 1));
            getExitR.SetDefaultNumber(12.0);
            getExitR.SetLowerLimit(12.0, false);
            if (getExitR.Get() != GetResult.Number)
                return Result.Cancel;
            exitRadii[i] = getExitR.Number();
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Layer setup
            var layers = new LayerManager(doc);
            string roundaboutPath = LayerScheme.BuildPath(LayerScheme.Roundabout);
            int roundaboutLayerIdx = layers.EnsureLayer(roundaboutPath,
                System.Drawing.Color.FromArgb(0, 0, 0));
            var attrs = new ObjectAttributes { LayerIndex = roundaboutLayerIdx };

            // Create circles
            var outerCircle = new Circle(new Plane(center, Vector3d.ZAxis), outerRadius);
            var innerCircle = new Circle(new Plane(center, Vector3d.ZAxis), innerRadius);
            var outerCurve = new ArcCurve(outerCircle);
            var innerCurve = new ArcCurve(innerCircle);

            doc.Objects.AddCurve(outerCurve, attrs);
            doc.Objects.AddCurve(innerCurve, attrs);

            ArcCurve? apronCurve = null;
            if (hasApron)
            {
                var apronCircle = new Circle(new Plane(center, Vector3d.ZAxis), apronInnerRadius);
                apronCurve = new ArcCurve(apronCircle);
                doc.Objects.AddCurve(apronCurve, attrs);
            }

            // Build arm axis lines from center to outer circle intersection
            var axisLines = new LineCurve[armCount];
            var outerIntersections = new Point3d[armCount];
            for (int i = 0; i < armCount; i++)
            {
                var dir = armDirections[i] - center;
                dir.Unitize();
                var farPt = center + dir * (outerRadius + AxisExtension);
                var axisLine = new LineCurve(center, farPt);

                // Find intersection with outer circle
                var isect = global::Rhino.Geometry.Intersect.Intersection.CurveCurve(
                    axisLine, outerCurve, tolerance, tolerance);
                if (isect != null && isect.Count > 0)
                    outerIntersections[i] = isect[0].PointA;
                else
                    outerIntersections[i] = center + dir * outerRadius;

                axisLines[i] = new LineCurve(outerIntersections[i], armDirections[i]);
            }

            // For each arm: create entry/exit edges and fillets
            var armEntryFillets = new Curve[armCount][];
            var armExitFillets = new Curve[armCount][];
            var armEntryEdgeEnds = new Point3d[armCount];
            var armExitEdgeEnds = new Point3d[armCount];

            for (int i = 0; i < armCount; i++)
            {
                var axisDir = armDirections[i] - center;
                axisDir.Unitize();

                // Rotate direction 90° to get perpendicular for offset side
                var rightDir = new Vector3d(-axisDir.Y, axisDir.X, 0);
                var entryOffsetPt = center + rightDir * (outerRadius + 10);
                var exitOffsetPt = center - rightDir * (outerRadius + 10);

                // Offset axis for entry edge (right side looking from center)
                var entryOffsets = axisLines[i].Offset(
                    new Point3d(entryOffsetPt.X, entryOffsetPt.Y, entryOffsetPt.Z),
                    Vector3d.ZAxis, entryWidths[i], tolerance, CurveOffsetCornerStyle.Sharp);

                if (entryOffsets != null && entryOffsets.Length > 0)
                {
                    // Fillet entry edge with outer circle
                    var entryMid = entryOffsets[0].PointAt(entryOffsets[0].Domain.Mid);
                    armEntryFillets[i] = Curve.CreateFilletCurves(
                        entryOffsets[0], entryMid,
                        outerCurve, new Point3d(entryOffsetPt.X, entryOffsetPt.Y, entryOffsetPt.Z),
                        entryRadii[i], true, true, true, tolerance, tolerance);
                    armEntryEdgeEnds[i] = entryOffsets[0].PointAtEnd;
                }

                // Offset axis for exit edge (left side looking from center)
                var exitOffsets = axisLines[i].Offset(
                    new Point3d(exitOffsetPt.X, exitOffsetPt.Y, exitOffsetPt.Z),
                    Vector3d.ZAxis, exitWidths[i], tolerance, CurveOffsetCornerStyle.Sharp);

                if (exitOffsets != null && exitOffsets.Length > 0)
                {
                    var exitMid = exitOffsets[0].PointAt(exitOffsets[0].Domain.Mid);
                    armExitFillets[i] = Curve.CreateFilletCurves(
                        exitOffsets[0], exitMid,
                        outerCurve, new Point3d(exitOffsetPt.X, exitOffsetPt.Y, exitOffsetPt.Z),
                        exitRadii[i], true, true, true, tolerance, tolerance);
                    armExitEdgeEnds[i] = exitOffsets[0].PointAtEnd;
                }
            }

            // Create arm surfaces: for each arm, make arc + profile + fillet curves
            for (int i = 0; i < armCount; i++)
            {
                if (armEntryFillets[i] == null || armEntryFillets[i].Length < 3 ||
                    armExitFillets[i] == null || armExitFillets[i].Length < 3)
                {
                    RhinoApp.WriteLine(string.Format(Strings.RoundaboutFilletFailed, i + 1));
                    continue;
                }

                // Get fillet endpoints on outer circle
                var entryFillet = armEntryFillets[i];
                var exitFillet = armExitFillets[i];

                // Add fillet curves to document
                foreach (var fc in entryFillet)
                    doc.Objects.AddCurve(fc, attrs);
                foreach (var fc in exitFillet)
                    doc.Objects.AddCurve(fc, attrs);

                // Create arc between entry and exit fillet endpoints through outer intersection
                var arcStartPt = entryFillet[0].PointAtEnd;
                var arcEndPt = exitFillet[0].PointAtEnd;
                var arc = new Arc(arcStartPt, outerIntersections[i], arcEndPt);
                var arcCurve = new ArcCurve(arc);
                doc.Objects.AddCurve(arcCurve, attrs);

                // Create profile line connecting entry and exit edge ends
                var profileLine = new LineCurve(armEntryEdgeEnds[i], armExitEdgeEnds[i]);
                doc.Objects.AddCurve(profileLine, attrs);

                // Create planar surface from boundary curves
                var boundaryCurves = new Curve[] { profileLine, arcCurve, entryFillet[0], exitFillet[0] };
                var planarSurfaces = Brep.CreatePlanarBreps(boundaryCurves, tolerance);
                if (planarSurfaces != null)
                {
                    foreach (var srf in planarSurfaces)
                        doc.Objects.AddBrep(srf, attrs);
                }
            }

            // Create annular road surface (outer - inner circles)
            var annularSurfaces = Brep.CreatePlanarBreps(
                new Curve[] { outerCurve, innerCurve }, tolerance);
            if (annularSurfaces != null)
            {
                foreach (var srf in annularSurfaces)
                    doc.Objects.AddBrep(srf, attrs);
            }

            // Create apron ring and raised island
            if (hasApron && apronCurve != null)
            {
                // Apron ring surface
                var apronSurfaces = Brep.CreatePlanarBreps(
                    new Curve[] { innerCurve, apronCurve }, tolerance);
                if (apronSurfaces != null)
                {
                    foreach (var srf in apronSurfaces)
                        doc.Objects.AddBrep(srf, attrs);
                }

                // Raised island (planar surface raised by island height)
                var islandSurfaces = Brep.CreatePlanarBreps(
                    new Curve[] { apronCurve }, tolerance);
                if (islandSurfaces != null)
                {
                    foreach (var srf in islandSurfaces)
                    {
                        srf.Translate(new Vector3d(0, 0, RoundaboutLaneComputer.IslandHeight));
                        doc.Objects.AddBrep(srf, attrs);
                    }
                }

                // Extrude apron circle vertically for island wall
                var wallSurface = Surface.CreateExtrusion(apronCurve, new Vector3d(0, 0, RoundaboutLaneComputer.IslandHeight));
                if (wallSurface != null)
                    doc.Objects.AddSurface(wallSurface, attrs);
            }
            else
            {
                // No apron: inner circle is just a flat island
                var islandSurfaces = Brep.CreatePlanarBreps(
                    new Curve[] { innerCurve }, tolerance);
                if (islandSurfaces != null)
                {
                    foreach (var srf in islandSurfaces)
                        doc.Objects.AddBrep(srf, attrs);
                }
            }

            // Scale axis lines to half-length and move to arm direction points
            for (int i = 0; i < armCount; i++)
            {
                var halfAxis = new LineCurve(outerIntersections[i],
                    new Point3d(
                        (outerIntersections[i].X + armDirections[i].X) / 2,
                        (outerIntersections[i].Y + armDirections[i].Y) / 2,
                        (outerIntersections[i].Z + armDirections[i].Z) / 2));
                doc.Objects.AddCurve(halfAxis, attrs);
            }

            RhinoApp.WriteLine(Strings.RoundaboutCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
