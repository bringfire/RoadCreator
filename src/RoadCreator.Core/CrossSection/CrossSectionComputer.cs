using RoadCreator.Core.Math;

namespace RoadCreator.Core.CrossSection;

/// <summary>
/// Computes cross-section profile points for a road at a given station.
/// From RC2_3DSilnice_CZ.rvb — the profile point construction block.
///
/// The cross-section is a polyline in local coordinates:
///   X = lateral offset from centerline, Y = elevation, Z = 0
///
/// Note: The Rhino command remaps Y→Z when creating Rhino polylines to match
/// the VBScript convention of (X, 0, Z) for profile geometry.
///
/// Profile point layout (undivided road without verge):
///   [-HW-W, elev_left, 0] -- [0, 0, 0] -- [+HW+W, elev_right, 0]
///
/// Profile point layout (undivided road with verge):
///   [-HW-W-Verge, elev_verge, 0] -- [-HW-W, elev_left, 0] -- [0, 0, 0]
///   -- [+HW+W, elev_right, 0] -- [+HW+W+Verge, elev_verge, 0]
///
/// Profile point layout (divided road without verge):
///   [-edge, elev_left, 0] -- [-median/2, 0, 0] -- [+median/2, 0, 0]
///   -- [+edge, elev_right, 0]
///
/// Crossfall convention (from VBScript):
///   Z direction: P (right curve) => Z=-1, L (left curve) => Z=+1, R (straight) => Z=0, M=1
///   Left edge:  Z_left  = ((M × (-p)) + (-pmax × Z)) / 100 × (HW + Widening)
///   Right edge: Z_right = ((M × (-p)) + (+pmax × Z)) / 100 × (HW + Widening)
///
/// Where:
///   p = crossfall in straight sections (%, default 2.5)
///   pmax = crossfall in curves (%, default 4)
///   M = straight-crossfall blend factor: 1.0 for straight, 0.0 for full curve,
///       linearly interpolated for transitions (e.g., 0.5 at LM/PM points)
///   Z = direction indicator: +1 for left curve, -1 for right curve, 0 for straight,
///       fractional (±0.5) for LM/PM transitions
/// </summary>
public static class CrossSectionComputer
{
    /// <summary>
    /// Verge slope in percent (always 8% downward from road edge).
    /// </summary>
    private const double VergeSlopePercent = 8.0;

    /// <summary>
    /// Compute the cross-section profile points in local coordinates.
    /// </summary>
    /// <param name="category">Road category (defines half-width and median).</param>
    /// <param name="widening">Lane widening in meters (from WideningTable).</param>
    /// <param name="crossfallStraight">Crossfall in straight sections (percent, e.g., 2.5).</param>
    /// <param name="crossfallCurve">Crossfall in curves (percent, e.g., 4.0).</param>
    /// <param name="curveDirection">Curve direction: +1 = left curve, -1 = right curve, 0 = straight.
    /// Fractional values (±0.5) for LM/PM transitions. Clamped to [-1, +1].</param>
    /// <param name="includeVerge">Whether to include road verge (krajnice).</param>
    /// <param name="vergeWidth">Total verge width in meters.</param>
    /// <returns>Profile points in local XZ coordinates (Y = 0).</returns>
    public static Point3[] ComputeProfilePoints(
        RoadCategory category,
        double widening,
        double crossfallStraight,
        double crossfallCurve,
        double curveDirection,
        bool includeVerge,
        double vergeWidth)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        double hw = category.HalfWidth;
        double median = category.MedianWidth;

        // Clamp curveDirection to [-1, +1] to prevent invalid M values
        double clampedDirection = System.Math.Max(-1.0, System.Math.Min(1.0, curveDirection));

        // Decode VBScript convention: M=1 when straight, M=0 when in full curve.
        // For transitions (fractional curveDirection like ±0.5), M blends linearly:
        //   |curveDirection|=0 => M=1 (pure straight), |curveDirection|=1 => M=0 (full curve)
        double M = 1.0 - System.Math.Abs(clampedDirection);
        double Z = clampedDirection;

        if (category.IsDivided)
            return ComputeDividedProfile(hw, median, widening, crossfallStraight, crossfallCurve, M, Z, includeVerge, vergeWidth);
        else
            return ComputeUndividedProfile(hw, widening, crossfallStraight, crossfallCurve, M, Z, includeVerge, vergeWidth);
    }

    private static Point3[] ComputeUndividedProfile(
        double hw, double widening,
        double p, double pmax,
        double M, double Z,
        bool includeVerge, double vergeWidth)
    {
        double edgeWidth = hw + widening;
        double zLeft = ComputeEdgeElevation(p, pmax, M, Z, edgeWidth, isLeft: true);
        double zRight = ComputeEdgeElevation(p, pmax, M, Z, edgeWidth, isLeft: false);

        if (includeVerge)
        {
            double zVergeLeft = zLeft + (-VergeSlopePercent / 100.0 * vergeWidth);
            double zVergeRight = zRight + (-VergeSlopePercent / 100.0 * vergeWidth);

            return new[]
            {
                new Point3(-(edgeWidth + vergeWidth), zVergeLeft, 0),
                new Point3(-edgeWidth, zLeft, 0),
                new Point3(0, 0, 0),
                new Point3(edgeWidth, zRight, 0),
                new Point3(edgeWidth + vergeWidth, zVergeRight, 0),
            };
        }
        else
        {
            return new[]
            {
                new Point3(-edgeWidth, zLeft, 0),
                new Point3(0, 0, 0),
                new Point3(edgeWidth, zRight, 0),
            };
        }
    }

    private static Point3[] ComputeDividedProfile(
        double hw, double median, double widening,
        double p, double pmax,
        double M, double Z,
        bool includeVerge, double vergeWidth)
    {
        double halfMedian = median / 2.0;
        double edgeWidth = hw + widening + halfMedian;
        double zLeft = ComputeEdgeElevation(p, pmax, M, Z, edgeWidth, isLeft: true);
        double zRight = ComputeEdgeElevation(p, pmax, M, Z, edgeWidth, isLeft: false);

        if (includeVerge)
        {
            double zVergeLeft = zLeft + (-VergeSlopePercent / 100.0 * vergeWidth);
            double zVergeRight = zRight + (-VergeSlopePercent / 100.0 * vergeWidth);

            return new[]
            {
                new Point3(-(edgeWidth + vergeWidth), zVergeLeft, 0),
                new Point3(-edgeWidth, zLeft, 0),
                new Point3(-halfMedian, 0, 0),
                new Point3(halfMedian, 0, 0),
                new Point3(edgeWidth, zRight, 0),
                new Point3(edgeWidth + vergeWidth, zVergeRight, 0),
            };
        }
        else
        {
            return new[]
            {
                new Point3(-edgeWidth, zLeft, 0),
                new Point3(-halfMedian, 0, 0),
                new Point3(halfMedian, 0, 0),
                new Point3(edgeWidth, zRight, 0),
            };
        }
    }

    /// <summary>
    /// Compute edge elevation using the VBScript crossfall formula.
    /// Left:  ((M × (-p)) + (-pmax × Z)) / 100 × edgeWidth
    /// Right: ((M × (-p)) + (+pmax × Z)) / 100 × edgeWidth
    /// </summary>
    private static double ComputeEdgeElevation(
        double p, double pmax, double M, double Z,
        double edgeWidth, bool isLeft)
    {
        double crossfallSign = isLeft ? -1.0 : 1.0;
        return ((M * (-p)) + (crossfallSign * pmax * Z)) / 100.0 * edgeWidth;
    }

    /// <summary>
    /// Compute verge width based on equipment type.
    /// From VBScript: guardrail => 0.5 + 1.0 = 1.5m, road poles => 0.5 + 0.25 = 0.75m
    /// </summary>
    public static double ComputeVergeWidth(bool hasGuardrail)
    {
        return hasGuardrail ? 1.5 : 0.75;
    }
}
