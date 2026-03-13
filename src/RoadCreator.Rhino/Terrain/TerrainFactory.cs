using global::Rhino;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using RoadCreator.Core.Terrain;

namespace RoadCreator.Rhino.Terrain;

/// <summary>
/// Factory for creating ITerrain instances from Rhino objects.
/// Centralizes the Surface/Mesh detection logic from the original scripts.
/// </summary>
public static class TerrainFactory
{
    /// <summary>
    /// Create an ITerrain from a RhinoObject, auto-detecting Surface vs Mesh.
    /// Tolerance is read from the document that owns the object.
    /// </summary>
    public static ITerrain? FromRhinoObject(RhinoObject obj)
    {
        if (obj == null) return null;

        double tolerance = obj.Document?.ModelAbsoluteTolerance ?? 0.001;
        return FromGeometry(obj.Geometry, tolerance);
    }

    /// <summary>
    /// Create an ITerrain from a GeometryBase, auto-detecting type.
    /// </summary>
    public static ITerrain? FromGeometry(GeometryBase geometry, double tolerance = 0.001)
    {
        if (geometry is Mesh mesh)
            return new MeshTerrainAdapter(mesh, tolerance);

        if (geometry is Brep brep)
            return new SurfaceTerrainAdapter(brep, tolerance);

        if (geometry is Extrusion extrusion)
            return new SurfaceTerrainAdapter(extrusion.ToBrep(), tolerance);

        if (geometry is Surface surface)
            return new SurfaceTerrainAdapter(surface.ToBrep(), tolerance);

        return null;
    }
}
