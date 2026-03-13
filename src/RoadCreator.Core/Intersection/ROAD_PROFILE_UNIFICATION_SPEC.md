# Road Profile Unification Spec

## Goal

Define one canonical road-profile model for the RoadCreator suite so that:

- 2D footprint generation
- 3D road generation
- intersection analysis and realization
- future network/agent workflows

all consume the same road-domain input contract.

## Problem

The codebase currently has multiple profile idioms:

- `OffsetProfile` for 2D plan offsets and styles
- native intersection profile JSON in `knowledge/roads/profiles/*.json`
- numeric/category prompts in older commands such as `RC_Road3D`

These overlap conceptually but are not the same contract. That creates:

- duplicated logic
- command input drift
- inconsistent boundary semantics
- weak interoperability between commands

## Canonical Model

Introduce a new core contract:

- `RoadProfileDefinition`

This becomes the single source of truth for road profile semantics.

It should live in `RoadCreator.Core`.

## Required Capabilities

`RoadProfileDefinition` must represent:

- profile metadata
  - `name`
  - `units`
  - `description`
  - `symmetric`
  - tags/source metadata

- geometric features
  - `id`
  - `type`
  - `offset`
  - `width`
  - `bilateral`
  - optional `baseline`
  - optional notes/labels

- boundary semantics
  - which feature is the carriageway surface boundary
  - which feature is curb face
  - which feature is explicit outer envelope / ROW
  - whether a feature is eligible for intersection topology

- cross-section behavior
  - divided vs undivided behavior
  - median semantics
  - crossfall defaults
  - verge policy/defaults

- side resolution
  - symmetric profiles
  - unilateral features
  - explicit asymmetric-side resolution rules

## Feature Taxonomy

Feature `type` should be semantic, not presentation-oriented.

Initial types should include:

- `centerline_reference`
- `carriageway_edge`
- `edge_of_pavement`
- `curb_face`
- `bike_lane_inner`
- `bike_lane_outer`
- `sidewalk_inner`
- `sidewalk_outer`
- `median_edge`
- `row`
- `ditch`
- `custom`

Command logic must depend on semantic type, not layer naming.

## Boundary Rules

Different consumers need different boundary interpretations.

- footprint tools may want all visible semantic offsets
- 3D road tools may want the carriageway surface boundary
- intersections may need:
  - surface boundary
  - curb-return-driving boundary
  - explicit outer envelope for constraint checks

The canonical model must therefore support boundary-role queries, not only raw offsets.

## Storage and Resolution

The canonical profile should support these resolution modes:

1. named profile reference
2. inline JSON profile definition
3. document-stored profile
4. derived adapter from legacy inputs

Named profile resolution should not live only in native code.

## Command Input Contract

Future commands should prefer:

1. geometry selection
2. `profileRef` or `profileDefinition`
3. explicit side/asymmetry resolution if needed
4. small optional overrides

They should avoid making raw widths/slopes/category codes the primary input unless the command is explicitly an advanced/manual tool.

Examples:

- intersection:
  - `centerlineA`
  - `centerlineB`
  - `profileA`
  - `profileB`
  - candidate selection
  - optional overrides

- road generation:
  - route
  - `profile`
  - optional crossfall/verge overrides

## Existing System Mapping

Current systems should map into `RoadProfileDefinition` as follows:

- native intersection profile JSON:
  - direct source model

- `OffsetProfile`:
  - derived plan-view projection of selected semantic features

- `RoadCategory` + crossfall/verge prompts:
  - temporary adapter into a generated `RoadProfileDefinition`

## Migration Plan

Phase 1:

- add `RoadProfileDefinition` to `RoadCreator.Core`
- add adapter from native intersection profile JSON
- keep existing commands working

Phase 2:

- update intersection analysis to consume `RoadProfileDefinition`
- move profile parsing/loading out of native-only code

Phase 3:

- update `RC_RoadFootprint` to accept `RoadProfileDefinition` directly or via adapter
- update `RC_Road3D` to use named profiles first, numeric prompts second

Phase 4:

- standardize future command signatures around profile references and overrides

## Non-Goals

This spec does not define:

- final JSON schema details
- style/layer schema details
- packaging or repo layout
- UI flow details for every command

It defines the canonical direction for road-profile semantics and command inputs.
