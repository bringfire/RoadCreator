namespace RoadCreator.Core.Signs;

/// <summary>
/// Catalog of Czech traffic sign IDs and their legacy base-point offsets.
/// Converts from 14 identical Dopravniznacky/*.rvb placement scripts.
///
/// Each VBScript had a hardcoded ZnackaID and Base array defining the sign's
/// position in the template .3dm file. In the C# version, companion points
/// (see <see cref="Accessories.DatabaseNaming"/>) are preferred, but these
/// legacy offsets serve as fallback for older template files.
///
/// The sign database uses the "RC_Database" layer. Signs are named by their
/// Czech standard ID: A1a, B1, P4, etc.
/// </summary>
public static class TrafficSignCatalog
{
    /// <summary>Layer name for placed traffic signs.</summary>
    public const string PlacementLayerName = "Traffic Signs";

    /// <summary>Object name assigned to placed sign copies.</summary>
    public const string PlacedSignName = "TrafficSign";

    /// <summary>
    /// VBScript rotation offset: angle(0) + 90.
    /// The original scripts add 90 degrees to the computed angle between
    /// the placement point and the user's rotation reference point.
    /// </summary>
    public const double RotationOffsetDegrees = 90.0;

    /// <summary>
    /// Legacy base-point offsets from the original VBScript template files.
    /// Key = sign ID, Value = (X, Y, Z) offset in the template coordinate system.
    /// </summary>
    private static readonly Dictionary<string, (double X, double Y, double Z)> LegacyBasePoints = new(StringComparer.Ordinal)
    {
        // Warning signs (A-series)
        ["A1a"] = (2, 2, 0),
        ["A1b"] = (1, 2, 0),
        ["A2a"] = (3, 2, 0),
        ["A2b"] = (4, 2, 0),

        // Prohibition signs (B-series)
        ["B1"]  = (1, 1, 0),
        ["B20"] = (1, 0, 0),
        ["B28"] = (0, 0, 0),
        ["B29"] = (3, 0, 0),

        // Command signs (C-series)
        ["C1"]  = (2, 0, 0),

        // Priority signs (P-series)
        ["P1"]  = (3, 1, 0),
        ["P2"]  = (0, 1, 0),
        ["P3"]  = (0, 2, 0),
        ["P4"]  = (4, 1, 0),
        ["P6"]  = (2, 1, 0),
    };

    /// <summary>
    /// Get all known sign IDs from the catalog.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllSignIds()
    {
        return LegacyBasePoints.Keys;
    }

    /// <summary>
    /// Try to get the legacy base-point offset for a sign ID.
    /// Returns false if the sign ID is not in the catalog.
    /// </summary>
    public static bool TryGetLegacyBasePoint(string signId, out (double X, double Y, double Z) basePoint)
    {
        return LegacyBasePoints.TryGetValue(signId, out basePoint);
    }

    /// <summary>
    /// Check whether a sign ID is in the catalog.
    /// </summary>
    public static bool IsKnownSign(string signId)
    {
        return LegacyBasePoints.ContainsKey(signId);
    }

    /// <summary>
    /// Compute the rotation angle in degrees from a placement point to a
    /// direction reference point, with the VBScript +90° offset applied.
    ///
    /// VBScript: angle = Rhino.Angle(Array(pt(0), pt(1), 0), Array(rot(0), rot(1), 0))
    ///           Rhino.RotateObjects copy, pt, angle(0) + 90
    /// </summary>
    /// <param name="placementX">Placement point X.</param>
    /// <param name="placementY">Placement point Y.</param>
    /// <param name="directionX">Direction reference point X.</param>
    /// <param name="directionY">Direction reference point Y.</param>
    /// <returns>Rotation angle in degrees (with +90° offset).</returns>
    public static double ComputeSignRotation(
        double placementX, double placementY,
        double directionX, double directionY)
    {
        double angle = System.Math.Atan2(
            directionY - placementY,
            directionX - placementX) * (180.0 / System.Math.PI);
        return angle + RotationOffsetDegrees;
    }
}
