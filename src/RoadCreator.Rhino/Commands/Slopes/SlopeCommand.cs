using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Slopes;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Slopes;

/// <summary>
/// Generates embankment/cut slopes along both road edges, with optional drainage ditches.
/// Converts from RC2_Svahy_CZ.rvb.
///
/// Algorithm:
///   1. Select terrain surface/mesh
///   2. Select two road edge curves (left and right boundaries from Phase 4)
///   3. Set fill/cut slope ratios (1:n) and optional ditch parameters
///   4. At each edge's start/end, build slope profile polyline
///   5. Determine outward direction (away from opposite edge)
///   6. Sweep profiles along each edge → slope surfaces
///   7. Create end-cap surfaces between edges
///   8. Split terrain and slopes against each other for fill/cut visibility
///   9. Flip surface normals for consistent orientation
/// </summary>
[System.Runtime.InteropServices.Guid("F6A7B8C9-D0E1-3F4A-5B6C-7D8E9F0A1B2C")]
public class SlopeCommand : Command
{
    public override string EnglishName => "RC_Slopes";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select terrain
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectTerrain);
        getTerrain.GeometryFilter = ObjectType.Surface | ObjectType.Mesh | ObjectType.Brep;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;
        var terrainRef = getTerrain.Object(0);
        var terrainGeo = terrainRef.Geometry();

        // Select left road edge
        var getLeft = new GetObject();
        getLeft.SetCommandPrompt(Strings.SelectLeftEdge);
        getLeft.GeometryFilter = ObjectType.Curve;
        if (getLeft.Get() != GetResult.Object)
            return Result.Cancel;
        var leftEdge = getLeft.Object(0).Curve();

        // Select right road edge
        var getRight = new GetObject();
        getRight.SetCommandPrompt(Strings.SelectRightEdge);
        getRight.GeometryFilter = ObjectType.Curve;
        if (getRight.Get() != GetResult.Object)
            return Result.Cancel;
        var rightEdge = getRight.Object(0).Curve();

        // Get fill slope ratio
        var getFill = new GetNumber();
        getFill.SetCommandPrompt(Strings.EnterFillSlope);
        getFill.SetDefaultNumber(1.75);
        getFill.SetLowerLimit(1.0, false);
        getFill.SetUpperLimit(4.0, false);
        if (getFill.Get() != GetResult.Number)
            return Result.Cancel;
        double fillRatio = getFill.Number();

        // Get cut slope ratio
        var getCut = new GetNumber();
        getCut.SetCommandPrompt(Strings.EnterCutSlope);
        getCut.SetDefaultNumber(1.75);
        getCut.SetLowerLimit(1.0, false);
        getCut.SetUpperLimit(4.0, false);
        if (getCut.Get() != GetResult.Number)
            return Result.Cancel;
        double cutRatio = getCut.Number();

        // Include ditches?
        var getDitch = new GetOption();
        getDitch.SetCommandPrompt(Strings.IncludeDitches);
        int yesIdx = getDitch.AddOption("Yes");
        getDitch.AddOption("No");
        if (getDitch.Get() != GetResult.Option)
            return Result.Cancel;
        bool includeDitch = getDitch.OptionIndex() == yesIdx;

        double ditchDepth = 0;
        double ditchWidth = 0;
        if (includeDitch)
        {
            var getDepth = new GetNumber();
            getDepth.SetCommandPrompt(Strings.EnterDitchDepth);
            getDepth.SetDefaultNumber(0.4);
            getDepth.SetLowerLimit(0.3, false);
            getDepth.SetUpperLimit(2.0, false);
            if (getDepth.Get() != GetResult.Number)
                return Result.Cancel;
            ditchDepth = getDepth.Number();

            var getWidth = new GetNumber();
            getWidth.SetCommandPrompt(Strings.EnterDitchWidth);
            getWidth.SetDefaultNumber(0.5);
            getWidth.SetLowerLimit(0.2, false);
            getWidth.SetUpperLimit(2.0, false);
            if (getWidth.Get() != GetResult.Number)
                return Result.Cancel;
            ditchWidth = getWidth.Number();
        }

        // Parse road name from edge curve names
        string roadName = ParseRoadName(getLeft.Object(0).Object());

        doc.Views.RedrawEnabled = false;

        try
        {
            // Compute slope profiles from Core
            var slopeProfile = SlopeProfileComputer.ComputeSlopeProfile(
                fillRatio, cutRatio, includeDitch, ditchDepth, ditchWidth);
            var embankmentProfile = SlopeProfileComputer.ComputeEmbankmentProfile(cutRatio);

            // Layer setup
            var layers = new LayerManager(doc);
            string slopePath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.Slopes);
            int slopeLayerIdx = layers.EnsureLayer(slopePath,
                System.Drawing.Color.FromArgb(150, 75, 0));

            var slopeAttrs = new ObjectAttributes { LayerIndex = slopeLayerIdx };

            // Process each edge: create slope and embankment surfaces
            var allSlopeIds = new List<Guid>();

            ProcessEdge(doc, leftEdge, rightEdge, slopeProfile, embankmentProfile,
                slopeAttrs, allSlopeIds);
            ProcessEdge(doc, rightEdge, leftEdge, slopeProfile, embankmentProfile,
                slopeAttrs, allSlopeIds);

            // Split slopes against terrain for fill/cut visibility
            SplitSlopesAgainstTerrain(doc, terrainGeo, allSlopeIds, includeDitch, slopeAttrs);

            // Flip surface normals for consistent orientation
            FlipSlopeNormals(doc, slopeLayerIdx);

            RhinoApp.WriteLine(Strings.SlopesCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    /// <summary>
    /// Process one road edge: build profiles at start/end, determine outward direction,
    /// sweep to create slope and embankment surfaces.
    /// </summary>
    private static void ProcessEdge(
        RhinoDoc doc, Curve edge, Curve oppositeEdge,
        Core.Math.Point3[] slopeProfile, Core.Math.Point3[] embankmentProfile,
        ObjectAttributes attrs, List<Guid> allSlopeIds)
    {
        var startPt = edge.PointAtStart;
        var endPt = edge.PointAtEnd;

        // Build Rhino polylines from Core profile points (remap Y→Z for 3D)
        var slopeStartCurve = CreateProfileCurve(slopeProfile);
        var slopeEndCurve = CreateProfileCurve(slopeProfile);
        var embStartCurve = CreateProfileCurve(embankmentProfile);
        var embEndCurve = CreateProfileCurve(embankmentProfile);

        // Get tangent angles at start and end for perpendicular orientation
        double startAngle = GetTangentAngle(edge, edge.Domain.Min);
        double endAngle = GetTangentAngle(edge, edge.Domain.Max);

        // Position and orient profiles at edge endpoints
        OrientProfile(slopeStartCurve, startPt, startAngle + 90);
        OrientProfile(slopeEndCurve, endPt, endAngle + 90);
        OrientProfile(embStartCurve, startPt, startAngle + 90);
        OrientProfile(embEndCurve, endPt, endAngle + 90);

        // Determine outward direction: profiles should point away from opposite edge.
        // Only checked at start — SweepOneRail maintains relative orientation along the rail,
        // so start and end profiles always need the same flip direction.
        bool needsFlip = ShouldFlipDirection(slopeStartCurve, startPt, oppositeEdge);
        if (needsFlip)
        {
            RotateProfile(slopeStartCurve, startPt, 180);
            RotateProfile(slopeEndCurve, endPt, 180);
            RotateProfile(embStartCurve, startPt, 180);
            RotateProfile(embEndCurve, endPt, 180);
        }

        // Sweep slope profiles along edge
        var sweep = new SweepOneRail();
        var slopeBreps = sweep.PerformSweep(edge,
            new[] { slopeStartCurve, slopeEndCurve });
        if (slopeBreps != null)
        {
            foreach (var brep in slopeBreps)
            {
                var id = doc.Objects.AddBrep(brep, attrs);
                if (id != Guid.Empty)
                    allSlopeIds.Add(id);
            }
        }

        // Sweep embankment profiles along edge
        var embBreps = sweep.PerformSweep(edge,
            new[] { embStartCurve, embEndCurve });
        if (embBreps != null)
        {
            foreach (var brep in embBreps)
            {
                var id = doc.Objects.AddBrep(brep, attrs);
                if (id != Guid.Empty)
                    allSlopeIds.Add(id);
            }
        }
    }

    /// <summary>
    /// Split slope surfaces against terrain to determine fill/cut visibility.
    /// From VBScript: DoSplit(slope, terrain) then delete appropriate half.
    /// </summary>
    private static void SplitSlopesAgainstTerrain(
        RhinoDoc doc, GeometryBase terrainGeo,
        List<Guid> slopeIds, bool includeDitch, ObjectAttributes attrs)
    {
        var terrainBrep = terrainGeo as Brep
            ?? (terrainGeo as Surface)?.ToBrep()
            ?? (terrainGeo is Mesh mesh ? Brep.CreateFromMesh(mesh, false) : null);

        if (terrainBrep == null)
            return;

        double tolerance = doc.ModelAbsoluteTolerance;

        foreach (var slopeId in slopeIds)
        {
            var rhinoObj = doc.Objects.FindId(slopeId);
            if (rhinoObj?.Geometry is not Brep slope)
                continue;

            var splitResults = slope.Split(terrainBrep, tolerance);
            if (splitResults == null || splitResults.Length < 2)
                continue;

            // VBScript logic: with ditches → delete upper part (index 0),
            // without ditches → delete lower part (last index).
            // We keep the opposite part.
            doc.Objects.Delete(slopeId, true);
            if (includeDitch)
            {
                for (int i = 1; i < splitResults.Length; i++)
                    doc.Objects.AddBrep(splitResults[i], attrs);
            }
            else
            {
                for (int i = 0; i < splitResults.Length - 1; i++)
                    doc.Objects.AddBrep(splitResults[i], attrs);
            }
        }
    }

    /// <summary>
    /// Flip normals on all slope surfaces for consistent rendering.
    /// From Svahyteren.rvb: _Flip command on all Svahy layer objects.
    /// </summary>
    private static void FlipSlopeNormals(RhinoDoc doc, int slopeLayerIdx)
    {
        var layer = doc.Layers[slopeLayerIdx];
        if (layer == null) return;
        var objects = doc.Objects.FindByLayer(layer);
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (obj.Geometry is Brep brep)
            {
                brep.Flip();
                obj.CommitChanges();
            }
        }
    }

    /// <summary>
    /// Create a Rhino PolylineCurve from Core profile points, remapping Y→Z.
    /// </summary>
    private static PolylineCurve CreateProfileCurve(Core.Math.Point3[] profilePts)
    {
        var rhinoPts = new List<Point3d>();
        foreach (var p in profilePts)
            rhinoPts.Add(new Point3d(p.X, 0, p.Y)); // X=offset, Y=0, Z=elevation

        return new Polyline(rhinoPts).ToPolylineCurve();
    }

    /// <summary>
    /// Get the tangent angle (in degrees, in XY plane) at a parameter on the curve.
    /// From VBScript: circle-extrude-intersect method simplified to RhinoCommon tangent.
    /// </summary>
    private static double GetTangentAngle(Curve curve, double t)
    {
        var tangent = curve.TangentAt(t);
        return System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);
    }

    /// <summary>
    /// Position a profile curve at a point and rotate it to the given angle.
    /// </summary>
    private static void OrientProfile(PolylineCurve curve, Point3d position, double angleDegrees)
    {
        // Move from origin to position
        curve.Translate(position - Point3d.Origin);
        // Rotate around Z-axis at position
        curve.Rotate(angleDegrees * System.Math.PI / 180.0, Vector3d.ZAxis, position);
    }

    /// <summary>
    /// Rotate a profile 180 degrees around the Z-axis at a given point.
    /// </summary>
    private static void RotateProfile(PolylineCurve curve, Point3d center, double angleDegrees)
    {
        curve.Rotate(angleDegrees * System.Math.PI / 180.0, Vector3d.ZAxis, center);
    }

    /// <summary>
    /// Determine if the profile needs to be flipped 180° to point outward.
    /// From VBScript: compare distances from opposite edge to profile endpoint.
    /// If the current orientation points toward the opposite edge, flip it.
    /// </summary>
    private static bool ShouldFlipDirection(
        PolylineCurve profileCurve, Point3d edgePoint, Curve oppositeEdge)
    {
        var profileEnd = profileCurve.PointAtEnd;

        // Get closest point on opposite edge
        if (!oppositeEdge.ClosestPoint(edgePoint, out double t))
            return false;
        var closestOnOpposite = oppositeEdge.PointAt(t);

        // If profile endpoint is closer to opposite edge than the edge point itself,
        // the profile is pointing inward → needs flip
        double distEdge = edgePoint.DistanceTo(closestOnOpposite);
        double distProfile = profileEnd.DistanceTo(closestOnOpposite);

        return distProfile < distEdge;
    }

    /// <summary>
    /// Extract road name from edge curve object name.
    /// </summary>
    private static string ParseRoadName(RhinoObject obj)
    {
        string name = obj?.Attributes.Name ?? "";
        var parts = name.Split(' ');
        return parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : "";
    }
}
