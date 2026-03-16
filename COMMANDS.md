# RoadCreator Command Reference

All commands use the `RC_` prefix and are available in the Rhino command line after loading the plugin.

---

## Alignment

Commands for horizontal and vertical road alignment design.

### `RC_TangentPolygon`

Creates a tangent polygon (alignment baseline) from a user-drawn polyline.

**Parameters:** None — interactive point picking only.

**Usage:**
1. Click points in sequence to draw the alignment polyline
2. Press Enter when finished

**Result:** Line segments on the `RoadCreator::{RoadName}::Tangent Polygon` layer, with stationing tick marks, chainage labels, bearing/distance annotations, and a `ZU km 0.000000` start marker. Road name is auto-assigned (`Road_1`, `Road_2`, ...). Stationing layers are locked.

---

### `RC_ElevationPolygon`

Creates an elevation polygon (vertical tangent polygon) in profile space.

**Parameters:** None — interactive point picking, plus road selection dialog.

**Usage:**
1. Draw polyline points in profile space (X = chainage, Y = elevation x10 exaggeration)
2. Press Enter when finished
3. Select an existing road to associate with, or create a standalone profile

**Result:** Grade line segments on `RoadCreator::{RoadName}::Grade Line` layer, named `{RoadName} grade 1`, `grade 2`, etc. Distance labels (`d=...m`), slope labels (`s=...%`), and vertical reference lines at start/end. Stationing layer locked.

---

### `RC_Clothoid`

Inserts a symmetric clothoid transition curve between two tangent segments.

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| L | number | 70 | Transition length (m) |
| R | number | 150 | Circular curve radius (m) |

**Usage:**
1. Select first tangent segment (line)
2. Select second tangent segment (line)
3. Enter transition length L
4. Enter radius R

**Result:** Clothoid transition curve inserted between the two tangent segments. Curve parameters (large tangent, shift, xs) displayed in console.

---

### `RC_CubicParabola`

Inserts a symmetric cubic parabola transition curve between two tangent segments.

**Parameters:**

| Parameter | Type | Default | Constraint | Description |
|-----------|------|---------|------------|-------------|
| L | number | 60 | L < 2R | Transition length (m) |
| R | number | 200 | | Circular curve radius (m) |

**Usage:**
1. Select first tangent segment (line)
2. Select second tangent segment (line)
3. Enter transition length L
4. Enter radius R

**Result:** Cubic parabola transition curve inserted between segments. Fails with error if L >= 2R.

---

### `RC_ParabolicCurve`

Inserts a parabolic vertical curve between two grade segments in profile space.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| R | number | 2000 | 50–100,000 | Vertical curve radius (m) |

**Usage:**
1. Select first grade segment (from elevation polygon)
2. Select second grade segment
3. Enter vertical radius R

**Result:** Parabolic curve on the grade line layer, with important points (ZZ = start, V = vertex, KZ = end) on a separate sublayer. Vertical reference lines, legend circle with `R`, `t` (tangent length), and `Ymax` annotations. Original grade segments are deleted. Console reports `R`, `t`, `ymax`, and sag/crest type.

---

### `RC_Assemble3DRoute`

Assembles a 3D route centerline from a horizontal alignment curve and a grade line.

**Parameters:** Auto-detected from document layers; manual fallback if not found.

**Usage:**
1. Select road (auto-detected or pick from list)
2. System locates tangent polygon axis, grade line, and datum elevation
3. If any input is missing, user is prompted to select manually

**Result:** 3D route curve on `RoadCreator::{RoadName}::Route 3D` layer, with station points at 2m intervals and projected important points (ZP, PO, OP, KP, ZU, KU). Console reports point count, length, and important point count.

---

## Road

Commands for generating 3D road geometry.

### `RC_Road3D`

Generates a 3D road surface by sweeping cross-section profiles along an entire 3D route.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Road category | option | — | S6.5–D4/8 | Czech road standard category |
| Crossfall straight | number | 2.5 | 2.0–4.0 | Crossfall on straight sections (%) |
| Crossfall curve | number | 4.0 | 4.0–20.0 | Crossfall in curves (%) |
| Include verge | Yes/No | No | | Add road verge to profile |
| Verge equipment | option | — | Guardrail/RoadPoles | Equipment type if verge included |

**Usage:**
1. Select 3D route curve (auto-discover or manual)
2. Choose road category
3. Enter crossfall values
4. Choose verge options
5. Important points are auto-detected or manually selected

**Result:** Road surface BREP (`{RoadName} 3D_model`) and two boundary edge curves (`{RoadName} boundary`) on `{RoadName}/Road3D/` layer. Cross-section profiles on a locked sublayer.

---

### `RC_Road3DSection`

Generates a 3D road surface for a subsection of the route.

**Parameters:** Same as `RC_Road3D`, plus:

| Parameter | Type | Description |
|-----------|------|-------------|
| Start point | point (optional) | Start of section on route (Enter to skip = route start) |
| End point | point (optional) | End of section on route (Enter to skip = route end) |

**Usage:**
1. Select 3D route curve
2. Pick start point on route (or Enter for beginning)
3. Pick end point on route (or Enter for end)
4. Enter road category and crossfall parameters

**Result:** Section BREP (`{RoadName} 3D_section`) and section boundary curves on `{RoadName}/Sections/` layer.

---

### `RC_Verge`

Creates a road verge (shoulder) surface by sweeping an 8% slope profile along a road edge.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Verge width | number | 0.5 | 0.1–20.0 | Width of verge (m) |

**Usage:**
1. Select road edge curve
2. Pick direction point (outward side of verge)
3. Enter verge width

**Result:** Verge BREP surface on `{RoadName}/Verge/` layer, swept at 8% outward slope.

---

### `RC_Junction`

Creates a junction surface connecting a side road to a main road edge.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Fillet radius 1 | number | 7.0 | 1.0–100.0 | Left corner radius (m) |
| Fillet radius 2 | number | 7.0 | 1.0–100.0 | Right corner radius (m) |

**Usage:**
1. Select main road edge curve (3D)
2. Select connecting road end profile curve (3D)
3. Enter left fillet radius
4. Enter right fillet radius

**Result:** Network surface (edge surface) connecting 4 boundary curves with filleted corners on `Road3D/` layer.

---

### `RC_ResolveIntersection`

Resolves an intersection between two roads via the RookRoads HTTP API.

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Profile A | string | arterial_with_bike_lanes | Offset profile name for road A |
| Profile B | string | collector_one_side_bike | Offset profile name for road B |
| Candidate mode | option | Single | Single (best) or All candidates |
| Asymmetric side A | string | (none) | Left/Right if profile is asymmetric |
| Asymmetric side B | string | right | Left/Right if profile is asymmetric |

**Usage:**
1. Select two centerline curves
2. Enter profile names for each road
3. Choose candidate mode
4. If multiple candidates: select one or choose "All"
5. If asymmetric profiles: specify side for each road

**Result:** Intersection geometry (boundary curves and surfaces) on `RoadCreator::ResolveIntersection_{timestamp}/` layer. Console reports candidate ID, surface area, and boundary length.

---

## Footprint

Commands for 2D plan-view road footprints using offset profiles and style sets.

### `RC_RoadFootprint`

Generates 2D plan-view offset curves from a centerline.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Centerline | curve | Select or pre-select centerline curve |
| Profile | string or JSON | Named profile or inline OffsetProfile JSON |
| Style set | string | Named style set or "default" |

**Usage (interactive):**
1. Select centerline curve
2. Choose stored profile from list or paste inline JSON
3. Choose style set from list

**Usage (scripted/MCP):**
Pre-select curve, then: `_RC_RoadFootprint _Enter profileName _Enter styleSetName _Enter`

**Result:** One offset curve per profile feature, placed on appropriate layers. Feature IDs and object GUIDs reported to command line.

---

### `RC_StoreProfile`

Stores an OffsetProfile JSON definition in the current document.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| JSON | string | Minified OffsetProfile JSON (single line) |

**Usage:**
1. Run command
2. Paste profile JSON (must contain `"name"` field, schema `roadcreator.offset-profile/v1`)

**Result:** Profile stored in document, retrievable by name in `RC_RoadFootprint`. Console confirms name and feature count.

---

### `RC_StoreStyleSet`

Stores a StyleSet JSON definition in the current document.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| JSON | string | Minified StyleSet JSON (single line) |

**Usage:**
1. Run command
2. Paste style set JSON (must contain `"name"` field, schema `roadcreator.style-set/v1`)

**Result:** Style set stored in document. Console confirms name and style count.

---

### `RC_ListProfiles`

Lists all OffsetProfile definitions stored in the current document.

**Parameters:** None.

**Result:** Command line output listing each profile with name, feature count, units, and baseline value.

---

### `RC_ListStyleSets`

Lists all StyleSet definitions available.

**Parameters:** None.

**Result:** Command line output in two sections: document-stored style sets, then built-in style sets, each with style counts.

---

### `RC_DeleteProfile`

Deletes a named OffsetProfile from the current document.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Profile name | string | Name of profile to delete |

**Usage (interactive):** Pick from list. **Usage (scripted):** `_RC_DeleteProfile _Enter profileName _Enter`

**Result:** Profile removed from document storage.

---

### `RC_DeleteStyleSet`

Deletes a named StyleSet from the current document. Built-in style sets cannot be deleted.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Style set name | string | Name of document-stored style set to delete |

**Usage (interactive):** Pick from list of document-stored sets. **Usage (scripted):** `_RC_DeleteStyleSet _Enter styleName _Enter`

**Result:** Style set removed from document storage.

---

## Terrain

Commands for terrain analysis and profile generation.

### `RC_LongitudinalProfile`

Generates a longitudinal profile by projecting a route axis onto terrain.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Elevation label spacing | number | 20 | 5–200 | Interval for elevation labels (m) |

**Usage:**
1. Select or auto-detect route axis curve
2. Select terrain (mesh or NURBS surface)
3. Enter elevation label spacing
4. Pick profile origin point in drawing

**Result:** 2D terrain profile curve (X = chainage, Y = elevation x10) with baseline, km markers, elevation labels with rotated text, and projected important points. Organized in sublayers: Terrain Profile, Elevation Labels, Important Points. Layers locked.

---

### `RC_LongitudinalProfileExport`

Exports a longitudinal profile to an Excel (.xlsx) file.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Road name | string | Auto-detected or manual entry |
| Chainage interval | option | 5m, 10m, 20m, 50m, 100m, or 200m |
| File path | file dialog | Save location for .xlsx file |

**Usage:**
1. Select or identify road
2. Choose chainage sampling interval
3. Browse and select save location

**Result:** Formatted Excel workbook with columns for chainage, slope, grade polygon elevation, vertical curve parameters, grade line elevation, terrain elevation, and elevation difference. Formatted with borders and optimized column widths.

---

### `RC_ContourLines`

Generates contour lines from a terrain surface or mesh.

**Parameters:** None — contour interval is fixed at 1m with classification at 5m and 10m.

**Usage:**
1. Select terrain (mesh, surface, or NURBS surface)

**Result:** Three layers of contour curves:
- Main contours (10m intervals) — yellow/gold
- Secondary contours (5m intervals) — dark gray, hidden by default
- Minor contours (2m intervals) — dark purple

Contours named with elevation (e.g., `RC_Main 100`). All layers locked. Console reports count and elevation range.

---

## Slopes

### `RC_Slopes`

Generates embankment/cut slopes along both road edges with optional drainage ditches.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Fill slope ratio | number | 1.75 | 1.0–4.0 | Fill slope as 1:n |
| Cut slope ratio | number | 1.75 | 1.0–4.0 | Cut slope as 1:n |
| Include ditches | Yes/No | No | | Add drainage ditches |
| Ditch depth | number | 0.4 | 0.3–2.0 | Ditch depth (m), if ditches enabled |
| Ditch width | number | 0.5 | 0.2–2.0 | Ditch width (m), if ditches enabled |

**Usage:**
1. Select terrain (surface or mesh)
2. Select left road edge curve
3. Select right road edge curve
4. Enter fill and cut slope ratios
5. Choose ditch inclusion; if yes, enter depth and width

**Result:** Slope BREP surfaces on `Slopes` layer (brown), split against terrain to distinguish fill/cut regions. Surface normals flipped for consistent rendering. End-cap surfaces between edges.

---

## Nature

Commands for vegetation, tree databases, and landscape elements.

### `RC_Forest`

Grid-based random forest placement on a surface with terrain projection.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Density | number | varies | | Grid density value |
| Scale variance | number | 20 | 0–100 | Random scale variation (%) |

**Usage:**
1. Select planar surface defining the forest area
2. Select terrain for projection
3. Enter density
4. Enter scale variance

**Result:** Grid of randomly placed trees on the Trees layer. Each tree has random rotation (0-360) and scale variation, projected to terrain. Console reports tree count.

**Prerequisite:** Tree Database must contain at least one tree template.

---

### `RC_ForestSilhouette`

Places 3 offset rows of trees along a road boundary for a forest silhouette effect.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Scale variance | number | 20 | 0–100 | Random scale variation (%) |

**Usage:**
1. Select road edge curve
2. Pick direction point (offset side)
3. Select terrain
4. Enter scale variance

**Result:** 3 rows of trees at 4m, 7m, and 10m offset from the edge, spaced at 5.5m along each row. Each tree has +/-2m XY jitter and random rotation/scale. Console reports total count.

---

### `RC_ForestSilhouetteCurve`

Forest silhouette along an existing curve with adaptive spacing.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Spacing | number | 5.0 | 0.5–100.0 | Distance between trees along curve (m) |
| Scale variance | number | 20 | 0–100 | Random scale variation (%) |

**Usage:**
1. Select guide curve
2. Pick direction point (offset side)
3. Select terrain
4. Enter spacing
5. Enter scale variance

**Result:** 3 offset rows of trees along the guide curve at user-defined spacing. Each tree has jitter, random rotation, and scale variation. Console reports total count.

---

### `RC_MagicCopy`

Interactive single-object placement with random rotation and scale from the Tree Database.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Scale variance | number | 20 | 0–100 | Random scale variation (%) |

**Usage:**
1. Select terrain
2. Enter scale variance
3. Click repeatedly to place trees — each click places a random tree from the database
4. Press Escape to finish

**Result:** One randomly selected tree per click, projected to terrain, with random rotation and scale. Console reports total copies made.

---

### `RC_MagicCopyMulti`

Interactive multi-object placement from user-selected templates (not database).

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Number of templates | number | 2 | 1–20 | How many different templates to define |
| Scale variance | number | 20 | 0–100 | Per-template scale variation (%) |

**Usage:**
1. Select terrain
2. Enter number of templates
3. For each template: select objects, pick base point, enter scale variance
4. Click repeatedly to place — each click picks a random template
5. Press Escape to finish

**Result:** Random template placed per click with random rotation and scale. Console reports total copies.

---

### `RC_Grass`

Grass patch placement along road edges with terrain projection.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select road edge curve
2. Pick direction point (offset side)
3. Select terrain

**Result:** 2 rows of grass patches at 2m and 4m offset. 4m horizontal lines at 0.8m intervals, randomly rotated, projected to terrain and extruded 0.5m vertically. Console reports patch count.

---

### `RC_UtilityPoles`

Utility pole placement along a curve with terrain projection.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select pole template objects
2. Pick base point of pole
3. Select terrain
4. Select guide curve for pole route

**Result:** Poles placed at 10m intervals along the guide curve, each projected to terrain and rotated by curve tangent angle. Console reports pole count.

---

### `RC_TreeDatabaseInsert`

Stores tree objects in the Tree Database layer with a companion base point.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Tree name | string | Unique name for the tree template |

**Usage:**
1. Select tree objects to store
2. Pick base point
3. Enter unique name

**Result:** Tree stored in `Tree Database` layer (or external `.3dm` database if configured). Companion point created at base location. Console confirms insertion.

---

### `RC_TreeDatabaseRetrieve`

Retrieves a tree from the Tree Database and places it with interactive rotation.

**Parameters:** None — selection from database list.

**Usage:**
1. Select tree from presented list
2. Pick placement point
3. Pick rotation angle point (or Escape to skip)

**Result:** Tree copies placed on `Trees` layer (green) at the placement point, rotated toward the rotation point. Block instances from external database are resolved automatically.

---

## Urban

Commands for urban road design elements.

### `RC_UrbanRoad`

Creates a 3D urban road surface by sweeping user-selected cross-section profiles along a route.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select route curve
2. Select one or more cross-section profile curves

**Result:** Swept road surface BREP and edge curves on `Road 3D` layer (black).

---

### `RC_Sidewalk`

Creates a sidewalk with raised curb along a road edge curve.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Sidewalk width | number | 5.0 | 0.5–30.0 | Width of sidewalk (m) |

**Usage:**
1. Select curb edge curve
2. Pick direction point (sidewalk side)
3. Enter sidewalk width

**Result:** Curb surface (raised 0.2m, lofted from 3 curves) and sidewalk surface (lofted between curb top and outer edge) on `Sidewalk` layer (gray).

---

### `RC_CrossingArea`

Creates a pedestrian crossing area by splitting a road surface with a rectangle.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Crossing width | number | 4.0 | 1–20 | Width along road direction (m) |

**Usage:**
1. Select road surface (BREP)
2. Pick crossing start point
3. Pick crossing end point (defines direction)
4. Enter crossing width

**Result:** Road surface split into crossing area and surrounding fragments. Original road deleted, fragments on `Crossing` layer (white).

---

### `RC_ZebraCrossing`

Creates zebra crossing stripes by repeatedly splitting a road surface.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Crossing width | number | 4.0 | 1–20 | Width along road direction (m) |

**Usage:**
1. Select road surface
2. Pick crossing start and end points
3. Enter crossing width

**Result:** Alternating 0.5m stripe fragments at 1m spacing (zebra pattern). Original road deleted, all stripes on `Crossing` layer (white).

---

### `RC_Roundabout`

Creates a roundabout with configurable arms, fillets, and lane widths.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Outer diameter | number | 25 | 14–50 | Outer circle diameter (m) |
| Number of arms | number | 4 | 2–6 | Number of roundabout arms |
| Per arm: entry width | number | 3.0 | | Entry lane width (m) |
| Per arm: entry radius | number | 8.0 | 6–17 | Entry fillet radius (m) |
| Per arm: exit width | number | 3.0 | | Exit lane width (m) |
| Per arm: exit radius | number | 12.0 | min 12 | Exit fillet radius (m) |

**Usage:**
1. Pick roundabout center point
2. Enter outer diameter
3. Enter number of arms
4. For each arm (counter-clockwise): pick direction, enter entry/exit widths and fillet radii

**Result:** Outer/inner circles, arm surfaces, annular road surface, apron ring (if diameter >= 25m), raised island surface, and island wall extrusion on `Roundabout` layer (black).

---

### `RC_SimpleIntersection`

Creates a simple multi-arm intersection with fillet curves between arms.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Number of arms | number | 3 | 2–8 | Number of intersection arms |
| Per arm: right width | number | 3.75 | min 2 | Right lane width (m) |
| Per arm: left width | number | 3.75 | min 2 | Left lane width (m) |
| Per arm: fillet radius | number | varies | | Fillet radius to next arm (m) |

**Usage:**
1. Pick intersection center point
2. Enter number of arms
3. For each arm (counter-clockwise): pick direction, enter lane widths, enter fillet radius to next arm

**Result:** Boundary curves, fillet arcs, profile lines, half-length axis lines, and planar intersection BREP surface on `Intersection` layer (black).

---

## Accessories

Commands for road furniture, barriers, and utility operations.

### `RC_GuardrailSingle`

Creates a single-sided metal W-beam guardrail along a road edge.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select road edge curve
2. Pick direction point (guardrail side)

**Result:** Curve offset 0.37m, divided at 4m post spacing. W-beam profile curves, post boxes, bracket surfaces, and lofted guardrail surface on `Guardrail` and `GuardrailProfile` layers.

---

### `RC_GuardrailDouble`

Creates a double-sided metal W-beam guardrail along a road center line.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select center line curve

**Result:** Divided at 4m spacing. Front and back W-beam profiles, post boxes at mid-points, two lofted guardrail surfaces on `Guardrail` and `GuardrailProfile` layers.

---

### `RC_ConcreteBarrier`

Creates a concrete barrier with posts and horizontal rods along a curve.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Post spacing | number | 2.5 | min 0.5 | Distance between posts (m) |
| Custom posts | optional | default box | | Select custom post objects or use default 0.35x0.35x1.4m box |
| Post base point | point | — | | If custom posts: pick base point |

**Usage:**
1. Select guide curve
2. Enter post spacing
3. Choose custom post objects or use default
4. If custom: pick base point

**Result:** Post boxes, lower rod surface (at 0.5m), and upper rod surface (at 0.9m) on `ConcreteBarrier` and `ConcreteBarrierRods` layers.

---

### `RC_DeltaBlokBarrier`

Creates a Delta Blok concrete barrier along a curve.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Variant | option | DeltaBlok80, 100S, 100, or 120 |

**Usage:**
1. Select guide curve
2. Select DeltaBlok variant

**Result:** Barrier divided at 4m block spacing with end-cap profiles, transition profiles, and main profiles lofted into a continuous barrier surface. Edge surface caps at both ends. On `DeltaBlok` layer (purple).

---

### `RC_RoadPolesSingle`

Places road delineator poles along one side of a road edge with adaptive spacing.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select road edge curve
2. Pick road axis point (determines pole facing side)

**Result:** Pole template copies from database (`RoadPole(RoadCreator)`) placed on `RoadPoles` layer (black). Spacing adapts to local curve radius: closer in tight curves, wider on straights. Base interval is 5m.

---

### `RC_RoadPolesDouble`

Places road delineator poles on both sides of a road with adaptive spacing.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select first road edge curve
2. Select second road edge curve

**Result:** Poles placed on both curves with matching adaptive spacing. Second curve poles placed at closest corresponding points. On `RoadPoles` layer (black).

---

### `RC_CopyAlongCurve`

Copies objects along a curve at regular spacing, rotating each copy to match the curve tangent.

**Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| Spacing | number | 5.0 | 1–1000 | Distance between copies (m) |

**Usage:**
1. Select guide curve
2. Select objects to copy
3. Pick base point of source objects
4. Enter spacing

**Result:** Multiple copies placed at regular intervals along the curve, each rotated to follow the curve direction. Original object attributes/layers maintained.

---

### `RC_PerpendicularProfile`

Places a profile curve perpendicular to a guide curve at a specified point.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select guide curve
2. Select profile curve to place
3. Pick placement point on guide curve (or skip for curve start)

**Result:** Profile curve moved to the placement point and rotated to align perpendicular to the guide curve tangent. Transformed in-place.

---

### `RC_SplitCurveAtPoints`

Splits a curve at one or two user-picked points.

**Parameters:** None beyond geometry selection.

**Usage:**
1. Select curve to split
2. Pick first split point on the curve
3. Pick second split point (optional)

**Result:** Original curve deleted. Two or three curve segments added to the document, maintaining original attributes/layer.

---

### `RC_DatabaseInsert`

Stores selected objects in the RC_Database layer with a companion base point.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Object name | string | Unique name for the template |

**Usage:**
1. Select objects to store
2. Pick base point (reference for future placement)
3. Enter unique name

**Result:** Objects stored in `RC_Database` layer (or external `.3dm` database) with companion point named `{Name}-point(RoadCreator)`. Layer locked and hidden. Fails if name already exists.

---

### `RC_DatabaseRetrieve`

Retrieves a named object from the database with placement and rotation.

**Parameters:** None — selection from database list.

**Usage:**
1. Select template from database list
2. Optionally select terrain surface (for projected placement)
3. Pick placement point
4. Pick rotation angle point

**Result:** Template copies placed on `DatabaseObjects` layer, translated to placement point and rotated toward rotation point. Block instances from external database resolved automatically.

---

## Signs

### `RC_TrafficSign`

Places a traffic sign from the database onto terrain with rotation.

**Parameters:** None — interactive selection from database list.

**Usage:**
1. Select terrain (surface or mesh)
2. Pick placement point (projected to terrain)
3. Select sign from database list
4. Pick rotation reference point

**Result:** Sign copies on `Traffic Signs` layer (black), named `TrafficSign`, placed at terrain-projected point and rotated toward the reference point.

---

## Database

### `RC_SetExternalDatabase`

Configures the external `.3dm` database file path.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Action | option | Browse (file dialog) or Clear (disable) |

**Usage:**
1. Run command — current path is displayed
2. Select Browse to pick a `.3dm` file, or Clear to disable

**Result:** Path persisted in plugin settings across Rhino sessions. When set, all database commands (`RC_DatabaseInsert`, `RC_DatabaseRetrieve`, `RC_TreeDatabaseInsert`, `RC_TreeDatabaseRetrieve`, `RC_TrafficSign`, `RC_Forest`, `RC_RoadPolesSingle`, `RC_RoadPolesDouble`) read/write the external file instead of document layers. If the file is later moved/deleted, commands fail explicitly rather than silently falling back.

---

## Notes

- All terrain-aware commands work with both NURBS surfaces and meshes via the unified `ITerrain` interface.
- Block instances (e.g., Enscape proxies) are fully supported in the external database. Definitions are transferred automatically with `RC_DB:` namespace prefix.
- Scripted mode (`RunMode.Scripted`) is supported on key commands for automation via RhinoScript or MCP agents.
- Commands that use the database validate the external path at entry — if a path is configured but the file is missing, the command fails immediately with a message to run `RC_SetExternalDatabase`.
