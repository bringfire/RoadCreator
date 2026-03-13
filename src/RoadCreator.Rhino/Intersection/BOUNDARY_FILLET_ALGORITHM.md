# Boundary-Based Intersection Fillet Algorithm

## Problem

The intersection fallback path already produces one valid closed sharp boundary curve that can be surfaced.
The direct path must replace the four inner arm-mouth sharp corners on that one boundary with deterministic
curb-return fillets, without re-inferring topology from the noisy pre-surface curve pile.

## Single Source of Truth

Use the preserved closed sharp boundary curve as the only authoritative geometry source for direct filleting.

Do not derive fillet inputs from:

- anonymous split curves
- approach patch fragments
- candidate scoring across unrelated region pieces

## Deterministic Construction

1. Start with the closed sharp boundary curve that would create the fallback surface.
2. Analyze that curve in its own parameter space.
3. Detect the four inner arm-mouth corners from intrinsic curve structure.
   - walk the curve in parameter order
   - compute local turning
   - keep strong local turning peaks that are inward relative to the boundary centroid
4. Detect the remaining ordered boundary break points from the same curve analysis using different
   classification criteria.
5. Choose a geometrically stable seam break. Do not choose the seam from the current raw parameter start.
6. Duplicate the boundary and reseam it at the chosen seam break.
   - `Curve.DuplicateCurve()`
   - `Curve.ChangeClosedCurveSeam(seamT)` and check the returned `bool`
7. Remap every break point back onto the reseamed curve with geometric lookup.
   - use `Curve.ClosestPoint(...)`
   - do not try to arithmetically shift the original parameters
8. Split the reseamed curve at the ordered interior break parameters.
   - `Curve.Split(IEnumerable<double> parameters)`
9. Persist the resulting pieces as tracked document curves.
   - `doc.Objects.AddCurve(piece, attrs)`
   - `attrs.SetUserString(...)`
10. For each inner corner, find the two split pieces whose endpoints touch that corner point.
    Those two touching pieces are the fillet pair for that corner.
11. Fillet each corner pair with explicit keep-side pick points.
    - `Curve.CreateFilletCurves(...)`
    - picks must indicate the side of each piece to keep, not the side to remove
12. Parse the fillet result defensively.
    - handle `null`
    - handle short or incomplete result arrays
13. Rebuild the final boundary in the original traversal order by substituting trimmed pieces and arcs into
    the ordered piece walk.
14. Join the rebuilt pieces into one closed loop.
    - `Curve.JoinCurves(...)`
15. Validate the final result semantically, not just syntactically.

## Validation Requirements

After each corner replacement, and again after full assembly, verify:

- one intended closed loop exists
- no self-intersections are introduced
- area remains within the expected range
- the loop still contains the expected central seed region
- the fillet arc lies on the intended interior side of the original sharp corner

`Curve.CreateFilletCurves(...)` can return geometrically valid but semantically wrong trims when pick points are
on the wrong side. Validation is therefore part of the algorithm, not optional cleanup.

## Tracking Requirements

When boundary pieces are materialized into Rhino, each should carry stable metadata at minimum:

- source boundary id
- piece index in traversal order
- piece parameter interval on the reseamed parent curve
- start break index
- end break index
- semantic role if known

This makes the direct path inspectable and recoverable when a local fillet step fails.

## Current Insight

The hard problem was not fillet construction itself. It was identifying the correct local branches.
Now that the four inner corners can be recovered directly from the one closed boundary curve, corner pairing is
topological: after splitting, exactly two pieces touch each inner corner point, and those two touching ends are
the fillet inputs.
