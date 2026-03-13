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
/// Places road delineator poles along one side of a road edge with adaptive spacing.
/// Converts from silnicnisloupkyjednostranny.rvb.
///
/// Algorithm:
///   1. Select road edge curve
///   2. Pick road axis direction (to determine pole facing side)
///   3. Divide curve at 5m base intervals
///   4. At each point: estimate local radius from angle change
///   5. Place pole only when enough intervals have passed (radius-dependent)
///   6. Pole objects come from RC_Database layer ("Silnicnisloupek(RoadCreator)")
/// </summary>
[System.Runtime.InteropServices.Guid("A1000005-B2C3-D4E5-F6A7-B8C9D0E1F205")]
public class RoadPolesSingleCommand : Command
{
    private sealed record RoadPolesSingleInputs(
        Curve EdgeCurve,
        Point3d RoadAxis);

    public override string EnglishName => "RC_RoadPolesSingle";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectPoleEdgeCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var edgeCurve = getCurve.Object(0).Curve();
        if (edgeCurve == null)
            return Result.Cancel;

        var getDir = new GetPoint();
        getDir.SetCommandPrompt(Strings.SelectRoadAxisForPoles);
        if (getDir.Get() != GetResult.Point)
            return Result.Cancel;
        var roadAxis = getDir.Point();

        return RunCore(doc, new RoadPolesSingleInputs(edgeCurve, roadAxis));
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

        var getEdgeRef = new GetPoint();
        getEdgeRef.SetCommandPrompt("Select edge side for poles");
        if (getEdgeRef.Get() != GetResult.Point)
            return Result.Cancel;
        var edgeReference = getEdgeRef.Point();

        var edgeCurve = RoadCurveResolver.ResolveNearestBoundaryCurve(doc, roadName, edgeReference);
        if (edgeCurve == null)
        {
            RhinoApp.WriteLine($"No road boundary curve found for road '{roadName}'.");
            return Result.Failure;
        }

        var getAxis = new GetPoint();
        getAxis.SetCommandPrompt(Strings.SelectRoadAxisForPoles);
        if (getAxis.Get() != GetResult.Point)
            return Result.Cancel;
        var roadAxis = getAxis.Point();

        return RunCore(doc, new RoadPolesSingleInputs(edgeCurve, roadAxis));
    }

    private Result RunCore(RhinoDoc doc, RoadPolesSingleInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            // Find pole template in database
            var poleObjects = FindDatabasePoleObjects(doc);
            if (poleObjects == null)
                return Result.Failure;

            double tolerance = doc.ModelAbsoluteTolerance;

            // Layer setup
            var layers = new LayerManager(doc);
            int poleLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.RoadPoles),
                System.Drawing.Color.FromArgb(0, 0, 0));
            var poleAttrs = new ObjectAttributes { LayerIndex = poleLayerIdx };

            // Divide at base interval.
            // includeEnds=true: includes both start and endpoint (may produce shorter final segment).
            inputs.EdgeCurve.DivideByLength(PoleSpacingComputer.BaseInterval, true,
                out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length < 2)
                return Result.Failure;

            // Determine facing side
            double sideOffset = DetermineSideOffset(inputs.EdgeCurve, inputs.RoadAxis, divPoints, tolerance);

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
                    // Estimate radius from angle change of consecutive segments
                    double angle0 = AngleBetween2D(divPoints[i - 1], divPoints[i]);
                    double angle1 = AngleBetween2D(divPoints[i], divPoints[i + 1]);
                    double diff = System.Math.Abs(angle0 - angle1);
                    radius = PoleSpacingComputer.EstimateRadius(diff);
                }

                int requiredSkip = PoleSpacingComputer.GetRequiredSkip(radius);
                bool shouldPlace = isEndpoint || (i - lastPlaced >= requiredSkip);

                if (shouldPlace)
                {
                    inputs.EdgeCurve.ClosestPoint(divPoints[i], out double t);
                    var tangent = inputs.EdgeCurve.TangentAt(t);
                    double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

                    // VBScript: R > 1250 uses angle only (no Strana); other bands use angle + Strana
                    double rotation;
                    if (isEndpoint)
                        rotation = tangentAngle + sideOffset;
                    else if (radius > 1250)
                        rotation = tangentAngle;
                    else
                        rotation = tangentAngle + sideOffset;

                    PlacePoleFromDatabase(doc, poleObjects, divPoints[i], rotation + 90, poleAttrs);
                    lastPlaced = i;
                }
            }

            // Restore database layer state
            RestoreDatabaseLayer(doc, layers);

            RhinoApp.WriteLine(Strings.RoadPolesCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static double DetermineSideOffset(Curve curve, Point3d roadAxis,
        Point3d[] divPoints, double tolerance)
    {
        // Find closest point on curve to road axis
        curve.ClosestPoint(roadAxis, out double t);
        var closestPt = curve.PointAt(t);
        var tangent = curve.TangentAt(t);
        double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

        // Test perpendicular direction
        var perpDir = new Vector3d(-tangent.Y, tangent.X, 0);
        perpDir.Unitize();
        var testPtA = closestPt + perpDir;
        var testPtB = closestPt - perpDir;

        double distA = roadAxis.DistanceTo(testPtA);
        double distB = roadAxis.DistanceTo(testPtB);

        return distB > distA ? 0 : 180;
    }

    internal static RhinoObject[]? FindDatabasePoleObjects(RhinoDoc doc)
    {
        string dbPath = LayerScheme.BuildPath(LayerScheme.Database);
        int dbLayerIdx = doc.Layers.FindByFullPath(dbPath, -1);
        if (dbLayerIdx < 0)
        {
            RhinoApp.WriteLine(Strings.PolesDatabaseNotFound);
            return null;
        }

        // Unlock and show database layer temporarily
        var dbLayer = doc.Layers[dbLayerIdx];
        bool wasLocked = dbLayer.IsLocked;
        bool wasVisible = dbLayer.IsVisible;
        dbLayer.IsLocked = false;
        dbLayer.IsVisible = true;
        doc.Layers.Modify(dbLayer, dbLayerIdx, true);

        // Find pole objects by name using ObjectEnumeratorSettings
        var settings = new ObjectEnumeratorSettings
        {
            LayerIndexFilter = dbLayerIdx,
            NameFilter = "Silnicnisloupek(RoadCreator)",
        };
        var objects = doc.Objects.GetObjectList(settings).ToArray();
        if (objects.Length == 0)
        {
            RhinoApp.WriteLine(Strings.PoleObjectNotFound);
            return null;
        }

        return objects;
    }

    internal static void PlacePoleFromDatabase(RhinoDoc doc, RhinoObject[] poleObjects,
        Point3d position, double rotation, ObjectAttributes attrs)
    {
        // VBScript base point: (-1, 0, 0)
        var basePoint = new Point3d(-1, 0, 0);

        foreach (var poleObj in poleObjects)
        {
            if (poleObj.Geometry == null) continue;
            var copy = poleObj.Geometry.Duplicate();
            copy.Transform(Transform.Translation(position - basePoint));
            copy.Transform(Transform.Rotation(rotation * System.Math.PI / 180.0,
                Vector3d.ZAxis, position));
            attrs.Name = "Silnicnisloupek";
            doc.Objects.Add(copy, attrs);
        }
    }

    internal static void RestoreDatabaseLayer(RhinoDoc doc, LayerManager layers)
    {
        string dbPath = LayerScheme.BuildPath(LayerScheme.Database);
        layers.LockLayer(dbPath);
        layers.SetLayerVisible(dbPath, false);
    }

    private static double AngleBetween2D(Point3d from, Point3d to)
    {
        return System.Math.Atan2(to.Y - from.Y, to.X - from.X) * (180.0 / System.Math.PI);
    }
}
