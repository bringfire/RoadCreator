namespace RoadCreator.Core.Localization;

/// <summary>
/// User-facing strings for RoadCreator. English is the primary language.
/// Czech translations can be added via Strings.cs.resx / satellite assemblies.
///
/// Organized by command/feature area. String keys follow: Area.Context.Message
/// </summary>
public static class Strings
{
    // General
    public const string Working = "Working...";
    public const string Done = "Done.";
    public const string Cancelled = "Cancelled.";

    // Selection prompts
    public const string SelectTerrain = "Select terrain surface or mesh";
    public const string SelectCurve = "Select curve";
    public const string SelectCenterline = "Select road centerline";
    public const string SelectProfile = "Select cross-section profile";
    public const string SelectProfiles = "Select cross-section profiles";
    public const string SelectEdgeCurve = "Select road edge curve";
    public const string SelectDirectionPoint = "Select direction point";
    public const string SelectStartPoint = "Select start point";
    public const string SelectEndPoint = "Select end point";
    public const string SelectCenterPoint = "Select center point";
    public const string SelectPlacementPoint = "Select placement point";
    public const string SelectRotationPoint = "Select rotation point";
    public const string SelectObjects = "Select objects";
    public const string SelectBasePoint = "Select base point";

    // Alignment — Horizontal
    public const string SelectFirstTangent = "Select first tangent segment";
    public const string SelectSecondTangent = "Select second tangent segment";
    public const string EnterTransitionLength = "Transition length (L)";
    public const string EnterRadius = "Radius (R)";

    // Alignment — Vertical
    public const string SelectFirstGradeSegment = "Select first grade segment";
    public const string SelectSecondGradeSegment = "Select second grade segment";
    public const string EnterParabolicRadius = "Parabolic arc radius (R)";
    public const string SelectRoadForProfile = "Select road for vertical profile";
    public const string SelectHorizontalAlignment = "Select horizontal alignment curve";
    public const string SelectGradeLine = "Select grade line (niveleta)";
    public const string EnterReferenceElevation = "Reference elevation (datum)";

    // Road profiles
    public const string SelectRoadCategory = "Select road category";
    public const string EnterLaneWidth = "Lane width";
    public const string EnterCrossfallStraight = "Crossfall in straight section (%)";
    public const string EnterCrossfallCurve = "Crossfall in curve (%)";
    public const string EnterVergeWidth = "Verge width";
    public const string EnableShoulders = "Include shoulders?";
    public const string EnableWidening = "Enable curve widening?";

    // Terrain
    public const string EnterContourInterval = "Contour interval";
    public const string EnterReferencePlane = "Reference elevation (datum)";
    public const string SelectRouteAxis = "Select route axis for longitudinal profile";
    public const string SelectProfileOrigin = "Select origin point for profile drawing";
    public const string EnterElevationLabelSpacing = "Elevation label spacing (m)";

    // Road 3D model
    public const string Select3DRoute = "Select 3D route curve";
    public const string SelectRoadDesign = "Select road design for 3D model";
    public const string IncludeVerge = "Include road verge (krajnice)?";
    public const string VergeEquipment = "Verge equipment: guardrail or road poles?";
    public const string SelectImportantPoints = "Select important points along 3D route";
    public const string NeedAtLeast2Points = "Need at least 2 important points.";
    public const string NotEnoughProfiles = "Could not create enough cross-section profiles.";
    public const string SweepFailed = "Sweep operation failed. Check route and profile alignment.";

    // Slopes
    public const string SelectLeftEdge = "Select left road edge curve";
    public const string SelectRightEdge = "Select right road edge curve";
    public const string EnterFillSlope = "Fill slope ratio (1:n)";
    public const string EnterCutSlope = "Cut slope ratio (1:n)";
    public const string IncludeDitches = "Include drainage ditches?";
    public const string EnterDitchDepth = "Ditch depth";
    public const string EnterDitchWidth = "Ditch width";
    public const string SlopesCreated = "Slopes created for both road edges.";

    // Verge
    public const string SelectSweepCurve = "Select curve to sweep along";
    public const string SelectDirection = "Pick direction point (outward side)";
    public const string VergeCreated = "Verge surface created.";

    // Junction
    public const string SelectRoadEdge = "Select road edge curve for junction";
    public const string SelectEndProfile = "Select end profile curve of connecting road";
    public const string EnterFilletRadius1 = "First fillet radius";
    public const string EnterFilletRadius2 = "Second fillet radius";
    public const string JunctionFitError = "Junction with specified radii does not fit.";
    public const string JunctionProjectionFailed = "Failed to project curves to XY plane.";
    public const string JunctionCenterNotFound = "Cannot find junction center on road edge.";
    public const string JunctionPerpNoIntersect = "Perpendicular lines do not intersect road edge.";
    public const string JunctionNo3DPosition = "Cannot find 3D positions on road edge.";
    public const string JunctionEdgeSegmentFailed = "Cannot extract road edge segment for junction.";
    public const string JunctionSurfaceFailed = "Edge surface creation failed.";
    public const string JunctionTrimExtrusionFailed = "Warning: trim extrusion creation failed, using untrimmed surface.";
    public const string JunctionTrimSplitFailed = "Warning: fillet trim did not split surface, using untrimmed surface.";
    public const string JunctionCreated = "Junction surface created.";

    // Intersections
    public const string EnterDiameter = "Outer diameter";
    public const string EnterNumberOfArms = "Number of arms";
    public const string EnterEntryWidth = "Entry width";
    public const string EnterExitWidth = "Exit width";
    public const string EnterEntryRadius = "Entry fillet radius";
    public const string EnterExitRadius = "Exit fillet radius";
    public const string EnterFilletRadius = "Fillet radius";

    // Roundabout
    public const string SelectRoundaboutCenter = "Select roundabout center point";
    public const string EnterOuterDiameter = "Outer diameter";
    public const string EnterArmDirection = "Pick direction of arm {0} (counter-clockwise)";
    public const string RoundaboutCreated = "Roundabout created.";
    public const string RoundaboutFilletFailed = "Fillet failed for arm {0}.";

    // Simple intersection
    public const string SelectIntersectionCenter = "Select intersection center point";
    public const string EnterArmRightWidth = "Right lane width for arm {0}";
    public const string EnterArmLeftWidth = "Left lane width for arm {0}";
    public const string EnterArmFilletRadius = "Fillet radius between arms {0} and {1}";
    public const string IntersectionCreated = "Intersection surface created.";
    public const string IntersectionFilletFailed = "Warning: fillet failed between arms {0} and {1}, joining curves instead.";

    // Sidewalk
    public const string SelectCurbEdge = "Select curb edge curve";
    public const string SelectSidewalkSide = "Pick sidewalk side";
    public const string EnterSidewalkWidth = "Sidewalk width";
    public const string SidewalkCreated = "Sidewalk created.";
    public const string SidewalkLoftFailed = "Loft operation failed for sidewalk.";

    // Urban road
    public const string SelectRouteForUrbanRoad = "Select route curve for urban road";
    public const string SelectUrbanProfiles = "Select cross-section profiles for urban road";
    public const string UrbanRoadCreated = "Urban road surface created.";

    // Pedestrian crossing
    public const string SelectRoadSurface = "Select road surface";
    public const string SelectCrossingStart = "Select crossing start point";
    public const string SelectCrossingEnd = "Select crossing end point";
    public const string EnterCrossingWidth = "Crossing width";
    public const string CrossingAreaCreated = "Pedestrian crossing area created.";
    public const string CrossingSplitFailed = "Failed to split road surface with crossing.";
    public const string ZebraCrossingCreated = "Zebra crossing created ({0} stripes).";

    // Accessories — Guardrails
    public const string SelectGuardrailCurve = "Select curve for guardrail placement";
    public const string SelectGuardrailDirection = "Pick guardrail direction (offset side)";
    public const string GuardrailSingleCreated = "Single-sided guardrail created.";
    public const string GuardrailDoubleCreated = "Double-sided guardrail created.";
    public const string GuardrailLoftFailed = "Guardrail loft failed.";

    // Accessories — Concrete barrier
    public const string SelectConcreteBarrierCurve = "Select curve for concrete barrier";
    public const string SelectConcreteBarrierPost = "Select concrete post object (or press Enter for default)";
    public const string SelectPostBasePoint = "Select post base point";
    public const string EnterPostSpacing = "Spacing between posts";
    public const string ConcreteBarrierCreated = "Concrete barrier created.";

    // Accessories — Delta Blok
    public const string SelectDeltaBlokCurve = "Select curve for Delta Blok barrier";
    public const string SelectDeltaBlokVariant = "Select Delta Blok variant";
    public const string DeltaBlokCreated = "Delta Blok barrier created.";

    // Accessories — Road poles
    public const string SelectPoleEdgeCurve = "Select road edge curve for pole placement";
    public const string SelectSecondPoleEdgeCurve = "Select second road edge curve";
    public const string SelectRoadAxisForPoles = "Pick road axis direction";
    public const string RoadPolesCreated = "Road poles placed.";
    public const string PolesDatabaseNotFound = "Database layer not found. Create an RC_Database layer with a pole object named 'RoadPole(RoadCreator)'.";
    public const string PoleObjectNotFound = "Pole object 'RoadPole(RoadCreator)' not found in database.";

    // Accessories — Perpendicular profile
    public const string SelectProfileCurve = "Select guide curve";
    public const string SelectProfileToPlace = "Select profile curve to place perpendicular";
    public const string SelectProfilePlacementPoint = "Pick placement point on curve (or press Enter for start)";
    public const string PerpendicularProfilePlaced = "Profile placed perpendicular to curve.";

    // Accessories — Copy along curve
    public const string SelectCurveForCopy = "Select curve for object placement";
    public const string SelectObjectsToCopy = "Select objects to copy along curve";
    public const string SelectCopyBasePoint = "Select base point of objects";
    public const string EnterCopySpacing = "Spacing between copies";
    public const string CopyAlongCurveCreated = "Objects copied along curve.";

    // Accessories — Split curve
    public const string SelectCurveToSplit = "Select curve to split";
    public const string SelectFirstSplitPoint = "Select first split point on curve";
    public const string SelectSecondSplitPoint = "Select second split point (or press Enter to skip)";
    public const string CurveSplit = "Curve split completed.";
    public const string CurveSplitFailed = "Failed to split curve at the specified point(s).";

    // Accessories — Database
    public const string SelectObjectsForDatabase = "Select objects to store in database";
    public const string SelectDatabaseBasePoint = "Select base point of the object";
    public const string EnterDatabaseObjectName = "Enter object name for database";
    public const string DatabaseNameExists = "An object with this name already exists in the database.";
    public const string DatabaseInsertDone = "Object stored in database.";
    public const string SelectTerrainForPlacement = "Select terrain for placement (or press Enter for flat)";
    public const string SelectPlacementPointOnTerrain = "Select placement point on terrain";
    public const string DatabaseRetrieveDone = "Object placed from database.";
    public const string DatabaseEmpty = "Database is empty.";
    public const string DatabaseNoNamedObjects = "No named objects found in database.";

    // Accessories — General
    public const string EnterSpacing = "Spacing distance";
    public const string EnterScaleVariance = "Scale variance (%)";
    public const string EnterObjectName = "Object name";

    // Vegetation — Forest
    public const string EnterDensity = "Tree density (average spacing)";
    public const string SelectForestSurface = "Select planar surface for forest placement";
    public const string SelectForestTerrain = "Select terrain surface or mesh for projection";
    public const string EnterTreeCount = "Number of tree templates to use";
    public const string SelectTreeTemplate = "Select tree template objects (tree {0})";
    public const string SelectTreeBasePoint = "Select base point (tree {0})";
    public const string TreeDatabaseEmpty = "Tree database is empty.";
    public const string TreeDatabaseNoTrees = "No trees found in tree database.";
    public const string ForestCreated = "Forest placed ({0} trees).";
    public const string ForestSilhouetteCreated = "Forest silhouette placed ({0} trees).";

    // Vegetation — Grass
    public const string SelectGrassEdgeCurve = "Select road edge curve for grass placement";
    public const string SelectGrassDirection = "Pick grass direction (offset side)";
    public const string GrassCreated = "Grass patches created ({0} patches).";

    // Vegetation — Magic Copy
    public const string SelectObjectToCopy = "Select object to copy";
    public const string ClickToPlace = "Click to place (Escape to finish)";
    public const string MagicCopyDone = "Magic copy completed ({0} copies).";

    // Vegetation — Tree Database
    public const string SelectTreesForDatabase = "Select tree objects to store in database";
    public const string SelectTreeDatabaseBasePoint = "Select base point of the tree";
    public const string EnterTreeName = "Enter tree name for database";
    public const string TreeDatabaseInsertDone = "Tree stored in database.";
    public const string TreeDatabaseRetrieveDone = "Tree placed from database.";
    public const string TreeDatabaseNameExists = "A tree with this name already exists in the database.";
    public const string SelectTreeFromDatabase = "Select tree from database";
    public const string TreePlacementLayer = "Trees";

    // Vegetation — Utility Poles
    public const string SelectPoleObject = "Select utility pole template object";
    public const string SelectPoleCurve = "Select curve for pole placement";
    public const string UtilityPolesCreated = "Utility poles placed ({0} poles).";

    // Traffic Signs
    public const string SelectSignPlacementPoint = "Select placement point for traffic sign";
    public const string SelectSignFromDatabase = "Select traffic sign from database";
    public const string SelectSignRotationPoint = "Select rotation direction point";
    public const string TrafficSignPlaced = "Traffic sign placed.";
    public const string SignDatabaseNotFound = "Database layer not found. Create an 'RC_Database' layer with sign objects.";
    public const string SignDatabaseEmpty = "No signs found in database.";

    // Route Discovery / RC2 Enhancements
    public const string SelectRoute = "Select road";
    public const string NoRoadsFound = "No roads found. Create a tangent polygon first.";
    public const string Route3DAlreadyExists = "3D route already exists for {0}. Delete it first to regenerate.";
    public const string AxisNotFound = "Tangent polygon (axis) not found for {0}.";
    public const string GradeLineNotFound = "Grade line (niveleta) not found for {0}. Select manually.";
    public const string DatumAutoDetected = "Reference elevation auto-detected: {0:F0} m";
    public const string Route3DCreatedWithPoints = "3D route created with {0} points over {1:F1} m. {2} important points projected.";
    public const string ContourStats = "Terrain: {0}, elevation range {1:F1} - {2:F1} m";

    // Export
    public const string SelectChainageInterval = "Chainage interval for export (m)";
    public const string ExportSaved = "Profile exported to {0}.";
    public const string ExportFailed = "Export failed: {0}";
    public const string ProfileDataNotFound = "Could not find profile data for {0}. Ensure longitudinal profile exists.";

    // Errors
    public const string ErrorNoSelection = "Nothing selected.";
    public const string ErrorInvalidTerrain = "Selected object is not a valid terrain (surface or mesh).";
    public const string ErrorNotACurve = "Selected object is not a curve.";
    public const string ErrorParallelLines = "Lines are parallel, no intersection found.";
}
