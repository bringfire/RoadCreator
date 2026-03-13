using RoadCreator.Core.Math;

namespace RoadCreator.Core.Accessories;

/// <summary>
/// W-beam metal guardrail profile data and dimensions.
/// Converts from Svodidlajednostranna.rvb / svodidlaoboustrany.rvb.
///
/// The W-beam profile is an 8-point polyline in the YZ plane (X=0),
/// derived from the VBScript raw points after translation(-0.22, 0, 0.72)
/// and 90° rotation around Z.
///
/// Posts are steel I-beam boxes placed at 4m intervals along the offset curve.
/// Bracket surfaces connect the post to the W-beam rail.
/// </summary>
public static class GuardrailProfileComputer
{
    /// <summary>Distance from road edge to guardrail axis (meters).</summary>
    public const double EdgeOffset = 0.37;

    /// <summary>Spacing between posts along the curve (meters).</summary>
    public const double PostSpacing = 4.0;

    /// <summary>Post box center offset for placement (meters).</summary>
    public static readonly Point3 PostCenterOffset = new(0.05, 0.07, 0);

    /// <summary>
    /// W-beam cross-section profile (8 points in YZ plane).
    /// After VBScript transformation: translate(-0.22, 0, 0.72) then rotate 90° around Z.
    /// </summary>
    public static Point3[] GetWBeamProfile()
    {
        return new[]
        {
            new Point3(0, -0.27, 0.56),
            new Point3(0, -0.32, 0.56),
            new Point3(0, -0.32, 0.64),
            new Point3(0, -0.22, 0.64),
            new Point3(0, -0.22, 0.80),
            new Point3(0, -0.32, 0.80),
            new Point3(0, -0.32, 0.88),
            new Point3(0, -0.27, 0.88),
        };
    }

    /// <summary>
    /// Post box corner points (8 corners of the box, bottom 4 then top 4).
    /// Box dimensions: 0.10 × 0.14 × 1.29m (from Z=-0.4 to Z=0.89).
    /// </summary>
    public static Point3[] GetPostBoxCorners()
    {
        return new[]
        {
            new Point3(0, 0, -0.4),
            new Point3(0.10, 0, -0.4),
            new Point3(0.10, 0.14, -0.4),
            new Point3(0, 0.14, -0.4),
            new Point3(0, 0, 0.89),
            new Point3(0.10, 0, 0.89),
            new Point3(0.10, 0.14, 0.89),
            new Point3(0, 0.14, 0.89),
        };
    }

    /// <summary>
    /// Upper bracket line endpoints (post-to-rail connection at ~0.86m height).
    /// Line from (0,0,0.86) to (0,-0.22,0.82), extruded by (0.1,0,0).
    /// </summary>
    public static (Point3 Start, Point3 End) GetUpperBracket()
    {
        return (new Point3(0, 0, 0.86), new Point3(0, -0.22, 0.82));
    }

    /// <summary>
    /// Lower bracket line endpoints (post-to-rail connection at ~0.50m height).
    /// Line from (0,0,0.50) to (0,-0.22,0.60), extruded by (0.1,0,0).
    /// </summary>
    public static (Point3 Start, Point3 End) GetLowerBracket()
    {
        return (new Point3(0, 0, 0.50), new Point3(0, -0.22, 0.60));
    }

    /// <summary>Bracket extrusion direction (0.1m in X).</summary>
    public static readonly Vector3 BracketExtrusionDir = new(0.1, 0, 0);
}
