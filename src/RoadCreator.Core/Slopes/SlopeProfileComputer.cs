using RoadCreator.Core.Math;

namespace RoadCreator.Core.Slopes;

/// <summary>
/// Computes slope cross-section profiles for embankment/cut slopes at road edges.
/// From RC2_Svahy_CZ.rvb — the slope profile construction block.
///
/// The profile is a polyline in local coordinates starting at the road edge (origin):
///   X = horizontal distance outward from road edge
///   Y = vertical offset (negative = below road grade, positive = above)
///   Z = 0 (2D profile)
///
/// Note: The Rhino command remaps Y→Z when creating 3D geometry.
///
/// Profile layout WITHOUT ditch:
///   [0, 0] -- [10×cutRatio, 10]
///   (road edge → slope terminal 10m above)
///
/// Profile layout WITH ditch:
///   [0, 0] -- [depth×fillRatio, -depth] -- [depth×fillRatio+width, -depth]
///   -- [depth×fillRatio+width+10×cutRatio, 12]
///   (road edge → ditch inner → ditch outer → slope terminal)
///
/// Slope ratio convention:
///   Format 1:n where n = horizontal distance per 1m vertical rise.
///   A ratio of 1.75 means 1.75m horizontal for every 1m vertical.
///
/// After sweeping along the road edge, the slope surface is split against
/// the terrain to determine fill (embankment) vs cut (excavation) visibility.
/// </summary>
public static class SlopeProfileComputer
{
    /// <summary>
    /// Terminal height for slope profiles (arbitrary large value to ensure terrain intersection).
    /// From VBScript: Z = 12 for ditch profiles, Z = 10 for no-ditch profiles.
    /// </summary>
    private const double TerminalHeightWithDitch = 12.0;
    private const double TerminalHeightNoDitch = 10.0;

    /// <summary>
    /// Compute the slope profile points starting at the road edge.
    /// Points are in local 2D coordinates (X = outward, Y = vertical).
    /// </summary>
    /// <param name="fillSlopeRatio">Fill slope ratio n in 1:n (e.g., 1.75).</param>
    /// <param name="cutSlopeRatio">Cut slope ratio n in 1:n (e.g., 1.75).</param>
    /// <param name="includeDitch">Whether to include a drainage ditch.</param>
    /// <param name="ditchDepth">Ditch depth in meters (positive value, applied downward).</param>
    /// <param name="ditchWidth">Ditch bottom width in meters.</param>
    /// <returns>Profile points from road edge outward.</returns>
    public static Point3[] ComputeSlopeProfile(
        double fillSlopeRatio,
        double cutSlopeRatio,
        bool includeDitch,
        double ditchDepth,
        double ditchWidth)
    {
        if (fillSlopeRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(fillSlopeRatio), fillSlopeRatio,
                "Fill slope ratio must be positive.");
        if (cutSlopeRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(cutSlopeRatio), cutSlopeRatio,
                "Cut slope ratio must be positive.");

        if (includeDitch)
        {
            if (ditchDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(ditchDepth), ditchDepth,
                    "Ditch depth must be positive.");
            if (ditchWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(ditchWidth), ditchWidth,
                    "Ditch width must be positive.");

            return ComputeProfileWithDitch(fillSlopeRatio, cutSlopeRatio, ditchDepth, ditchWidth);
        }

        return ComputeProfileNoDitch(cutSlopeRatio);
    }

    /// <summary>
    /// Compute the simplified embankment top-edge profile (2 points).
    /// This is used for the secondary sweep surface in the VBScript.
    /// </summary>
    /// <param name="cutSlopeRatio">Cut slope ratio n in 1:n.</param>
    /// <returns>2-point profile: road edge → slope terminal.</returns>
    public static Point3[] ComputeEmbankmentProfile(double cutSlopeRatio)
    {
        if (cutSlopeRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(cutSlopeRatio), cutSlopeRatio,
                "Cut slope ratio must be positive.");

        return new[]
        {
            new Point3(0, 0, 0),
            new Point3(TerminalHeightNoDitch * cutSlopeRatio, TerminalHeightNoDitch, 0),
        };
    }

    /// <summary>
    /// Profile without ditch: identical to embankment profile.
    /// VBScript: [0,0,0] → [10*sklonvykop, 0, 10]
    /// </summary>
    private static Point3[] ComputeProfileNoDitch(double cutSlopeRatio)
    {
        return ComputeEmbankmentProfile(cutSlopeRatio);
    }

    /// <summary>
    /// Profile with ditch: road edge → ditch inner → ditch outer → slope terminal.
    /// VBScript:
    ///   [0, 0, 0]
    ///   [hd*sr, 0, -hd]           (ditch inner bottom)
    ///   [hd*sr + sw, 0, -hd]      (ditch outer bottom)
    ///   [hd*sr + sw + 12*sv, 0, 12] (cut slope terminal)
    /// </summary>
    private static Point3[] ComputeProfileWithDitch(
        double fillSlopeRatio, double cutSlopeRatio,
        double ditchDepth, double ditchWidth)
    {
        double ditchInnerX = ditchDepth * fillSlopeRatio;
        double ditchOuterX = ditchInnerX + ditchWidth;
        double terminalX = ditchOuterX + TerminalHeightWithDitch * cutSlopeRatio;

        return new[]
        {
            new Point3(0, 0, 0),
            new Point3(ditchInnerX, -ditchDepth, 0),
            new Point3(ditchOuterX, -ditchDepth, 0),
            new Point3(terminalX, TerminalHeightWithDitch, 0),
        };
    }
}
