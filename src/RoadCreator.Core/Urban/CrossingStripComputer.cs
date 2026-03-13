using RoadCreator.Core.Math;

namespace RoadCreator.Core.Urban;

/// <summary>
/// Computes pedestrian crossing geometry (rectangles and stripe patterns).
/// From Prechodyplocha.rvb (single crossing area) and Prechodplochy.rvb (zebra stripes).
///
/// A crossing is defined by:
///   - Start point and end point (defines crossing direction across road)
///   - Width (crossing dimension along the road, default 4m)
///
/// Single crossing: one rectangle splitting the road surface.
/// Zebra crossing: multiple 0.5m-wide stripes at 1m spacing.
/// </summary>
public static class CrossingStripComputer
{
    /// <summary>
    /// Default crossing width: 4m.
    /// From VBScript: Rhino.GetReal("Urci sirku prechodu", 4).
    /// </summary>
    public const double DefaultWidth = 4.0;

    /// <summary>
    /// Zebra stripe width: 0.5m.
    /// From VBScript: Rhino.AddPlaneSurface(Rhino.WorldXYPlane, sirka, 0.5).
    /// </summary>
    public const double StripeWidth = 0.5;

    /// <summary>
    /// Zebra stripe spacing: 1.0m center-to-center.
    /// From VBScript: MoveObject prechod, Array(0,0,0), Array(0,1,0) in loop.
    /// </summary>
    public const double StripeSpacing = 1.0;

    /// <summary>
    /// Compute the number of zebra stripes for a given crossing length.
    /// From VBScript: loop while i &lt; distance(start, end).
    /// </summary>
    /// <param name="crossingLength">Distance from start to end point.</param>
    /// <returns>Number of stripes.</returns>
    public static int ComputeStripeCount(double crossingLength)
    {
        if (crossingLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossingLength), crossingLength,
                "Crossing length must be positive.");

        return (int)crossingLength;
    }

    /// <summary>
    /// Compute the crossing direction angle (in degrees) from start to end.
    /// </summary>
    public static double ComputeCrossingAngle(Point3 start, Point3 end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        return System.Math.Atan2(dy, dx) * (180.0 / System.Math.PI);
    }

    /// <summary>
    /// Compute the crossing rectangle corners in local coordinates (before rotation).
    /// The rectangle is centered on the crossing width axis.
    /// From VBScript: plane centered at (-width/2, 0), then moved and rotated.
    /// </summary>
    /// <param name="crossingWidth">Width of the crossing (along road direction).</param>
    /// <param name="crossingLength">Length of the crossing (across road).</param>
    /// <returns>4 corner points of the crossing rectangle in local coordinates.</returns>
    public static Point3[] ComputeCrossingRectangle(double crossingWidth, double crossingLength)
    {
        if (crossingWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossingWidth), crossingWidth,
                "Crossing width must be positive.");
        if (crossingLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossingLength), crossingLength,
                "Crossing length must be positive.");

        double halfW = crossingWidth / 2.0;
        double halfL = crossingLength / 2.0;

        return new[]
        {
            new Point3(-halfW, -halfL, 0),
            new Point3(halfW, -halfL, 0),
            new Point3(halfW, halfL, 0),
            new Point3(-halfW, halfL, 0),
        };
    }
}
