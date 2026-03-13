namespace RoadCreator.Core.Nature;

/// <summary>
/// Random transform generation for vegetation placement.
/// Converts from Les.rvb / Leskulisa.rvb / Magiccopy.rvb random formulas.
///
/// VBScript scale formula:
///   randscale = (((Scale) * Rnd + 1) - (Scale / 2)) / 100 + 1
///
/// The "+1" inside the parentheses is a VBScript artifact that shifts the center
/// to 1.01 rather than 1.0 (range [0.91, 1.11] for Scale=20). Preserved for
/// backward compatibility with the original script behavior.
/// </summary>
public static class RandomPlacementComputer
{
    /// <summary>
    /// Compute a random scale multiplier using the VBScript formula.
    /// </summary>
    /// <param name="scalePercent">Scale variation in percent (e.g. 20 for ±10%).</param>
    /// <param name="random01">Random value in [0, 1).</param>
    /// <returns>Scale multiplier centered at ~1.01 (VBScript artifact).</returns>
    public static double ComputeScale(double scalePercent, double random01)
    {
        // VBScript: (((Scale) * Rnd + 1) - (Scale / 2)) / 100 + 1
        // The "+1" inside produces a 0.01 offset from 1.0 — preserved for compatibility.
        return ((scalePercent * random01 + 1.0) - (scalePercent / 2.0)) / 100.0 + 1.0;
    }

    /// <summary>
    /// Compute a random rotation angle in degrees.
    /// VBScript: randrotate = 360 * Rnd
    /// </summary>
    /// <param name="random01">Random value in [0, 1).</param>
    /// <returns>Rotation angle in degrees [0, 360).</returns>
    public static double ComputeRotationDegrees(double random01)
    {
        return 360.0 * random01;
    }

    /// <summary>
    /// Select a random tree index from a database of n trees.
    /// VBScript: randtree = ((ntree - 1) * Rnd) \ 1
    ///
    /// Note: This formula has a slight bias — the last index (treeCount-1)
    /// is underrepresented because (treeCount-1) * random01 never reaches
    /// treeCount-1 when random01 is in [0, 1). This matches the original
    /// VBScript behavior. For uniform distribution, callers could use
    /// Random.Next(treeCount) directly instead.
    /// </summary>
    /// <param name="treeCount">Total number of trees in the database.</param>
    /// <param name="random01">Random value in [0, 1).</param>
    /// <returns>Zero-based tree index in [0, treeCount-1].</returns>
    public static int SelectTreeIndex(int treeCount, double random01)
    {
        if (treeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(treeCount));
        return (int)((treeCount - 1) * random01);
    }

    /// <summary>
    /// Apply random grid jitter to a coordinate.
    /// VBScript: arrPoint(axis) = arrPoint(axis) + (hustota * Rnd) - (hustota / 2)
    /// </summary>
    /// <param name="coordinate">Original coordinate value.</param>
    /// <param name="cellSize">Grid cell size (jitter range = ±cellSize/2).</param>
    /// <param name="random01">Random value in [0, 1).</param>
    /// <returns>Jittered coordinate.</returns>
    public static double ApplyGridJitter(double coordinate, double cellSize, double random01)
    {
        return coordinate + (cellSize * random01) - (cellSize / 2.0);
    }

    /// <summary>
    /// Apply fixed-range jitter to a coordinate.
    /// VBScript (forest silhouette): U = 4 * Rnd - 2 → ±2 units
    /// </summary>
    /// <param name="coordinate">Original coordinate value.</param>
    /// <param name="jitterRange">Total jitter range (offset = ±range/2).</param>
    /// <param name="random01">Random value in [0, 1).</param>
    /// <returns>Jittered coordinate.</returns>
    public static double ApplyFixedJitter(double coordinate, double jitterRange, double random01)
    {
        return coordinate + (jitterRange * random01) - (jitterRange / 2.0);
    }
}
