using RoadCreator.Core.Math;

namespace RoadCreator.Core.Terrain;

/// <summary>
/// Abstraction over terrain geometry (Surface or Mesh).
/// Implemented in the Rhino project by SurfaceTerrainAdapter and MeshTerrainAdapter.
/// Eliminates the dual Surface/Mesh code paths found throughout the original scripts.
/// </summary>
public interface ITerrain
{
    /// <summary>
    /// Project a point onto the terrain along the given direction.
    /// Returns null if the point doesn't hit the terrain.
    /// Replaces: Rhino.ProjectPointToSurface / Rhino.ProjectPointToMesh
    /// </summary>
    Point3? ProjectPoint(Point3 point, Vector3 direction);

    /// <summary>
    /// Project a point onto the terrain along -Z (straight down).
    /// Convenience overload for the most common case.
    /// </summary>
    Point3? ProjectPointDown(Point3 point) =>
        ProjectPoint(point, new Vector3(0, 0, -1));

    /// <summary>
    /// Get the elevation (Z value) at a given XY position.
    /// Returns null if the position is outside the terrain.
    /// </summary>
    double? GetElevationAt(double x, double y);

    /// <summary>
    /// Get the bounding box of the terrain.
    /// </summary>
    (Point3 Min, Point3 Max) GetBoundingBox();

    /// <summary>
    /// Whether the underlying representation is a mesh.
    /// </summary>
    bool IsMesh { get; }
}
