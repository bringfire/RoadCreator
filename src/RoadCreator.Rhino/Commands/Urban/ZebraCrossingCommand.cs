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
/// Creates zebra crossing stripes by repeatedly splitting a road surface.
/// Converts from Prechodplochy.rvb.
///
/// Algorithm:
///   1. Select road surface
///   2. Pick crossing start/end points
///   3. Enter crossing width (default 4m)
///   4. Create a narrow stripe surface (width × 0.5m)
///   5. Loop: for each 1m step along crossing direction:
///      - Rotate stripe to crossing angle
///      - Split road surface
///      - Keep the larger part, advance stripe 1m
///   6. Delete stripe
///
/// Result: road surface split into alternating stripes (zebra pattern).
/// </summary>
[System.Runtime.InteropServices.Guid("F6A7B8C9-D0E1-2345-F012-3456789ABCDE")]
public class ZebraCrossingCommand : Command
{
    public override string EnglishName => "RC_ZebraCrossing";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select road surface
        var getSurface = new GetObject();
        getSurface.SetCommandPrompt(Strings.SelectRoadSurface);
        getSurface.GeometryFilter = ObjectType.Brep | ObjectType.Surface;
        if (getSurface.Get() != GetResult.Object)
            return Result.Cancel;
        var roadObjRef = getSurface.Object(0);
        var roadBrep = roadObjRef.Brep();
        if (roadBrep == null)
            return Result.Failure;
        var roadId = roadObjRef.ObjectId;

        // Pick crossing start point
        var getStart = new GetPoint();
        getStart.SetCommandPrompt(Strings.SelectCrossingStart);
        if (getStart.Get() != GetResult.Point)
            return Result.Cancel;
        var startPt = getStart.Point();

        // Pick crossing end point
        var getEnd = new GetPoint();
        getEnd.SetCommandPrompt(Strings.SelectCrossingEnd);
        getEnd.SetBasePoint(startPt, true);
        if (getEnd.Get() != GetResult.Point)
            return Result.Cancel;
        var endPt = getEnd.Point();

        // Enter crossing width
        var getWidth = new GetNumber();
        getWidth.SetCommandPrompt(Strings.EnterCrossingWidth);
        getWidth.SetDefaultNumber(4.0);
        getWidth.SetLowerLimit(1.0, false);
        getWidth.SetUpperLimit(20.0, false);
        if (getWidth.Get() != GetResult.Number)
            return Result.Cancel;
        double crossingWidth = getWidth.Number();

        doc.Views.RedrawEnabled = false;

        try
        {
            double distance = startPt.DistanceTo(endPt);
            double tolerance = doc.ModelAbsoluteTolerance;
            int stripeCount = CrossingStripComputer.ComputeStripeCount(distance);

            if (stripeCount == 0)
            {
                RhinoApp.WriteLine(Strings.CrossingSplitFailed);
                return Result.Failure;
            }

            // Compute crossing direction
            double angle = System.Math.Atan2(endPt.Y - startPt.Y, endPt.X - startPt.X)
                * (180.0 / System.Math.PI);
            double angleRad = (angle + 270) * System.Math.PI / 180.0;

            // Direction vector along crossing (perpendicular to stripes)
            var crossDir = new Vector3d(endPt.X - startPt.X, endPt.Y - startPt.Y, 0);
            crossDir.Unitize();

            // Layer setup
            var layers = new LayerManager(doc);
            string crossingPath = LayerScheme.BuildPath(LayerScheme.Crossing);
            int crossingLayerIdx = layers.EnsureLayer(crossingPath,
                System.Drawing.Color.FromArgb(255, 255, 255));
            var crossingAttrs = new ObjectAttributes { LayerIndex = crossingLayerIdx };

            // Delete original road object
            doc.Objects.Delete(roadId, true);
            var currentBrep = roadBrep;

            // VBScript centering: offset = (fractional_part + 1) / 4
            double remainder = distance - System.Math.Floor(distance);
            var stripeStart = startPt + crossDir * ((remainder + 1.0) / 4.0);

            // Collect all fragments; do NOT add to document during the loop
            var allFragments = new List<Brep>();
            int successfulStripes = 0;

            for (int i = 0; i < stripeCount; i++)
            {
                // Create stripe splitter at current position
                var stripePt = stripeStart + crossDir * (i * CrossingStripComputer.StripeSpacing);

                var stripePlane = Plane.WorldXY;
                var stripeSrf = new PlaneSurface(stripePlane,
                    new Interval(-crossingWidth / 2, crossingWidth / 2),
                    new Interval(-CrossingStripComputer.StripeWidth / 2,
                                  CrossingStripComputer.StripeWidth / 2));
                var stripeBrep = stripeSrf.ToBrep();

                stripeBrep.Translate(stripePt - Point3d.Origin);
                stripeBrep.Rotate(angleRad, Vector3d.ZAxis, stripePt);

                // Split current surface
                var splitParts = currentBrep.Split(stripeBrep, tolerance);
                if (splitParts != null && splitParts.Length >= 2)
                {
                    // Find largest fragment (by area) to continue splitting
                    int largestIdx = 0;
                    double largestArea = 0;
                    for (int j = 0; j < splitParts.Length; j++)
                    {
                        var amp = AreaMassProperties.Compute(splitParts[j]);
                        double area = amp?.Area ?? 0;
                        if (area > largestArea)
                        {
                            largestArea = area;
                            largestIdx = j;
                        }
                    }

                    // Collect non-largest fragments as final pieces
                    for (int j = 0; j < splitParts.Length; j++)
                    {
                        if (j != largestIdx)
                            allFragments.Add(splitParts[j]);
                    }

                    // Continue splitting the largest fragment
                    currentBrep = splitParts[largestIdx];
                    successfulStripes++;
                }
            }

            // Add the final remaining piece
            allFragments.Add(currentBrep);

            // Add all fragments to document at once
            foreach (var frag in allFragments)
                doc.Objects.AddBrep(frag, crossingAttrs);

            RhinoApp.WriteLine(string.Format(Strings.ZebraCrossingCreated, successfulStripes));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
