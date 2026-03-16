# RoadCreator Command Reference

All commands use the `RC_` prefix and are available in the Rhino command line after loading the plugin.

---

## Alignment

Commands for horizontal and vertical road alignment design.

| Command | Description |
|---------|-------------|
| `RC_TangentPolygon` | Creates a tangent polygon (alignment baseline) from a user-drawn polyline. Labels segments with bearings, distances, and chainage. |
| `RC_ElevationPolygon` | Creates an elevation polygon (vertical tangent polygon) in profile space (X = chainage, Y = elevation x10). Supports road association or standalone mode. |
| `RC_Clothoid` | Inserts a symmetric clothoid transition curve between two tangent segments. Parameters: L (length), R (radius). |
| `RC_CubicParabola` | Inserts a symmetric cubic parabola transition curve between two tangent segments. Parameters: L (length), R (radius). |
| `RC_ParabolicCurve` | Inserts a parabolic vertical curve between two grade segments. Drawn in profile space with 10:1 elevation exaggeration. |
| `RC_Assemble3DRoute` | Assembles a 3D route centerline from a horizontal alignment curve and a grade line (niveleta). Auto-detects road or allows manual selection. |

---

## Road

Commands for generating 3D road geometry.

| Command | Description |
|---------|-------------|
| `RC_Road3D` | Generates a 3D road surface by sweeping cross-section profiles along an entire 3D route. Selects road category for profile dimensions. |
| `RC_Road3DSection` | Generates a 3D road surface for a subsection of the route. User picks start and end points to model only a portion. |
| `RC_Verge` | Creates a road verge (shoulder) surface by sweeping an 8% slope profile along a road edge. Configurable width and direction. |
| `RC_Junction` | Creates a junction surface connecting a side road to a main road edge, with filleted corners at configurable radii. |
| `RC_ResolveIntersection` | Resolves an intersection between two roads via the RookRoads HTTP API. Sends road profiles and geometry to the analysis endpoint, then realizes the result in the document. |

---

## Footprint

Commands for 2D plan-view road footprints using offset profiles and style sets.

| Command | Description |
|---------|-------------|
| `RC_RoadFootprint` | Generates 2D plan-view offset curves from a centerline using a named or inline OffsetProfile and StyleSet. Supports both interactive and scripted (MCP agent) modes. |
| `RC_StoreProfile` | Stores an OffsetProfile JSON definition in the current document. The profile can then be referenced by name in `RC_RoadFootprint`. |
| `RC_StoreStyleSet` | Stores a StyleSet JSON definition in the current document. The style set can then be referenced by name in `RC_RoadFootprint`. |
| `RC_ListProfiles` | Lists all OffsetProfile definitions stored in the current document. Output is written to the command line. |
| `RC_ListStyleSets` | Lists all StyleSet definitions available: document-stored first, then built-ins. |
| `RC_DeleteProfile` | Deletes a named OffsetProfile from the current document. Interactive or scripted mode. |
| `RC_DeleteStyleSet` | Deletes a named StyleSet from the current document. Built-in style sets cannot be deleted. |

---

## Terrain

Commands for terrain analysis and profile generation.

| Command | Description |
|---------|-------------|
| `RC_LongitudinalProfile` | Generates a longitudinal profile by projecting a route axis onto terrain (mesh or NURBS surface). Auto-detects route via RouteDiscovery with manual fallback. Draws labeled profile with elevation markers. |
| `RC_LongitudinalProfileExport` | Exports a longitudinal profile to an Excel (.xlsx) file. Samples terrain and grade line at user-specified intervals. Uses ClosedXML for cross-platform output. |
| `RC_ContourLines` | Generates contour lines from a terrain surface or mesh at a specified vertical interval. |

---

## Slopes

| Command | Description |
|---------|-------------|
| `RC_Slopes` | Generates embankment/cut slopes along both road edges, with optional drainage ditches. Automatically determines fill vs. cut by splitting against terrain. |

---

## Nature

Commands for vegetation, tree databases, and landscape elements.

| Command | Description |
|---------|-------------|
| `RC_Forest` | Grid-based random forest placement on a surface with terrain projection. Selects trees from the Tree Database, randomizes rotation and scale. |
| `RC_ForestSilhouette` | Places 3 offset rows of trees along a road boundary to create a forest silhouette effect. Trees are projected onto terrain. |
| `RC_ForestSilhouetteCurve` | Forest silhouette along an existing curve with adaptive spacing. Similar to `RC_ForestSilhouette` but follows any user-drawn curve. |
| `RC_MagicCopy` | Interactive single-object placement with random rotation and scale. Uses tree templates from the Tree Database. Click to place, each click gets random variation. |
| `RC_MagicCopyMulti` | Interactive multi-object placement: each click places a random tree from a set of user-selected templates with random rotation and scale. |
| `RC_Grass` | Grass patch placement along road edges with terrain projection. Creates offset rows of grass blocks along a curve. |
| `RC_UtilityPoles` | Utility pole placement along a curve with terrain projection. Places pole template objects at regular intervals. |
| `RC_TreeDatabaseInsert` | Stores tree objects in the Tree Database layer with a companion base point. Works with both document layers and external `.3dm` database. |
| `RC_TreeDatabaseRetrieve` | Retrieves a tree from the Tree Database and places it at a user-picked point with interactive rotation. |

---

## Urban

Commands for urban road design elements.

| Command | Description |
|---------|-------------|
| `RC_UrbanRoad` | Creates a 3D urban road surface by sweeping user-selected cross-section profiles along a route. Simplified version of `RC_Road3D` for urban contexts. |
| `RC_Sidewalk` | Creates a sidewalk with raised curb along a road edge curve. Configurable width and curb height. |
| `RC_CrossingArea` | Creates a pedestrian crossing area by splitting a road surface with a rectangle defined by two points. |
| `RC_ZebraCrossing` | Creates zebra crossing stripes by repeatedly splitting a road surface. Alternating white/road stripes form the zebra pattern. |
| `RC_Roundabout` | Creates a roundabout with configurable number of arms, fillet radii, and lane widths. |
| `RC_SimpleIntersection` | Creates a simple multi-arm intersection with fillet curves between arms. User picks center and arm endpoints. |

---

## Accessories

Commands for road furniture, barriers, and utility operations.

| Command | Description |
|---------|-------------|
| `RC_GuardrailSingle` | Creates a single-sided metal W-beam guardrail along a road edge. Posts at regular spacing with beam rail on top. |
| `RC_GuardrailDouble` | Creates a double-sided metal W-beam guardrail along a road center line. Beams on both sides of the posts. |
| `RC_ConcreteBarrier` | Creates a concrete barrier (New Jersey style) with posts and horizontal rods along a curve. |
| `RC_DeltaBlokBarrier` | Creates a Delta Blok concrete barrier along a curve. Modular precast barrier segments. |
| `RC_RoadPolesSingle` | Places road delineator poles along one side of a road edge with adaptive spacing. Spacing adapts to local curve radius (closer in curves, wider on straights). |
| `RC_RoadPolesDouble` | Places road delineator poles on both sides of a road with adaptive spacing. Combines two single-side passes. |
| `RC_CopyAlongCurve` | Copies objects along a curve at regular spacing, rotating each copy to match the curve tangent. |
| `RC_PerpendicularProfile` | Places a profile curve perpendicular to a guide curve at a specified point. Useful for cross-section visualization. |
| `RC_SplitCurveAtPoints` | Splits a curve at one or two user-picked points. Replaces the original curve with the resulting segments. |
| `RC_DatabaseInsert` | Stores selected objects in the RC_Database layer with a companion base point. Objects can later be retrieved by name. Works with external `.3dm` database. |
| `RC_DatabaseRetrieve` | Retrieves a named object from the database, copies it to a placement point with optional terrain selection and rotation. |

---

## Signs

| Command | Description |
|---------|-------------|
| `RC_TrafficSign` | Places a traffic sign from the database onto terrain with rotation. Consolidates 14 original VBScript sign commands into one parameterized command. Select sign by name, pick terrain point, set rotation. |

---

## Database

| Command | Description |
|---------|-------------|
| `RC_SetExternalDatabase` | Configures the external `.3dm` database file path. Options: Browse (file dialog), Clear (disable external database). Path persists across Rhino sessions. When set, all database commands read/write the external file instead of document layers. |

---

## Notes

- Commands that read from the database (`RC_DatabaseRetrieve`, `RC_TreeDatabaseRetrieve`, `RC_TrafficSign`, `RC_Forest`, `RC_RoadPolesSingle`, `RC_RoadPolesDouble`) support both document layers and the external `.3dm` database.
- Block instances (e.g., Enscape proxies) are fully supported in the external database. Definitions are transferred automatically when placing templates.
- All terrain-aware commands work with both NURBS surfaces and meshes via the unified `ITerrain` interface.
- Scripted mode (`RunMode.Scripted`) is supported on key commands for automation via RhinoScript or MCP agents.
