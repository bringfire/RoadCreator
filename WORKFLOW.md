# RoadCreator Workflow: Curve Centerline to 3D Road

This document defines the complete workflow for creating a 3D road from a curve centerline using the RoadCreator plugin. It is written for both human designers and AI agents that automate or assist with the process.

---

## Overview

Road creation follows a six-phase pipeline. Each phase builds on the outputs of the previous one. The first three phases are mandatory; the remaining three are optional enhancements.

```
Phase 1: Horizontal Alignment    (2D plan, Z=0)
Phase 2: Vertical Alignment      (profile space, X=chainage, Y=elevation x10)
Phase 3: 3D Route Assembly        (merge horizontal + vertical into 3D centerline)
Phase 4: 3D Road Surface          (sweep cross-section profiles along 3D route)
Phase 5: Terrain & Slopes         (longitudinal profile, contours, earthworks)
Phase 6: Accessories & Landscape  (guardrails, barriers, poles, trees, markings)
```

Each phase produces named geometry on a structured layer hierarchy rooted at `RoadCreator::Road_N`. Road names are auto-assigned (`Road_1`, `Road_2`, ...) and propagated through every subsequent step.

---

## Phase 1: Horizontal Alignment

**Goal:** Define the road's path in plan view (2D, flattened to Z=0).

### Step 1.1 -- Create the Tangent Polygon

**Command:** `RC_TangentPolygon`

The tangent polygon is the skeleton of the horizontal alignment -- a polyline of straight segments (tangent lines) that define the road's planned direction changes.

**Inputs:**
- Click a sequence of points defining the alignment. Each vertex is a Point of Intersection (PI) where the road changes direction.
- Press Enter when done. All points are flattened to Z=0.

**Outputs:**
- Layer: `RoadCreator::Road_N::Tangent Polygon`
- Each consecutive pair of points becomes a named line: `Road_N 1`, `Road_N 2`, etc.
- A **ZU** (start-of-section) marker is placed at the first point:
  - Perpendicular tick line (33 model units)
  - Text label: `ZU km 0.000000`
  - Named point: `Road_N 0 R ZU 0.000000`
- Sublayers `Stationing`, `Stationing::Stationing Points`, and `Legend` are created and locked.

**Design notes:**
- Vertices are the PIs where transition curves will later be inserted.
- Segment count = vertex count - 1. A road with 4 PIs has 3 tangent segments.

```
        PI_2
        /\
       /  \        Tangent segments: Road_N 1, Road_N 2, Road_N 3
      /    \
PI_1 /      \ PI_3 ──────── PI_4
```

### Step 1.2 -- Insert Transition Curves

**Commands:** `RC_Clothoid` or `RC_CubicParabola`

Repeat this step for **every pair of adjacent tangent segments** that meet at an angle. Each command inserts a smooth transition between two tangent lines.

#### RC_Clothoid

**Inputs:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| First tangent | (select) | First tangent line segment |
| Second tangent | (select) | Second tangent line segment |
| L | 70 m | Transition curve length |
| R | 150 m | Circular arc radius |

**Geometry:**
- Computes the vertex (intersection of extended tangent lines)
- Deflection angle alpha between tangents
- Clothoid parameter: `A = sqrt(L * R)`
- Shift: `m = L^2/(24R) - L^4/(2688R^3)`
- Tangent projection: `Xs = L/2 - L^3/(240R^2)`
- Large tangent: `T = (R + m) * tan(alpha/2) + Xs`
- 26 sample points along the clothoid spiral, mirrored for right turns
- Two transition spirals connected by a circular arc

**Outputs:**
- Joined curve replacing original tangents: `Road_N 1 R_clothoid_R_150`
- Four stationing points with tick marks and labels:
  - **ZP** (transition start) at chainage from ZU
  - **PO** (arc start) at chainage + L
  - **OP** (arc end) at chainage + L + arc_length
  - **KP** (transition end) at chainage + 2L + arc_length
- Legend text: `R = 150m; clothoid; alpha = XX.XX deg; T = XXX.XXX m; Lk = 70 m`

#### RC_CubicParabola

Same workflow as clothoid with these differences:
- 16 sample points instead of 26
- Equation: `Y = corrFactor * X^3 / (6RL)` where `corrFactor = 1/cos(asin(L/(2R)))`
- **Constraint:** `L < 2R` (required for asin domain)
- Default L=60m, R=200m

**After Phase 1 is complete:** The tangent polygon segments and transition curves form a continuous horizontal alignment curve on the `Tangent Polygon` layer, with stationing points marking every geometric event.

---

## Phase 2: Vertical Alignment

**Goal:** Define the road's elevation profile (grade line).

### Step 2.1 -- Create the Elevation Polygon

**Command:** `RC_ElevationPolygon`

**Coordinate system:**
- X axis = chainage (horizontal distance along route, in meters)
- Y axis = elevation * 10 (10:1 vertical exaggeration, matching Czech road design convention)
- All geometry flattened to Z=0

**Inputs:**
- Select the associated road (auto-detected or manual)
- Click points defining grade change locations in profile space
- Press Enter when done

**Outputs:**
- Layer: `RoadCreator::Road_N::Grade Line`
- Each segment becomes a named line: `Road_N grade 1`, `Road_N grade 2`, etc.
- At each segment midpoint, two labels:
  - Distance: `d=XXX.XX m`
  - Slope: `s=X.XX %`
- Vertical reference lines at start and end points (83 units tall)
- Sublayer `Stationing` created and locked

### Step 2.2 -- Insert Vertical Curves

**Command:** `RC_ParabolicCurve`

Repeat for **every pair of adjacent grade segments**. Inserts a smooth parabolic curve at grade transitions (sag or crest).

**Inputs:**
| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| First grade segment | (select) | | First grade line |
| Second grade segment | (select) | | Second grade line |
| R | 2000 m | 50--100,000 | Vertical curve radius |

**Geometry:**
- Finds vertex V (intersection of extended grade lines)
- Grades: `s1`, `s2` in percent
- Tangent length: `t = R/200 * |s1 - s2|`
- Maximum ordinate: `ymax = t^2 / (2R)`
- Curve type: **sag** if s1 < s2 (concave up), **crest** if s1 > s2 (convex up)
- Parabolic offset at distance x from start: `y(x) = x^2 / (2R)`
- Sampled at 1-meter intervals, combined with tangent elevation

**Outputs:**
- Original grade segments are **deleted**
- Joined curve: `Road_N parabolic_R_2000`
- Important points on locked sublayer `Important Points`:
  - **ZZ** (parabola start) at vertex_chainage - t
  - **V** (vertex) at intersection
  - **KZ** (parabola end) at vertex_chainage + t
- Circle markers and vertical reference lines at ZZ and KZ
- Legend: `R = 2000m`, `t = XXX.XX m`, `Ymax = X.XXXX m`

**After Phase 2 is complete:** The grade line is a continuous curve in profile space representing the road's designed elevation change.

---

## Phase 3: 3D Route Assembly

**Goal:** Combine horizontal and vertical alignment into a single 3D centerline curve.

**Command:** `RC_Assemble3DRoute`

**Inputs:**
- Road selection (auto-detected from layers or manual)
- Horizontal alignment curve (from Tangent Polygon layer)
- Grade line curve (from Grade Line layer)
- Reference elevation (datum) -- auto-detected from Longitudinal Profile origin point, or manual entry

**Algorithm:**
1. Divide horizontal curve into stations at **2-meter intervals**
2. For each station:
   - Get (X, Y) position from horizontal curve at parameter t
   - Compute chainage = arc length from start to t
   - Sample grade line at chainage using vertical intersection
   - Convert profile Y to real elevation: `Z = datum + profile_Y / 10`
3. Create 3D point: `(X_horiz, Y_horiz, Z_elevation)`
4. Interpolate cubic spline through all 3D points
5. Project all stationing/important points (ZU, ZP, PO, OP, KP, ZZ, V, KZ) onto the 3D curve

**Outputs:**
- Layer: `RoadCreator::Road_N::3D Route`
- Main curve: `Road_N 3D_route`
- Sublayer `Important Points` (blue) -- all stationing points projected to 3D with " 3D" suffix
- Sublayer `Points` -- station points every 2m

**After Phase 3 is complete:** You have a 3D centerline curve that encodes both horizontal alignment and elevation. This is the rail along which the road surface will be swept.

---

## Phase 4: 3D Road Surface

**Goal:** Generate the complete 3D road surface by sweeping cross-section profiles along the 3D route.

### Road Categories

Czech standard road categories determine the cross-section dimensions:

| Category | Half-Width (m) | Total Width (m) | Type |
|----------|----------------|------------------|------|
| S 6.5 | 2.75 | 6.5 | Undivided |
| S 7.5 | 3.25 | 7.5 | Undivided |
| S 9.5 | 4.25 | 9.5 | Undivided |
| S 11.5 | 5.25 | 11.5 | Undivided |
| S 20.75 | 10.25 + 1.25 median | 20.75 | Divided |
| S 24.5 | 10.75 + 3.0 median | 24.5 | Divided |
| D 25.5 | 11.25 + 3.0 median | 25.5 | Motorway |
| D 27.5 | 12.0 + 3.5 median | 27.5 | Motorway |
| D 33.5 | 15.0 + 3.5 median | 33.5 | Motorway |
| D 4/8 | 8.0 + 4.0 median | varies | Motorway |

### Lane Widening (by curve radius)

| Radius Range | Widening (m) |
|-------------|-------------|
| >= 250 m | 0.00 |
| 200--249 m | 0.20 |
| 170--199 m | 0.25 |
| 141--169 m | 0.30 |
| 125--140 m | 0.35 |
| 110--124 m | 0.40 |
| < 110 m | 0.50 |

Category corrections: S 6.5 adds +0.30 m, S 7.5 adds +0.05 m.

### Crossfall (Banking)

Cross-section elevation is computed per station based on curve direction:

- **Straight sections:** default 2.5% crossfall (roof profile, slopes both sides)
- **Full curve sections:** default 4.0% superelevation (one-sided bank)
- **Transition zones (LM/PM):** blended between straight and curve values
- Blend factor: `M = 1 - |direction|` where direction is 0 (straight), +/-1 (full curve)
- Edge elevation: `Z_edge = ((M * (-p) + pmax * direction) / 100) * (half_width + widening)`

### Step 4.1 -- Generate Full Road

**Command:** `RC_Road3D`

**Inputs:**
| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| 3D route | (select) | | Route curve from Phase 3 |
| Road category | (choose) | S6.5--D4/8 | Determines cross-section width |
| Crossfall straight | 2.5% | 2.0--4.0% | Banking on straight segments |
| Crossfall curve | 4.0% | 4.0--20.0% | Banking in curves |
| Include verge | No | Yes/No | Add shoulder to profile |
| Verge equipment | -- | Guardrail/RoadPoles | Determines verge width if included |

**Algorithm:**
1. Auto-detect important points from `3D Route::Important Points`
2. For each station point along the 3D route:
   - Parse direction indicator (L/R/LM/PM) from point name
   - Compute local curve radius from route curvature
   - Look up widening from table
   - Generate cross-section polyline in local X-Z plane
   - Apply crossfall based on direction and blend factor
   - Add verge points (8% outward slope) if enabled
3. Orient each profile perpendicular to the route tangent at that station
4. Sweep all profiles along the 3D route (Rhino SweepOneRail)
5. Extract boundary edge curves (the two longest BREP edges)

**Outputs:**
- Layer: `RoadCreator::Road_N::3D Road`
- Road surface BREP: `Road_N 3D_model`
- Boundary curves: `Road_N boundary` (2 curves, left and right edges)
- Cross-section profiles on locked sublayer `Cross Sections`

### Step 4.2 -- Generate Road Section (optional)

**Command:** `RC_Road3DSection`

Same as `RC_Road3D` but operates on a subsection of the route. Additional inputs:
- Start point on route (Enter to use route start)
- End point on route (Enter to use route end)

Outputs go to `Road_N::Sections` layer.

---

## Phase 5: Terrain Integration (Optional)

### Step 5.1 -- Longitudinal Profile

**Command:** `RC_LongitudinalProfile`

Projects the route axis onto terrain to create a terrain profile in the same profile space as the grade line.

**Inputs:**
- Route axis curve (auto or manual)
- Terrain surface (mesh or NURBS)
- Elevation label spacing (default 20m)
- Profile origin point in drawing

**Outputs:**
- 2D terrain profile curve (X=chainage, Y=elevation*10)
- Baseline, km markers, elevation labels
- Projected important points
- Layer: `RoadCreator::Terrain::Longitudinal Profile`

### Step 5.2 -- Contour Lines

**Command:** `RC_ContourLines`

**Inputs:** Terrain surface or mesh

**Outputs:**
- Main contours at 10m intervals (yellow)
- Secondary contours at 5m intervals (dark gray, hidden)
- Minor contours at 2m intervals (dark purple)
- Layer: `RoadCreator::Terrain::Contours`

### Step 5.3 -- Slopes (Earthworks)

**Command:** `RC_Slopes`

**Inputs:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| Terrain | (select) | Surface or mesh |
| Left edge | (select) | Left road boundary curve |
| Right edge | (select) | Right road boundary curve |
| Fill slope | 1:1.75 | Fill (embankment) slope ratio |
| Cut slope | 1:1.75 | Cut slope ratio |
| Ditches | No | Include drainage ditches |
| Ditch depth | 0.4 m | If ditches enabled |
| Ditch width | 0.5 m | If ditches enabled |

**Outputs:** Slope BREP surfaces on `Slopes` layer, split against terrain for fill/cut regions.

### Step 5.4 -- Excel Export

**Command:** `RC_LongitudinalProfileExport`

Exports chainage, slope, grade polygon elevation, vertical curve parameters, grade line elevation, terrain elevation, and elevation difference to a formatted `.xlsx` file.

---

## Phase 6: Accessories & Landscape (Optional)

These commands operate on the road boundary curves or edge curves produced in Phase 4.

### Safety Equipment

| Command | Description | Key Parameters |
|---------|-------------|----------------|
| `RC_GuardrailSingle` | W-beam guardrail, one side | Edge curve + direction |
| `RC_GuardrailDouble` | W-beam guardrail, both sides | Center line curve |
| `RC_ConcreteBarrier` | Concrete posts + rods | Guide curve, post spacing (default 2.5m) |
| `RC_DeltaBlokBarrier` | Delta Blok barrier | Guide curve, variant (80/100S/100/120) |
| `RC_RoadPolesSingle` | Delineator poles, one side | Edge curve, adaptive spacing by curvature |
| `RC_RoadPolesDouble` | Delineator poles, both sides | Two edge curves |

### 2D Footprint / Markings

| Command | Description |
|---------|-------------|
| `RC_RoadFootprint` | 2D plan-view offset curves (lane lines, edge lines) from centerline |
| `RC_StoreProfile` | Store named offset profile JSON in document |
| `RC_StoreStyleSet` | Store named style set JSON in document |
| `RC_ListProfiles` | List stored profiles |
| `RC_ListStyleSets` | List stored style sets |

### Landscape

| Command | Description |
|---------|-------------|
| `RC_Forest` | Grid-based random tree placement on surface |
| `RC_ForestSilhouette` | 3-row tree silhouette along road edge |
| `RC_ForestSilhouetteCurve` | 3-row tree silhouette along arbitrary curve |
| `RC_Grass` | Grass patches along road edge |
| `RC_MagicCopy` | Interactive single-object random placement from tree database |
| `RC_MagicCopyMulti` | Interactive multi-template random placement |
| `RC_UtilityPoles` | Utility poles at 10m spacing along curve |
| `RC_TrafficSign` | Traffic sign from database, terrain-projected |

### Urban Elements

| Command | Description |
|---------|-------------|
| `RC_UrbanRoad` | 3D urban road from user-selected cross-sections |
| `RC_Sidewalk` | Raised curb + sidewalk surface (0.2m curb height) |
| `RC_CrossingArea` | Split road surface for pedestrian crossing |
| `RC_ZebraCrossing` | Alternating stripe crossing (0.5m stripes at 1m spacing) |
| `RC_Roundabout` | Full roundabout with configurable arms and fillets |
| `RC_SimpleIntersection` | Multi-arm intersection with fillet curves |

### Utility Commands

| Command | Description |
|---------|-------------|
| `RC_CopyAlongCurve` | Copy objects at regular spacing along curve |
| `RC_PerpendicularProfile` | Place profile perpendicular to curve |
| `RC_SplitCurveAtPoints` | Split curve at 1-2 picked points |
| `RC_Verge` | Standalone verge surface (8% slope) |
| `RC_Junction` | Junction surface connecting side road to main road |
| `RC_ResolveIntersection` | Intersection analysis via RookRoads HTTP API |

---

## Layer Hierarchy

Every road produces this layer tree. Layers marked **(locked)** are locked after creation.

```
RoadCreator/
  Road_1/
    Tangent Polygon/                 Red    -- tangent line segments
      Stationing/                    Red    -- tick marks, chainage labels (locked)
        Stationing Points/           Red    -- ZU, ZP, PO, OP, KP point objects (locked)
      Legend/                        Red    -- curve parameter annotations (locked)
    Grade Line/                             -- grade line segments
      Stationing/                           -- distance & slope labels (locked)
      Important Points/                     -- ZZ, V, KZ point objects (locked)
    3D Route/                               -- 3D centerline curve
      Important Points/              Blue   -- all stationing points in 3D
      Points/                               -- station points every 2m
    3D Road/                                -- road surface BREPs, boundary curves
      Cross Sections/                       -- profile polylines (locked)
    Sections/                               -- partial road sections
      Cross Sections/                       -- section profile polylines
    Verge/                                  -- verge surfaces
    Guardrail/                              -- guardrail geometry
    GuardrailProfile/                       -- guardrail W-beam profiles
    ConcreteBarrier/                        -- barrier posts
    ConcreteBarrierRods/                    -- barrier rod surfaces
    DeltaBlok/                     Purple   -- Delta Blok surfaces
    RoadPoles/                     Black    -- delineator pole copies
    Slopes/                        Brown    -- earthwork surfaces
    Sidewalk/                      Gray     -- curb + sidewalk surfaces
    Crossing/                      White    -- crossing fragments
    Roundabout/                    Black    -- roundabout geometry
    Intersection/                  Black    -- intersection geometry
    Trees/                         Green    -- placed tree copies
    Traffic Signs/                 Black    -- placed sign copies
  Terrain/
    Longitudinal Profile/                   -- terrain profile curves
    Contours/                               -- contour line curves
```

---

## Object Naming Convention

All road geometry follows a structured naming scheme defined in `RoadObjectNaming`:

### Tangent Segments
```
Road_1 1          -- first tangent segment
Road_1 2          -- second tangent segment
```

### Stationing Points
```
Road_1 1 R ZP 45.000000
  |    | | |  |
  |    | | |  +-- chainage from ZU (6 decimal places, meters)
  |    | | +-- type code (see table below)
  |    | +-- side: R=right/straight, L=left, P=right curve, LM/PM=transitions
  |    +-- segment index
  +-- road name
```

### Type Codes

| Code | Czech Name | English | Phase |
|------|-----------|---------|-------|
| ZU | Zacatek Useku | Section start | Horizontal |
| KU | Konec Useku | Section end | Horizontal |
| ZP | Zacatek Prechodnice | Transition start | Horizontal |
| PO | Pocatek Oblouku | Arc start | Horizontal |
| OP | Oblouku Prechodnice | Arc end | Horizontal |
| KP | Konec Prechodnice | Transition end | Horizontal |
| ZZ | Zacatek Zakruzovaciho | Parabola start | Vertical |
| V | Vrchol | Vertex | Vertical |
| KZ | Konec Zakruzovaciho | Parabola end | Vertical |

### Other Named Objects
```
Road_1 grade 1                -- first grade segment
Road_1 grade 2                -- second grade segment
Road_1 3D_route               -- 3D centerline curve
Road_1 3D_model               -- 3D road surface BREP
Road_1 3D_section             -- partial road surface BREP
Road_1 boundary               -- road edge curve
Road_1 section_boundary       -- section edge curve
Road_1 parabolic_R_2000       -- vertical parabolic curve
Road_1 1 R_clothoid_R_150     -- horizontal clothoid transition
Road_1 LongProfile 420        -- longitudinal profile datum (elevation embedded)
```

---

## Minimal End-to-End Example

A complete road with one horizontal curve and one vertical curve:

```
1.  RC_TangentPolygon           Click 3 points (PI_1, PI_2, PI_3) -> 2 tangent segments
2.  RC_Clothoid                 Select segments 1 & 2, L=70, R=150 -> transition curve
3.  RC_ElevationPolygon         Draw 3 points in profile space -> 2 grade segments
4.  RC_ParabolicCurve           Select grade 1 & 2, R=2000 -> vertical curve
5.  RC_LongitudinalProfile      Select terrain -> terrain profile (provides datum)
6.  RC_Assemble3DRoute          Auto-detect horizontal + grade + datum -> 3D route
7.  RC_Road3D                   Select route, category S 7.5, crossfall 2.5/4.0 -> 3D road
8.  RC_GuardrailSingle          Select left boundary, pick outward direction -> guardrail
9.  RC_RoadPolesSingle          Select right boundary, pick axis -> delineator poles
10. RC_Slopes                   Select terrain + both boundaries, 1:1.75 -> earthworks
```

**Result:** A complete 3D road model with horizontal clothoid transitions, vertical parabolic curves, road surface with superelevation, guardrails, delineator poles, and earthwork slopes.

---

## Agent Integration Notes

- **Scripted mode:** Key commands support `RunMode.Scripted` for automation. Pre-select geometry, then invoke with `_RC_CommandName _Enter param1 _Enter param2 _Enter`.
- **Auto-detection:** `RC_Assemble3DRoute` and `RC_Road3D` auto-detect inputs from the layer hierarchy. Agents should ensure layers are correctly populated before invoking.
- **Layer locking:** Stationing and legend layers are locked after creation. Agents must unlock before modifying.
- **External database:** If `RC_SetExternalDatabase` is configured, all database commands read/write the external `.3dm` file. Agents should verify the path is valid.
- **RookRoads API:** `RC_ResolveIntersection` calls an HTTP API. The RookRoads service must be running for intersection analysis.
- **Profile/style storage:** `RC_StoreProfile` and `RC_StoreStyleSet` accept minified JSON on a single line. Agents can use these to configure footprint generation before calling `RC_RoadFootprint`.
