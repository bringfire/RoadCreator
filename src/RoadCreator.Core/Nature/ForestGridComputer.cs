using RoadCreator.Core.Math;

namespace RoadCreator.Core.Nature;

/// <summary>
/// Grid-based random forest placement algorithm.
/// Converts from Les.rvb / LesMesh.rvb.
///
/// Algorithm:
///   1. Divide a planar surface domain into grid cells of size <c>density</c>.
///   2. At each cell center, add ±density/2 random offset in X and Y.
///   3. Project the point down to the terrain surface/mesh.
///   4. Place a random tree from the database with random rotation and scale.
///
/// The grid iteration uses integer division of the domain range by density,
/// matching VBScript: For i = 0 To ((U(1) - U(0)) / hustota) \ 1
/// </summary>
public static class ForestGridComputer
{
    /// <summary>Default grid spacing in model units.</summary>
    public const double DefaultDensity = 6.0;

    /// <summary>Minimum allowed density.</summary>
    public const double MinDensity = 1.0;

    /// <summary>Maximum allowed density.</summary>
    public const double MaxDensity = 200.0;

    /// <summary>
    /// Compute grid cell origins for a rectangular domain.
    /// Each cell origin is at (uMin + density*i, vMin + density*j).
    /// </summary>
    /// <param name="uMin">Domain minimum in U direction.</param>
    /// <param name="uMax">Domain maximum in U direction.</param>
    /// <param name="vMin">Domain minimum in V direction.</param>
    /// <param name="vMax">Domain maximum in V direction.</param>
    /// <param name="density">Grid cell size.</param>
    /// <returns>Array of (u, v) grid cell origins.</returns>
    public static (double U, double V)[] ComputeGridOrigins(
        double uMin, double uMax, double vMin, double vMax, double density)
    {
        if (density <= 0)
            throw new ArgumentOutOfRangeException(nameof(density));

        int cols = (int)((uMax - uMin) / density);
        int rows = (int)((vMax - vMin) / density);

        int count = (cols + 1) * (rows + 1);
        var origins = new (double, double)[count];
        int idx = 0;
        for (int i = 0; i <= cols; i++)
        {
            double u = uMin + density * i;
            for (int j = 0; j <= rows; j++)
            {
                double v = vMin + density * j;
                origins[idx++] = (u, v);
            }
        }
        return origins;
    }

    /// <summary>
    /// Apply random jitter to a grid point's XY coordinates.
    /// VBScript: arrPoint(0) += hustota * Rnd - hustota/2
    /// </summary>
    public static Point3 ApplyJitter(Point3 point, double density, double randomU, double randomV)
    {
        double x = RandomPlacementComputer.ApplyGridJitter(point.X, density, randomU);
        double y = RandomPlacementComputer.ApplyGridJitter(point.Y, density, randomV);
        return new Point3(x, y, point.Z);
    }
}
