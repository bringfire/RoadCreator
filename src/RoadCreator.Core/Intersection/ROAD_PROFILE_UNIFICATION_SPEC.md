# Road Profile Unification Spec

## Goal

Define one canonical road-profile model for the RoadCreator suite so that:

- 2D footprint generation
- 3D road generation
- intersection analysis and realization
- future network and agent workflows

all consume the same road-domain input contract.

This spec is intentionally grounded in the current live system, not an idealized rewrite.

## Current State

The codebase currently has three profile idioms:

1. `OffsetProfile` in `RoadCreator.Core.Footprint`
   - lightweight plan-view offset geometry
   - semantic `Role`
   - style indirection via `StyleRef`
   - optional non-centerline baseline

2. Native intersection profile JSON in `Rook/knowledge/roads/profiles/*.json`
   - loaded by `RookNative` in `IntersectionHandler.cpp`
   - carries semantic feature types like `carriageway_edge`, `bike_lane_outer`, `row`
   - supports symmetric and asymmetric cases
   - currently drives:
     - candidate clearance checks
     - boundary-target selection via `carriagewayEdgeOffset`

3. Numeric and category prompts in older commands such as `RC_Road3D`
   - `RoadCategory`
   - crossfall straight
   - crossfall curve
   - verge toggles and derived verge width

These overlap conceptually but are not the same contract.

## Important Constraint

The intersection routine is now live and working.

That means unification must preserve this near-term runtime shape:

- `RookNative` still accepts `profileA` and `profileB` by name
- native analysis still resolves profile JSON from `knowledge/roads/profiles`
- `IntersectionRealizationRequest.SourceRoads` currently carries only:
  - `profileName`
  - `carriagewayEdgeOffset`
  - selected parameter and tangent
- `RoadCreator.Rhino` realization currently uses that offset as the boundary-driving input

So the immediate objective is not "replace everything." It is:

- introduce a canonical schema in `RoadCreator.Core`
- add adapters around the existing live path
- widen the intersection seam gradually

## Canonical Model

Introduce a new core contract:

- `RoadProfileDefinition`

This becomes the single source of truth for road profile semantics.

It should live in `RoadCreator.Core`.

## Representation Principle

The canonical schema should be machine-first, especially LLM-first.

That means the primary representation should optimize for:

- low token cost
- stable repeated structure
- low ambiguity
- compact semantic density
- deterministic ordering

It should not optimize primarily for:

- explanatory prose inside the payload
- human-friendly redundancy
- presentation-oriented naming
- mixed concerns like display labels and debugging notes in the core object

Human legibility should be handled by a second layer:

- viewer
- inspector
- formatter
- docs/examples

The canonical payload should be the thing an agent reasons over directly.

## Canonical Shape

`RoadProfileDefinition` should represent the necessary road semantics, but that does not mean every concern belongs in the canonical payload.

The canonical payload should carry:

- metadata
  - `name`
  - `schemaVersion`
  - `units`
  - optional compact provenance/reference ids when needed

- orientation and symmetry
  - `symmetric`
  - explicit side-sensitive feature behavior
  - support for unilateral features without baking the chosen side into the base definition

- geometric features
  - `id`
  - `type`
  - `offset`
  - `width`
  - `bilateral`
  - optional `baseline`
  - optional label/notes

- boundary semantics
  - the feature(s) that define the carriageway surface boundary
  - the feature(s) that define curb face
  - the feature(s) that define edge of pavement
  - the feature(s) that define outer envelope / ROW
  - whether a feature is eligible for intersection topology

- cross-section behavior
  - road class or category metadata
  - divided vs undivided behavior
  - median semantics
  - crossfall defaults
  - verge policy/defaults

- rendering hooks
  - only if strictly needed for derived projection

The following should be treated as optional sidecar/view concerns, not required canonical fields:

- long descriptions
- narrative notes
- verbose labels
- UI-specific grouping text
- documentation-oriented examples
- viewer formatting hints

## Feature Taxonomy

Feature `type` must be semantic, not presentation-oriented.

It should also use a fixed vocabulary so agents see the same tokens repeatedly.

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

Command logic should depend on semantic type or boundary role, not on layer names or ad hoc prompts.

## Boundary Roles

The canonical model must support explicit boundary-role queries.

At minimum:

- `carriageway_surface`
- `curb_return_driver`
- `edge_of_pavement`
- `outer_envelope`
- `intersection_topology_candidate`

This matters because different consumers need different boundaries:

- footprint tools may want every visible semantic offset
- 3D road tools may want the carriageway surface boundary plus slope policy
- intersections need:
  - a realizable surface boundary
  - a curb-return-driving boundary
  - an outer envelope for constraint checks

Where a role can be derived deterministically from feature type, the compact machine form should prefer derivation over repetition.

## Side Resolution

The canonical base definition should remain side-agnostic where possible.

Asymmetric profiles should be resolved in two stages:

1. `RoadProfileDefinition`
   - declares the existence of unilateral features
   - does not force a side choice

2. `ResolvedRoadProfile`
   - derived from a base definition plus side selection
   - materializes the final signed offsets and effective boundary roles

This is necessary for cases like `collector_one_side_bike`, where the bike side is chosen at application time.

## Relationship To Existing Models

### `OffsetProfile`

`OffsetProfile` should become a derived plan-view projection of `RoadProfileDefinition`, not the canonical source.

What it remains good at:

- lightweight offset geometry
- document storage
- footprint rendering

What it lacks as the canonical model:

- width semantics
- side resolution
- boundary roles
- cross-section policy
- intersection-specific boundary selection

### Native intersection JSON

The current `knowledge/roads/profiles/*.json` should become one serialized form of `RoadProfileDefinition`.

However, the current JSON is still more human-authored than machine-optimal.

The target state should distinguish between:

1. canonical compact schema
2. human-authored/source-friendly schema
3. viewer-expanded schema

These may round-trip through the same core model, but they are not required to be identical on the wire.

Near term:

- keep the files where they are
- keep native loading working
- define schema ownership in `RoadCreator.Core`

Long term:

- parsing and validation should live in `RoadCreator.Core`
- native code should consume a validated summary or adapter, not own the schema definition

### `RoadCategory` and 3D prompts

`RoadCategory` plus crossfall and verge inputs should become a legacy adapter source for generating a `RoadProfileDefinition` or `ResolvedRoadProfile`.

That lets `RC_Road3D` move from:

- category-first
- numeric-prompt-first

to:

- profile-first
- overrides-second

without breaking older workflows immediately.

## Intersection Integration

The working intersection path gives us a practical seam.

### Current live behavior

`RookNative` currently does all of the following:

- loads `profileA` and `profileB` by name
- validates them
- computes `maxOffset`
- computes `carriagewayEdgeOffset`
- uses full envelope width for some clearance checks
- passes only `profileName` and `carriagewayEdgeOffset` into the realization request

`RoadCreator.Rhino` currently realizes from:

- the analyzed provisional geometry
- the preserved sharp boundary
- `carriagewayEdgeOffset` as the boundary-driving profile value

### Near-term target

Do not destabilize the working realization path.

Instead:

1. Add `RoadProfileDefinition` and `ResolvedRoadProfile` to `RoadCreator.Core`.
2. Add a parser and validator in `RoadCreator.Core` for the current JSON shape.
3. Add a compact profile summary/adapter type for intersection use.
4. Keep native analysis behavior the same initially, but align its data model to the new core schema.
5. Expand `IntersectionRealizationRequest.SourceRoads` only when `RoadCreator.Rhino` is ready to use richer profile semantics.

### Recommended profile summary for intersections

The next non-breaking step is a richer but still compact summary, for example:

- `profileName`
- `resolvedSide`
- `carriagewaySurfaceOffset`
- `curbReturnDriverOffset`
- `outerEnvelopeOffset`
- `intersectionEligibleFeatures`

That is enough to improve boundary-role correctness without forcing full profile parsing inside the realization layer on day one.

## Command Input Contract

Future commands should prefer:

1. geometry selection
2. `profileRef` or `profileDefinition`
3. explicit side/asymmetry resolution if needed
4. small optional overrides

They should avoid making raw widths, slopes, or category codes the primary input unless the command is explicitly advanced or manual.

Examples:

- intersection
  - `centerlineA`
  - `centerlineB`
  - `profileA`
  - `profileB`
  - candidate selector
  - optional side selection
  - optional boundary-role override

- road generation
  - route
  - `profile`
  - optional crossfall override
  - optional verge override

- footprint
  - centerline
  - `profile`
  - optional style-set override

## Storage and Resolution

The canonical profile should support these resolution modes:

1. named profile reference
2. inline JSON profile definition
3. document-stored profile
4. derived adapter from legacy inputs

Named profile resolution should not live only in native code.

Recommended resolution layering:

- `RoadCreator.Core`
  - schema types
  - validation
  - JSON parser/serializer
  - adapters

- `RoadCreator.Rhino`
  - document profile store and document-specific resolution

- `RookNative`
  - temporary file-based named-profile lookup while native owns the active loader

## Compactness Rules

The canonical machine form should follow these rules:

1. Prefer fixed arrays and deterministic ordering over repeated verbose object structure where practical.
2. Prefer controlled vocabularies over free-form text.
3. Keep keys stable and reasonably short, but not cryptic to the point of harming model reasoning.
4. Do not duplicate derivable values.
5. Separate heavy provenance, comments, labels, and display metadata into sidecar/view models.
6. Keep feature ordering deterministic so array position itself carries meaning when useful.
7. Make the intersection-facing summary even smaller than the canonical full profile when only boundary semantics are needed.

In practical terms:

- the full canonical profile can remain semantically named in code
- the serialized machine form may later adopt a more compact JSON shape
- the viewer/debug form can remain verbose and human-oriented

## First Compact JSON Shape

The first machine-native wire shape should be compact JSON.

Top-level keys:

- `s` = schema
- `n` = name
- `u` = units
- `sym` = symmetric
- `tw` = total width
- `f` = ordered feature tuples
- `lm` = optional layer map
- `tg` = optional tags
- `src` = optional compact provenance sidecar
- `x` = optional cross-section defaults sidecar

Feature tuple shape:

```json
[
  "feature-id",
  "feature-type",
  7.5,
  3.0,
  true,
  "cs|cr|it",
  "optional-baseline"
]
```

Tuple fields are:

1. stable feature id
2. semantic feature type
3. offset
4. width
5. bilateral
6. compact boundary-role code string
7. optional baseline

Boundary-role codes:

- `cs` = carriageway surface
- `cr` = curb-return driver
- `ep` = edge of pavement
- `oe` = outer envelope
- `it` = intersection topology candidate

Example:

```json
{
  "s": "roadcreator.road-profile.compact/v1",
  "n": "collector_one_side_bike",
  "u": "m",
  "sym": false,
  "tw": 15.1,
  "f": [
    ["carriageway_edge-0", "carriageway_edge", 3.8, 3.8, true,  "cs|cr|it"],
    ["bike_lane_inner-0",  "bike_lane_inner",  4.8, 1.0, false, ""],
    ["bike_lane_outer-0",  "bike_lane_outer",  7.8, 3.0, false, ""],
    ["row-0",              "row",             11.3, 3.5, false, "oe"]
  ]
}
```

This is the machine form.

The human viewer layer can expand this into:

- labels
- descriptions
- grouped feature tables
- diagrams
- provenance details

## Migration Plan

### Phase 1: Canonical schema in Core

- add `RoadProfileDefinition`
- add `ResolvedRoadProfile`
- add JSON serialization/deserialization in `RoadCreator.Core`
- add adapter from current `knowledge/roads/profiles/*.json`
- keep open the option for a second, more compact wire serialization once the core semantics stabilize

Acceptance:

- current road profile JSON can round-trip through the new schema
- no live intersection behavior changes yet

### Phase 2: Intersection summary alignment

- define a `RoadProfileSummary` or equivalent compact intersection-facing type
- update native profile loading to map through the new core schema conceptually
- preserve the current public `profileA` / `profileB` contract

Acceptance:

- native analysis still returns the same candidate and commit behavior
- the realization seam can ask for more than just `carriagewayEdgeOffset`

### Phase 3: RoadCreator-side consumers

- add `RoadProfileDefinition -> OffsetProfile` projection
- update `RC_RoadFootprint` to consume canonical profiles directly or through projection
- add `RoadCategory -> RoadProfileDefinition` adapter for `RC_Road3D`

Acceptance:

- footprint and 3D commands can both be driven from the same named profile concept

### Phase 4: Boundary-role integration in intersections

- replace hard-coded `carriagewayEdgeOffset` assumptions with explicit boundary-role selection
- allow intersection analysis and realization to choose:
  - carriageway surface boundary
  - curb-return driver
  - outer envelope

Acceptance:

- intersection correctness is no longer coupled to a single semantic feature name

### Phase 5: Future network workflows

- standardize command and agent inputs around `profileRef` plus small overrides
- reuse the same resolved-profile logic for corridor, junction, and network generation

## Non-Goals

This spec does not define:

- every final JSON field name
- style-set schema details
- repo packaging details
- every command UI flow
- the final viewer/inspector format for humans

It defines the canonical direction for road-profile semantics and the staged path that preserves the now-working intersection routine while unifying the rest of the system around it.
