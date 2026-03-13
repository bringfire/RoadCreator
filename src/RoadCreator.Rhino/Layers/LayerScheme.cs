namespace RoadCreator.Rhino.Layers;

/// <summary>
/// Defines the standard layer names and hierarchy for RoadCreator.
/// Uses "::" separator for parent-child relationships (Rhino convention).
/// Layer names kept as English identifiers; original Czech names in comments for reference.
/// </summary>
public static class LayerScheme
{
    // Top-level parent layer
    public const string Root = "RoadCreator";

    // Alignment layers (original: Tečnový polygon, Přechodnice, etc.)
    public const string TangentPolygon = "Tangent Polygon";
    public const string Stationing = "Stationing";
    public const string StationingPoints = "Stationing Points";
    public const string TransitionCurves = "Transition Curves";
    public const string ElevationPolygon = "Elevation Polygon";   // Výškový polygon
    public const string GradeLine = "Grade Line";                 // Niveleta
    public const string ParabolicArcs = "Parabolic Arcs";         // Parabolický oblouk
    public const string Route3D = "3D Route";                     // 3D Trasa

    // Road layers
    public const string Road3D = "3D Road";                       // Silnice 3D
    public const string CrossSectionProfiles = "Cross Sections";  // Příčné řezy
    public const string LaneStrips = "Lane Strips";               // Silniční pruhy
    public const string Verge = "Verge";                          // Krajnice

    // Terrain layers
    public const string Terrain = "TERRAIN";                      // TEREN
    public const string Contours = "Contours";                    // Vrstevnice
    public const string MainContours = "Main Contours";           // Hlavní vrstevnice
    public const string SecondaryContours5m = "Contours 5m";      // Vrstevnice 5m
    public const string MinorContours2m = "Contours 2m";          // Vrstevnice 2m
    public const string LongitudinalProfile = "Longitudinal Profile"; // Podélný profil
    public const string Slopes = "Slopes";                        // Svahy
    public const string TerrainCut = "Terrain Cut";               // Řez terénu svahy

    // Intersection layers
    public const string Intersection = "Intersection";            // Křižovatka
    public const string Roundabout = "Roundabout";                // Okružní křižovatka

    // Urban layers
    public const string Sidewalk = "Sidewalk";                    // Chodník / Obrubník
    public const string Crossing = "Crossing";                    // Přechod

    // Accessories layers
    public const string Guardrail = "Guardrail";                  // Svodidlo
    public const string GuardrailProfile = "Guardrail Profile";   // Svodidlo profil
    public const string RoadPoles = "Road Poles";                 // Silniční sloupky
    public const string ConcreteBarrier = "Concrete Barrier";     // Svodidla betonová

    public const string DeltaBlok = "Delta Blok";                 // Deltablok
    public const string ConcreteBarrierRods = "Concrete Barrier Rods"; // Tyče svodidla

    // Vegetation layers
    public const string Forest = "Forest";                        // Lesy
    public const string Trees = "Trees";                          // Stromy
    public const string Grass = "Grass";                          // Tráva

    // Signs
    public const string TrafficSigns = "Traffic Signs";           // Značky

    // Database
    public const string Database = "RC_Database";                 // DATABAZE
    public const string TreeDatabase = "RC_Database::Trees";      // Stromy databáze
    public const string SignDatabase = "RC_Database::Signs";
    public const string CustomDatabase = "RC_Database::Custom";

    // Objects placed from database
    public const string DatabaseObjects = "Database Objects";     // Objekty z databáze

    // 2D Footprint / Plan Markings layers (RC_RoadFootprint)
    // Full paths: "RoadCreator::Markings::{name}" and "RoadCreator::Reference::{name}"
    public const string MarkingsGroup      = "Markings";
    public const string MarkingsCenterline = "Markings::Centerline";
    public const string MarkingsLaneDivider = "Markings::LaneDivider";
    public const string MarkingsEdgeLine   = "Markings::EdgeLine";
    public const string MarkingsShoulder   = "Markings::Shoulder";
    public const string MarkingsCurb       = "Markings::Curb";

    public const string ReferenceGroup     = "Reference";
    public const string ReferenceSetback   = "Reference::Setback";
    public const string ReferenceRightOfWay = "Reference::RightOfWay";
    public const string ReferenceUtility   = "Reference::Utility";

    // Sublayer names (shared across multiple commands)
    public const string ImportantPoints = "Important Points";     // Důležité body
    public const string RoutePoints = "Points";

    /// <summary>
    /// Build a full layer path with the root prefix.
    /// Example: BuildPath("Tangent Polygon") => "RoadCreator::Tangent Polygon"
    /// </summary>
    public static string BuildPath(params string[] segments)
    {
        return Root + "::" + string.Join("::", segments);
    }

    /// <summary>
    /// Build a named road layer path.
    /// Example: BuildRoadPath("Highway_1", "Tangent Polygon") => "RoadCreator::Highway_1::Tangent Polygon"
    /// </summary>
    public static string BuildRoadPath(string roadName, params string[] segments)
    {
        return Root + "::" + roadName + "::" + string.Join("::", segments);
    }

    /// <summary>
    /// Build a layer path, using road-specific nesting if a road name is provided.
    /// </summary>
    public static string BuildOptionalRoadPath(string? roadName, params string[] segments)
    {
        return string.IsNullOrEmpty(roadName)
            ? BuildPath(segments)
            : BuildRoadPath(roadName, segments);
    }
}
