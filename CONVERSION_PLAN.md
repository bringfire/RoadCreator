# RoadCreator: RhinoScript to C# Plugin Conversion Plan

## Solution Architecture

```
RoadCreator.sln
├── src/
│   ├── RoadCreator.Core/                    # Pure C# library, no Rhino dependency
│   │   ├── Alignment/
│   │   │   ├── HorizontalAlignment.cs       # Tangent polygon model
│   │   │   ├── ClothoidTransition.cs        # Klotoida 5-term parametric
│   │   │   ├── CubicParabolaTransition.cs   # Cubic parabola
│   │   │   ├── CircularArc.cs               # Circular arc between transitions
│   │   │   ├── VerticalAlignment.cs         # Elevation polygon model
│   │   │   ├── ParabolicCurve.cs            # Parabolic vertical curves
│   │   │   └── Route3DAssembler.cs          # Combine H+V into 3D route
│   │   ├── CrossSection/
│   │   │   ├── CrossSectionProfile.cs       # Generic profile definition
│   │   │   ├── RoadCategoryDatabase.cs      # Czech S/D categories
│   │   │   ├── LaneStrip.cs                 # Lane with crossfall
│   │   │   ├── Verge.cs                     # Road verge
│   │   │   └── WideningTable.cs             # Radius-dependent widening
│   │   ├── Terrain/
│   │   │   ├── ContourGenerator.cs
│   │   │   ├── LongitudinalProfile.cs
│   │   │   └── SlopeCalculator.cs
│   │   ├── Intersection/
│   │   │   ├── Roundabout.cs
│   │   │   ├── SimpleIntersection.cs
│   │   │   └── RoadJunction.cs
│   │   ├── Accessories/
│   │   │   ├── GuardrailProfile.cs
│   │   │   ├── ConcreteBarrier.cs
│   │   │   ├── DeltaBlokBarrier.cs
│   │   │   ├── RoadPoleSpacing.cs
│   │   │   └── PedestrianCrossing.cs
│   │   ├── Vegetation/
│   │   │   ├── ForestPlacement.cs
│   │   │   ├── ForestSilhouette.cs
│   │   │   ├── GrassPlacement.cs
│   │   │   └── ObjectScatterer.cs
│   │   ├── Standards/
│   │   │   ├── CzechRoadStandards.cs
│   │   │   └── TrafficSignRegistry.cs
│   │   ├── Export/
│   │   │   └── ProfileExcelExporter.cs
│   │   ├── Math/
│   │   │   ├── AngleUtils.cs
│   │   │   ├── GeometryMath.cs
│   │   │   └── CurveUtils.cs
│   │   └── Localization/
│   │       ├── Strings.resx                 # Czech (default)
│   │       └── Strings.en.resx              # English
│   │
│   ├── RoadCreator.Rhino/                   # RhinoCommon plugin
│   │   ├── Plugin/
│   │   │   └── RoadCreatorPlugin.cs
│   │   ├── Commands/
│   │   │   ├── Alignment/                   # 7 commands
│   │   │   ├── Road/                        # 6 commands
│   │   │   ├── Terrain/                     # 3 commands
│   │   │   ├── Intersection/                # 4 commands
│   │   │   ├── Urban/                       # 2 commands
│   │   │   ├── Accessories/                 # 7 commands
│   │   │   ├── Vegetation/                  # 4 commands
│   │   │   ├── Signs/                       # 1 parameterized command
│   │   │   ├── Database/                    # 2 commands
│   │   │   └── Export/                      # 1 command
│   │   ├── Layers/
│   │   │   ├── LayerManager.cs
│   │   │   └── LayerScheme.cs
│   │   ├── Terrain/
│   │   │   ├── ITerrain.cs
│   │   │   ├── SurfaceTerrainAdapter.cs
│   │   │   ├── MeshTerrainAdapter.cs
│   │   │   └── PerpendicularDetector.cs
│   │   ├── Database/
│   │   │   ├── ComponentDatabase.cs
│   │   │   └── ComponentLibrary.cs
│   │   └── UI/
│   │       └── Panels/
│   │
│   └── RoadCreator.Grasshopper/             # Grasshopper components
│       ├── RoadCreatorGHInfo.cs
│       ├── Icons/
│       ├── Parameters/
│       │   ├── Param_Alignment.cs
│       │   ├── Param_CrossSection.cs
│       │   └── Param_RoadCategory.cs
│       └── Components/
│           ├── Alignment/                   # 5 components
│           ├── Road/                        # 4 components
│           ├── Terrain/                     # 3 components
│           ├── Intersection/                # 2 components
│           ├── Accessories/                 # 3 components
│           └── Vegetation/                  # 3 components
│
├── tests/
│   ├── RoadCreator.Core.Tests/
│   └── RoadCreator.Rhino.Tests/
│
└── docs/
    ├── migration-notes.md
    └── algorithm-reference.md
```

## Core Data Models

### Horizontal Alignment Chain
```
HorizontalAlignment
  ├── List<AlignmentElement>         # Abstract base
  │   ├── TangentSegment             # Line from tangent polygon
  │   ├── ClothoidTransition         # L, R, A=L*R parameters
  │   ├── CubicParabolaTransition
  │   └── CircularArc                # R, start/end angles
  ├── StationingPoints               # ZP, PO, OP, KP, ZU, KU
  └── Metadata
```

### Vertical Alignment
```
VerticalAlignment
  ├── List<GradeSegment>             # slope %, start/end chainage
  ├── List<ParabolicCurve>           # R, t = R/200 * |s1-s2|
  ├── ImportantPoints                # ZZ, KZ, V
  └── ReferencePlane
```

### Cross Section
```
CrossSectionProfile
  ├── RoadCategory                   # S65..D48
  ├── HalfWidth                      # Category lookup
  ├── MedianWidth                    # 0 or 1.25-4.0
  ├── CrossSlope                     # 2-4% normal, 4-20% super
  ├── Widening                       # Radius-dependent 0-0.5m
  ├── VergeWidth, VergeSlope
  └── Points[]                       # Polyline points
```

### Widening Table (Czech ČSN)
```
R < 110       -> 0.5m (+ adjustments by category)
110 <= R < 125 -> 0.4m
125 <= R < 141 -> 0.35m
141 <= R < 170 -> 0.30m
170 <= R < 200 -> 0.25m
200 <= R < 250 -> 0.2m
R >= 250       -> 0m
```

### Terrain Interface
```
ITerrain
  ├── ProjectPoint(point, direction) -> point?
  ├── ProjectCurve(curve, direction) -> curve?
  ├── GetIntersectionCurves(planeZ) -> curves[]
  └── ClosestPoint(point) -> point?

SurfaceTerrainAdapter : ITerrain     # wraps Brep/Surface
MeshTerrainAdapter : ITerrain        # wraps Mesh
```

## Key Simplifications in Conversion

1. **Trig workaround eliminated**: VBScript cosin()/sinus() used line-rotate-read-back trick.
   C# simply uses Math.Sin/Cos.

2. **Perpendicular detection simplified**: VBScript circle-extrude-intersect pattern replaced
   by Curve.TangentAt(t) in RhinoCommon.

3. **14 traffic sign scripts -> 1 parameterized command**: All identical pattern.

4. **Surface/Mesh dual scripts merged**: ITerrain interface unifies 10+ duplicate scripts.

5. **Contour generation**: VBScript loop-and-intersect replaced by
   Brep.CreateContourCurves() / Mesh.CreateContourCurves().

## Conversion Phases

| Phase | Scope | Duration | Dependencies |
|-------|-------|----------|--------------|
| 0 - Foundation | Solution skeleton, Core.Math, LayerManager, ITerrain, plugin shell | 2 weeks | None |
| 1 - H-Alignment | Tangent polygon, clothoid, cubic parabola | 3 weeks | Phase 0 |
| 2 - V-Alignment | Elevation polygon, grade line, parabolic curve, 3D route | 3 weeks | Phase 1 |
| 3 - Terrain | Contour lines, longitudinal profile | 2 weeks | Phase 0 |
| 4 - Cross Sections | Road categories, profiles, 3D road model | 3 weeks | Phase 2, 3 |
| 5 - Slopes | Embankment slopes, verges | 2 weeks | Phase 4 |
| 6 - Intersections | Roundabout, simple intersection, junctions | 2 weeks | Phase 4 |
| 7 - Urban | Sidewalks, crossings, urban roads | 1 week | Phase 4 |
| 8 - Accessories | Guardrails, barriers, poles, utilities | 2 weeks | Phase 1 |
| 9 - Vegetation | Forest, grass, magic copy, database system | 2 weeks | Phase 0 |
| 10 - Signs | Traffic sign placement (single command) | 1 week | Phase 9 |
| 11 - Export | Excel export (ClosedXML/EPPlus) | 1 week | Phase 3 |
| 12 - Grasshopper | All GH components | 3 weeks | All Core done |
| 13 - Polish | UI panel, Yak package, docs, testing | 2 weeks | All |

**Total: ~28 weeks (7 months) with one developer**

## Shared Utilities to Extract

| VBScript Function | Used In | C# Replacement |
|---|---|---|
| Closestpoint() | 6+ files | CurveUtils.ClosestPointOnCurve() |
| DoSplit() / Split() | 5+ files | RhinoGeometryOps.SplitBrep() |
| Joinmore() | 3+ files | RhinoGeometryOps.JoinBreps() |
| LevaPrava() | 1 file, core concept | GeometryMath.DetermineSide() |
| cosin() / sinus() | 2 files | Math.Cos / Math.Sin |
| Layer setup pattern | All files | LayerManager.EnsureLayer() |
| Perpendicular detection | 8+ files | PerpendicularDetector (uses Curve.TangentAt) |
| Database enumeration | 5+ files | ComponentDatabase.GetObjectNames() |

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Sweep1 behavior differences | High | Extensive visual comparison testing |
| Rhino.Command dependencies (DoSplit) | High | Use RhinoCommon BrepFace.Split() with edge-case testing |
| Clothoid numerical precision | Medium | Unit tests with known reference values |
| RC2 layer hierarchy assumptions | Medium | Document exact naming convention, validate on load |
| Backward compat with existing .3dm | Medium | Migration command for DATABAZE layer |

## Grasshopper Component Tabs

| Category | Components |
|----------|------------|
| 1 - Alignment | TangentPolygon, Clothoid, CubicParabola, CircularArc |
| 2 - Vertical | ElevationPolygon, GradeLine, ParabolicCurve |
| 3 - Route | Assemble3DRoute, LongitudinalProfile |
| 4 - Cross Section | RoadCategory, CrossSectionProfile, LaneStrip |
| 5 - Road 3D | Road3DFromProfiles, VergeGeneration, RoadSurface |
| 6 - Terrain | ContourGeneration, SlopeCalculation, TerrainProjection |
| 7 - Intersection | Roundabout, SimpleIntersection |
| 8 - Urban | Sidewalk, PedestrianCrossing |
| 9 - Accessories | Guardrail, Barrier, RoadPoles, CopyAlongCurve |
| 10 - Vegetation | Forest, Grass, ObjectScatter |
