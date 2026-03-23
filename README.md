# RoadCreator

> **Credit where credit is due.** This project is a conversion of the original [RoadCreator for Rhino](https://www.food4rhino.com/en/app/roadcreator-rhino) scripts by **adaorlicky_1**. All design concepts, algorithms, and road-engineering workflows originate from their work. We are grateful they shared the code and permitted us to create this plugin version. **This is not our original work** — we simply restructured and ported the existing VBScript macros into a compiled C# plugin.

A Rhino 8 plugin for road design, providing 50 commands (`RC_*`) that cover the full workflow from horizontal/vertical alignment through 3D road surfaces, terrain analysis, accessories, and landscaping.

Built as a C# (.NET 7.0) RhinoCommon plugin. Converted from the original VBScript macro collection into a structured, testable codebase with a shared computation core (`RoadCreator.Core`) and a Rhino-specific layer (`RoadCreator.Rhino`).

## Features

- **Alignment** — Tangent polygons, clothoid/cubic-parabola transitions, parabolic vertical curves, 3D route assembly
- **Road surfaces** — Full-length and sectioned 3D road generation via cross-section sweeps, verges, junctions
- **Footprint** — 2D plan-view offset curves with stored profiles and style sets
- **Terrain** — Longitudinal profiles, contour lines, Excel export
- **Slopes** — Embankment/cut slopes with optional drainage ditches
- **Nature** — Forest placement (grid, silhouette, curve-based), grass, tree/utility-pole databases, magic copy
- **Urban** — Sidewalks, crossings, zebra stripes, roundabouts, simple intersections, urban road sweeps
- **Accessories** — Guardrails (single/double), concrete barriers, Delta Blok barriers, road poles, copy-along-curve, perpendicular profiles, curve splitting
- **Signs** — Traffic sign placement from database
- **Database** — Object and tree template storage with companion base points, external `.3dm` database support

## Architecture

```
RoadCreator.Core/        Pure C# computation library (no Rhino dependency)
  ├── Alignment/         Clothoid, cubic parabola, parabolic curve, route assembly
  ├── CrossSection/      Road categories, profile computation
  ├── Intersection/      Intersection analysis and realization
  └── ...

RoadCreator.Rhino/       Rhino 8 plugin (RhinoCommon)
  ├── Commands/          50 RC_* commands organized by category
  ├── Database/          External .3dm database support
  ├── Layers/            Layer management and naming conventions
  └── Plugin/            Plugin lifecycle, settings persistence
```

## Installation

Copy the built DLLs to your Rhino 8 plugins folder:

```
%APPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\RookRC\
```

Required files:
- `RoadCreator.Core.dll`
- `RoadCreator.Rhino.rhp`

## External Database

RoadCreator can read templates (trees, signs, poles, accessories) from a shared `.3dm` file instead of per-document layers. This lets multiple projects share one template library.

- `RC_SetExternalDatabase` — Configure or clear the external database path
- Templates are stored on named layers (`Tree Database`, `RC_Database`) with companion base points
- Block instances are fully supported — definitions are transferred automatically when placing from the external file

## Command Reference

See [COMMANDS.md](COMMANDS.md) for a full reference of all 50 commands.

## Building

```bash
dotnet build src/RoadCreator.Core/RoadCreator.Core.csproj
dotnet build src/RoadCreator.Rhino/RoadCreator.Rhino.csproj
```

## Testing

```bash
dotnet test
```

772 tests covering alignment math, cross-section computation, naming conventions, and more.

## Related

- [RookRoads](https://github.com/aryan/RookRoads) — HTTP adapter exposing RoadCreator.Core as a REST API for external tools and AI agents
