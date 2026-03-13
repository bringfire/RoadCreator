using global::Rhino.Geometry;
using global::Rhino.Geometry.Intersect;
using RoadCreator.Core.Terrain;
using CorePoint3 = RoadCreator.Core.Math.Point3;
using CoreVector3 = RoadCreator.Core.Math.Vector3;

namespace RoadCreator.Rhino.Terrain;

/// <summary>
/// ITerrain implementation wrapping a Rhino Brep/Surface.
/// Replaces all "If Rhino.IsSurface()" branches in the original scripts.
/// </summary>
public class SurfaceTerrainAdapter : ITerrain
{
    private readonly Brep _brep;
    private readonly double _tolerance;

    public SurfaceTerrainAdapter(Brep brep, double tolerance = 0.001)
    {
        _brep = brep ?? throw new ArgumentNullException(nameof(brep));
        _tolerance = tolerance;
    }

    public bool IsMesh => false;

    public CorePoint3? ProjectPoint(CorePoint3 point, CoreVector3 direction)
    {
        var rhinoPt = RhinoConversions.ToRhino(point);
        var rhinoDir = RhinoConversions.ToRhino(direction);

        var points = Intersection.ProjectPointsToBreps(
            new[] { _brep },
            new[] { rhinoPt },
            rhinoDir,
            _tolerance);

        if (points == null || points.Length == 0)
            return null;

        return RhinoConversions.ToCore(points[0]);
    }

    public double? GetElevationAt(double x, double y)
    {
        var bb = _brep.GetBoundingBox(false);
        double zAbove = bb.Max.Z + 1000;
        var result = ProjectPoint(
            new CorePoint3(x, y, zAbove),
            new CoreVector3(0, 0, -1));
        return result?.Z;
    }

    public (CorePoint3 Min, CorePoint3 Max) GetBoundingBox()
    {
        var bb = _brep.GetBoundingBox(false);
        return (RhinoConversions.ToCore(bb.Min), RhinoConversions.ToCore(bb.Max));
    }
}
