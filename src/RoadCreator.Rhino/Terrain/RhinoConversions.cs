using global::Rhino.Geometry;
using CorePoint3 = RoadCreator.Core.Math.Point3;
using CoreVector3 = RoadCreator.Core.Math.Vector3;

namespace RoadCreator.Rhino.Terrain;

/// <summary>
/// Centralized conversion helpers between Core math types and RhinoCommon types.
/// </summary>
public static class RhinoConversions
{
    public static Point3d ToRhino(CorePoint3 p) => new(p.X, p.Y, p.Z);
    public static Vector3d ToRhino(CoreVector3 v) => new(v.X, v.Y, v.Z);
    public static CorePoint3 ToCore(Point3d p) => new(p.X, p.Y, p.Z);
    public static CoreVector3 ToCore(Vector3d v) => new(v.X, v.Y, v.Z);
}
