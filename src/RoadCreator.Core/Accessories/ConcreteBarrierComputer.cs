using RoadCreator.Core.Math;

namespace RoadCreator.Core.Accessories;

/// <summary>
/// Concrete barrier dimensions and rod profile data.
/// Converts from Svodidlabetonova.rvb.
///
/// The concrete barrier consists of:
///   - Box posts at user-defined spacing (default 2.5m)
///   - Two horizontal rods (lofted circles) at heights 0.5m and 0.9m
/// </summary>
public static class ConcreteBarrierComputer
{
    /// <summary>Default spacing between posts (meters).</summary>
    public const double DefaultPostSpacing = 2.5;

    /// <summary>Rod cross-section radius (meters).</summary>
    public const double RodRadius = 0.05;

    /// <summary>Lower rod height (meters).</summary>
    public const double LowerRodHeight = 0.5;

    /// <summary>Upper rod height (meters).</summary>
    public const double UpperRodHeight = 0.9;

    /// <summary>
    /// Default post box corner points (8 corners, bottom 4 then top 4).
    /// Box dimensions: 0.35 × 0.35 × 1.40m (from Z=-0.2 to Z=1.2).
    /// </summary>
    public static Point3[] GetDefaultPostCorners()
    {
        return new[]
        {
            new Point3(0, 0, -0.2),
            new Point3(0.35, 0, -0.2),
            new Point3(0.35, 0.35, -0.2),
            new Point3(0, 0.35, -0.2),
            new Point3(0, 0, 1.2),
            new Point3(0.35, 0, 1.2),
            new Point3(0.35, 0.35, 1.2),
            new Point3(0, 0.35, 1.2),
        };
    }

    /// <summary>
    /// Default post center (for placement offset).
    /// VBScript: base = Array(0.17, 0.17, 0).
    /// </summary>
    public static readonly Point3 DefaultPostCenter = new(0.17, 0.17, 0);

    /// <summary>Post width in X (meters).</summary>
    public const double PostWidth = 0.35;

    /// <summary>Post depth in Y (meters).</summary>
    public const double PostDepth = 0.35;

    /// <summary>Post height (meters).</summary>
    public const double PostHeight = 1.4;
}
