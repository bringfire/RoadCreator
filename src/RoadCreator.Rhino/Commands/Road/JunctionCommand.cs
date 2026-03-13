using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Geometry.Intersect;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Road;

/// <summary>
/// Creates a junction surface connecting a side road to a main road edge,
/// with filleted corners at configurable radii.
/// Converts from Napojeninasilnici.rvb and Napojeninasilnicimesto.rvb (identical scripts).
///
/// Algorithm:
///   1. Select main road edge curve (3D)
///   2. Select end profile of connecting road (3D cross-section curve)
///   3. Enter two fillet radii (left and right corners)
///   4. Project both curves to XY plane (z=0) for 2D geometry operations
///   5. Find intersection center on main road edge (perpendicular from profile midpoint)
///   6. Create fillet curves at both profile endpoints against the road edge
///   7. Find 3D positions by vertical line intersection with the 3D road edge
///   8. Build boundary curves and create a network surface
///   9. Trim the network surface using vertical extrusions of the fillet curves
/// </summary>
[System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
public class JunctionCommand : Command
{
    /// <summary>
    /// Half-extent for vertical lines/extrusions used to map 2D↔3D.
    /// From VBScript: 50000 units in each direction to ensure intersection.
    /// </summary>
    private const double VerticalExtent = 50000.0;

    /// <summary>
    /// Length of search lines used to find intersections with the road edge.
    /// From VBScript: Array(0, 100, 0).
    /// </summary>
    private const double SearchLineLength = 100.0;

    /// <summary>
    /// Radius of small circle used to find nearby edge points for fillet side selection.
    /// From VBScript: Rhino.AddCircle(pt, 2).
    /// </summary>
    private const double NearbySearchRadius = 2.0;

    public override string EnglishName => "RC_Junction";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select main road edge curve
        var getEdge = new GetObject();
        getEdge.SetCommandPrompt(Strings.SelectRoadEdge);
        getEdge.GeometryFilter = ObjectType.Curve;
        if (getEdge.Get() != GetResult.Object)
            return Result.Cancel;
        var roadEdge3D = getEdge.Object(0).Curve();

        // Select end profile curve of connecting road
        var getProfile = new GetObject();
        getProfile.SetCommandPrompt(Strings.SelectEndProfile);
        getProfile.GeometryFilter = ObjectType.Curve;
        if (getProfile.Get() != GetResult.Object)
            return Result.Cancel;
        var endProfile3D = getProfile.Object(0).Curve();

        var profileStart3D = endProfile3D.PointAtStart;
        var profileEnd3D = endProfile3D.PointAtEnd;

        // Get fillet radii
        var getR1 = new GetNumber();
        getR1.SetCommandPrompt(Strings.EnterFilletRadius1);
        getR1.SetDefaultNumber(7.0);
        getR1.SetLowerLimit(1.0, false);
        getR1.SetUpperLimit(100.0, false);
        if (getR1.Get() != GetResult.Number)
            return Result.Cancel;
        double radius1 = getR1.Number();

        var getR2 = new GetNumber();
        getR2.SetCommandPrompt(Strings.EnterFilletRadius2);
        getR2.SetDefaultNumber(7.0);
        getR2.SetLowerLimit(1.0, false);
        getR2.SetUpperLimit(100.0, false);
        if (getR2.Get() != GetResult.Number)
            return Result.Cancel;
        double radius2 = getR2.Number();

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Step 1: Project curves to XY plane (z=0) for 2D operations
            var projectedEdge = ProjectCurveToXY(roadEdge3D);
            var projectedProfile = ProjectCurveToXY(endProfile3D);

            if (projectedEdge == null || projectedProfile == null)
            {
                RhinoApp.WriteLine(Strings.JunctionProjectionFailed);
                return Result.Failure;
            }

            // Step 2: Get 2D profile endpoints and midpoint
            var prStart = projectedProfile.PointAtStart;
            var prEnd = projectedProfile.PointAtEnd;
            var prMid = projectedProfile.PointAt(projectedProfile.Domain.Mid);

            // Get angle of profile line in XY
            var profileDir = prEnd - prStart;
            double angle = System.Math.Atan2(profileDir.Y, profileDir.X) * (180.0 / System.Math.PI);

            // Step 3: Find junction center — perpendicular from profile midpoint to road edge.
            // VBScript uses a Y-axis line rotated by profile angle, which produces a perpendicular.
            // angle is the profile direction; angle+90 is perpendicular to it.
            double perpAngle = angle + 90.0;
            var centerLine = CreateDirectedLine(prMid, perpAngle, SearchLineLength);
            var centerIntersection = Intersection.CurveCurve(centerLine, projectedEdge, tolerance, tolerance);

            if (centerIntersection == null || centerIntersection.Count == 0)
            {
                // Try flipping 180°
                perpAngle += 180.0;
                centerLine = CreateDirectedLine(prMid, perpAngle, SearchLineLength);
                centerIntersection = Intersection.CurveCurve(centerLine, projectedEdge, tolerance, tolerance);
            }

            if (centerIntersection == null || centerIntersection.Count == 0)
            {
                RhinoApp.WriteLine(Strings.JunctionCenterNotFound);
                return Result.Failure;
            }

            var prCenter = centerIntersection[0].PointB;

            // Step 4: Create perpendicular lines from each profile endpoint
            var perpLine0 = CreateDirectedLine(prStart, perpAngle, SearchLineLength);
            var perpLine1 = CreateDirectedLine(prEnd, perpAngle, SearchLineLength);

            // Find where perpendiculars intersect the road edge
            var isect0 = Intersection.CurveCurve(perpLine0, projectedEdge, tolerance, tolerance);
            var isect1 = Intersection.CurveCurve(perpLine1, projectedEdge, tolerance, tolerance);

            if (isect0 == null || isect0.Count == 0 || isect1 == null || isect1.Count == 0)
            {
                RhinoApp.WriteLine(Strings.JunctionPerpNoIntersect);
                return Result.Failure;
            }

            var edgePt0 = isect0[0].PointB;  // Point on road edge nearest profile start
            var edgePt1 = isect1[0].PointB;  // Point on road edge nearest profile end

            // Step 5: Determine which side of intersection points is away from center.
            // VBScript picks the point farther from center for fillet side selection.
            var outerPt0 = FindOutwardPoint(projectedEdge, edgePt0, prCenter, tolerance);
            var outerPt1 = FindOutwardPoint(projectedEdge, edgePt1, prCenter, tolerance);

            // Step 6: Create fillet curves
            var fillet0 = Curve.CreateFilletCurves(perpLine0, prStart, projectedEdge, outerPt0,
                radius1, true, true, true, tolerance, tolerance);
            if (fillet0 == null || fillet0.Length == 0)
            {
                RhinoApp.WriteLine(Strings.JunctionFitError);
                return Result.Failure;
            }

            var fillet1 = Curve.CreateFilletCurves(perpLine1, prEnd, projectedEdge, outerPt1,
                radius2, true, true, true, tolerance, tolerance);
            if (fillet1 == null || fillet1.Length == 0)
            {
                RhinoApp.WriteLine(Strings.JunctionFitError);
                return Result.Failure;
            }

            // Step 7: Find 3D positions by vertical intersection with 3D road edge
            var tangentPt0_3D = FindVerticalIntersection3D(roadEdge3D, fillet0[2], projectedEdge, tolerance);
            var tangentPt1_3D = FindVerticalIntersection3D(roadEdge3D, fillet1[2], projectedEdge, tolerance);

            if (tangentPt0_3D == null || tangentPt1_3D == null)
            {
                RhinoApp.WriteLine(Strings.JunctionNo3DPosition);
                return Result.Failure;
            }

            // Step 8: Build boundary curves for network surface
            var boundaryLine0 = new LineCurve(tangentPt0_3D.Value, profileStart3D);
            var boundaryLine1 = new LineCurve(tangentPt1_3D.Value, profileEnd3D);

            // Split 3D road edge to get the segment between the two tangent points
            var center3D = FindVerticalIntersectionPoint3D(roadEdge3D, prCenter, tolerance);
            var edgeSegment = ExtractEdgeSegment(roadEdge3D, tangentPt0_3D.Value,
                tangentPt1_3D.Value, center3D, tolerance);

            if (edgeSegment == null)
            {
                RhinoApp.WriteLine(Strings.JunctionEdgeSegmentFailed);
                return Result.Failure;
            }

            // Step 9: Create surface from 4 boundary curves.
            // VBScript uses AddNetworkSrf. For a simple 4-sided boundary,
            // Brep.CreateEdgeSurface is the RhinoCommon equivalent (Gordon surface).
            var boundaryCurves = new[] { endProfile3D, boundaryLine0, edgeSegment, boundaryLine1 };
            var edgeSurface = Brep.CreateEdgeSurface(boundaryCurves);

            if (edgeSurface == null)
            {
                RhinoApp.WriteLine(Strings.JunctionSurfaceFailed);
                return Result.Failure;
            }

            // Step 10: Trim junction surface using fillet curve extrusions
            var mid3D = endProfile3D.PointAt(endProfile3D.Domain.Mid);

            // Join fillet trim curves and extrude vertically for splitting
            var joinedFillet0 = JoinFilletCurves(fillet0);
            var joinedFillet1 = JoinFilletCurves(fillet1);

            var junctionSurface = edgeSurface;

            if (joinedFillet0 != null)
                junctionSurface = TrimWithVerticalExtrusion(junctionSurface, joinedFillet0,
                    mid3D, tolerance);

            if (joinedFillet1 != null)
                junctionSurface = TrimWithVerticalExtrusion(junctionSurface, joinedFillet1,
                    mid3D, tolerance);

            // Add to document
            var layers = new LayerManager(doc);
            string junctionPath = LayerScheme.BuildPath(LayerScheme.Road3D);
            int layerIdx = layers.EnsureLayer(junctionPath,
                System.Drawing.Color.FromArgb(0, 0, 0));

            var attrs = new ObjectAttributes { LayerIndex = layerIdx };
            doc.Objects.AddBrep(junctionSurface, attrs);

            RhinoApp.WriteLine(Strings.JunctionCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    /// <summary>
    /// Project a 3D curve onto the XY plane (z=0).
    /// VBScript: ProjectCurveToSurface with large flat surface at z=0.
    /// Uses Curve.ProjectToPlane for exact projection (preserves arcs, degree, etc.).
    /// </summary>
    private static Curve? ProjectCurveToXY(Curve curve3D)
    {
        return Curve.ProjectToPlane(curve3D, Plane.WorldXY);
    }

    /// <summary>
    /// Create a line from a point in the given direction angle.
    /// </summary>
    private static LineCurve CreateDirectedLine(Point3d origin, double angleDegrees, double length)
    {
        double rad = angleDegrees * System.Math.PI / 180.0;
        var dir = new Vector3d(System.Math.Cos(rad), System.Math.Sin(rad), 0);
        var endPt = origin + dir * length;
        return new LineCurve(origin, endPt);
    }

    /// <summary>
    /// Find the point on the road edge that is farther from center (outward side).
    /// VBScript picks the farther point for fillet side selection.
    /// Uses a small circle at the intersection point to find two nearby edge points.
    /// </summary>
    private static Point3d FindOutwardPoint(Curve edge, Point3d edgePt, Point3d center, double tolerance)
    {
        var circle = new Circle(edgePt, NearbySearchRadius);
        var circleCurve = new ArcCurve(circle);
        var isects = Intersection.CurveCurve(circleCurve, edge, tolerance, tolerance);

        if (isects == null || isects.Count < 2)
            return edgePt;

        // Pick the intersection point farther from center (outward)
        var pt0 = isects[0].PointB;
        var pt1 = isects[1].PointB;
        return pt0.DistanceTo(center) > pt1.DistanceTo(center) ? pt0 : pt1;
    }

    /// <summary>
    /// Find where a 2D fillet curve's tangent point maps to 3D on the road edge.
    /// Uses a vertical line at the fillet-edge tangent point.
    /// </summary>
    private static Point3d? FindVerticalIntersection3D(
        Curve roadEdge3D, Curve filletArcCurve, Curve projectedEdge, double tolerance)
    {
        // Find where fillet arc meets the projected edge
        var isect = Intersection.CurveCurve(filletArcCurve, projectedEdge, tolerance, tolerance);
        if (isect == null || isect.Count == 0) return null;

        var pt2D = isect[0].PointA;
        return FindVerticalIntersectionPoint3D(roadEdge3D, pt2D, tolerance);
    }

    /// <summary>
    /// Find the 3D point on the road edge directly above/below a 2D XY point.
    /// </summary>
    private static Point3d? FindVerticalIntersectionPoint3D(
        Curve roadEdge3D, Point3d pt2D, double tolerance)
    {
        var vertLine = new LineCurve(
            new Point3d(pt2D.X, pt2D.Y, -VerticalExtent),
            new Point3d(pt2D.X, pt2D.Y, VerticalExtent));

        var isect = Intersection.CurveCurve(vertLine, roadEdge3D, tolerance, tolerance);
        if (isect == null || isect.Count == 0) return null;
        return isect[0].PointB;
    }

    /// <summary>
    /// Extract the segment of the road edge between two 3D points,
    /// ensuring the segment passes through (or near) the center point.
    /// Uses center point's parameter to verify correct segment selection.
    /// </summary>
    private static Curve? ExtractEdgeSegment(
        Curve roadEdge3D, Point3d pt0, Point3d pt1, Point3d? center3D, double tolerance)
    {
        if (!roadEdge3D.ClosestPoint(pt0, out double t0)) return null;
        if (!roadEdge3D.ClosestPoint(pt1, out double t1)) return null;

        // Ensure t0 < t1
        if (t0 > t1) (t0, t1) = (t1, t0);

        var segment = roadEdge3D.Trim(t0, t1);
        if (segment == null) return null;

        // Verify the segment contains the center point (not the complement arc).
        // If center's parameter falls between t0 and t1, we have the correct segment.
        if (center3D.HasValue && roadEdge3D.ClosestPoint(center3D.Value, out double tCenter))
        {
            bool centerInSegment = tCenter >= t0 && tCenter <= t1;
            if (!centerInSegment)
            {
                // Center is outside [t0,t1] → we need the complement segments
                var seg1 = roadEdge3D.Trim(roadEdge3D.Domain.Min, t0);
                var seg2 = roadEdge3D.Trim(t1, roadEdge3D.Domain.Max);
                if (seg1 != null && seg2 != null)
                {
                    var joined = Curve.JoinCurves(new[] { seg1, seg2 }, tolerance);
                    if (joined != null && joined.Length > 0)
                        return joined[0];
                }
            }
        }

        return segment;
    }

    /// <summary>
    /// Join the perpendicular trim line and fillet arc from FilletCurves result.
    /// FilletCurves returns [trimmedCurve0, trimmedCurve1, filletArc].
    /// We join trimmedCurve0 (the perpendicular) and filletArc for the extrusion cutter.
    /// </summary>
    private static Curve? JoinFilletCurves(Curve[] filletResult)
    {
        if (filletResult == null || filletResult.Length < 3)
            return null;

        var joined = Curve.JoinCurves(new[] { filletResult[0], filletResult[2] });
        return joined != null && joined.Length > 0 ? joined[0] : null;
    }

    /// <summary>
    /// Trim a brep by extruding a 2D curve vertically and splitting.
    /// Keeps the part containing the reference point (mid3D).
    /// VBScript: ExtrudeCurveStraight then DoSplit, keep part with midpoint.
    /// </summary>
    private static Brep TrimWithVerticalExtrusion(
        Brep surface, Curve trimCurve, Point3d keepNear, double tolerance)
    {
        var extrusion = Surface.CreateExtrusion(trimCurve,
            new Vector3d(0, 0, 2 * VerticalExtent));
        if (extrusion == null)
        {
            RhinoApp.WriteLine(Strings.JunctionTrimExtrusionFailed);
            return surface;
        }

        var extrusionBrep = extrusion.ToBrep();
        // Center the extrusion vertically
        extrusionBrep.Translate(new Vector3d(0, 0, -VerticalExtent));

        var splitParts = surface.Split(extrusionBrep, tolerance);
        if (splitParts == null || splitParts.Length < 2)
        {
            RhinoApp.WriteLine(Strings.JunctionTrimSplitFailed);
            return surface;
        }

        // Keep the part closest to the reference point
        Brep? closest = null;
        double minDist = double.MaxValue;
        foreach (var part in splitParts)
        {
            var centroid = AreaMassProperties.Compute(part);
            if (centroid == null) continue;
            double dist = centroid.Centroid.DistanceTo(keepNear);
            if (dist < minDist)
            {
                minDist = dist;
                closest = part;
            }
        }

        return closest ?? surface;
    }
}
