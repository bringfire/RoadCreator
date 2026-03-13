namespace RoadCreator.Core.Nature;

/// <summary>
/// Offset-curve forest silhouette placement.
/// Converts from Leskulisa.rvb / LeskulisaMesh.rvb / Leskulisakrivka.rvb.
///
/// Two modes:
///   1. Fixed (Leskulisa): 3 offset curves at distances 4, 7, 10 from boundary,
///      divided at 5.5m spacing, ±2m jitter.
///   2. Adaptive (Leskulisakrivka): 3 offset curves at distances computed from
///      user spacing, divided at user spacing, ±2m jitter.
///
/// VBScript offset formula (fixed):
///   curve(i) = OffsetCurve(boundary, direction, 3*i + 4)  → 4, 7, 10
///   arrPoints(i) = DivideCurveEquidistant(curve(i), 5.5)
///
/// VBScript offset formula (adaptive):
///   curve(i) = OffsetCurve(boundary, direction, 3*i + rozestup/0.8)
///   arrPoints(i) = DivideCurveEquidistant(curve(i), rozestup)
/// </summary>
public static class ForestSilhouetteComputer
{
    /// <summary>Number of offset curve rows.</summary>
    public const int RowCount = 3;

    /// <summary>Fixed-mode point spacing along each curve.</summary>
    public const double FixedSpacing = 5.5;

    /// <summary>Fixed-mode base offset distance.</summary>
    public const double FixedBaseOffset = 4.0;

    /// <summary>Fixed-mode increment between rows.</summary>
    public const double FixedRowIncrement = 3.0;

    /// <summary>XY jitter range (±2 units).</summary>
    public const double JitterRange = 4.0;

    /// <summary>Default adaptive spacing.</summary>
    public const double DefaultAdaptiveSpacing = 5.0;

    /// <summary>
    /// Compute offset distances for fixed-mode silhouette rows.
    /// VBScript: 3*i + 4 → [4, 7, 10]
    /// </summary>
    public static double[] GetFixedOffsetDistances()
    {
        var distances = new double[RowCount];
        for (int i = 0; i < RowCount; i++)
            distances[i] = FixedRowIncrement * i + FixedBaseOffset;
        return distances;
    }

    /// <summary>
    /// Compute offset distances for adaptive-mode silhouette rows.
    /// VBScript: 3*i + rozestup/0.8
    /// </summary>
    /// <param name="spacing">User-defined spacing (rozestup).</param>
    public static double[] GetAdaptiveOffsetDistances(double spacing)
    {
        if (spacing <= 0)
            throw new ArgumentOutOfRangeException(nameof(spacing));

        double baseOffset = spacing / 0.8;
        var distances = new double[RowCount];
        for (int i = 0; i < RowCount; i++)
            distances[i] = FixedRowIncrement * i + baseOffset;
        return distances;
    }

    /// <summary>
    /// Apply silhouette jitter (±2 units in X and Y).
    /// VBScript: U = 4*Rnd - 2; V = 4*Rnd - 2
    /// </summary>
    /// <param name="x">Original X coordinate.</param>
    /// <param name="y">Original Y coordinate.</param>
    /// <param name="randomU">Random [0,1) for X jitter.</param>
    /// <param name="randomV">Random [0,1) for Y jitter.</param>
    public static (double X, double Y) ApplyJitter(double x, double y, double randomU, double randomV)
    {
        double jx = RandomPlacementComputer.ApplyFixedJitter(x, JitterRange, randomU);
        double jy = RandomPlacementComputer.ApplyFixedJitter(y, JitterRange, randomV);
        return (jx, jy);
    }
}
