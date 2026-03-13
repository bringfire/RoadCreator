using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Places road delineator poles on both sides of a road with adaptive spacing.
/// Converts from silnicnisloupkyoboustranny.rvb.
///
/// Algorithm:
///   1. Select two road edge curves
///   2. Use shorter curve for spacing (the other is the "projection" target)
///   3. Divide shorter curve at 5m base intervals
///   4. At each point: estimate local radius, place poles on both curves
///   5. Pole on second curve is placed at closest point
/// </summary>
[System.Runtime.InteropServices.Guid("A1000006-B2C3-D4E5-F6A7-B8C9D0E1F206")]
public class RoadPolesDoubleCommand : Command
{
    private sealed record RoadPolesDoubleInputs(
        Curve Curve1,
        Curve Curve2);

    public override string EnglishName => "RC_RoadPolesDouble";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        var getCurve1 = new GetObject();
        getCurve1.SetCommandPrompt(Strings.SelectPoleEdgeCurve);
        getCurve1.GeometryFilter = ObjectType.Curve;
        if (getCurve1.Get() != GetResult.Object)
            return Result.Cancel;
        var curve1 = getCurve1.Object(0).Curve();
        if (curve1 == null)
            return Result.Cancel;

        var getCurve2 = new GetObject();
        getCurve2.SetCommandPrompt(Strings.SelectSecondPoleEdgeCurve);
        getCurve2.GeometryFilter = ObjectType.Curve;
        getCurve2.EnablePreSelect(false, true);
        if (getCurve2.Get() != GetResult.Object)
            return Result.Cancel;
        var curve2 = getCurve2.Object(0).Curve();
        if (curve2 == null)
            return Result.Cancel;

        return RunCore(doc, new RoadPolesDoubleInputs(curve1, curve2));
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

        var curves = RoadCurveResolver.ResolveBoundaryCurves(doc, roadName);
        if (curves.Length < 2)
        {
            RhinoApp.WriteLine($"Need two road boundary curves for road '{roadName}'.");
            return Result.Failure;
        }

        return RunCore(doc, new RoadPolesDoubleInputs(curves[0], curves[1]));
    }

    private Result RunCore(RhinoDoc doc, RoadPolesDoubleInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            // Find pole template in database
            var poleObjects = RoadPolesSingleCommand.FindDatabasePoleObjects(doc);
            if (poleObjects == null)
                return Result.Failure;

            double tolerance = doc.ModelAbsoluteTolerance;

            // Use shorter curve for spacing
            Curve primaryCurve, secondaryCurve;
            if (inputs.Curve1.GetLength() <= inputs.Curve2.GetLength())
            {
                primaryCurve = inputs.Curve1;
                secondaryCurve = inputs.Curve2;
            }
            else
            {
                primaryCurve = inputs.Curve2;
                secondaryCurve = inputs.Curve1;
            }

            // Layer setup
            var layers = new LayerManager(doc);
            int poleLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.RoadPoles),
                System.Drawing.Color.FromArgb(0, 0, 0));
            var poleAttrs = new ObjectAttributes { LayerIndex = poleLayerIdx };

            // Divide primary curve at base interval.
            // includeEnds=true: includes both start and endpoint (may produce shorter final segment).
            primaryCurve.DivideByLength(PoleSpacingComputer.BaseInterval, true,
                out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length < 2)
                return Result.Failure;

            // Determine facing side
            secondaryCurve.ClosestPoint(divPoints[0], out double tTest);
            var oppositePoint = secondaryCurve.PointAt(tTest);
            primaryCurve.ClosestPoint(divPoints[0], out double tPrim);
            var tangent0 = primaryCurve.TangentAt(tPrim);
            double tangentAngle0 = System.Math.Atan2(tangent0.Y, tangent0.X) * (180.0 / System.Math.PI);

            var perpDir = new Vector3d(-tangent0.Y, tangent0.X, 0);
            perpDir.Unitize();
            var testA = divPoints[0] + perpDir;
            var testB = divPoints[0] - perpDir;
            double distA = oppositePoint.DistanceTo(testA);
            double distB = oppositePoint.DistanceTo(testB);
            double sideOffset = distB > distA ? 180 : 0;

            int lastPlaced = 0;

            for (int i = 0; i < divPoints.Length; i++)
            {
                bool isEndpoint = (i == 0 || i == divPoints.Length - 1);
                double radius;

                if (isEndpoint)
                {
                    radius = PoleSpacingComputer.StraightRadius;
                }
                else
                {
                    double angle0 = AngleBetween2D(divPoints[i - 1], divPoints[i]);
                    double angle1 = AngleBetween2D(divPoints[i], divPoints[i + 1]);
                    double diff = System.Math.Abs(angle0 - angle1);
                    radius = PoleSpacingComputer.EstimateRadius(diff);
                }

                int requiredSkip = PoleSpacingComputer.GetRequiredSkip(radius);
                bool shouldPlace = isEndpoint || (i - lastPlaced >= requiredSkip);

                if (shouldPlace)
                {
                    primaryCurve.ClosestPoint(divPoints[i], out double t);
                    var tangent = primaryCurve.TangentAt(t);
                    double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

                    // VBScript: R > 1250 and R <= 50 use angle without Strana
                    double primaryRotation, secondaryRotation;
                    if (radius > 1250 || radius <= 50)
                    {
                        primaryRotation = tangentAngle + 270;
                        secondaryRotation = tangentAngle + 90;
                    }
                    else
                    {
                        primaryRotation = tangentAngle + sideOffset + 270;
                        secondaryRotation = tangentAngle + sideOffset + 90;
                    }

                    // Pole on primary side (faces away from road)
                    RoadPolesSingleCommand.PlacePoleFromDatabase(doc, poleObjects,
                        divPoints[i], primaryRotation, poleAttrs);

                    // Pole on secondary side (closest point)
                    secondaryCurve.ClosestPoint(divPoints[i], out double t2);
                    var secondaryPt = secondaryCurve.PointAt(t2);
                    RoadPolesSingleCommand.PlacePoleFromDatabase(doc, poleObjects,
                        secondaryPt, secondaryRotation, poleAttrs);

                    lastPlaced = i;
                }
            }

            // Restore database layer state
            var layerMgr = new LayerManager(doc);
            RoadPolesSingleCommand.RestoreDatabaseLayer(doc, layerMgr);

            RhinoApp.WriteLine(Strings.RoadPolesCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static double AngleBetween2D(Point3d from, Point3d to)
    {
        return System.Math.Atan2(to.Y - from.Y, to.X - from.X) * (180.0 / System.Math.PI);
    }
}
