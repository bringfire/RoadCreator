namespace RoadCreator.Core.Alignment;

/// <summary>
/// Centralized naming conventions for road alignment objects.
/// Converts from the RC2 space-separated naming pattern:
///   "{RoadName} {SegmentIndex} {Side} {TypeCode} {Chainage} {Radius}"
///
/// VBScript examples:
///   "Silnice_1 1"                           → tangent segment
///   "Silnice_1 0 R ZU 0.000000"             → start stationing point
///   "Silnice_1 2 P PO 70.000000 R_150"      → clothoid point
///   "Silnice_1 Podélný_profil 420"           → longitudinal profile datum point
///   "Silnice_1 3DTrasa"                      → 3D route curve
///
/// In C# code, "Silnice_N" is "Road_N" (English-first UI policy).
/// </summary>
public static class RoadObjectNaming
{
    /// <summary>Road name prefix used by TangentPolygonCommand.</summary>
    public const string RoadPrefix = "Road_";

    /// <summary>Suffix for 3D route curves.</summary>
    public const string Route3DSuffix = "3D_route";

    /// <summary>Suffix for terrain profile curves.</summary>
    public const string TerrainProfileSuffix = "TerrainProfile";

    /// <summary>Suffix for longitudinal profile datum points.</summary>
    public const string LongProfileSuffix = "LongProfile";

    /// <summary>
    /// Type codes for stationing/important points.
    /// These match the VBScript naming convention (ZU/KU/ZP/PO/OP/KP/ZZ/V/KZ).
    /// </summary>
    public static class TypeCodes
    {
        public const string Start = "ZU";       // Začátek úseku (section start)
        public const string End = "KU";         // Konec úseku (section end)
        public const string TransitionStart = "ZP";   // Začátek přechodnice
        public const string ArcStart = "PO";          // Počátek oblouku
        public const string ArcEnd = "OP";            // Oblouku přechodnice
        public const string TransitionEnd = "KP";     // Konec přechodnice
        public const string ParabolicStart = "ZZ";    // Začátek zakružovacího oblouku
        public const string ParabolicVertex = "V";    // Vrchol
        public const string ParabolicEnd = "KZ";      // Konec zakružovacího oblouku
    }

    /// <summary>
    /// Extract the road name (first space-separated token) from an object name.
    /// Returns null if the name is empty.
    /// </summary>
    public static string? ParseRoadName(string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;
        var idx = objectName.IndexOf(' ');
        return idx > 0 ? objectName[..idx] : objectName;
    }

    /// <summary>
    /// Format a chainage value with 6 decimal places (VBScript convention).
    /// Example: 70.0 → "70.000000"
    /// </summary>
    public static string FormatChainage(double chainage)
    {
        return chainage.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Build a tangent segment name.
    /// Example: BuildSegmentName("Road_1", 2) → "Road_1 2"
    /// </summary>
    public static string BuildSegmentName(string roadName, int segmentIndex)
    {
        return $"{roadName} {segmentIndex}";
    }

    /// <summary>
    /// Build a stationing point name.
    /// Example: BuildStationingName("Road_1", 1, "R", "ZP", 45.5) → "Road_1 1 R ZP 45.500000"
    /// </summary>
    public static string BuildStationingName(string roadName, int segmentIndex,
        string side, string typeCode, double chainage)
    {
        return $"{roadName} {segmentIndex} {side} {typeCode} {FormatChainage(chainage)}";
    }

    /// <summary>
    /// Build a 3D route curve name.
    /// Example: BuildRoute3DName("Road_1") → "Road_1 3D_route"
    /// </summary>
    public static string BuildRoute3DName(string roadName)
    {
        return $"{roadName} {Route3DSuffix}";
    }

    /// <summary>
    /// Build a longitudinal profile datum point name.
    /// Example: BuildLongProfileName("Road_1", 420) → "Road_1 LongProfile 420"
    /// </summary>
    public static string BuildLongProfileName(string roadName, double datum)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0} {1} {2:F0}", roadName, LongProfileSuffix, datum);
    }

    /// <summary>
    /// Try to parse a datum value from a longitudinal profile point name.
    /// Expected format: "{roadName} LongProfile {datum}"
    /// </summary>
    public static bool TryParseLongProfileDatum(string? objectName, out double datum)
    {
        datum = 0;
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        var parts = objectName.Split(' ');
        if (parts.Length < 3)
            return false;

        if (parts[1] == LongProfileSuffix)
            return double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out datum);

        return false;
    }

    /// <summary>
    /// Build the next available road name by checking existing names.
    /// </summary>
    public static string GetNextRoadName(Func<string, bool> roadExists)
    {
        int n = 1;
        while (roadExists(RoadPrefix + n))
            n++;
        return RoadPrefix + n;
    }
}
