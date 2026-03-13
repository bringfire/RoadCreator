using RoadCreator.Core.Math;

namespace RoadCreator.Core.Nature;

/// <summary>
/// Grass patch placement along road edges.
/// Converts from Trava.rvb / TravaMesh.rvb.
///
/// Algorithm:
///   1. Offset the road edge curve at 2m and 4m (2 bands).
///   2. Divide each offset curve at 0.8m intervals.
///   3. At each point: place a 4m horizontal line, rotate randomly 0-360°.
///   4. Project the line to the terrain.
///   5. Extrude the projected line 0.5m vertically to create a grass surface.
///
/// VBScript:
///   Linebase = AddLine((-2,0,0), (2,0,0))   → 4m base line
///   curve(i) = OffsetCurve(edge, dir, 2*i+2) → offsets 2, 4
///   arrPoints(i) = DivideCurveEquidistant(curve(i), 0.8)
///   ExtrudeCurveStraight(line, (0,0,0), (0,0,0.5))
/// </summary>
public static class GrassComputer
{
    /// <summary>Number of offset bands.</summary>
    public const int BandCount = 2;

    /// <summary>Half-length of the grass base line (total length = 4m).</summary>
    public const double BaseLineHalfLength = 2.0;

    /// <summary>Spacing between grass patches along each band.</summary>
    public const double PointSpacing = 0.8;

    /// <summary>Vertical extrusion height for grass surfaces.</summary>
    public const double ExtrusionHeight = 0.5;

    /// <summary>
    /// Compute offset distances for grass bands.
    /// VBScript: 2*i + 2 → [2, 4]
    /// </summary>
    public static double[] GetOffsetDistances()
    {
        var distances = new double[BandCount];
        for (int i = 0; i < BandCount; i++)
            distances[i] = 2.0 * i + 2.0;
        return distances;
    }

    /// <summary>
    /// Get the grass base line endpoints (centered at origin, along X-axis).
    /// VBScript: AddLine((-2,0,0), (2,0,0))
    /// </summary>
    public static (Point3 Start, Point3 End) GetBaseLineEndpoints()
    {
        return (new Point3(-BaseLineHalfLength, 0, 0), new Point3(BaseLineHalfLength, 0, 0));
    }

    /// <summary>
    /// Get the extrusion direction vector (vertical, 0.5m up).
    /// VBScript: ExtrudeCurveStraight(line, (0,0,0), (0,0,0.5))
    /// </summary>
    public static Vector3 GetExtrusionVector()
    {
        return new Vector3(0, 0, ExtrusionHeight);
    }
}
