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
/// Creates a pedestrian crossing area by splitting a road surface with a rectangle.
/// Converts from Prechodyplocha.rvb.
///
/// Algorithm:
///   1. Select road surface (brep/polysurface)
///   2. Pick crossing start point
///   3. Pick crossing end point (defines direction across road)
///   4. Enter crossing width (default 4m, along road direction)
///   5. Create a plane surface sized width × 2×distance
///   6. Center on width axis, position at midpoint between start/end
///   7. Rotate to crossing angle (+270°)
///   8. Split road surface with the plane, delete splitter
/// </summary>
[System.Runtime.InteropServices.Guid("E5F6A7B8-C9D0-1234-EF01-23456789ABCD")]
public class CrossingAreaCommand : Command
{
    public override string EnglishName => "RC_CrossingArea";

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

            // Compute crossing angle from start to end
            double angle = System.Math.Atan2(endPt.Y - startPt.Y, endPt.X - startPt.X)
                * (180.0 / System.Math.PI);

            // VBScript placement sequence:
            //   1. Create plane (width × 2*distance) at world XY
            //   2. Move by (-width/2, 0, 0) to center on width
            //   3. Move from (0, +distance/2, 0) to startpoint
            //   4. Rotate around startpoint by (angle + 270)
            var plane = Plane.WorldXY;
            var splitterSrf = new PlaneSurface(plane,
                new Interval(0, crossingWidth),
                new Interval(0, distance * 2));
            var splitter = splitterSrf.ToBrep();

            // Step 2: center on width
            splitter.Translate(new Vector3d(-crossingWidth / 2, 0, 0));

            // Step 3: move from (0, +distance/2, 0) to startpoint
            splitter.Translate(startPt - new Point3d(0, distance / 2, 0));

            // Step 4: rotate around startpoint
            splitter.Rotate((angle + 270) * System.Math.PI / 180.0,
                Vector3d.ZAxis, startPt);

            // Split road surface
            var splitParts = roadBrep.Split(splitter, tolerance);
            if (splitParts == null || splitParts.Length < 2)
            {
                RhinoApp.WriteLine(Strings.CrossingSplitFailed);
                return Result.Failure;
            }

            // Layer setup
            var layers = new LayerManager(doc);
            string crossingPath = LayerScheme.BuildPath(LayerScheme.Crossing);
            int crossingLayerIdx = layers.EnsureLayer(crossingPath,
                System.Drawing.Color.FromArgb(255, 255, 255));
            var crossingAttrs = new ObjectAttributes { LayerIndex = crossingLayerIdx };

            // Delete original road object and add split parts
            doc.Objects.Delete(roadId, true);
            foreach (var part in splitParts)
                doc.Objects.AddBrep(part, crossingAttrs);

            RhinoApp.WriteLine(Strings.CrossingAreaCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
