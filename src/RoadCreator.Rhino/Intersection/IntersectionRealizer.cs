using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using global::Rhino;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using RoadCreator.Core.Intersection;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Intersections;

/// <summary>
/// Non-interactive realization of an already-analyzed intersection payload.
/// This is the Phase A seam: realize the surface and debug guides from explicit
/// analyzed geometry rather than from Rhino prompts.
/// </summary>
public sealed class IntersectionRealizer
{
    private static readonly Plane LeftPlane = Plane.WorldXY;
    private static readonly Plane RightPlane =
        new(Point3d.Origin, Vector3d.XAxis, -Vector3d.YAxis);

    public IntersectionRealizationResult Realize(
        RhinoDoc doc,
        IntersectionRealizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AnalysisToken))
            throw new ArgumentException("analysisToken is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TargetLayerRoot))
            throw new ArgumentException("targetLayerRoot is required", nameof(request));

        var polygon = BuildClosedPolyline(request.ProvisionalBoundary2D.CornerPoints);
        if (polygon == null || polygon.Count < 4)
            throw new InvalidOperationException("At least three provisional boundary points are required");

        var tolerance = doc.ModelAbsoluteTolerance;
        var layers = new LayerManager(doc);
        var preserveConstructionArtifacts = request.DebugArtifacts.PreserveConstructionArtifacts;
        var persistGuideArtifacts = preserveConstructionArtifacts && request.DebugArtifacts.WriteGuides;
        var persistApproachEdges = preserveConstructionArtifacts && request.DebugArtifacts.WriteApproachEdges;
        var persistApproachPatches = preserveConstructionArtifacts && request.DebugArtifacts.WriteApproachPatches;
        var persistDebugArtifacts = preserveConstructionArtifacts && request.DebugArtifacts.WriteDebugDots;

        var analysisLayerPath = request.TargetLayerRoot + "::Analysis";
        var boundaryLayerPath = request.TargetLayerRoot + "::Boundary";
        var curbReturnsLayerPath = request.TargetLayerRoot + "::Curb Returns";
        var surfaceLayerPath = request.TargetLayerRoot + "::Surface";
        var approachEdgesLayerPath = request.TargetLayerRoot + "::Approach Edges";
        var approachPatchesLayerPath = request.TargetLayerRoot + "::Approach Patches";
        var debugDotsLayerPath = request.TargetLayerRoot + "::Debug Dots";
        var debugCandidatesLayerPath = request.TargetLayerRoot + "::Debug Candidates";

        int analysisLayerIndex = persistGuideArtifacts
            ? layers.EnsureLayer(analysisLayerPath, Color.Black)
            : -1;
        int boundaryLayerIndex = layers.EnsureLayer(boundaryLayerPath, Color.Black);
        // Curb return arcs are always persisted — they are first-class intersection
        // artifacts consumed by downstream corner sidewalk generation, not debug guides.
        int curbReturnsLayerIndex = layers.EnsureLayer(curbReturnsLayerPath, Color.Black);
        int surfaceLayerIndex = layers.EnsureLayer(surfaceLayerPath, Color.Black);
        int approachEdgesLayerIndex = persistApproachEdges
            ? layers.EnsureLayer(approachEdgesLayerPath, Color.Black)
            : -1;
        int approachPatchesLayerIndex = persistApproachPatches
            ? layers.EnsureLayer(approachPatchesLayerPath, Color.Black)
            : -1;
        int debugDotsLayerIndex = persistDebugArtifacts
            ? layers.EnsureLayer(debugDotsLayerPath, Color.Black)
            : -1;
        int debugCandidatesLayerIndex = persistDebugArtifacts
            ? layers.EnsureLayer(debugCandidatesLayerPath, Color.Black)
            : -1;

        var result = new IntersectionRealizationResult
        {
            AnalysisToken = request.AnalysisToken,
            AnalysisStage = request.AnalysisStage,
            RealizationMode = string.IsNullOrWhiteSpace(request.RealizationMode)
                ? "planar_surface_with_analysis_guides"
                : request.RealizationMode,
            TargetLayerRoot = request.TargetLayerRoot,
            LayerPaths = new IntersectionLayerPaths
            {
                Analysis = persistGuideArtifacts ? analysisLayerPath : string.Empty,
                Boundary = boundaryLayerPath,
                CurbReturns = curbReturnsLayerPath,
                Surface = surfaceLayerPath,
                ApproachEdges = persistApproachEdges ? approachEdgesLayerPath : null,
                ApproachPatches = persistApproachPatches ? approachPatchesLayerPath : null,
            },
            Summaries = new IntersectionRealizationSummaries
            {
                Area = request.ProvisionalBoundary2D.Area,
            },
            UnresolvedConditions = CloneJson(request.UnresolvedConditions),
        };
        // CurbReturnArcLength is accumulated from actual built geometry in the
        // always-persisted curb return arc loop below — no pre-initialization needed.

        var namePrefix = string.IsNullOrWhiteSpace(request.NamePrefix)
            ? "IntersectionAnalysis2D"
            : request.NamePrefix;
        var createdObjectIds = new List<Guid>();
        var undoRecord = doc.BeginUndoRecord("Realize Intersection");
        var completed = false;
        var approachEdges = new List<ApproachEdgeSegment>();
        var approachPatches = new List<ApproachPatch>();

        doc.Views.RedrawEnabled = false;
        try
        {
            using var polygonCurve = new PolylineCurve(polygon);

            if (persistGuideArtifacts)
            {
                var polygonAttrs = BuildAttributes(
                    analysisLayerIndex,
                    $"{namePrefix}::provisional-boundary-polygon",
                    request.AnalysisToken,
                    "provisional_boundary_polygon",
                    "analysis_guides_2d");
                var polygonId = doc.Objects.AddCurve(polygonCurve, polygonAttrs);
                if (polygonId == Guid.Empty)
                    throw new InvalidOperationException("Failed to add provisional boundary polygon");

                createdObjectIds.Add(polygonId);
                result.Created.ProvisionalBoundaryPolygonId = polygonId.ToString();
                result.CreatedIds.Add(polygonId.ToString());

                for (int i = 0; i < request.AnalysisGeometry2D.BoundarySegments.Count; i++)
                {
                    var segment = request.AnalysisGeometry2D.BoundarySegments[i];
                    var start = ToRhinoPoint(segment.StartPoint);
                    var end = ToRhinoPoint(segment.EndPoint);
                    result.Summaries.BoundaryLineLength += start.DistanceTo(end);

                    using var lineCurve = new LineCurve(start, end);
                    var attrs = BuildAttributes(
                        boundaryLayerIndex,
                        $"{namePrefix}::boundary-segment-{i}",
                        request.AnalysisToken,
                        "boundary_segment",
                        "analysis_guides_2d");
                    attrs.SetUserString("rook_intersection_index", i.ToString());

                    var lineId = doc.Objects.AddCurve(lineCurve, attrs);
                    if (lineId == Guid.Empty)
                        throw new InvalidOperationException($"Failed to add boundary segment {i}");

                    createdObjectIds.Add(lineId);
                    result.Created.BoundarySegmentIds.Add(lineId.ToString());
                    result.CreatedIds.Add(lineId.ToString());
                }

            }

            // Curb return arcs are always persisted — they are first-class intersection
            // artifacts that drive downstream corner sidewalk generation. They are NOT
            // debug guides; they represent the intersection's semantic curb-return geometry.
            for (int i = 0; i < request.AnalysisGeometry2D.CurbReturnArcs.Count; i++)
            {
                var arc = request.AnalysisGeometry2D.CurbReturnArcs[i];
                if (!TryBuildArc(arc, out var rhinoArc, out var arcLength))
                    throw new InvalidOperationException($"Failed to build curb return arc {i}");

                using var arcCurve = new ArcCurve(rhinoArc);
                var attrs = BuildAttributes(
                    curbReturnsLayerIndex,
                    $"{namePrefix}::curb-return-{i}",
                    request.AnalysisToken,
                    "curb_return_arc",
                    "intersection_curb_return");
                attrs.SetUserString("rook_intersection_index", i.ToString());
                if (arc.CornerOrder.HasValue)
                    attrs.SetUserString("rook_intersection_corner", arc.CornerOrder.Value.ToString());
                ApplyPersistentIntersectionRecord(attrs, request, "curb_return_arc");

                var arcId = doc.Objects.AddCurve(arcCurve, attrs);
                if (arcId == Guid.Empty)
                    throw new InvalidOperationException($"Failed to add curb return arc {i}");

                createdObjectIds.Add(arcId);
                result.Created.CurbReturnArcIds.Add(arcId.ToString());
                result.CreatedIds.Add(arcId.ToString());
                result.Summaries.CurbReturnArcLength += arcLength;
            }

            approachEdges = BuildApproachEdgeSegments(doc, request, tolerance).ToList();
            approachPatches = BuildApproachPatches(approachEdges, request, tolerance).ToList();

            result.Summaries.ApproachEdgeLength = RoundTo(
                approachEdges.Sum(static edge => edge.Curve.GetLength()),
                4);
            result.Summaries.ApproachPatchArea = RoundTo(
                ComputeApproachPatchArea(approachPatches, polygonCurve, tolerance),
                4);

            if (persistApproachEdges)
            {
                foreach (var approachEdge in approachEdges)
                {
                    using var curveToAdd = approachEdge.Curve.DuplicateCurve();
                    if (curveToAdd == null)
                        continue;

                    var attrs = BuildAttributes(
                        approachEdgesLayerIndex,
                        $"{namePrefix}::{approachEdge.ObjectName}",
                        request.AnalysisToken,
                        "approach_edge_segment",
                        "approach_edges_2d");
                    attrs.SetUserString("rook_intersection_road", approachEdge.Road);
                    attrs.SetUserString("rook_intersection_side", approachEdge.Side);
                    attrs.SetUserString("rook_intersection_arm_direction", approachEdge.ArmDirection);

                    var approachEdgeId = doc.Objects.AddCurve(curveToAdd, attrs);
                    if (approachEdgeId == Guid.Empty)
                    {
                        throw new InvalidOperationException(
                            $"Failed to add approach edge for road {approachEdge.Road} {approachEdge.Side}");
                    }

                    createdObjectIds.Add(approachEdgeId);
                    result.Created.ApproachEdgeIds.Add(approachEdgeId.ToString());
                    result.CreatedIds.Add(approachEdgeId.ToString());
                }
            }

            SurfaceDebugInfo surfaceDebug = null!;
            try
            {
                using var realizedBoundary = BuildRealizedSurface(
                    polygonCurve,
                    request,
                    approachEdges,
                    approachPatches,
                    tolerance,
                    out var realizedSurface,
                    out surfaceDebug);
                using (realizedSurface)
                {
                    var boundaryAttrs = BuildAttributes(
                        boundaryLayerIndex,
                        $"{namePrefix}::boundary",
                        request.AnalysisToken,
                        "realized_boundary",
                        "planar_boundary_curve");
                    ApplyPersistentIntersectionRecord(boundaryAttrs, request, "realized_boundary");
                    var boundaryId = doc.Objects.AddCurve(realizedBoundary, boundaryAttrs);
                    if (boundaryId == Guid.Empty)
                        throw new InvalidOperationException("Failed to add realized boundary");

                    createdObjectIds.Add(boundaryId);
                    result.Created.RealizedBoundaryId = boundaryId.ToString();
                    result.CreatedIds.Add(boundaryId.ToString());
                    result.Summaries.BoundaryLineLength = RoundTo(realizedBoundary.GetLength(), 4);

                    var surfaceAttrs = BuildAttributes(
                        surfaceLayerIndex,
                        $"{namePrefix}::surface",
                        request.AnalysisToken,
                        "realized_surface",
                        "planar_surface_patch");
                    ApplyPersistentIntersectionRecord(surfaceAttrs, request, "realized_surface");
                    var surfaceId = doc.Objects.AddBrep(realizedSurface, surfaceAttrs);
                    if (surfaceId == Guid.Empty)
                        throw new InvalidOperationException("Failed to add realized surface");

                    createdObjectIds.Add(surfaceId);
                    result.Created.RealizedSurfaceId = surfaceId.ToString();
                    result.CreatedIds.Add(surfaceId.ToString());

                    using (var surfaceMassProps = AreaMassProperties.Compute(realizedSurface))
                    {
                        result.Summaries.SurfaceArea = surfaceMassProps != null
                            ? RoundTo(surfaceMassProps.Area, 4)
                            : RoundTo(request.ProvisionalBoundary2D.Area, 4);
                    }

                    if (persistDebugArtifacts)
                    {
                        foreach (var debugDot in BuildSurfaceDebugDots(
                            request,
                            surfaceDebug,
                            debugDotsLayerIndex,
                            namePrefix))
                        {
                            using (debugDot.Dot)
                            {
                                var dotId = doc.Objects.AddTextDot(debugDot.Dot, debugDot.Attributes);
                                if (dotId == Guid.Empty)
                                    throw new InvalidOperationException($"Failed to add debug dot {debugDot.Attributes.Name}");

                                createdObjectIds.Add(dotId);
                                result.CreatedIds.Add(dotId.ToString());
                            }
                        }

                        foreach (var debugCurve in BuildSurfaceDebugCurves(
                            request,
                            surfaceDebug,
                            debugCandidatesLayerIndex,
                            namePrefix))
                        {
                            using (debugCurve.Curve)
                            {
                                var curveId = doc.Objects.AddCurve(debugCurve.Curve, debugCurve.Attributes);
                                if (curveId == Guid.Empty)
                                    throw new InvalidOperationException($"Failed to add debug curve {debugCurve.Attributes.Name}");

                                createdObjectIds.Add(curveId);
                                result.CreatedIds.Add(curveId.ToString());
                            }
                        }
                    }

                    if (persistApproachPatches)
                    {
                        foreach (var approachPatch in approachPatches)
                        {
                            foreach (var patchCurve in TrimApproachPatchCurves(
                                approachPatch.BorderCurve,
                                realizedBoundary,
                                tolerance))
                            {
                                using (patchCurve)
                                {
                                    var patchBreps = Brep.CreatePlanarBreps(new[] { patchCurve }, tolerance);
                                    if (patchBreps == null || patchBreps.Length == 0)
                                        continue;

                                    foreach (var trimmedPatchBrep in patchBreps)
                                    {
                                        if (trimmedPatchBrep == null)
                                            continue;

                                        using (trimmedPatchBrep)
                                        {
                                            var attrs = BuildAttributes(
                                                approachPatchesLayerIndex,
                                                $"{namePrefix}::{approachPatch.ObjectName}",
                                                request.AnalysisToken,
                                                "approach_patch",
                                                "carriageway_patch_2d");
                                            attrs.SetUserString("rook_intersection_road", approachPatch.Road);
                                            attrs.SetUserString("rook_intersection_arm_direction", approachPatch.ArmDirection);

                                            var patchId = doc.Objects.AddBrep(trimmedPatchBrep, attrs);
                                            if (patchId == Guid.Empty)
                                            {
                                                throw new InvalidOperationException(
                                                    $"Failed to add approach patch for road {approachPatch.Road} {approachPatch.ArmDirection}");
                                            }

                                            createdObjectIds.Add(patchId);
                                            result.Created.ApproachPatchIds.Add(patchId.ToString());
                                            result.CreatedIds.Add(patchId.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }

                    result.FallbackFlags = ExtractFallbackFlags(request.UnresolvedConditions);
                    result.CreatedCount = result.CreatedIds.Count;
                    completed = true;
                }
            }
            finally
            {
                if (surfaceDebug != null)
                    DisposeSurfaceDebugArtifacts(surfaceDebug);
            }
        }
        catch
        {
            for (int i = createdObjectIds.Count - 1; i >= 0; i--)
                doc.Objects.Delete(createdObjectIds[i], quiet: true);
            throw;
        }
        finally
        {
            DisposeApproachEdges(approachEdges);
            DisposeApproachPatches(approachPatches);

            if (undoRecord != 0)
                doc.EndUndoRecord(undoRecord);
            doc.Views.RedrawEnabled = true;
            if (completed || createdObjectIds.Count > 0)
                doc.Views.Redraw();
        }

        return result;
    }

    private static Polyline? BuildClosedPolyline(IReadOnlyList<IntersectionPoint3> points)
    {
        if (points == null || points.Count < 3)
            return null;

        var polygon = new Polyline();
        foreach (var point in points)
            polygon.Add(ToRhinoPoint(point));

        if (!polygon[0].EpsilonEquals(polygon[polygon.Count - 1], 1e-6))
            polygon.Add(polygon[0]);

        return polygon;
    }

    private static Point3d ToRhinoPoint(IntersectionPoint3 point) =>
        new(point.X, point.Y, point.Z);

    private static Vector3d ToRhinoVector(IntersectionPoint3 point) =>
        new(point.X, point.Y, point.Z);

    private static ObjectAttributes BuildAttributes(
        int layerIndex,
        string name,
        string analysisToken,
        string artifactRole,
        string artifactKind)
    {
        var attrs = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = name,
        };
        attrs.SetUserString("rook_intersection_analysis_token", analysisToken);
        attrs.SetUserString("rook_intersection_artifact_role", artifactRole);
        attrs.SetUserString("rook_intersection_artifact_kind", artifactKind);
        return attrs;
    }

    private static void ApplyPersistentIntersectionRecord(
        ObjectAttributes attrs,
        IntersectionRealizationRequest request,
        string artifactRole)
    {
        var recordJson = JsonSerializer.Serialize(new
        {
            schema = "roadcreator.intersection-record/v1",
            analysisToken = request.AnalysisToken,
            analysisStage = request.AnalysisStage,
            targetLayerRoot = request.TargetLayerRoot,
            realizationMode = request.RealizationMode,
            candidateId = request.SelectedCandidate?.CandidateId,
            artifactRole,
            sourceRoads = request.SourceRoads.Select(static road => new
            {
                road = road.Road,
                centerlineId = road.CenterlineId,
                profileName = road.ProfileName,
                requiresSideSelection = road.RequiresSideSelection,
                resolvedSide = road.ResolvedSide,
            }),
        });

        attrs.SetUserString("rook_intersection_record_schema", "roadcreator.intersection-record/v1");
        attrs.SetUserString("rook_intersection_record", recordJson);
        if (!string.IsNullOrWhiteSpace(request.SelectedCandidate?.CandidateId))
            attrs.SetUserString("rook_intersection_candidate_id", request.SelectedCandidate!.CandidateId);
        attrs.SetUserString("rook_intersection_target_layer_root", request.TargetLayerRoot);
    }

    private static IEnumerable<ApproachEdgeSegment> BuildApproachEdgeSegments(
        RhinoDoc doc,
        IntersectionRealizationRequest request,
        double tolerance)
    {
        if (request.SourceRoads.Count == 0 || request.SelectedCandidate?.Point == null)
            yield break;

        var trimPointsByEdge = BuildApproachTrimPoints(request);
        var usedExplicitBoundaryIds = new HashSet<Guid>();
        foreach (var sourceRoad in request.SourceRoads)
        {
            if (string.IsNullOrWhiteSpace(sourceRoad.CenterlineId)
                || !(sourceRoad.EffectiveCarriagewaySurfaceOffset > 0.0)
                || !Guid.TryParse(sourceRoad.CenterlineId, out var centerlineId))
            {
                continue;
            }

            var rhinoObject = doc.Objects.FindId(centerlineId);
            var centerline = rhinoObject?.Geometry as Curve;
            if (centerline == null)
                continue;

            using var flatCenterline = ProjectToXY(centerline);
            var sourceForwardTangent = ToRhinoVector(sourceRoad.ForwardTangent);
            sourceForwardTangent.Z = 0.0;
            if (!sourceForwardTangent.Unitize())
                continue;

            var selectedPoint = request.SelectedCandidate?.Point != null
                ? ToRhinoPoint(request.SelectedCandidate.Point)
                : flatCenterline.PointAt(Math.Min(flatCenterline.Domain.Max,
                    Math.Max(flatCenterline.Domain.Min, sourceRoad.SelectedParameter)));

            var resolvedBoundaries = ResolveSemanticBoundaryCurves(
                doc,
                sourceRoad,
                centerlineId,
                selectedPoint,
                sourceForwardTangent,
                usedExplicitBoundaryIds,
                tolerance);
            var explicitLeftEdge = resolvedBoundaries.LeftEdge;
            var explicitRightEdge = resolvedBoundaries.RightEdge;
            if (resolvedBoundaries.LeftObjectId.HasValue)
                usedExplicitBoundaryIds.Add(resolvedBoundaries.LeftObjectId.Value);
            if (resolvedBoundaries.RightObjectId.HasValue)
                usedExplicitBoundaryIds.Add(resolvedBoundaries.RightObjectId.Value);

            foreach (var side in new[] { "left", "right" })
            {
                Curve? explicitBoundaryCurve = side.Equals("left", StringComparison.OrdinalIgnoreCase)
                    ? explicitLeftEdge
                    : explicitRightEdge;
                if (side.Equals("left", StringComparison.OrdinalIgnoreCase))
                    explicitLeftEdge = null;
                else
                    explicitRightEdge = null;

                using var edgeCurve = explicitBoundaryCurve ?? BuildPhysicalEdgeCurve(flatCenterline, sourceRoad, side, tolerance);
                if (edgeCurve == null)
                    continue;

                var edgeKey = BuildApproachEdgeKey(sourceRoad.Road, side);
                trimPointsByEdge.TryGetValue(edgeKey, out var trimPoints);
                var previewLength = ComputeApproachPreviewLength(request, sourceRoad);
                int segmentIndex = 0;
                foreach (var segment in BuildLocalizedApproachSegments(
                    edgeCurve,
                    trimPoints,
                    sourceForwardTangent,
                    previewLength,
                    tolerance))
                {
                    var sourceCurve = edgeCurve.DuplicateCurve();
                    if (sourceCurve == null)
                        continue;

                    yield return new ApproachEdgeSegment(
                        sourceRoad.Road,
                        side,
                        segment.ArmDirection,
                        $"approach-edge-{sourceRoad.Road.ToLowerInvariant()}-{side}-{segmentIndex++}",
                        segment.Curve,
                        sourceCurve);
                }
            }

            explicitLeftEdge?.Dispose();
            explicitRightEdge?.Dispose();
        }
    }

    private static void DisposeApproachEdges(IEnumerable<ApproachEdgeSegment> approachEdges)
    {
        foreach (var approachEdge in approachEdges)
        {
            approachEdge.Curve.Dispose();
            approachEdge.SourceCurve.Dispose();
        }
    }

    private static void DisposeApproachPatches(IReadOnlyList<ApproachPatch> approachPatches)
    {
        foreach (var approachPatch in approachPatches)
            approachPatch.BorderCurve.Dispose();
    }

    private static ResolvedSemanticBoundaries ResolveSemanticBoundaryCurves(
        RhinoDoc doc,
        IntersectionSourceRoad sourceRoad,
        Guid centerlineId,
        Point3d selectedPoint,
        Vector3d sourceForwardTangent,
        ISet<Guid> usedExplicitBoundaryIds,
        double tolerance)
    {
        var centerlineObject = doc.Objects.FindId(centerlineId);
        var centerlineLayerPath = GetLayerPath(doc, centerlineObject?.Attributes.LayerIndex ?? -1);
        var expectedOffset = sourceRoad.EffectiveCarriagewaySurfaceOffset;

        SemanticBoundaryCandidate? bestLeft = null;
        SemanticBoundaryCandidate? bestRight = null;

        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var layer = doc.Layers[i];
            if (layer == null || layer.IsDeleted)
                continue;

            var layerPath = layer.FullPath ?? layer.Name ?? string.Empty;
            var layerSemanticScore = GetBoundaryLayerSemanticScore(layerPath, centerlineLayerPath);
            if (layerSemanticScore <= 0)
                continue;

            var objects = doc.Objects.FindByLayer(layer);
            if (objects == null)
                continue;

            foreach (var obj in objects)
            {
                if (obj == null || obj.Id == centerlineId || usedExplicitBoundaryIds.Contains(obj.Id) || obj.Geometry is not Curve curve)
                    continue;

                using var flatCurve = ProjectToXY(curve);
                if (!flatCurve.ClosestPoint(selectedPoint, out var curveParameter))
                    continue;

                if (!TryGetCurveDirection(flatCurve, curveParameter, tolerance, out var candidateDirection))
                    continue;

                var tangentAlignment = Math.Abs(candidateDirection * sourceForwardTangent);
                if (tangentAlignment < 0.7)
                    continue;

                var closestPoint = flatCurve.PointAt(curveParameter);
                var radial = closestPoint - selectedPoint;
                radial.Z = 0.0;
                var distance = radial.Length;
                if (!(distance > tolerance) || !radial.Unitize())
                    continue;

                var side = Vector3d.CrossProduct(sourceForwardTangent, radial).Z >= 0.0
                    ? "left"
                    : "right";
                var distanceScore = Math.Max(0.0, 40.0 - Math.Abs(distance - expectedOffset) * 8.0);
                var alignmentScore = tangentAlignment * 80.0;
                var objectSemanticScore = GetBoundaryObjectSemanticScore(obj.Attributes.Name, layerPath, side);
                var totalScore = layerSemanticScore + objectSemanticScore + distanceScore + alignmentScore;
                if (totalScore <= 0.0)
                    continue;

                var candidate = new SemanticBoundaryCandidate(
                    obj.Id,
                    side,
                    totalScore,
                    flatCurve.DuplicateCurve());

                if (side.Equals("left", StringComparison.OrdinalIgnoreCase))
                {
                    if (bestLeft == null || candidate.Score > bestLeft.Score)
                    {
                        bestLeft?.Curve.Dispose();
                        bestLeft = candidate;
                    }
                    else
                    {
                        candidate.Curve.Dispose();
                    }
                }
                else
                {
                    if (bestRight == null || candidate.Score > bestRight.Score)
                    {
                        bestRight?.Curve.Dispose();
                        bestRight = candidate;
                    }
                    else
                    {
                        candidate.Curve.Dispose();
                    }
                }
            }
        }

        return new ResolvedSemanticBoundaries(
            bestLeft?.Curve,
            bestRight?.Curve,
            bestLeft?.ObjectId,
            bestRight?.ObjectId);
    }

    private static IEnumerable<ApproachPatch> BuildApproachPatches(
        IReadOnlyList<ApproachEdgeSegment> approachEdges,
        IntersectionRealizationRequest request,
        double tolerance)
    {
        if (approachEdges.Count == 0)
            yield break;

        var grouped = approachEdges
            .GroupBy(edge => (Road: edge.Road.ToUpperInvariant(), ArmDirection: edge.ArmDirection.ToUpperInvariant()));

        foreach (var group in grouped)
        {
            var leftEdges = group
                .Where(edge => edge.Side.Equals("left", StringComparison.OrdinalIgnoreCase))
                .OrderBy(edge => edge.Curve.PointAtStart.DistanceTo(edge.Curve.PointAtEnd))
                .ToList();
            var rightEdges = group
                .Where(edge => edge.Side.Equals("right", StringComparison.OrdinalIgnoreCase))
                .OrderBy(edge => edge.Curve.PointAtStart.DistanceTo(edge.Curve.PointAtEnd))
                .ToList();

            var pairCount = Math.Min(leftEdges.Count, rightEdges.Count);
            for (int i = 0; i < pairCount; i++)
            {
                var patch = BuildApproachPatch(leftEdges[i], rightEdges[i], tolerance);
                if (patch != null)
                    yield return patch with
                    {
                        Road = group.Key.Road,
                        ArmDirection = group.Key.ArmDirection,
                        ObjectName = $"approach-patch-{group.Key.Road.ToLowerInvariant()}-{group.Key.ArmDirection.ToLowerInvariant()}-{i}"
                    };
            }
        }
    }

    private static ApproachPatch? BuildApproachPatch(
        ApproachEdgeSegment leftEdge,
        ApproachEdgeSegment rightEdge,
        double tolerance)
    {
        var leftCopy = leftEdge.Curve.DuplicateCurve();
        var rightCopy = rightEdge.Curve.DuplicateCurve();
        if (leftCopy == null || rightCopy == null)
        {
            leftCopy?.Dispose();
            rightCopy?.Dispose();
            return null;
        }

        var sharedTargetLength = Math.Min(leftCopy.GetLength(), rightCopy.GetLength());
        using var synchronizedLeft = SynchronizeCurveLength(leftCopy, sharedTargetLength, tolerance);
        using var synchronizedRight = SynchronizeCurveLength(rightCopy, sharedTargetLength, tolerance);
        if (synchronizedLeft == null || synchronizedRight == null)
            return null;

        var sharedLength = Math.Min(synchronizedLeft.GetLength(), synchronizedRight.GetLength());
        if (!(sharedLength > tolerance * 4.0))
            return null;

        var leftNear = synchronizedLeft.PointAtStart;
        var leftFar = synchronizedLeft.PointAtEnd;
        var rightNear = synchronizedRight.PointAtStart;
        var rightFar = synchronizedRight.PointAtEnd;

        if (leftNear.DistanceTo(rightNear) <= tolerance || leftFar.DistanceTo(rightFar) <= tolerance)
            return null;

        using var farCap = new LineCurve(leftFar, rightFar);
        using var nearCap = new LineCurve(rightNear, leftNear);
        using var rightReversed = synchronizedRight.DuplicateCurve();
        rightReversed.Reverse();

        var joined = Curve.JoinCurves(
            new Curve[]
            {
                synchronizedLeft.DuplicateCurve(),
                farCap.DuplicateCurve(),
                rightReversed,
                nearCap.DuplicateCurve()
            },
            tolerance);

        if (joined == null || joined.Length == 0)
            return null;

        for (int i = 1; i < joined.Length; i++)
            joined[i]?.Dispose();

        var border = joined[0];
        if (!border.IsClosed)
        {
            border.Dispose();
            return null;
        }

        return new ApproachPatch("", "", "", border);
    }

    private static Curve BuildRealizedSurface(
        Curve centralBoundary,
        IntersectionRealizationRequest request,
        IReadOnlyList<ApproachEdgeSegment> approachEdges,
        IReadOnlyList<ApproachPatch> approachPatches,
        double tolerance,
        out Brep realizedSurface,
        out SurfaceDebugInfo debugInfo)
    {
        debugInfo = new SurfaceDebugInfo("region-fallback");
        realizedSurface = null!;
        List<Curve>? resolvedCurves = null;
        try
        {
            resolvedCurves = ResolveFinalSurfaceRegionCurves(
                centralBoundary,
                request,
                approachPatches,
                tolerance);

            var primaryResolvedBoundary = SelectPrimaryResolvedBoundaryCurve(
                resolvedCurves,
                request,
                tolerance);
            List<OuterCornerFillet> resolvedBoundaryFillets = new();
            List<OuterCornerAttempt> resolvedBoundaryAttempts = new();
            List<DebugBoundaryCurve> directBoundaryDebugCurves = new();
            using var directResolvedLoop = primaryResolvedBoundary != null
                ? TryBuildFilletedBoundaryFromResolvedCurve(
                    primaryResolvedBoundary,
                    request,
                    tolerance,
                    out resolvedBoundaryFillets,
                    out resolvedBoundaryAttempts,
                    out directBoundaryDebugCurves)
                : null;
            if (directResolvedLoop != null)
            {
                var directBreps = Brep.CreatePlanarBreps(new[] { directResolvedLoop }, tolerance);
                if (directBreps != null && directBreps.Length > 0)
                {
                    debugInfo = new SurfaceDebugInfo(
                        "direct-boundary-fillet",
                        resolvedBoundaryFillets,
                        resolvedBoundaryAttempts,
                        BuildBoundaryDebugCurves(directResolvedLoop, resolvedBoundaryFillets, resolvedCurves, directBoundaryDebugCurves));
                    realizedSurface = SelectSingleRealizedSurface(directBreps, request, tolerance);
                    return directResolvedLoop.DuplicateCurve()
                        ?? throw new InvalidOperationException("Failed to duplicate direct resolved boundary");
                }
            }

            using var exteriorLoop = BuildExteriorCrossBoundaryLoop(
                approachEdges,
                request,
                SelectInteriorClassifierCurves(resolvedCurves, tolerance),
                tolerance,
                out var outerCornerFillets,
                out var outerCornerAttempts);
            if (exteriorLoop != null)
            {
                var directBreps = Brep.CreatePlanarBreps(new[] { exteriorLoop }, tolerance);
                if (directBreps != null && directBreps.Length > 0)
                {
                    debugInfo = new SurfaceDebugInfo(
                        "outer-fillet-loop",
                        outerCornerFillets,
                        outerCornerAttempts,
                        BuildBoundaryDebugCurves(exteriorLoop, outerCornerFillets, null));
                    realizedSurface = SelectSingleRealizedSurface(directBreps, request, tolerance);
                    return exteriorLoop.DuplicateCurve()
                        ?? throw new InvalidOperationException("Failed to duplicate exterior boundary loop");
                }
            }

            var debugCornerFillets = resolvedBoundaryFillets.Count > 0
                ? resolvedBoundaryFillets
                : outerCornerFillets;
            var debugCornerAttempts = resolvedBoundaryAttempts.Count > 0
                ? resolvedBoundaryAttempts
                : outerCornerAttempts;
            debugInfo = new SurfaceDebugInfo(
                "region-fallback",
                debugCornerFillets,
                debugCornerAttempts,
                BuildBoundaryDebugCurves(exteriorLoop, outerCornerFillets, resolvedCurves, directBoundaryDebugCurves));

            if (resolvedCurves == null || resolvedCurves.Count == 0)
                throw new InvalidOperationException("Failed to resolve final carriageway region curves");

            var surfaceBreps = Brep.CreatePlanarBreps(resolvedCurves, tolerance);
            if (surfaceBreps == null || surfaceBreps.Length == 0)
                throw new InvalidOperationException("Failed to create planar intersection surface");

            realizedSurface = SelectSingleRealizedSurface(surfaceBreps, request, tolerance);
            var finalBoundary = primaryResolvedBoundary?.DuplicateCurve();
            if (finalBoundary == null)
                throw new InvalidOperationException("Failed to duplicate realized boundary from resolved curves");

            return finalBoundary;
        }
        catch
        {
            realizedSurface?.Dispose();
            throw;
        }
        finally
        {
            if (resolvedCurves != null)
            {
                foreach (var curve in resolvedCurves)
                    curve.Dispose();
            }
        }
    }

    private static Curve? SelectPrimaryResolvedBoundaryCurve(
        IReadOnlyList<Curve>? resolvedCurves,
        IntersectionRealizationRequest request,
        double tolerance)
    {
        if (resolvedCurves == null || resolvedCurves.Count == 0)
            return null;

        var requiredPoint = ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints);
        var minimumArea = Math.Abs(request.ProvisionalBoundary2D.Area) + tolerance * tolerance;
        Curve? best = null;
        double bestArea = double.NegativeInfinity;
        foreach (var curve in resolvedCurves)
        {
            if (curve == null || !curve.IsClosed)
                continue;

            using var massProps = AreaMassProperties.Compute(curve);
            var area = Math.Abs(massProps?.Area ?? 0.0);
            if (area <= minimumArea)
                continue;

            var containment = curve.Contains(requiredPoint, Plane.WorldXY, tolerance);
            if (containment != PointContainment.Inside && containment != PointContainment.Coincident)
                continue;

            if (area > bestArea)
            {
                best = curve;
                bestArea = area;
            }
        }

        return best;
    }

    private static Curve? TryBuildFilletedBoundaryFromResolvedCurve(
        Curve resolvedBoundary,
        IntersectionRealizationRequest request,
        double tolerance,
        out List<OuterCornerFillet> fillets,
        out List<OuterCornerAttempt> attempts,
        out List<DebugBoundaryCurve> debugBoundaryCurves)
    {
        fillets = new List<OuterCornerFillet>();
        attempts = new List<OuterCornerAttempt>();
        debugBoundaryCurves = new List<DebugBoundaryCurve>();
        var cornerResults = new List<BoundaryCornerFilletResult>();
        var succeeded = false;

        if (!TryResolveBoundaryEdgesFromResolvedCurve(
                resolvedBoundary,
                request,
                tolerance,
                out var boundaryResolution,
                out var resolutionFailure))
        {
            attempts.Add(new OuterCornerAttempt(
                -1,
                false,
                resolutionFailure,
                new ArmEdgeRef("boundary", "incoming", "unresolved"),
                new ArmEdgeRef("boundary", "outgoing", "unresolved"),
                0.0,
                null,
                ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints)));
            return null;
        }

        try
        {
            debugBoundaryCurves = BuildResolvedBoundaryDebugCurves(boundaryResolution, tolerance).ToList();
            var requestedRadii = request.AnalysisGeometry2D.CurbReturnArcs
                .Where(static arc => arc.CornerOrder.HasValue && arc.Radius > 0.0)
                .GroupBy(static arc => arc.CornerOrder!.Value)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.First().Radius);
            if (requestedRadii.Count == 0)
            {
                attempts.Add(new OuterCornerAttempt(
                    -1,
                    false,
                    "no_valid_curb_return_radii",
                    null,
                    null,
                    0.0,
                    null,
                    ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints)));
                return null;
            }

            var plannedRadii = BuildNormalizedBoundaryCornerRadiusPlan(
                boundaryResolution,
                requestedRadii,
                tolerance);

            for (int cornerOrder = 0; cornerOrder < boundaryResolution.Corners.Count; cornerOrder++)
            {
                var incomingIndex = (cornerOrder + boundaryResolution.Edges.Count - 1) % boundaryResolution.Edges.Count;
                var outgoingIndex = cornerOrder;
                var incoming = boundaryResolution.Edges[incomingIndex];
                var outgoing = boundaryResolution.Edges[outgoingIndex];
                    var corner = boundaryResolution.Corners[cornerOrder];
                    var cornerPoint = corner.Point;
                    if (!requestedRadii.TryGetValue(corner.SemanticIndex, out var requestedRadius))
                    {
                        attempts.Add(new OuterCornerAttempt(
                            corner.SemanticIndex,
                            false,
                            $"missing_radius:{corner.SemanticIndex}",
                            new ArmEdgeRef("boundary", "incoming", incoming.Id),
                            new ArmEdgeRef("boundary", "outgoing", outgoing.Id),
                            0.0,
                            null,
                            cornerPoint));
                        return null;
                    }

                var plannedRadius = plannedRadii.TryGetValue(corner.SemanticIndex, out var normalizedRadius)
                    ? normalizedRadius
                    : requestedRadius;

                if (!TryBuildBoundaryCornerFillet(
                        corner.SemanticIndex,
                        incoming.Id,
                        incoming.Curve,
                        outgoing.Id,
                        outgoing.Curve,
                        cornerPoint,
                        requestedRadius,
                        plannedRadius,
                        tolerance,
                        out var result,
                        out var attempt))
                {
                    attempts.Add(attempt);
                    return null;
                }

                cornerResults.Add(result);
                attempts.Add(attempt);
                var filletCurve = result.ArcCurve.DuplicateCurve();
                if (filletCurve == null)
                {
                    attempts.Add(new OuterCornerAttempt(
                        cornerOrder,
                        false,
                        "arc_copy_failed",
                        new ArmEdgeRef("boundary", "incoming", incoming.Id),
                        new ArmEdgeRef("boundary", "outgoing", outgoing.Id),
                        requestedRadius,
                        null,
                        cornerPoint));
                    return null;
                }

                fillets.Add(new OuterCornerFillet(
                    corner.SemanticIndex,
                    new ArmEdgeRef("boundary", "incoming", incoming.Id),
                    new ArmEdgeRef("boundary", "outgoing", outgoing.Id),
                    result.Radius,
                    result.IncomingParameter,
                    result.OutgoingParameter,
                    filletCurve));
            }

            var assembledPieces = new List<Curve>();
            try
            {
                for (int segmentIndex = 0; segmentIndex < boundaryResolution.Edges.Count; segmentIndex++)
                {
                    var startCorner = cornerResults[segmentIndex];
                    var endCorner = cornerResults[(segmentIndex + 1) % cornerResults.Count];
                    var sourceEdge = boundaryResolution.Edges[segmentIndex];
                    var sourceSegment = sourceEdge.Curve;

                    if (!TryComputeBoundarySegmentTrimInterval(
                            sourceSegment,
                            startCorner.OutgoingParameter,
                            endCorner.IncomingParameter,
                            tolerance,
                            out var trimStart,
                            out var trimEnd))
                    {
                        attempts.Add(new OuterCornerAttempt(
                            sourceEdge.StartCornerIndex,
                            false,
                            $"segment_trim_interval_invalid:{sourceEdge.Id}",
                            new ArmEdgeRef("boundary", "segment", sourceEdge.Id),
                            null,
                            0.0,
                            null,
                            sourceSegment.PointAtStart));
                        return null;
                    }

                    var trimmed = sourceSegment.Trim(trimStart, trimEnd);
                    if (trimmed == null)
                    {
                        attempts.Add(new OuterCornerAttempt(
                            sourceEdge.StartCornerIndex,
                            false,
                            $"segment_trim_failed:{sourceEdge.Id}",
                            new ArmEdgeRef("boundary", "segment", sourceEdge.Id),
                            null,
                            0.0,
                            null,
                            sourceSegment.PointAtStart));
                        return null;
                    }

                    OrientCurveToStartNear(trimmed, startCorner.OutgoingPoint);
                    if (!IsBoundaryAssemblyEndpointMatch(trimmed.PointAtStart, startCorner.OutgoingPoint, tolerance)
                        || !IsBoundaryAssemblyEndpointMatch(trimmed.PointAtEnd, endCorner.IncomingPoint, tolerance))
                    {
                        trimmed.Dispose();
                        attempts.Add(new OuterCornerAttempt(
                            sourceEdge.StartCornerIndex,
                            false,
                            $"segment_trim_endpoint_mismatch:{sourceEdge.Id}",
                            new ArmEdgeRef("boundary", "segment", sourceEdge.Id),
                            null,
                            0.0,
                            null,
                            sourceSegment.PointAtStart));
                        return null;
                    }

                    assembledPieces.Add(trimmed);

                    var arcCopy = endCorner.ArcCurve.DuplicateCurve();
                    if (arcCopy == null)
                    {
                        attempts.Add(new OuterCornerAttempt(
                            endCorner.CornerOrder,
                            false,
                            $"arc_copy_failed:{endCorner.CornerOrder}",
                            new ArmEdgeRef("boundary", "incoming", sourceEdge.Id),
                            new ArmEdgeRef("boundary", "outgoing", boundaryResolution.Edges[(segmentIndex + 1) % boundaryResolution.Edges.Count].Id),
                            endCorner.Radius,
                            null,
                            endCorner.CornerPoint));
                        return null;
                    }

                    OrientCurveToStartNear(arcCopy, trimmed.PointAtEnd);
                    if (!IsBoundaryAssemblyEndpointMatch(arcCopy.PointAtStart, trimmed.PointAtEnd, tolerance))
                    {
                        arcCopy.Dispose();
                        attempts.Add(new OuterCornerAttempt(
                            endCorner.CornerOrder,
                            false,
                            $"arc_start_endpoint_mismatch:{endCorner.CornerOrder}",
                            new ArmEdgeRef("boundary", "incoming", sourceEdge.Id),
                            new ArmEdgeRef("boundary", "outgoing", boundaryResolution.Edges[(segmentIndex + 1) % boundaryResolution.Edges.Count].Id),
                            endCorner.Radius,
                            null,
                            endCorner.CornerPoint));
                        return null;
                    }

                    assembledPieces.Add(arcCopy);
                }

                var requiredPoint = ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints);
                var joined = Curve.JoinCurves(assembledPieces, tolerance * 8.0);
                if (joined == null || joined.Length == 0)
                {
                    attempts.Add(new OuterCornerAttempt(
                        -1,
                        false,
                        "assembled_join_failed",
                        null,
                        null,
                        0.0,
                        null,
                        requiredPoint));
                    return null;
                }

                var minimumArea = Math.Abs(request.ProvisionalBoundary2D.Area) + tolerance * tolerance;
                Curve? best = null;
                double bestArea = double.NegativeInfinity;
                foreach (var curve in joined)
                {
                    if (curve == null)
                        continue;

                    if (!TryValidateFilletedBoundaryCurve(
                            curve,
                            resolvedBoundary,
                            cornerResults,
                            requiredPoint,
                            minimumArea,
                            tolerance,
                            out var area))
                    {
                        curve.Dispose();
                        continue;
                    }

                    if (area > bestArea)
                    {
                        best?.Dispose();
                        best = curve;
                        bestArea = area;
                    }
                    else
                    {
                        curve.Dispose();
                    }
                }

                if (best == null)
                {
                    attempts.Add(new OuterCornerAttempt(
                        -1,
                        false,
                        "assembled_boundary_validation_failed",
                        null,
                        null,
                        0.0,
                        null,
                        requiredPoint));
                }

                succeeded = best != null;
                return best;
            }
            finally
            {
                foreach (var piece in assembledPieces)
                    piece.Dispose();
            }
        }
        finally
        {
            DisposeBoundaryEdgeResolution(boundaryResolution);
            DisposeBoundaryCornerFilletResults(cornerResults);
            if (!succeeded)
            {
                DisposeOuterCornerFillets(fillets);
                fillets.Clear();
            }
        }
    }

    private static bool TryResolveBoundaryEdgesFromResolvedCurve(
        Curve resolvedBoundary,
        IntersectionRealizationRequest request,
        double tolerance,
        out BoundaryEdgeResolution resolution,
        out string failureReason)
    {
        resolution = null!;
        failureReason = string.Empty;

        if (!TryResolveBoundaryCornerParameters(
                resolvedBoundary,
                request,
                tolerance,
                out var selectedCorners,
                out var seamCorner,
                out failureReason))
        {
            return false;
        }

        var workingBoundary = resolvedBoundary.DuplicateCurve();
        if (workingBoundary == null)
        {
            failureReason = "duplicate_curve_failed";
            return false;
        }

        if (!workingBoundary.ChangeClosedCurveSeam(seamCorner.Parameter))
        {
            workingBoundary.Dispose();
            failureReason = "change_closed_curve_seam_failed";
            return false;
        }

        var reseamedCorners = new List<BoundaryCornerRef>(selectedCorners.Count);
        reseamedCorners.Add(new BoundaryCornerRef(
            seamCorner.SemanticIndex,
            workingBoundary.PointAtStart,
            workingBoundary.Domain.Min,
            seamCorner.RadiusToCentroid,
            seamCorner.TurningAngleDegrees));

        foreach (var selectedCorner in selectedCorners.Where(corner => corner.SemanticIndex != seamCorner.SemanticIndex))
        {
            if (!workingBoundary.ClosestPoint(selectedCorner.Point, out var reseamedParameter))
            {
                workingBoundary.Dispose();
                failureReason = $"reseamed_closest_point_failed:{selectedCorner.SemanticIndex}";
                return false;
            }

            reseamedCorners.Add(new BoundaryCornerRef(
                selectedCorner.SemanticIndex,
                workingBoundary.PointAt(reseamedParameter),
                reseamedParameter,
                selectedCorner.RadiusToCentroid,
                selectedCorner.TurningAngleDegrees));
        }

        reseamedCorners.Sort(static (a, b) => a.Parameter.CompareTo(b.Parameter));

        var edges = new List<ResolvedBoundaryEdge>(4);
        for (int i = 0; i < reseamedCorners.Count; i++)
        {
            var startParameter = reseamedCorners[i].Parameter;
            var endParameter = i + 1 < reseamedCorners.Count
                ? reseamedCorners[i + 1].Parameter
                : workingBoundary.Domain.Max;
            var segment = TrimBoundarySpan(
                workingBoundary,
                startParameter,
                endParameter,
                tolerance);
            if (segment == null)
            {
                foreach (var edge in edges)
                    edge.Curve.Dispose();
                workingBoundary.Dispose();
                failureReason = $"trim_span_failed:{i}";
                return false;
            }

            OrientCurveToStartNear(segment, reseamedCorners[i].Point);
            edges.Add(new ResolvedBoundaryEdge(
                $"edge-{i}",
                i,
                reseamedCorners[i].SemanticIndex,
                reseamedCorners[(i + 1) % reseamedCorners.Count].SemanticIndex,
                startParameter,
                endParameter,
                segment));
        }

        workingBoundary.Dispose();

        if (edges.Count != 4)
        {
            foreach (var edge in edges)
                edge.Curve.Dispose();
            failureReason = $"resolved_edge_count:{edges.Count}";
            return false;
        }

        resolution = new BoundaryEdgeResolution(reseamedCorners, edges);
        return true;
    }

    private static Curve? TrimBoundarySpan(
        Curve boundary,
        double startParameter,
        double endParameter,
        double tolerance)
    {
        var start = Math.Max(boundary.Domain.Min, Math.Min(boundary.Domain.Max, startParameter));
        var end = Math.Max(boundary.Domain.Min, Math.Min(boundary.Domain.Max, endParameter));
        if (end - start <= tolerance)
            return null;

        var trimmed = boundary.Trim(start, end);
        if (trimmed == null)
            return null;

        return trimmed.GetLength() > tolerance * 4.0
            ? trimmed
            : DisposeAndReturnNull(trimmed);
    }

    private static Curve? DisposeAndReturnNull(Curve curve)
    {
        curve.Dispose();
        return null;
    }

    private static void DisposeBoundaryEdgeResolution(BoundaryEdgeResolution resolution)
    {
        foreach (var edge in resolution.Edges)
            edge.Curve.Dispose();
    }

    private static void DisposeBoundaryCornerFilletResults(IReadOnlyList<BoundaryCornerFilletResult> cornerResults)
    {
        foreach (var cornerResult in cornerResults)
            cornerResult.ArcCurve.Dispose();
    }

    private static void DisposeOuterCornerFillets(IReadOnlyList<OuterCornerFillet> fillets)
    {
        foreach (var fillet in fillets)
            fillet.ArcCurve.Dispose();
    }

    private static Dictionary<int, double> BuildNormalizedBoundaryCornerRadiusPlan(
        BoundaryEdgeResolution boundaryResolution,
        IReadOnlyDictionary<int, double> requestedRadii,
        double tolerance)
    {
        var planned = new Dictionary<int, double>();
        if (boundaryResolution.Edges.Count == 0 || requestedRadii.Count == 0)
            return planned;

        var edgeLengths = boundaryResolution.Edges
            .Select(edge => Math.Max(edge.Curve.GetLength(), tolerance * 10.0))
            .ToArray();
        var meanEdgeLength = edgeLengths.Average();
        if (!(meanEdgeLength > tolerance))
        {
            foreach (var pair in requestedRadii)
                planned[pair.Key] = pair.Value;
            return planned;
        }

        for (int cornerOrder = 0; cornerOrder < boundaryResolution.Corners.Count; cornerOrder++)
        {
            var semanticIndex = boundaryResolution.Corners[cornerOrder].SemanticIndex;
            if (!requestedRadii.TryGetValue(semanticIndex, out var requestedRadius) || !(requestedRadius > 0.0))
                continue;

            var previousEdgeLength = edgeLengths[(cornerOrder + boundaryResolution.Edges.Count - 1) % boundaryResolution.Edges.Count];
            var nextEdgeLength = edgeLengths[cornerOrder];
            var adjacentMeanLength = (previousEdgeLength + nextEdgeLength) * 0.5;

            // Short adjacent edges get slightly smaller radii so they retain more usable arm length.
            var normalizedScale = Math.Clamp(adjacentMeanLength / meanEdgeLength, 0.82, 1.0);
            planned[semanticIndex] = RoundTo(requestedRadius * normalizedScale, 4);
        }

        return planned;
    }

    private static double ClampBoundaryCornerRadiusToGeometry(
        Curve incomingSegment,
        Curve outgoingSegment,
        double targetRadius,
        double tolerance)
    {
        if (!(targetRadius > 0.0))
            return targetRadius;

        var incomingLength = incomingSegment.GetLength();
        var outgoingLength = outgoingSegment.GetLength();
        if (!(incomingLength > tolerance * 10.0) || !(outgoingLength > tolerance * 10.0))
            return targetRadius;

        var incomingTangent = incomingSegment.TangentAtEnd;
        var outgoingTangent = outgoingSegment.TangentAtStart;
        incomingTangent.Reverse();
        incomingTangent.Z = 0.0;
        outgoingTangent.Z = 0.0;
        if (!incomingTangent.Unitize() || !outgoingTangent.Unitize())
            return targetRadius;

        var cornerAngle = Vector3d.VectorAngle(incomingTangent, outgoingTangent, Plane.WorldXY);
        if (!double.IsFinite(cornerAngle))
            return targetRadius;

        cornerAngle = Math.Clamp(cornerAngle, RhinoMath.ToRadians(5.0), RhinoMath.ToRadians(175.0));
        var tangentFactor = Math.Tan(cornerAngle * 0.5);
        if (!(tangentFactor > 1e-6))
            return targetRadius;

        var incomingBudget = incomingLength * 0.45;
        var outgoingBudget = outgoingLength * 0.45;
        var maxRadius = Math.Min(incomingBudget, outgoingBudget) * tangentFactor;
        if (!(maxRadius > tolerance * 2.0))
            return targetRadius;

        return RoundTo(Math.Max(tolerance * 2.0, Math.Min(targetRadius, maxRadius)), 4);
    }

    private static bool TryBuildBoundaryCornerFillet(
        int cornerOrder,
        string incomingEdgeId,
        Curve incomingSegment,
        string outgoingEdgeId,
        Curve outgoingSegment,
        Point3d cornerPoint,
        double requestedRadius,
        double targetRadius,
        double tolerance,
        out BoundaryCornerFilletResult result,
        out OuterCornerAttempt attempt)
    {
        result = null!;
        var incomingRef = new ArmEdgeRef("boundary", "incoming", incomingEdgeId);
        var outgoingRef = new ArmEdgeRef("boundary", "outgoing", outgoingEdgeId);

        if (incomingSegment.GetLength() <= tolerance * 10.0 || outgoingSegment.GetLength() <= tolerance * 10.0)
        {
            attempt = new OuterCornerAttempt(cornerOrder, false, "segment_too_short", incomingRef, outgoingRef, requestedRadius, null, cornerPoint);
            return false;
        }

        if (!TryBuildBoundaryCornerLocalSegments(
                incomingSegment,
                outgoingSegment,
                tolerance,
                out var incomingLocal,
                out var outgoingLocal))
        {
            attempt = new OuterCornerAttempt(cornerOrder, false, "local_segment_failed", incomingRef, outgoingRef, requestedRadius, null, cornerPoint);
            return false;
        }

        var lastRadius = requestedRadius;
        using (incomingLocal)
        using (outgoingLocal)
        {
            var angleTolerance = RhinoMath.ToRadians(1.0);
            var tryRadius = ClampBoundaryCornerRadiusToGeometry(
                incomingSegment,
                outgoingSegment,
                targetRadius,
                tolerance);
            lastRadius = tryRadius;
            for (int i = 0; i < 6; i++)
            {
                lastRadius = tryRadius;
                var incomingPick = ComputeKeepSidePickPoint(incomingLocal, tolerance);
                var outgoingPick = ComputeKeepSidePickPoint(outgoingLocal, tolerance);
                if (incomingPick.HasValue && outgoingPick.HasValue)
                {
                    var pieces = Curve.CreateFilletCurves(
                        incomingLocal,
                        incomingPick.Value,
                        outgoingLocal,
                        outgoingPick.Value,
                        tryRadius,
                        false,
                        true,
                        false,
                        tolerance,
                        angleTolerance);
                    if (pieces == null || pieces.Length < 3)
                    {
                        continue;
                    }

                    try
                    {
                        Curve? arc = null;
                        foreach (var candidate in ExtractCandidateFilletArcs(pieces))
                        {
                            using var rawCandidate = candidate.Curve;
                            if (rawCandidate == null)
                                continue;

                            if (!rawCandidate.TryGetArc(out _))
                                continue;

                            if (!TryOrientArcToSourceCurves(
                                    rawCandidate,
                                    incomingSegment,
                                    outgoingSegment,
                                    tolerance,
                                    out var candidateIncomingParameter,
                                    out var candidateOutgoingParameter))
                            {
                                continue;
                            }

                            if (rawCandidate.GetLength() <= tolerance * 4.0)
                            {
                                continue;
                            }

                            var candidateIncomingPoint = incomingSegment.PointAt(candidateIncomingParameter);
                            var candidateOutgoingPoint = outgoingSegment.PointAt(candidateOutgoingParameter);

                            arc = rawCandidate.DuplicateCurve();
                            if (arc == null)
                                continue;

                            result = new BoundaryCornerFilletResult(
                                cornerOrder,
                                tryRadius,
                                cornerPoint,
                                candidateIncomingParameter,
                                candidateOutgoingParameter,
                                candidateIncomingPoint,
                                candidateOutgoingPoint,
                                arc);
                            attempt = new OuterCornerAttempt(
                                cornerOrder,
                                true,
                                "ok:midpoint_keep_side",
                                incomingRef,
                                outgoingRef,
                                requestedRadius,
                                tryRadius,
                                arc.PointAtNormalizedLength(0.5));
                            return true;
                        }

                        if (arc == null)
                            continue;
                    }
                    finally
                    {
                        foreach (var piece in pieces)
                            piece?.Dispose();
                    }
                }

                tryRadius *= 0.8;
            }
        }

        attempt = new OuterCornerAttempt(cornerOrder, false, "no_connecting_arc_piece", incomingRef, outgoingRef, requestedRadius, lastRadius, cornerPoint);
        return false;
    }

    private static bool TryBuildBoundaryCornerLocalSegments(
        Curve incomingSegment,
        Curve outgoingSegment,
        double tolerance,
        out Curve incomingLocal,
        out Curve outgoingLocal)
    {
        incomingLocal = null!;
        outgoingLocal = null!;

        if (!incomingSegment.LengthParameter(incomingSegment.GetLength() * 0.5, out var incomingMid))
            return false;
        if (!outgoingSegment.LengthParameter(outgoingSegment.GetLength() * 0.5, out var outgoingMid))
            return false;

        incomingLocal = TrimBoundarySpan(incomingSegment, incomingMid, incomingSegment.Domain.Max, tolerance)!;
        outgoingLocal = TrimBoundarySpan(outgoingSegment, outgoingSegment.Domain.Min, outgoingMid, tolerance)!;
        if (incomingLocal == null || outgoingLocal == null)
        {
            incomingLocal?.Dispose();
            outgoingLocal?.Dispose();
            return false;
        }

        return true;
    }

    private static bool TryResolveBoundaryCornerParameters(
        Curve boundary,
        IntersectionRealizationRequest request,
        double tolerance,
        out List<BoundaryCornerRef> orderedCorners,
        out BoundaryCornerRef seamCorner,
        out string failureReason)
    {
        orderedCorners = new List<BoundaryCornerRef>();
        seamCorner = null!;
        failureReason = string.Empty;

        if (request.ProvisionalBoundary2D.CornerPoints.Count != 4)
        {
            failureReason = $"provisional_corner_count:{request.ProvisionalBoundary2D.CornerPoints.Count}";
            return false;
        }

        if (!TryDetectIntrinsicBoundaryCorners(boundary, tolerance, out var detectedCorners, out var centroid, out failureReason))
            return false;

        var provisionalCorners = request.ProvisionalBoundary2D.CornerPoints
            .Select(ToRhinoPoint)
            .ToList();
        var permutation = FindBestCornerAssignment(detectedCorners, provisionalCorners);
        if (permutation == null)
        {
            failureReason = "corner_assignment_failed";
            return false;
        }

        var assignedCorners = new List<BoundaryCornerRef>(detectedCorners.Count);
        for (int semanticIndex = 0; semanticIndex < permutation.Length; semanticIndex++)
        {
            var detected = detectedCorners[permutation[semanticIndex]];
            assignedCorners.Add(new BoundaryCornerRef(
                semanticIndex,
                detected.Point,
                detected.Parameter,
                detected.RadiusToCentroid,
                detected.TurningAngleDegrees));
        }

        assignedCorners.Sort(static (a, b) => a.Parameter.CompareTo(b.Parameter));
        if (assignedCorners.Count != 4)
        {
            failureReason = $"assigned_corner_count:{assignedCorners.Count}";
            return false;
        }

        for (int i = 0; i < assignedCorners.Count; i++)
        {
            var current = assignedCorners[i];
            var next = assignedCorners[(i + 1) % assignedCorners.Count];
            var delta = i + 1 < assignedCorners.Count
                ? next.Parameter - current.Parameter
                : (boundary.Domain.Max - current.Parameter) + (next.Parameter - boundary.Domain.Min);
            if (delta <= tolerance)
            {
                failureReason = $"corner_spacing_invalid:{i}";
                return false;
            }
        }

        seamCorner = SelectStableBoundarySeamCorner(assignedCorners, centroid);
        orderedCorners = assignedCorners;
        return true;
    }

    private static bool TryDetectIntrinsicBoundaryCorners(
        Curve boundary,
        double tolerance,
        out List<BoundaryCornerRef> corners,
        out Point3d centroid,
        out string failureReason)
    {
        corners = new List<BoundaryCornerRef>();
        failureReason = string.Empty;
        centroid = ComputeClosedCurveSeedPoint(boundary);

        if (!boundary.IsClosed)
        {
            failureReason = "boundary_not_closed";
            return false;
        }

        var boundaryLength = boundary.GetLength();
        if (!(boundaryLength > tolerance * 20.0))
        {
            failureReason = "boundary_too_short";
            return false;
        }

        var sampleCount = (int)Math.Clamp(Math.Ceiling(boundaryLength / Math.Max(0.5, tolerance * 20.0)), 240.0, 720.0);
        var sampleSpacing = boundaryLength / sampleCount;
        var sampleParameters = boundary.DivideByLength(sampleSpacing, true, out Point3d[] samplePoints);
        if (sampleParameters == null || samplePoints == null || sampleParameters.Length < 8 || samplePoints.Length < 8)
        {
            failureReason = "boundary_sampling_failed";
            return false;
        }

        var samples = new List<BoundaryCornerSample>(sampleParameters.Length);
        for (int i = 0; i < sampleParameters.Length; i++)
        {
            var parameter = sampleParameters[i];
            if (parameter >= boundary.Domain.Max - tolerance * 0.5)
                continue;

            var point = samplePoints[i];
            if (samples.Count > 0 && point.DistanceTo(samples[^1].Point) <= tolerance * 0.5)
                continue;

            samples.Add(new BoundaryCornerSample(parameter, point));
        }

        if (samples.Count < 8)
        {
            failureReason = $"boundary_sample_count:{samples.Count}";
            return false;
        }

        var peaks = new List<BoundaryCornerPeak>();
        for (int i = 0; i < samples.Count; i++)
        {
            var previous = samples[(i + samples.Count - 1) % samples.Count].Point;
            var current = samples[i].Point;
            var next = samples[(i + 1) % samples.Count].Point;

            var incoming = current - previous;
            var outgoing = next - current;
            incoming.Z = 0.0;
            outgoing.Z = 0.0;
            if (!incoming.Unitize() || !outgoing.Unitize())
                continue;

            var dot = Math.Max(-1.0, Math.Min(1.0, incoming * outgoing));
            var cross = Vector3d.CrossProduct(incoming, outgoing).Z;
            var turningDegrees = Math.Abs(RhinoMath.ToDegrees(Math.Atan2(cross, dot)));
            if (turningDegrees < 20.0)
                continue;

            peaks.Add(new BoundaryCornerPeak(
                i,
                samples[i].Parameter,
                samples[i].Point,
                current.DistanceTo(centroid),
                turningDegrees));
        }

        if (peaks.Count < 4)
        {
            failureReason = $"corner_peak_count:{peaks.Count}";
            return false;
        }

        var peakLookup = peaks.ToDictionary(static peak => peak.SampleIndex);
        var localPeaks = peaks
            .Where(peak =>
            {
                var previousIndex = (peak.SampleIndex + samples.Count - 1) % samples.Count;
                var nextIndex = (peak.SampleIndex + 1) % samples.Count;
                var previousTurn = peakLookup.TryGetValue(previousIndex, out var previousPeak)
                    ? previousPeak.TurningAngleDegrees
                    : double.NegativeInfinity;
                var nextTurn = peakLookup.TryGetValue(nextIndex, out var nextPeak)
                    ? nextPeak.TurningAngleDegrees
                    : double.NegativeInfinity;
                return peak.TurningAngleDegrees >= previousTurn && peak.TurningAngleDegrees >= nextTurn;
            })
            .OrderBy(static peak => peak.RadiusToCentroid)
            .ThenByDescending(static peak => peak.TurningAngleDegrees)
            .ToList();

        var minimumSampleSeparation = Math.Max(8, samples.Count / 10);
        var selectedPeaks = new List<BoundaryCornerPeak>(4);
        foreach (var peak in localPeaks)
        {
            if (selectedPeaks.Any(existing =>
                    ComputeCircularSampleDistance(existing.SampleIndex, peak.SampleIndex, samples.Count) < minimumSampleSeparation))
            {
                continue;
            }

            selectedPeaks.Add(peak);
            corners.Add(new BoundaryCornerRef(
                -1,
                peak.Point,
                peak.Parameter,
                peak.RadiusToCentroid,
                peak.TurningAngleDegrees));
            if (corners.Count == 4)
                break;
        }

        if (corners.Count != 4)
        {
            failureReason = $"intrinsic_corner_count:{corners.Count}";
            corners.Clear();
            return false;
        }

        corners.Sort(static (a, b) => a.Parameter.CompareTo(b.Parameter));
        return true;
    }

    private static int[]? FindBestCornerAssignment(
        IReadOnlyList<BoundaryCornerRef> detectedCorners,
        IReadOnlyList<Point3d> provisionalCorners)
    {
        if (detectedCorners.Count != 4 || provisionalCorners.Count != 4)
            return null;

        var permutations = new[]
        {
            new[] { 0, 1, 2, 3 },
            new[] { 0, 1, 3, 2 },
            new[] { 0, 2, 1, 3 },
            new[] { 0, 2, 3, 1 },
            new[] { 0, 3, 1, 2 },
            new[] { 0, 3, 2, 1 },
            new[] { 1, 0, 2, 3 },
            new[] { 1, 0, 3, 2 },
            new[] { 1, 2, 0, 3 },
            new[] { 1, 2, 3, 0 },
            new[] { 1, 3, 0, 2 },
            new[] { 1, 3, 2, 0 },
            new[] { 2, 0, 1, 3 },
            new[] { 2, 0, 3, 1 },
            new[] { 2, 1, 0, 3 },
            new[] { 2, 1, 3, 0 },
            new[] { 2, 3, 0, 1 },
            new[] { 2, 3, 1, 0 },
            new[] { 3, 0, 1, 2 },
            new[] { 3, 0, 2, 1 },
            new[] { 3, 1, 0, 2 },
            new[] { 3, 1, 2, 0 },
            new[] { 3, 2, 0, 1 },
            new[] { 3, 2, 1, 0 }
        };

        int[]? best = null;
        double bestScore = double.PositiveInfinity;
        foreach (var permutation in permutations)
        {
            var score = 0.0;
            for (int i = 0; i < permutation.Length; i++)
                score += provisionalCorners[i].DistanceTo(detectedCorners[permutation[i]].Point);

            if (score < bestScore)
            {
                bestScore = score;
                best = permutation;
            }
        }

        return best;
    }

    private static BoundaryCornerRef SelectStableBoundarySeamCorner(
        IReadOnlyList<BoundaryCornerRef> corners,
        Point3d centroid)
    {
        return corners
            .OrderBy(corner => Math.Atan2(corner.Point.Y - centroid.Y, corner.Point.X - centroid.X))
            .ThenBy(static corner => corner.Point.X)
            .ThenBy(static corner => corner.Point.Y)
            .First();
    }

    private static int ComputeCircularSampleDistance(
        int sampleIndexA,
        int sampleIndexB,
        int sampleCount)
    {
        var raw = Math.Abs(sampleIndexA - sampleIndexB);
        var wrapped = sampleCount - raw;
        return Math.Min(raw, wrapped);
    }

    private static Point3d? ComputeKeepSidePickPoint(
        Curve segment,
        double tolerance)
    {
        var segmentLength = segment.GetLength();
        if (segmentLength <= tolerance * 10.0)
            return null;

        if (!segment.LengthParameter(segmentLength * 0.5, out var midpointParameter))
            return null;

        return segment.PointAt(midpointParameter);
    }

    private static bool TryComputeBoundarySegmentTrimInterval(
        Curve segment,
        double startParameter,
        double endParameter,
        double tolerance,
        out double trimStart,
        out double trimEnd)
    {
        trimStart = Math.Max(segment.Domain.Min, Math.Min(segment.Domain.Max, Math.Min(startParameter, endParameter)));
        trimEnd = Math.Max(segment.Domain.Min, Math.Min(segment.Domain.Max, Math.Max(startParameter, endParameter)));
        if (trimEnd - trimStart <= tolerance)
            return false;

        using var retainedSegment = segment.Trim(trimStart, trimEnd);
        return retainedSegment != null && retainedSegment.GetLength() > tolerance * 8.0;
    }

    private static bool IsBoundaryAssemblyEndpointMatch(
        Point3d actual,
        Point3d expected,
        double tolerance) =>
        actual.DistanceTo(expected) <= tolerance * 40.0;

    private static bool TryValidateFilletedBoundaryCurve(
        Curve curve,
        Curve sharpBoundary,
        IReadOnlyList<BoundaryCornerFilletResult> cornerResults,
        Point3d requiredPoint,
        double minimumArea,
        double tolerance,
        out double area)
    {
        area = 0.0;
        if (!curve.IsClosed)
            return false;

        var containment = curve.Contains(requiredPoint, Plane.WorldXY, tolerance);
        if (containment != PointContainment.Inside && containment != PointContainment.Coincident)
            return false;

        var selfIntersections = global::Rhino.Geometry.Intersect.Intersection.CurveSelf(curve, tolerance);
        if (selfIntersections != null && selfIntersections.Count > 0)
            return false;

        using var massProps = AreaMassProperties.Compute(curve);
        area = Math.Abs(massProps?.Area ?? 0.0);
        if (area <= minimumArea)
            return false;

        foreach (var corner in cornerResults)
        {
            var cornerContainment = curve.Contains(corner.CornerPoint, Plane.WorldXY, tolerance);
            if (cornerContainment != PointContainment.Inside && cornerContainment != PointContainment.Coincident)
                return false;

            var arcMidpoint = corner.ArcCurve.PointAtNormalizedLength(0.5);
            var finalArcContainment = curve.Contains(arcMidpoint, Plane.WorldXY, tolerance);
            if (finalArcContainment != PointContainment.Inside && finalArcContainment != PointContainment.Coincident)
                return false;

            var sharpArcContainment = sharpBoundary.Contains(arcMidpoint, Plane.WorldXY, tolerance);
            if (sharpArcContainment == PointContainment.Inside || sharpArcContainment == PointContainment.Coincident)
                return false;
        }

        return true;
    }

    private static Curve? BuildExteriorCrossBoundaryLoop(
        IReadOnlyList<ApproachEdgeSegment> approachEdges,
        IntersectionRealizationRequest request,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double tolerance,
        out List<OuterCornerFillet> cornerFillets,
        out List<OuterCornerAttempt> cornerAttempts)
    {
        cornerFillets = new List<OuterCornerFillet>();
        cornerAttempts = new List<OuterCornerAttempt>();
        if (approachEdges.Count == 0 || request.CornerPairings.ValueKind != JsonValueKind.Array)
            return null;

        var edgeLookup = approachEdges
            .GroupBy(edge => BuildApproachEdgeArmKey(edge.Road, edge.ArmDirection, edge.Side))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(edge => edge.Curve.GetLength())
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        cornerFillets = BuildOuterCornerFillets(edgeLookup, request, coarseRegionCurves, tolerance, out cornerAttempts);
        if (cornerFillets.Count < 2)
            return null;

        var pieces = new List<Curve>();
        try
        {
            for (int i = 0; i < cornerFillets.Count; i++)
            {
                var currentCorner = cornerFillets[i];
                var nextCorner = cornerFillets[(i + 1) % cornerFillets.Count];

                if (!edgeLookup.TryGetValue(BuildApproachEdgeArmKey(
                        currentCorner.CurrentEdge.Road,
                        currentCorner.CurrentEdge.ArmDirection,
                        currentCorner.CurrentEdge.Side), out var startSource))
                {
                    return null;
                }

                if (!edgeLookup.TryGetValue(BuildApproachEdgeArmKey(
                        nextCorner.NextEdge.Road,
                        nextCorner.NextEdge.ArmDirection,
                        nextCorner.NextEdge.Side), out var endSource))
                {
                    return null;
                }

                using var startTrim = TrimCurveFromParameterToEnd(startSource.Curve, currentCorner.CurrentParameter);
                using var endTrimBase = TrimCurveFromParameterToEnd(endSource.Curve, nextCorner.NextParameter);
                if (startTrim == null || endTrimBase == null)
                {
                    return null;
                }

                if (startTrim.GetLength() <= tolerance * 4.0 || endTrimBase.GetLength() <= tolerance * 4.0)
                    return null;

                var endTrim = endTrimBase.DuplicateCurve();
                if (endTrim == null)
                    return null;

                endTrim.Reverse();
                var farCap = new LineCurve(startTrim.PointAtEnd, endTrim.PointAtStart);
                var nextFillet = nextCorner.ArcCurve.DuplicateCurve();
                if (nextFillet == null)
                {
                    return null;
                }

                OrientCurveToStartNear(nextFillet, endTrim.PointAtEnd);

                pieces.Add(startTrim.DuplicateCurve());
                pieces.Add(farCap);
                pieces.Add(endTrim);
                pieces.Add(nextFillet);
            }

            var joined = Curve.JoinCurves(pieces, tolerance * 8.0);
            if (joined == null || joined.Length == 0)
                return null;

            Curve? best = null;
            double bestArea = double.NegativeInfinity;
            foreach (var joinedCurve in joined)
            {
                if (joinedCurve == null)
                    continue;

                if (!joinedCurve.IsClosed)
                {
                    joinedCurve.Dispose();
                    continue;
                }

                using var massProps = AreaMassProperties.Compute(joinedCurve);
                var area = massProps?.Area ?? 0.0;
                if (area > bestArea)
                {
                    best?.Dispose();
                    best = joinedCurve;
                    bestArea = area;
                }
                else
                {
                    joinedCurve.Dispose();
                }
            }

            return best;
        }
        finally
        {
            foreach (var piece in pieces)
                piece.Dispose();
        }
    }

    private static List<OuterCornerFillet> BuildOuterCornerFillets(
        IReadOnlyDictionary<string, ApproachEdgeSegment> edgeLookup,
        IntersectionRealizationRequest request,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double tolerance,
        out List<OuterCornerAttempt> attempts)
    {
        attempts = new List<OuterCornerAttempt>();
        var fillets = new List<OuterCornerFillet>();
        var orderedArms = BuildOrderedOuterArms(edgeLookup, tolerance);
        if (orderedArms.Count < 2)
            return fillets;

        var radii = request.AnalysisGeometry2D.CurbReturnArcs
            .Where(static arc => arc.Radius > 0.0)
            .OrderBy(static arc => arc.CornerOrder ?? int.MaxValue)
            .Select(static arc => arc.Radius)
            .ToList();

        for (int cornerOrder = 0; cornerOrder < orderedArms.Count; cornerOrder++)
        {
            var currentArm = orderedArms[cornerOrder];
            var nextArm = orderedArms[(cornerOrder + 1) % orderedArms.Count];
            var requestedRadius = radii.Count == 0
                ? 0.0
                : radii[Math.Min(cornerOrder, radii.Count - 1)];

            if (TryBuildOuterCornerFillet(
                    edgeLookup,
                    cornerOrder,
                    currentArm,
                    nextArm,
                    requestedRadius,
                    coarseRegionCurves,
                    tolerance,
                    out var fillet,
                    out var attempt))
            {
                fillets.Add(fillet);
            }

            attempts.Add(attempt);
        }

        return fillets;
    }

    private static bool TryBuildOuterCornerFillet(
        IReadOnlyDictionary<string, ApproachEdgeSegment> edgeLookup,
        int cornerOrder,
        OrderedOuterArm currentArm,
        OrderedOuterArm nextArm,
        double requestedRadius,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double tolerance,
        out OuterCornerFillet fillet,
        out OuterCornerAttempt attempt)
    {
        fillet = null!;
        var currentEdge = currentArm.RightEdge;
        var nextEdge = nextArm.LeftEdge;

        if (!(requestedRadius > tolerance))
        {
            attempt = new OuterCornerAttempt(
                cornerOrder,
                false,
                "missing_requested_radius",
                currentEdge,
                nextEdge,
                0.0,
                null,
                null);
            return false;
        }

        edgeLookup.TryGetValue(BuildApproachEdgeArmKey(currentEdge.Road, currentEdge.ArmDirection, currentEdge.Side), out var currentSource);
        edgeLookup.TryGetValue(BuildApproachEdgeArmKey(nextEdge.Road, nextEdge.ArmDirection, nextEdge.Side), out var nextSource);
        if (currentSource == null || nextSource == null)
        {
            attempt = new OuterCornerAttempt(
                cornerOrder,
                false,
                "missing_edge_lookup",
                currentEdge,
                nextEdge,
                requestedRadius,
                null,
                ComputeOuterCornerAttemptPoint(currentSource?.Curve, nextSource?.Curve));
            return false;
        }

        if (!TryResolveOuterFillet(
                currentSource,
                nextSource,
                ComputeOuterCornerAttemptPoint(currentSource.Curve, nextSource.Curve) ?? Point3d.Origin,
                requestedRadius,
                coarseRegionCurves,
                tolerance,
                out var candidates,
                out var resolvedRadius,
                out var currentParameter,
                out var nextParameter,
                out var arcCurve,
                out var failureReason))
        {
            attempt = new OuterCornerAttempt(
                cornerOrder,
                false,
                failureReason,
                currentEdge,
                nextEdge,
                requestedRadius,
                resolvedRadius > tolerance ? resolvedRadius : null,
                ComputeOuterCornerAttemptPoint(currentSource.Curve, nextSource.Curve),
                candidates);
            return false;
        }

        fillet = new OuterCornerFillet(
            cornerOrder,
            currentEdge,
            nextEdge,
            resolvedRadius,
            currentParameter,
            nextParameter,
            arcCurve);
        attempt = new OuterCornerAttempt(
            cornerOrder,
            true,
            "ok",
            currentEdge,
            nextEdge,
            requestedRadius,
            resolvedRadius,
            arcCurve.PointAtNormalizedLength(0.5),
            candidates);
        return true;
    }

    private static bool TryResolveOuterFillet(
        ApproachEdgeSegment currentSource,
        ApproachEdgeSegment nextSource,
        Point3d cornerHint,
        double requestedRadius,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double tolerance,
        out List<OuterCornerCandidate> candidates,
        out double resolvedRadius,
        out double currentParameter,
        out double nextParameter,
        out Curve arcCurve,
        out string failureReason)
    {
        candidates = new List<OuterCornerCandidate>();
        resolvedRadius = 0.0;
        currentParameter = 0.0;
        nextParameter = 0.0;
        arcCurve = null!;
        failureReason = "unknown";

        var currentCurve = currentSource.Curve;
        var nextCurve = nextSource.Curve;
        var currentSourceCurve = currentSource.SourceCurve;
        var nextSourceCurve = nextSource.SourceCurve;

        if (!(requestedRadius > tolerance))
        {
            failureReason = "radius_below_tolerance";
            return false;
        }

        var angleTolerance = RhinoMath.ToRadians(1.0);
        var tryRadius = requestedRadius;
        var lastResolvedRadius = 0.0;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            lastResolvedRadius = tryRadius;

            // ─── Analytic path: classical offset-line arc construction ───
            if (TryBuildAnalyticFilletArc(
                    currentCurve,
                    currentSourceCurve,
                    nextCurve,
                    nextSourceCurve,
                    cornerHint,
                    tryRadius,
                    tolerance,
                    out var analyticArc,
                    out var analyticCurrentParam,
                    out var analyticNextParam,
                    out var analyticFailure))
            {
                using var analyticCurrentTrim = TrimCurveFromParameterToEnd(currentCurve, analyticCurrentParam);
                using var analyticNextTrim = TrimCurveFromParameterToEnd(nextCurve, analyticNextParam);
                if (analyticCurrentTrim != null && analyticNextTrim != null
                    && analyticCurrentTrim.GetLength() > tolerance * 4.0
                    && analyticNextTrim.GetLength() > tolerance * 4.0)
                {
                    resolvedRadius = tryRadius;
                    currentParameter = analyticCurrentParam;
                    nextParameter = analyticNextParam;
                    arcCurve = analyticArc;
                    return true;
                }

                analyticFailure = "insufficient_retained_length";
                var debugAnalyticArc = analyticArc.DuplicateCurve();
                analyticArc.Dispose();
                if (debugAnalyticArc != null)
                    candidates.Add(new OuterCornerCandidate(
                        $"analytic:r={tryRadius:F3}",
                        null,
                        "analytic_insufficient_retained_length",
                        debugAnalyticArc));
            }

            // ─── Fallback: CreateFilletCurves-based path ───
            if (!TryCreateClassifiedFilletArc(
                    currentCurve,
                    currentSourceCurve,
                    nextCurve,
                    nextSourceCurve,
                    cornerHint,
                    tryRadius,
                    coarseRegionCurves,
                    tolerance,
                    angleTolerance,
                    out var attemptCandidates,
                    out var bestArc))
            {
                candidates.AddRange(attemptCandidates);
                failureReason = attempt < 5
                    ? "no_connecting_arc_piece"
                    : $"analytic:{analyticFailure ?? "n/a"}|fallback:no_connecting_arc_piece";
                tryRadius *= 0.8;
                continue;
            }

            candidates.AddRange(attemptCandidates);

            if (!TryOrientArcToSourceCurves(
                    bestArc,
                    currentSourceCurve,
                    nextSourceCurve,
                    tolerance,
                    out _,
                    out _))
            {
                bestArc.Dispose();
                failureReason = "arc_not_on_source_curves";
                tryRadius *= 0.8;
                continue;
            }

            if (!currentCurve.ClosestPoint(bestArc.PointAtStart, out var candidateCurrentParameter)
                || !nextCurve.ClosestPoint(bestArc.PointAtEnd, out var candidateNextParameter))
            {
                bestArc.Dispose();
                failureReason = "localized_curve_projection_failed";
                tryRadius *= 0.8;
                continue;
            }

            using var currentTrim = TrimCurveFromParameterToEnd(currentCurve, candidateCurrentParameter);
            using var nextTrim = TrimCurveFromParameterToEnd(nextCurve, candidateNextParameter);
            if (currentTrim == null || nextTrim == null
                || currentTrim.GetLength() <= tolerance * 4.0
                || nextTrim.GetLength() <= tolerance * 4.0)
            {
                bestArc.Dispose();
                failureReason = "insufficient_retained_length";
                tryRadius *= 0.8;
                continue;
            }

            resolvedRadius = tryRadius;
            currentParameter = candidateCurrentParameter;
            nextParameter = candidateNextParameter;
            arcCurve = bestArc;
            return true;
        }

        resolvedRadius = lastResolvedRadius;
        return false;
    }

    /// <summary>
    /// Constructs a fillet arc analytically using classical 2D offset-line intersection.
    /// Given two road boundary curves that cross near a corner, offsets each by the fillet
    /// radius toward the mouth interior, intersects the offsets to find the arc center,
    /// projects back to get tangent points, and builds the arc directly.
    /// This bypasses CreateFilletCurves entirely.
    /// </summary>
    private static bool TryBuildAnalyticFilletArc(
        Curve currentCurve,
        Curve currentSourceCurve,
        Curve nextCurve,
        Curve nextSourceCurve,
        Point3d cornerHint,
        double radius,
        double tolerance,
        out Curve arcCurve,
        out double currentParameter,
        out double nextParameter,
        out string failureReason)
    {
        arcCurve = null!;
        currentParameter = 0.0;
        nextParameter = 0.0;
        failureReason = "unknown";

        using var currentLocal = BuildLocalFilletSourceCurve(
            currentCurve,
            currentSourceCurve,
            nextSourceCurve,
            cornerHint,
            radius,
            tolerance);
        using var nextLocal = BuildLocalFilletSourceCurve(
            nextCurve,
            nextSourceCurve,
            currentSourceCurve,
            cornerHint,
            radius,
            tolerance);
        if (currentLocal == null || nextLocal == null)
        {
            failureReason = "local_source_build_failed";
            return false;
        }

        if (!TryResolveLocalCornerCrossing(
                currentLocal,
                nextLocal,
                cornerHint,
                tolerance,
                out var crossPoint,
                out var crossParamCurrent,
                out var crossParamNext))
        {
            failureReason = "local_crossing_failed";
            return false;
        }

        var tangentCurrent = currentLocal.TangentAt(crossParamCurrent);
        var tangentNext = nextLocal.TangentAt(crossParamNext);
        tangentCurrent.Z = 0.0;
        tangentNext.Z = 0.0;
        if (!tangentCurrent.Unitize() || !tangentNext.Unitize())
        {
            failureReason = "tangent_degenerate";
            return false;
        }

        var outwardVecCurrent = currentLocal.PointAtEnd - crossPoint;
        outwardVecCurrent.Z = 0.0;
        if (outwardVecCurrent * tangentCurrent < 0)
            tangentCurrent.Reverse();

        var outwardVecNext = nextLocal.PointAtEnd - crossPoint;
        outwardVecNext.Z = 0.0;
        if (outwardVecNext * tangentNext < 0)
            tangentNext.Reverse();

        var angle = Vector3d.VectorAngle(tangentCurrent, tangentNext, Plane.WorldXY);
        if (!double.IsFinite(angle) || angle <= RhinoMath.ToRadians(1.0) || angle >= Math.PI - RhinoMath.ToRadians(1.0))
        {
            failureReason = $"invalid_corner_angle:{angle:F6}";
            return false;
        }

        var tangentDistance = radius / Math.Tan(angle * 0.5);
        var centerDistance = radius / Math.Sin(angle * 0.5);
        if (!double.IsFinite(tangentDistance) || !double.IsFinite(centerDistance)
            || tangentDistance <= tolerance || centerDistance <= tolerance)
        {
            failureReason = $"invalid_fillet_distances:t={tangentDistance:F6},c={centerDistance:F6}";
            return false;
        }

        var tangentPointCurrent = crossPoint + tangentCurrent * tangentDistance;
        var tangentPointNext = crossPoint + tangentNext * tangentDistance;

        var bisector = tangentCurrent + tangentNext;
        if (!bisector.Unitize())
        {
            failureReason = "cannot_determine_bisector";
            return false;
        }

        var center = crossPoint + bisector * centerDistance;
        var centerCurrentDist = center.DistanceTo(tangentPointCurrent);
        var centerNextDist = center.DistanceTo(tangentPointNext);
        var radiusTolerance = Math.Max(tolerance * 20.0, radius * 0.25);
        if (Math.Abs(centerCurrentDist - radius) > radiusTolerance
            || Math.Abs(centerNextDist - radius) > radiusTolerance)
        {
            failureReason = $"radius_mismatch:r_cur={centerCurrentDist:F4},r_nxt={centerNextDist:F4},target={radius:F4}";
            return false;
        }

        if (!currentCurve.ClosestPoint(tangentPointCurrent, out currentParameter)
            || !nextCurve.ClosestPoint(tangentPointNext, out nextParameter))
        {
            failureReason = "tangent_projection_to_localized_curves_failed";
            return false;
        }

        var currentProjectionPoint = currentCurve.PointAt(currentParameter);
        var nextProjectionPoint = nextCurve.PointAt(nextParameter);
        var currentProjectionDist = tangentPointCurrent.DistanceTo(currentProjectionPoint);
        var nextProjectionDist = tangentPointNext.DistanceTo(nextProjectionPoint);
        var projectionTolerance = Math.Max(radius * 3.0, tolerance * 100.0);
        if (currentProjectionDist > projectionTolerance || nextProjectionDist > projectionTolerance)
        {
            failureReason = $"tangent_far_from_localized:d_cur={currentProjectionDist:F4},d_nxt={nextProjectionDist:F4}";
            return false;
        }

        tangentPointCurrent = new Point3d(currentProjectionPoint.X, currentProjectionPoint.Y, 0.0);
        tangentPointNext = new Point3d(nextProjectionPoint.X, nextProjectionPoint.Y, 0.0);
        var flatCenter = new Point3d(center.X, center.Y, 0.0);

        var vecToCurrent = tangentPointCurrent - flatCenter;
        var vecToNext = tangentPointNext - flatCenter;
        vecToCurrent.Z = 0.0;
        vecToNext.Z = 0.0;
        var midVec = vecToCurrent + vecToNext;
        if (!midVec.Unitize())
        {
            failureReason = "cannot_determine_arc_midpoint_direction";
            return false;
        }

        var midPoint = flatCenter + midVec * radius;
        var arc = new Arc(tangentPointCurrent, midPoint, tangentPointNext);
        if (!arc.IsValid)
        {
            failureReason = "arc_construction_invalid";
            return false;
        }

        arcCurve = new ArcCurve(arc);
        return true;
    }

    private static bool TryCreateClassifiedFilletArc(
        Curve currentLocalizedCurve,
        Curve currentSourceCurve,
        Curve nextLocalizedCurve,
        Curve nextSourceCurve,
        Point3d cornerHint,
        double radius,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double tolerance,
        double angleTolerance,
        out List<OuterCornerCandidate> candidates,
        out Curve arcCurve)
    {
        candidates = new List<OuterCornerCandidate>();
        arcCurve = null!;

        using var currentLocalSource = BuildLocalFilletSourceCurve(
            currentLocalizedCurve,
            currentSourceCurve,
            nextSourceCurve,
            cornerHint,
            radius,
            tolerance);
        using var nextLocalSource = BuildLocalFilletSourceCurve(
            nextLocalizedCurve,
            nextSourceCurve,
            currentSourceCurve,
            cornerHint,
            radius,
            tolerance);
        if (currentLocalSource == null || nextLocalSource == null)
            return false;

        var currentPickPoints = ComputeFilletPickPointOptions(
            currentLocalizedCurve,
            currentLocalSource,
            nextLocalSource,
            cornerHint,
            radius,
            tolerance);
        var nextPickPoints = ComputeFilletPickPointOptions(
            nextLocalizedCurve,
            nextLocalSource,
            currentLocalSource,
            cornerHint,
            radius,
            tolerance);

        Curve? bestArc = null;
        double bestScore = double.NegativeInfinity;

        foreach (var currentPickPoint in currentPickPoints)
        {
            foreach (var nextPickPoint in nextPickPoints)
            {
                var filletPieces = Curve.CreateFilletCurves(
                    currentLocalSource,
                    currentPickPoint.Point,
                    nextLocalSource,
                    nextPickPoint.Point,
                    radius,
                    true,
                    true,
                    true,
                    tolerance,
                    angleTolerance);
                if (filletPieces == null || filletPieces.Length == 0)
                    continue;

                try
                {
                    foreach (var candidateArc in ExtractCandidateFilletArcs(filletPieces))
                    {
                        using var rawCandidate = candidateArc.Curve;
                        if (rawCandidate == null)
                            continue;

                        var label = $"{currentPickPoint.Label}->{nextPickPoint.Label}:{candidateArc.Label}";
                        if (!TryExtractLocalConnectingPiece(
                                candidateArc.Label,
                                rawCandidate,
                                currentLocalSource,
                                nextLocalSource,
                                cornerHint,
                                radius,
                                tolerance,
                                out var localCandidate))
                        {
                            continue;
                        }

                        using var candidate = localCandidate;
                        if (!TryOrientArcToSourceCurves(
                                candidate,
                                currentLocalSource,
                                nextLocalSource,
                                tolerance,
                                out _,
                                out _))
                        {
                            var rejected = candidate.DuplicateCurve();
                            if (rejected != null)
                                candidates.Add(new OuterCornerCandidate(label, null, "arc_not_on_source_curves", rejected));
                            continue;
                        }

                        var score = ScoreFilletArcAgainstInterior(candidate, coarseRegionCurves, radius, tolerance);
                        var debugCurve = candidate.DuplicateCurve();
                        if (debugCurve != null)
                        {
                            var reason = score <= double.NegativeInfinity
                                ? "interior_classifier_rejected"
                                : "candidate";
                            candidates.Add(new OuterCornerCandidate(label, score, reason, debugCurve));
                        }

                        if (score <= double.NegativeInfinity)
                            continue;

                        if (score > bestScore)
                        {
                            bestArc?.Dispose();
                            bestArc = candidate.DuplicateCurve();
                            bestScore = score;
                        }
                    }
                }
                finally
                {
                    foreach (var piece in filletPieces)
                        piece?.Dispose();
                }
            }
        }

        if (bestArc == null)
            return false;

        arcCurve = bestArc;
        return true;
    }

    private static IEnumerable<FilletArcPiece> ExtractCandidateFilletArcs(Curve[] filletPieces)
    {
        if (filletPieces.Length >= 2 && filletPieces[1] != null)
        {
            var preferred = filletPieces[1].DuplicateCurve();
            if (preferred != null)
                yield return new FilletArcPiece("piece-1", preferred);
        }

        for (int i = 0; i < filletPieces.Length; i++)
        {
            if (i == 1)
                continue;

            var piece = filletPieces[i];
            if (piece == null)
                continue;

            var duplicate = piece.DuplicateCurve();
            if (duplicate != null)
                yield return new FilletArcPiece($"piece-{i}", duplicate);
        }
    }

    private static bool TryExtractLocalConnectingPiece(
        string candidateLabel,
        Curve candidateCurve,
        Curve currentSourceCurve,
        Curve nextSourceCurve,
        Point3d cornerHint,
        double radius,
        double tolerance,
        out Curve localPiece)
    {
        localPiece = null!;

        var maxLocalLength = Math.Max(radius * 4.0, tolerance * 200.0);
        if (candidateCurve.GetLength() <= maxLocalLength
            && TryOrientArcToSourceCurves(
                candidateCurve,
                currentSourceCurve,
                nextSourceCurve,
                tolerance,
                out _,
                out _))
        {
            if (candidateLabel.Equals("piece-1", StringComparison.OrdinalIgnoreCase)
                || candidateCurve.PointAtNormalizedLength(0.5).DistanceTo(cornerHint) <= maxLocalLength)
            {
                var direct = candidateCurve.DuplicateCurve();
                if (direct != null)
                {
                    localPiece = direct;
                    return true;
                }
            }
        }

        if (!candidateCurve.ClosestPoint(cornerHint, out var centerParameter))
            return false;

        var currentParameters = CollectCandidateIntersectionParameters(candidateCurve, currentSourceCurve, tolerance);
        var nextParameters = CollectCandidateIntersectionParameters(candidateCurve, nextSourceCurve, tolerance);
        if (currentParameters.Count == 0 || nextParameters.Count == 0)
            return false;

        Curve? bestPiece = null;
        double bestScore = double.PositiveInfinity;

        foreach (var currentParameter in currentParameters)
        {
            foreach (var nextParameter in nextParameters)
            {
                if (Math.Abs(currentParameter - nextParameter) <= tolerance)
                    continue;

                var start = Math.Min(currentParameter, nextParameter);
                var end = Math.Max(currentParameter, nextParameter);
                var trimmed = candidateCurve.Trim(start, end);
                if (trimmed == null)
                    continue;

                var disposeTrimmed = true;
                try
                {
                    var length = trimmed.GetLength();
                    if (!(length > tolerance * 4.0) || length > maxLocalLength)
                        continue;

                    if (!trimmed.ClosestPoint(cornerHint, out var closestParameter))
                        continue;

                    var closestPoint = trimmed.PointAt(closestParameter);
                    var centerDistance = closestPoint.DistanceTo(cornerHint);
                    if (centerDistance > maxLocalLength)
                        continue;

                    var score = centerDistance + length;
                    if (score >= bestScore)
                        continue;

                    bestPiece?.Dispose();
                    bestPiece = trimmed;
                    bestScore = score;
                    disposeTrimmed = false;
                }
                finally
                {
                    if (disposeTrimmed)
                        trimmed.Dispose();
                }
            }
        }

        if (bestPiece == null)
            return false;

        localPiece = bestPiece;
        return true;
    }

    private static Curve? BuildLocalFilletSourceCurve(
        Curve localizedCurve,
        Curve sourceCurve,
        Curve otherSourceCurve,
        Point3d fallbackHint,
        double radius,
        double tolerance)
    {
        if (!sourceCurve.ClosestPoint(localizedCurve.PointAtEnd, out var outwardParameter))
            return null;

        var intersections = global::Rhino.Geometry.Intersect.Intersection.CurveCurve(
            sourceCurve,
            otherSourceCurve,
            tolerance,
            tolerance);

        double crossingParameter = outwardParameter;
        if (intersections != null && intersections.Count > 0)
        {
            var best = intersections
                .OrderBy(evt => evt.PointA.DistanceTo(fallbackHint))
                .First();
            crossingParameter = best.ParameterA;
        }
        else if (sourceCurve.ClosestPoint(fallbackHint, out var fallbackParameter))
        {
            crossingParameter = fallbackParameter;
        }

        var directionSign = outwardParameter >= crossingParameter ? 1.0 : -1.0;
        var inwardLength = Math.Max(radius * 0.5, tolerance * 50.0);
        var outwardLength = Math.Max(radius * 6.0, localizedCurve.GetLength() * 0.6);

        var inwardPoint = AdvanceCurvePointByLength(sourceCurve, crossingParameter, -directionSign * inwardLength, tolerance)
            ?? sourceCurve.PointAt(crossingParameter);
        var outwardPoint = AdvanceCurvePointByLength(sourceCurve, crossingParameter, directionSign * outwardLength, tolerance)
            ?? sourceCurve.PointAt(crossingParameter);

        if (!sourceCurve.ClosestPoint(inwardPoint, out var startParameter)
            || !sourceCurve.ClosestPoint(outwardPoint, out var endParameter))
        {
            return null;
        }

        var trimStart = Math.Min(startParameter, endParameter);
        var trimEnd = Math.Max(startParameter, endParameter);
        var local = sourceCurve.Trim(trimStart, trimEnd);
        if (local == null)
            return null;

        var crossingPoint = sourceCurve.PointAt(crossingParameter);
        OrientCurveToStartNear(local, crossingPoint);
        return local;
    }

    private static bool TryResolveLocalCornerCrossing(
        Curve currentLocal,
        Curve nextLocal,
        Point3d cornerHint,
        double tolerance,
        out Point3d crossPoint,
        out double currentParameter,
        out double nextParameter)
    {
        crossPoint = Point3d.Unset;
        currentParameter = 0.0;
        nextParameter = 0.0;

        var intersections = global::Rhino.Geometry.Intersect.Intersection.CurveCurve(
            currentLocal,
            nextLocal,
            tolerance,
            tolerance);
        if (intersections != null && intersections.Count > 0)
        {
            var best = intersections
                .OrderBy(evt => evt.PointA.DistanceTo(cornerHint))
                .First();
            crossPoint = best.PointA;
            currentParameter = best.ParameterA;
            nextParameter = best.ParameterB;
            return true;
        }

        var currentStart = currentLocal.PointAtStart;
        var nextStart = nextLocal.PointAtStart;
        var currentTangent = currentLocal.TangentAtStart;
        var nextTangent = nextLocal.TangentAtStart;
        currentTangent.Z = 0.0;
        nextTangent.Z = 0.0;
        if (!currentTangent.Unitize() || !nextTangent.Unitize())
            return false;

        if (!TryIntersectLines2D(currentStart, currentTangent, nextStart, nextTangent, out crossPoint))
            return false;

        currentParameter = currentLocal.Domain.Min;
        nextParameter = nextLocal.Domain.Min;
        return true;
    }

    private static List<OrderedOuterArm> BuildOrderedOuterArms(
        IReadOnlyDictionary<string, ApproachEdgeSegment> edgeLookup,
        double tolerance)
    {
        return edgeLookup.Values
            .GroupBy(edge => (Road: edge.Road.ToUpperInvariant(), Arm: edge.ArmDirection.ToUpperInvariant()))
            .Select(group =>
            {
                var left = group.FirstOrDefault(edge => edge.Side.Equals("left", StringComparison.OrdinalIgnoreCase));
                var right = group.FirstOrDefault(edge => edge.Side.Equals("right", StringComparison.OrdinalIgnoreCase));
                if (left == null || right == null)
                    return null;

                if (!TryGetCurveStartDirection(left.Curve, tolerance, out var leftDirection)
                    || !TryGetCurveStartDirection(right.Curve, tolerance, out var rightDirection))
                {
                    return null;
                }

                var armDirection = leftDirection + rightDirection;
                if (!armDirection.Unitize())
                    armDirection = leftDirection;

                var angle = Math.Atan2(armDirection.Y, armDirection.X);
                return new OrderedOuterArm(
                    angle,
                    new ArmEdgeRef(group.Key.Road, "left", group.Key.Arm),
                    new ArmEdgeRef(group.Key.Road, "right", group.Key.Arm));
            })
            .Where(static arm => arm != null)
            .Cast<OrderedOuterArm>()
            .OrderByDescending(static arm => arm.Angle)
            .ToList();
    }

    private static bool TryIntersectLines2D(
        Point3d pointA,
        Vector3d directionA,
        Point3d pointB,
        Vector3d directionB,
        out Point3d intersection)
    {
        intersection = Point3d.Unset;

        var det = directionA.X * (-directionB.Y) - directionA.Y * (-directionB.X);
        if (Math.Abs(det) <= RhinoMath.ZeroTolerance)
            return false;

        var dx = pointB.X - pointA.X;
        var dy = pointB.Y - pointA.Y;
        var t = (dx * (-directionB.Y) - dy * (-directionB.X)) / det;
        intersection = new Point3d(
            pointA.X + t * directionA.X,
            pointA.Y + t * directionA.Y,
            0.0);
        return true;
    }

    private static List<double> CollectCandidateIntersectionParameters(
        Curve candidateCurve,
        Curve sourceCurve,
        double tolerance)
    {
        var parameters = new List<double>();
        var intersections = global::Rhino.Geometry.Intersect.Intersection.CurveCurve(
            candidateCurve,
            sourceCurve,
            tolerance,
            tolerance);
        if (intersections == null)
            return parameters;

        foreach (var intersection in intersections)
        {
            parameters.Add(intersection.ParameterA);
        }

        return parameters
            .OrderBy(static value => value)
            .DistinctBy(value => Math.Round(value, 6))
            .ToList();
    }

    private static List<FilletPickPoint> ComputeFilletPickPointOptions(
        Curve localizedCurve,
        Curve sourceCurve,
        Curve otherSourceCurve,
        Point3d fallbackHint,
        double requestedRadius,
        double tolerance)
    {
        var picks = new List<FilletPickPoint>();

        if (!sourceCurve.ClosestPoint(localizedCurve.PointAtEnd, out var outwardParameter))
            return picks;

        var intersections = global::Rhino.Geometry.Intersect.Intersection.CurveCurve(
            sourceCurve,
            otherSourceCurve,
            tolerance,
            tolerance);

        double crossingParameter = outwardParameter;
        if (intersections != null && intersections.Count > 0)
        {
            var best = intersections
                .OrderBy(evt => evt.PointA.DistanceTo(fallbackHint))
                .First();
            crossingParameter = best.ParameterA;
        }
        else if (sourceCurve.ClosestPoint(fallbackHint, out var fallbackParameter))
        {
            crossingParameter = fallbackParameter;
        }

        var directionSign = outwardParameter >= crossingParameter ? 1.0 : -1.0;
        var stepLength = Math.Max(
            tolerance * 20.0,
            Math.Min(requestedRadius * 0.5, localizedCurve.GetLength() * 0.4));

        var outwardPoint = AdvanceCurvePointByLength(sourceCurve, crossingParameter, directionSign * stepLength, tolerance);
        if (outwardPoint.HasValue)
            picks.Add(new FilletPickPoint("outward", outwardPoint.Value));

        var inwardPoint = AdvanceCurvePointByLength(sourceCurve, crossingParameter, -directionSign * stepLength, tolerance);
        if (inwardPoint.HasValue)
            picks.Add(new FilletPickPoint("inward", inwardPoint.Value));

        if (picks.Count == 0)
            picks.Add(new FilletPickPoint("crossing", sourceCurve.PointAt(crossingParameter)));

        return picks;
    }

    private static double ScoreFilletArcAgainstInterior(
        Curve arcCurve,
        IReadOnlyList<Curve>? coarseRegionCurves,
        double radius,
        double tolerance)
    {
        if (coarseRegionCurves == null || coarseRegionCurves.Count == 0)
            return arcCurve.GetLength();

        var midpoint = arcCurve.PointAtNormalizedLength(0.5);
        var tangent = arcCurve.TangentAt(arcCurve.Domain.Mid);
        tangent.Z = 0.0;
        if (!tangent.Unitize())
            return double.NegativeInfinity;

        var normal = new Vector3d(-tangent.Y, tangent.X, 0.0);
        if (!normal.Unitize())
            return double.NegativeInfinity;

        var sampleOffset = Math.Max(tolerance * 20.0, radius * 0.25);
        var leftPoint = midpoint + normal * sampleOffset;
        var rightPoint = midpoint - normal * sampleOffset;

        var leftInside = IsPointInsideAnyCurve(leftPoint, coarseRegionCurves, tolerance);
        var rightInside = IsPointInsideAnyCurve(rightPoint, coarseRegionCurves, tolerance);
        if (leftInside == rightInside)
            return double.NegativeInfinity;

        return arcCurve.GetLength();
    }

    private static bool IsPointInsideAnyCurve(
        Point3d point,
        IReadOnlyList<Curve> curves,
        double tolerance)
    {
        foreach (var curve in curves)
        {
            var containment = curve.Contains(point, Plane.WorldXY, tolerance);
            if (containment == PointContainment.Inside || containment == PointContainment.Coincident)
                return true;
        }

        return false;
    }

    private static Point3d? AdvanceCurvePointByLength(
        Curve curve,
        double startParameter,
        double signedLength,
        double tolerance)
    {
        var clampedParameter = Math.Max(curve.Domain.Min, Math.Min(curve.Domain.Max, startParameter));
        if (Math.Abs(signedLength) <= tolerance)
            return curve.PointAt(clampedParameter);

        Curve? working = null;
        try
        {
            if (signedLength > 0.0)
            {
                working = curve.Trim(clampedParameter, curve.Domain.Max);
            }
            else
            {
                working = curve.Trim(curve.Domain.Min, clampedParameter);
                if (working != null)
                    working.Reverse();
            }

            if (working == null)
                return null;

            var targetLength = Math.Abs(signedLength);
            if (!working.LengthParameter(targetLength, out var targetParameter))
                return working.PointAtEnd;

            return working.PointAt(targetParameter);
        }
        finally
        {
            working?.Dispose();
        }
    }

    private static bool TryOrientArcToSourceCurves(
        Curve arcCurve,
        Curve currentSourceCurve,
        Curve nextSourceCurve,
        double tolerance,
        out double currentParameter,
        out double nextParameter)
    {
        currentParameter = 0.0;
        nextParameter = 0.0;

        if (!currentSourceCurve.ClosestPoint(arcCurve.PointAtStart, out var currentStartParameter)
            || !nextSourceCurve.ClosestPoint(arcCurve.PointAtEnd, out var nextEndParameter)
            || !currentSourceCurve.ClosestPoint(arcCurve.PointAtEnd, out var currentEndParameter)
            || !nextSourceCurve.ClosestPoint(arcCurve.PointAtStart, out var nextStartParameter))
        {
            return false;
        }

        var directDistance =
            currentSourceCurve.PointAt(currentStartParameter).DistanceTo(arcCurve.PointAtStart) +
            nextSourceCurve.PointAt(nextEndParameter).DistanceTo(arcCurve.PointAtEnd);
        var reversedDistance =
            currentSourceCurve.PointAt(currentEndParameter).DistanceTo(arcCurve.PointAtEnd) +
            nextSourceCurve.PointAt(nextStartParameter).DistanceTo(arcCurve.PointAtStart);

        if (reversedDistance + tolerance < directDistance)
        {
            arcCurve.Reverse();
            currentParameter = currentEndParameter;
            nextParameter = nextStartParameter;
            return reversedDistance <= tolerance * 40.0;
        }

        currentParameter = currentStartParameter;
        nextParameter = nextEndParameter;
        return directDistance <= tolerance * 40.0;
    }

    private static Point3d? ComputeOuterCornerAttemptPoint(Curve? currentCurve, Curve? nextCurve)
    {
        if (currentCurve != null && nextCurve != null)
        {
            var currentPoint = currentCurve.PointAtStart;
            var nextPoint = nextCurve.PointAtStart;
            return new Point3d(
                (currentPoint.X + nextPoint.X) * 0.5,
                (currentPoint.Y + nextPoint.Y) * 0.5,
                (currentPoint.Z + nextPoint.Z) * 0.5);
        }

        if (currentCurve != null)
            return currentCurve.PointAtStart;

        if (nextCurve != null)
            return nextCurve.PointAtStart;

        return null;
    }

    private static Curve? TrimCurveFromParameterToEnd(Curve curve, double parameter)
    {
        var clamped = Math.Min(curve.Domain.Max, Math.Max(curve.Domain.Min, parameter));
        return curve.Trim(clamped, curve.Domain.Max);
    }

    private static bool TryParseApproachEdgeReference(
        JsonElement key,
        out string road,
        out string physicalSide,
        out string armDirection)
    {
        road = string.Empty;
        physicalSide = string.Empty;
        armDirection = string.Empty;
        if (!key.TryGetProperty("road", out var roadElement)
            || !key.TryGetProperty("side", out var sideElement)
            || !key.TryGetProperty("armId", out var armIdElement))
        {
            return false;
        }

        road = roadElement.GetString() ?? string.Empty;
        var side = sideElement.GetString() ?? string.Empty;
        var armId = armIdElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(road) || string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(armId))
            return false;

        armDirection = armId.EndsWith(":backward", StringComparison.OrdinalIgnoreCase)
            ? "backward"
            : armId.EndsWith(":forward", StringComparison.OrdinalIgnoreCase)
                ? "forward"
                : string.Empty;
        if (string.IsNullOrWhiteSpace(armDirection))
            return false;

        physicalSide = armDirection.Equals("backward", StringComparison.OrdinalIgnoreCase)
            ? OppositeSide(side)
            : side;
        return physicalSide == "left" || physicalSide == "right";
    }

    private static string BuildApproachEdgeArmKey(string road, string armDirection, string side) =>
        $"{road}|{armDirection}|{side}";

    private static void OrientCurveToStartNear(Curve curve, Point3d point)
    {
        if (curve.PointAtEnd.DistanceTo(point) < curve.PointAtStart.DistanceTo(point))
            curve.Reverse();
    }

    private static List<Curve> ResolveFinalSurfaceRegionCurves(
        Curve centralBoundary,
        IntersectionRealizationRequest request,
        IReadOnlyList<ApproachPatch> approachPatches,
        double tolerance)
    {
            var regionInputs = new List<Curve>();
        var seedPoints = new List<Point3d>();
        try
        {
            var centralCopy = centralBoundary.DuplicateCurve();
            if (centralCopy == null)
                return new List<Curve>();

            regionInputs.Add(centralCopy);
            seedPoints.Add(ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints));

            foreach (var approachPatch in approachPatches)
            {
                var borderCopy = approachPatch.BorderCurve.DuplicateCurve();
                if (borderCopy == null)
                    continue;

                regionInputs.Add(borderCopy);
                seedPoints.Add(ComputeClosedCurveSeedPoint(borderCopy));
            }

            var regionCurves = TryResolveSurfaceCurvesFromRegions(regionInputs, seedPoints, tolerance);
            if (regionCurves.Count > 0)
                return regionCurves;

            var unionCurves = TryResolveSurfaceCurvesFromUnion(regionInputs, seedPoints[0], tolerance);
            if (unionCurves.Count > 0)
                return unionCurves;

            var fallback = centralBoundary.DuplicateCurve();
            if (fallback == null)
                return new List<Curve>();

            return new List<Curve> { fallback };
        }
        finally
        {
            foreach (var curve in regionInputs)
                curve.Dispose();
        }
    }

    private static IReadOnlyList<Curve>? SelectInteriorClassifierCurves(
        IReadOnlyList<Curve>? resolvedCurves,
        double tolerance)
    {
        if (resolvedCurves == null || resolvedCurves.Count == 0)
            return resolvedCurves;

        Curve? bestCurve = null;
        double bestArea = double.NegativeInfinity;
        foreach (var curve in resolvedCurves)
        {
            if (curve == null || !curve.IsClosed)
                continue;

            using var massProps = AreaMassProperties.Compute(curve);
            var area = Math.Abs(massProps?.Area ?? 0.0);
            if (area <= tolerance * tolerance)
                continue;

            if (area > bestArea)
            {
                bestArea = area;
                bestCurve = curve;
            }
        }

        return bestCurve != null
            ? new[] { bestCurve }
            : resolvedCurves;
    }

    private static List<Curve> TryResolveSurfaceCurvesFromRegions(
        IReadOnlyList<Curve> regionInputs,
        IReadOnlyList<Point3d> seedPoints,
        double tolerance)
    {
        if (regionInputs.Count == 0 || seedPoints.Count == 0)
            return new List<Curve>();

        using var regions = Curve.CreateBooleanRegions(
            regionInputs,
            Plane.WorldXY,
            seedPoints,
            true,
            tolerance);
        if (regions == null || regions.RegionCount <= 0)
            return new List<Curve>();

        for (int regionIndex = 0; regionIndex < regions.RegionCount; regionIndex++)
        {
            if (regions.RegionPointIndex(regionIndex) != 0)
                continue;

            var regionCurves = regions.RegionCurves(regionIndex);
            if (regionCurves == null || regionCurves.Length == 0)
                continue;

            return regionCurves
                .Where(static curve => curve != null)
                .ToList()!;
        }

        return new List<Curve>();
    }

    private static List<Curve> TryResolveSurfaceCurvesFromUnion(
        IReadOnlyList<Curve> regionInputs,
        Point3d requiredPoint,
        double tolerance)
    {
        if (regionInputs.Count == 0)
            return new List<Curve>();

        var unions = Curve.CreateBooleanUnion(regionInputs, tolerance);
        if (unions == null || unions.Length == 0)
            return new List<Curve>();

        Curve? bestCurve = null;
        double bestArea = double.NegativeInfinity;
        foreach (var unionCurve in unions)
        {
            if (unionCurve == null)
                continue;

            if (!unionCurve.IsClosed)
            {
                unionCurve.Dispose();
                continue;
            }

            var containment = unionCurve.Contains(requiredPoint, Plane.WorldXY, tolerance);
            if (containment != PointContainment.Inside && containment != PointContainment.Coincident)
            {
                unionCurve.Dispose();
                continue;
            }

            using var massProps = AreaMassProperties.Compute(unionCurve);
            var area = massProps?.Area ?? 0.0;
            if (area > bestArea)
            {
                bestCurve?.Dispose();
                bestCurve = unionCurve;
                bestArea = area;
            }
            else
            {
                unionCurve.Dispose();
            }
        }

        if (bestCurve == null)
            return new List<Curve>();

        return new List<Curve> { bestCurve };
    }

    private static Point3d ComputeClosedCurveSeedPoint(Curve curve)
    {
        using var massProps = AreaMassProperties.Compute(curve);
        if (massProps != null)
            return massProps.Centroid;

        return curve.GetBoundingBox(accurate: true).Center;
    }

    private static Brep SelectSingleRealizedSurface(
        Brep[] surfaceBreps,
        IntersectionRealizationRequest request,
        double tolerance)
    {
        Brep? bestSurface = null;
        double bestArea = double.NegativeInfinity;
        var requiredPoint = ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints);

        foreach (var brep in surfaceBreps)
        {
            if (brep == null)
                continue;

            using var duplicate = brep.DuplicateBrep();
            var bbox = duplicate.GetBoundingBox(true);
            if (!bbox.Contains(requiredPoint))
            {
                brep.Dispose();
                continue;
            }

            using var massProps = AreaMassProperties.Compute(brep);
            var area = massProps?.Area ?? 0.0;
            if (area <= tolerance * tolerance)
            {
                brep.Dispose();
                continue;
            }

            if (area > bestArea)
            {
                bestSurface?.Dispose();
                bestSurface = brep;
                bestArea = area;
            }
            else
            {
                brep.Dispose();
            }
        }

        if (bestSurface != null)
            return bestSurface;

        foreach (var brep in surfaceBreps)
            brep?.Dispose();

        throw new InvalidOperationException("Failed to select realized surface from planar region result");
    }

    private static Point3d ComputeBoundarySeedPoint(IReadOnlyList<IntersectionPoint3> points)
    {
        if (points == null || points.Count == 0)
            return Point3d.Origin;

        var polyline = BuildClosedPolyline(points);
        if (polyline == null)
            return ToRhinoPoint(points[0]);

        using var curve = new PolylineCurve(polyline);
        return ComputeClosedCurveSeedPoint(curve);
    }

    private static Curve? SynchronizeCurveLength(
        Curve curve,
        double targetLength,
        double tolerance)
    {
        if (curve == null)
            return null;

        var currentLength = curve.GetLength();
        if (currentLength <= targetLength + tolerance)
            return curve;

        if (!curve.LengthParameter(targetLength, out var targetParameter))
            return curve;

        var trimmed = curve.Trim(curve.Domain.Min, targetParameter);
        if (trimmed == null)
            return curve;

        curve.Dispose();
        return trimmed;
    }

    private static IEnumerable<Curve> TrimApproachPatchCurves(
        Curve patchBorder,
        Curve centralBoundary,
        double tolerance)
    {
        var difference = Curve.CreateBooleanDifference(
            patchBorder,
            new[] { centralBoundary },
            tolerance);
        if (difference == null || difference.Length == 0)
        {
            var fallback = patchBorder.DuplicateCurve();
            if (fallback != null && fallback.IsClosed)
                yield return fallback;
            yield break;
        }

        foreach (var curve in difference)
        {
            if (curve == null)
                continue;

            if (!curve.IsClosed)
            {
                curve.Dispose();
                continue;
            }

            using var massProps = AreaMassProperties.Compute(curve);
            if (massProps == null || massProps.Area <= tolerance * tolerance)
            {
                curve.Dispose();
                continue;
            }

            yield return curve;
        }
    }

    private static double ComputeApproachPatchArea(
        IReadOnlyList<ApproachPatch> approachPatches,
        Curve centralBoundary,
        double tolerance)
    {
        double area = 0.0;
        foreach (var approachPatch in approachPatches)
        {
            foreach (var patchCurve in TrimApproachPatchCurves(
                approachPatch.BorderCurve,
                centralBoundary,
                tolerance))
            {
                using (patchCurve)
                using (var massProps = AreaMassProperties.Compute(patchCurve))
                {
                    area += massProps?.Area ?? 0.0;
                }
            }
        }

        return area;
    }

    private static double EstimateArcLength(IntersectionCurbReturnArc arc)
    {
        var center = ToRhinoPoint(arc.Center);
        var start = ToRhinoPoint(arc.StartPoint);
        var end = ToRhinoPoint(arc.EndPoint);
        var startVector = start - center;
        var endVector = end - center;
        if (!startVector.Unitize() || !endVector.Unitize())
            return 0.0;

        var angle = Vector3d.VectorAngle(startVector, endVector, Plane.WorldXY);
        if (!RhinoMath.IsValidDouble(angle))
            return 0.0;

        return arc.Radius * angle;
    }

    private static Dictionary<string, List<ApproachTrimPoint>> BuildApproachTrimPoints(IntersectionRealizationRequest request)
    {
        var trimPointsByEdge = new Dictionary<string, List<ApproachTrimPoint>>(StringComparer.OrdinalIgnoreCase);
        if (request.CornerPairings.ValueKind != JsonValueKind.Array)
            return trimPointsByEdge;

        var arcsByCorner = request.AnalysisGeometry2D.CurbReturnArcs
            .Where(static arc => arc.CornerOrder.HasValue)
            .ToDictionary(static arc => arc.CornerOrder!.Value);

        foreach (var pairing in request.CornerPairings.EnumerateArray())
        {
            if (!pairing.TryGetProperty("cornerOrder", out var cornerOrderElement)
                || !cornerOrderElement.TryGetInt32(out var cornerOrder)
                || !arcsByCorner.TryGetValue(cornerOrder, out var arc))
            {
                continue;
            }

            AppendTrimPoint(trimPointsByEdge, pairing, "incoming", arc.StartPoint);
            AppendTrimPoint(trimPointsByEdge, pairing, "outgoing", arc.EndPoint);
        }

        return trimPointsByEdge;
    }

    private static void AppendTrimPoint(
        IDictionary<string, List<ApproachTrimPoint>> trimPointsByEdge,
        JsonElement pairing,
        string keyName,
        IntersectionPoint3 point)
    {
        if (!pairing.TryGetProperty(keyName, out var key)
            || !TryGetApproachEdgeKey(key, out var edgeKey, out var armDirection))
        {
            return;
        }

        if (!trimPointsByEdge.TryGetValue(edgeKey, out var points))
        {
            points = new List<ApproachTrimPoint>();
            trimPointsByEdge[edgeKey] = points;
        }

        var rhinoPoint = ToRhinoPoint(point);
        if (!points.Any(existing =>
                existing.ArmDirection.Equals(armDirection, StringComparison.OrdinalIgnoreCase)
                && existing.Point.DistanceTo(rhinoPoint) <= 1e-4))
        {
            points.Add(new ApproachTrimPoint(rhinoPoint, armDirection));
        }
    }

    private static bool TryGetApproachEdgeKey(JsonElement key, out string edgeKey, out string armDirection)
    {
        edgeKey = string.Empty;
        armDirection = string.Empty;
        if (!key.TryGetProperty("road", out var roadElement)
            || !key.TryGetProperty("side", out var sideElement)
            || !key.TryGetProperty("armId", out var armIdElement))
        {
            return false;
        }

        var road = roadElement.GetString();
        var side = sideElement.GetString();
        var armId = armIdElement.GetString();
        if (string.IsNullOrWhiteSpace(road) || string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(armId))
            return false;

        armDirection = armId.EndsWith(":backward", StringComparison.OrdinalIgnoreCase)
            ? "backward"
            : armId.EndsWith(":forward", StringComparison.OrdinalIgnoreCase)
                ? "forward"
                : string.Empty;
        if (string.IsNullOrWhiteSpace(armDirection))
            return false;

        var physicalSide = armDirection.Equals("backward", StringComparison.OrdinalIgnoreCase)
            ? OppositeSide(side)
            : side;
        if (physicalSide != "left" && physicalSide != "right")
            return false;

        edgeKey = BuildApproachEdgeKey(road, physicalSide);
        return true;
    }

    private static string BuildApproachEdgeKey(string road, string side) =>
        $"{road}|{side}";

    private static string GetLayerPath(RhinoDoc doc, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= doc.Layers.Count)
            return string.Empty;

        var layer = doc.Layers[layerIndex];
        return layer?.FullPath ?? layer?.Name ?? string.Empty;
    }

    private static double GetBoundaryLayerSemanticScore(string layerPath, string centerlineLayerPath)
    {
        if (string.IsNullOrWhiteSpace(layerPath))
            return 0.0;

        var normalized = layerPath.Replace(" ", string.Empty).ToLowerInvariant();
        if (normalized.Contains("centerline")
            || normalized.Contains("analysis")
            || normalized.Contains("approachpatches")
            || normalized.Contains("approachedges")
            || normalized.Contains("curbreturns")
            || normalized.Contains("surface"))
        {
            return 0.0;
        }

        if (normalized.Contains("apblrow")
            || normalized.Contains("rightofway")
            || normalized.EndsWith("::row")
            || normalized.Contains("reference::rightofway"))
        {
            return 0.0;
        }

        double score = 0.0;
        if (normalized.Contains("cpcurbnl"))
            score += 120.0;
        if (normalized.Contains("curbleft") || normalized.Contains("curbright"))
            score += 120.0;
        if (normalized.Contains("::curb") || normalized.Contains("markings::curb"))
            score += 100.0;
        if (normalized.Contains("edgeline") || normalized.Contains("edgeofpavement"))
            score += 80.0;

        if (!string.IsNullOrWhiteSpace(centerlineLayerPath))
        {
            var centerlineNormalized = centerlineLayerPath.Replace(" ", string.Empty).ToLowerInvariant();
            var roadPrefix = TryGetRoadLayerPrefix(centerlineNormalized);
            if (!string.IsNullOrWhiteSpace(roadPrefix) && normalized.StartsWith(roadPrefix, StringComparison.Ordinal))
                score += 40.0;
            else if (centerlineNormalized.StartsWith("cad::", StringComparison.Ordinal)
                && normalized.StartsWith("cad::", StringComparison.Ordinal))
                score += 20.0;
        }

        return score;
    }

    private static double GetBoundaryObjectSemanticScore(string? objectName, string layerPath, string side)
    {
        double score = 0.0;
        var normalizedName = (objectName ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        var normalizedLayer = layerPath.Replace(" ", string.Empty).ToLowerInvariant();

        if (normalizedName.Contains("curb") || normalizedName.Contains("curb_face"))
            score += 40.0;
        if (normalizedName.Contains("edgeofpavement"))
            score += 30.0;

        if (normalizedLayer.Contains("curbleft") && side.Equals("left", StringComparison.OrdinalIgnoreCase))
            score += 30.0;
        if (normalizedLayer.Contains("curbright") && side.Equals("right", StringComparison.OrdinalIgnoreCase))
            score += 30.0;

        return score;
    }

    private static string TryGetRoadLayerPrefix(string centerlineLayerPath)
    {
        const string roadCreatorPrefix = "roadcreator::";
        if (!centerlineLayerPath.StartsWith(roadCreatorPrefix, StringComparison.Ordinal))
            return string.Empty;

        var parts = centerlineLayerPath.Split(new[] { "::" }, StringSplitOptions.None);
        if (parts.Length < 3)
            return string.Empty;

        return $"{parts[0]}::{parts[1]}::";
    }

    private static bool TryGetCurveDirection(
        Curve curve,
        double parameter,
        double tolerance,
        out Vector3d direction)
    {
        direction = curve.TangentAt(parameter);
        direction.Z = 0.0;
        if (direction.Unitize())
            return true;

        var length = curve.GetLength();
        if (!(length > tolerance))
            return false;

        var sampleLength = Math.Min(length * 0.1, Math.Max(tolerance * 8.0, 1.0));
        if (!curve.LengthParameter(sampleLength, out var sampleParameter))
            sampleParameter = curve.Domain.ParameterAt(0.5);

        direction = curve.PointAt(sampleParameter) - curve.PointAtStart;
        direction.Z = 0.0;
        return direction.Unitize();
    }

    private static string OppositeSide(string side) =>
        side.Equals("left", StringComparison.OrdinalIgnoreCase) ? "right"
        : side.Equals("right", StringComparison.OrdinalIgnoreCase) ? "left"
        : string.Empty;

    private static Curve? BuildPhysicalEdgeCurve(
        Curve flatCenterline,
        IntersectionSourceRoad sourceRoad,
        string physicalSide,
        double tolerance)
    {
        var domain = flatCenterline.Domain;
        var parameter = Math.Min(domain.Max, Math.Max(domain.Min, sourceRoad.SelectedParameter));
        var localTangent = flatCenterline.TangentAt(parameter);
        localTangent.Z = 0.0;
        var forwardTangent = ToRhinoVector(sourceRoad.ForwardTangent);
        forwardTangent.Z = 0.0;
        if (!localTangent.Unitize() || !forwardTangent.Unitize())
            return null;

        var alignment = localTangent * forwardTangent >= 0.0 ? 1.0 : -1.0;
        var baseSignedOffset = physicalSide.Equals("right", StringComparison.OrdinalIgnoreCase)
            ? sourceRoad.EffectiveCarriagewaySurfaceOffset
            : -sourceRoad.EffectiveCarriagewaySurfaceOffset;
        var signedOffset = baseSignedOffset * alignment;

        return Math.Abs(signedOffset) <= tolerance
            ? flatCenterline.DuplicateCurve()
            : OffsetCurve(flatCenterline, signedOffset, tolerance);
    }

    private static IEnumerable<LocalizedApproachSegment> BuildLocalizedApproachSegments(
        Curve edgeCurve,
        IReadOnlyList<ApproachTrimPoint>? trimPoints,
        Vector3d sourceForwardTangent,
        double previewLength,
        double tolerance)
    {
        if (trimPoints == null || trimPoints.Count == 0)
            yield break;

        var usedTrimPoints = new List<ApproachTrimPoint>();
        foreach (var trimPoint in trimPoints)
        {
            if (usedTrimPoints.Any(existing =>
                    existing.ArmDirection.Equals(trimPoint.ArmDirection, StringComparison.OrdinalIgnoreCase)
                    && existing.Point.DistanceTo(trimPoint.Point) <= Math.Max(tolerance, 1e-4)))
                continue;

            usedTrimPoints.Add(trimPoint);
            var localized = BuildLocalizedApproachSegment(
                edgeCurve,
                trimPoint.Point,
                trimPoint.ArmDirection,
                sourceForwardTangent,
                previewLength,
                tolerance);
            if (localized != null)
            {
                yield return new LocalizedApproachSegment(trimPoint.ArmDirection, localized);
            }
        }
    }

    private static Curve? BuildLocalizedApproachSegment(
        Curve edgeCurve,
        Point3d trimPoint,
        string armDirection,
        Vector3d sourceForwardTangent,
        double previewLength,
        double tolerance)
    {
        if (!edgeCurve.ClosestPoint(trimPoint, out var trimParameter))
            return null;

        var trimCurvePoint = edgeCurve.PointAt(trimParameter);

        var splitCurves = edgeCurve.Split(trimParameter);
        if (splitCurves == null || splitCurves.Length == 0)
            return null;

        try
        {
            Curve? bestCurve = null;
            var targetDirection = armDirection.Equals("backward", StringComparison.OrdinalIgnoreCase)
                ? -sourceForwardTangent
                : sourceForwardTangent;
            double bestAlignment = double.NegativeInfinity;
            double bestLength = double.NegativeInfinity;
            for (int i = 0; i < splitCurves.Length; i++)
            {
                var splitCurve = splitCurves[i];
                if (splitCurve == null)
                    continue;

                var oriented = OrientCurveFromTrimPoint(splitCurve, trimCurvePoint, tolerance);
                if (oriented == null)
                {
                    splitCurve.Dispose();
                    splitCurves[i] = null;
                    continue;
                }

                if (!ReferenceEquals(oriented, splitCurve))
                {
                    splitCurve.Dispose();
                    splitCurves[i] = null;
                }

                if (oriented.GetLength() <= tolerance * 4.0)
                {
                    oriented.Dispose();
                    continue;
                }

                if (!TryGetCurveStartDirection(oriented, tolerance, out var segmentDirection))
                {
                    oriented.Dispose();
                    continue;
                }

                var alignment = segmentDirection * targetDirection;
                var orientedLength = oriented.GetLength();
                if (alignment > bestAlignment + 1e-6
                    || (Math.Abs(alignment - bestAlignment) <= 1e-6 && orientedLength > bestLength))
                {
                    bestCurve?.Dispose();
                    bestCurve = oriented;
                    bestAlignment = alignment;
                    bestLength = orientedLength;
                    if (ReferenceEquals(oriented, splitCurve))
                        splitCurves[i] = null;
                }
                else
                {
                    oriented.Dispose();
                }
            }

            if (bestCurve == null)
                return null;

            return ShortenCurveToLength(bestCurve, previewLength, tolerance);
        }
        finally
        {
            foreach (var splitCurve in splitCurves)
            {
                if (splitCurve != null && !splitCurve.IsDocumentControlled)
                    splitCurve.Dispose();
            }
        }
    }

    private static bool TryGetCurveStartDirection(
        Curve curve,
        double tolerance,
        out Vector3d direction)
    {
        direction = Vector3d.Unset;

        var length = curve.GetLength();
        if (!(length > tolerance))
            return false;

        var sampleLength = Math.Min(length * 0.25, Math.Max(tolerance * 8.0, 1.0));
        if (!curve.LengthParameter(sampleLength, out var sampleParameter))
            sampleParameter = curve.Domain.ParameterAt(0.25);

        direction = curve.PointAt(sampleParameter) - curve.PointAtStart;
        direction.Z = 0.0;
        return direction.Unitize();
    }

    private static Curve? OrientCurveFromTrimPoint(
        Curve curve,
        Point3d trimCurvePoint,
        double tolerance)
    {
        var startDistance = curve.PointAtStart.DistanceTo(trimCurvePoint);
        var endDistance = curve.PointAtEnd.DistanceTo(trimCurvePoint);
        if (startDistance > tolerance * 4.0 && endDistance > tolerance * 4.0)
            return null;

        if (startDistance <= endDistance)
            return curve;

        var duplicate = curve.DuplicateCurve();
        duplicate.Reverse();
        return duplicate;
    }

    private static Curve ShortenCurveToLength(
        Curve curve,
        double targetLength,
        double tolerance)
    {
        var curveLength = curve.GetLength();
        if (curveLength <= targetLength + tolerance)
            return curve;

        if (!curve.LengthParameter(targetLength, out var targetParameter))
            return curve;

        var shortened = curve.Trim(curve.Domain.Min, targetParameter);
        if (shortened == null)
            return curve;

        curve.Dispose();
        return shortened;
    }

    private static double ComputeApproachPreviewLength(
        IntersectionRealizationRequest request,
        IntersectionSourceRoad sourceRoad)
    {
        var diagonal = ComputeBoundaryDiagonal(request.ProvisionalBoundary2D.CornerPoints);
        var maxRadius = request.AnalysisGeometry2D.CurbReturnArcs.Count == 0
            ? 0.0
            : request.AnalysisGeometry2D.CurbReturnArcs.Max(static arc => arc.Radius);
        var outerEnvelope = sourceRoad.EffectiveOuterEnvelopeOffset;
        var carriageway = sourceRoad.EffectiveCarriagewaySurfaceOffset;

        // Keep pre-union arm patches local to the crossing so the region-union
        // boundary is driven by the immediate mouth geometry instead of long arm tails.
        var baseLength = Math.Max(
            outerEnvelope > 0.0
                ? outerEnvelope * sourceRoad.EffectiveArmLengthOuterEnvelopeMultiplier
                : carriageway * sourceRoad.EffectiveArmLengthCarriagewayMultiplier,
            Math.Max(
                diagonal * sourceRoad.EffectiveArmLengthDiagonalMultiplier,
                maxRadius * sourceRoad.EffectiveArmLengthRadiusMultiplier));
        return Math.Clamp(baseLength, sourceRoad.EffectiveArmLengthMin, sourceRoad.EffectiveArmLengthMax);
    }

    private static double ComputeBoundaryDiagonal(IReadOnlyList<IntersectionPoint3> points)
    {
        if (points == null || points.Count == 0)
            return 0.0;

        var minX = points.Min(static point => point.X);
        var minY = points.Min(static point => point.Y);
        var minZ = points.Min(static point => point.Z);
        var maxX = points.Max(static point => point.X);
        var maxY = points.Max(static point => point.Y);
        var maxZ = points.Max(static point => point.Z);
        return new Point3d(minX, minY, minZ).DistanceTo(new Point3d(maxX, maxY, maxZ));
    }

    private static Curve ProjectToXY(Curve curve)
    {
        var duplicate = curve.DuplicateCurve();
        duplicate.Transform(Transform.PlanarProjection(Plane.WorldXY));
        return duplicate;
    }

    private static Curve? OffsetCurve(Curve flatCenterline, double signedOffset, double tolerance)
    {
        var plane = signedOffset > 0.0 ? RightPlane : LeftPlane;
        var offsets = flatCenterline.Offset(
            plane,
            Math.Abs(signedOffset),
            tolerance,
            CurveOffsetCornerStyle.Sharp);
        if (offsets == null || offsets.Length == 0)
            return null;

        if (offsets.Length == 1)
            return offsets[0];

        var joined = Curve.JoinCurves(offsets, tolerance);
        return joined?.Length > 0 ? joined[0] : offsets[0];
    }

    private static bool TryBuildArc(
        IntersectionCurbReturnArc arc,
        out Arc rhinoArc,
        out double length)
    {
        rhinoArc = Arc.Unset;
        length = 0.0;

        if (!(arc.Radius > 0.0) || double.IsNaN(arc.Radius) || double.IsInfinity(arc.Radius))
            return false;

        var center = ToRhinoPoint(arc.Center);
        var start = ToRhinoPoint(arc.StartPoint);
        var end = ToRhinoPoint(arc.EndPoint);

        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        double sweep = endAngle - startAngle;

        while (sweep <= -Math.PI) sweep += Math.PI * 2.0;
        while (sweep > Math.PI) sweep -= Math.PI * 2.0;

        double midAngle = startAngle + 0.5 * sweep;
        var mid = new Point3d(
            center.X + arc.Radius * Math.Cos(midAngle),
            center.Y + arc.Radius * Math.Sin(midAngle),
            center.Z);

        var candidate = new Arc(start, mid, end);
        if (!candidate.IsValid)
            return false;

        rhinoArc = candidate;
        length = Math.Abs(candidate.Length);
        return !double.IsNaN(length) && !double.IsInfinity(length);
    }

    private static List<string> ExtractFallbackFlags(JsonElement unresolvedConditions)
    {
        var flags = new List<string>();
        if (unresolvedConditions.ValueKind != JsonValueKind.Array)
            return flags;

        foreach (var condition in unresolvedConditions.EnumerateArray())
        {
            if (!condition.TryGetProperty("code", out var codeElement))
                continue;

            var code = codeElement.GetString();
            if (!string.IsNullOrWhiteSpace(code) && !flags.Contains(code, StringComparer.Ordinal))
                flags.Add(code);
        }

        return flags;
    }

    private static JsonElement CloneJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
            return default;

        using var document = JsonDocument.Parse(element.GetRawText());
        return document.RootElement.Clone();
    }

    private static double RoundTo(double value, int digits) =>
        Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static IEnumerable<DebugDotSpec> BuildSurfaceDebugDots(
        IntersectionRealizationRequest request,
        SurfaceDebugInfo debugInfo,
        int layerIndex,
        string namePrefix)
    {
        if (layerIndex < 0)
            yield break;

        var seedPoint = ComputeBoundarySeedPoint(request.ProvisionalBoundary2D.CornerPoints);
        yield return new DebugDotSpec(
            new TextDot($"surface:{debugInfo.Strategy}", seedPoint),
            BuildAttributes(
                layerIndex,
                $"{namePrefix}::debug-surface-strategy",
                request.AnalysisToken,
                "debug_dot",
                "debug_annotation"));

        if (debugInfo.OuterCornerAttempts is { Count: > 0 })
        {
            foreach (var attempt in debugInfo.OuterCornerAttempts)
            {
                var dotPoint = attempt.DotPoint ?? ComputeOuterCornerDebugPoint(request, attempt.CornerOrder);
                var currentLabel = attempt.CurrentEdge == null
                    ? "current:?"
                    : $"{attempt.CurrentEdge.Road}:{attempt.CurrentEdge.ArmDirection}:{attempt.CurrentEdge.Side}";
                var nextLabel = attempt.NextEdge == null
                    ? "next:?"
                    : $"{attempt.NextEdge.Road}:{attempt.NextEdge.ArmDirection}:{attempt.NextEdge.Side}";
                var text =
                    $"corner {attempt.CornerOrder}\n" +
                    $"{(attempt.Succeeded ? "fillet" : "fail")}: {attempt.Reason}\n" +
                    $"{currentLabel}\n" +
                    $"{nextLabel}\n" +
                    $"req={RoundTo(attempt.RequestedRadius, 3)}" +
                    (attempt.ResolvedRadius.HasValue ? $"\nres={RoundTo(attempt.ResolvedRadius.Value, 3)}" : string.Empty);
                yield return new DebugDotSpec(
                    new TextDot(text, dotPoint),
                    BuildAttemptDebugAttributes(
                        layerIndex,
                        $"{namePrefix}::debug-corner-{attempt.CornerOrder}",
                        request.AnalysisToken,
                        attempt));
            }

            yield break;
        }

        if (debugInfo.OuterCornerFillets == null)
            yield break;

        foreach (var fillet in debugInfo.OuterCornerFillets)
        {
            var dotPoint = fillet.ArcCurve.PointAtNormalizedLength(0.5);
            var text =
                $"corner {fillet.CornerOrder}\n" +
                $"{fillet.CurrentEdge.Road}:{fillet.CurrentEdge.ArmDirection}:{fillet.CurrentEdge.Side}\n" +
                $"{fillet.NextEdge.Road}:{fillet.NextEdge.ArmDirection}:{fillet.NextEdge.Side}\n" +
                $"r={RoundTo(fillet.Radius, 3)}";
            yield return new DebugDotSpec(
                new TextDot(text, dotPoint),
                BuildAttributes(
                    layerIndex,
                    $"{namePrefix}::debug-corner-{fillet.CornerOrder}",
                    request.AnalysisToken,
                    "debug_dot",
                    "debug_annotation"));
        }
    }

    private static IEnumerable<DebugCurveSpec> BuildSurfaceDebugCurves(
        IntersectionRealizationRequest request,
        SurfaceDebugInfo debugInfo,
        int layerIndex,
        string namePrefix)
    {
        if (layerIndex < 0)
            yield break;

        if (debugInfo.BoundaryDebugCurves is { Count: > 0 })
        {
            for (int i = 0; i < debugInfo.BoundaryDebugCurves.Count; i++)
            {
                var debugCurve = debugInfo.BoundaryDebugCurves[i];
                var duplicate = debugCurve.Curve.DuplicateCurve();
                if (duplicate == null)
                    continue;

                var attrs = BuildAttributes(
                    layerIndex,
                    $"{namePrefix}::debug-boundary-{i}",
                    request.AnalysisToken,
                    "debug_boundary_curve",
                    "debug_annotation");
                attrs.SetUserString("rook_intersection_debug_label", debugCurve.Label);
                ApplyBoundaryCurveMetadata(attrs, debugCurve);
                yield return new DebugCurveSpec(duplicate, attrs);
            }
        }

        if (debugInfo.OuterCornerAttempts is not { Count: > 0 })
            yield break;

        foreach (var attempt in debugInfo.OuterCornerAttempts)
        {
            if (attempt.Candidates == null)
                continue;

            for (int i = 0; i < attempt.Candidates.Count; i++)
            {
                var candidate = attempt.Candidates[i];
                var duplicate = candidate.Curve.DuplicateCurve();
                if (duplicate == null)
                    continue;

                var attrs = BuildAttributes(
                    layerIndex,
                    $"{namePrefix}::debug-candidate-{attempt.CornerOrder}-{i}",
                    request.AnalysisToken,
                    "debug_candidate_curve",
                    "debug_annotation");
                attrs.SetUserString("rook_intersection_corner_order", attempt.CornerOrder.ToString());
                attrs.SetUserString("rook_intersection_candidate_label", candidate.Label);
                attrs.SetUserString("rook_intersection_candidate_reason", candidate.Reason);
                if (candidate.Score.HasValue)
                    attrs.SetUserString("rook_intersection_candidate_score", RoundTo(candidate.Score.Value, 6).ToString("G"));

                yield return new DebugCurveSpec(duplicate, attrs);
            }
        }
    }

    private static void ApplyBoundaryCurveMetadata(ObjectAttributes attrs, DebugBoundaryCurve debugCurve)
    {
        if (!string.IsNullOrWhiteSpace(debugCurve.BoundaryId))
            attrs.SetUserString("rook_intersection_boundary_id", debugCurve.BoundaryId);

        if (debugCurve.PieceIndex.HasValue)
            attrs.SetUserString("rook_intersection_boundary_piece_index", debugCurve.PieceIndex.Value.ToString());

        if (debugCurve.StartParameter.HasValue)
            attrs.SetUserString("rook_intersection_boundary_start_parameter", RoundTo(debugCurve.StartParameter.Value, 6).ToString("G"));

        if (debugCurve.EndParameter.HasValue)
            attrs.SetUserString("rook_intersection_boundary_end_parameter", RoundTo(debugCurve.EndParameter.Value, 6).ToString("G"));

        if (debugCurve.StartCornerIndex.HasValue)
            attrs.SetUserString("rook_intersection_boundary_start_corner_index", debugCurve.StartCornerIndex.Value.ToString());

        if (debugCurve.EndCornerIndex.HasValue)
            attrs.SetUserString("rook_intersection_boundary_end_corner_index", debugCurve.EndCornerIndex.Value.ToString());

        if (!string.IsNullOrWhiteSpace(debugCurve.Role))
            attrs.SetUserString("rook_intersection_boundary_role", debugCurve.Role!);

        if (debugCurve.CornerOrder.HasValue)
            attrs.SetUserString("rook_intersection_boundary_corner_order", debugCurve.CornerOrder.Value.ToString());
    }

    private static void DisposeSurfaceDebugArtifacts(SurfaceDebugInfo debugInfo)
    {
        if (debugInfo.BoundaryDebugCurves is { Count: > 0 })
        {
            foreach (var debugCurve in debugInfo.BoundaryDebugCurves)
                debugCurve.Curve.Dispose();
        }

        if (debugInfo.OuterCornerFillets is { Count: > 0 })
        {
            foreach (var fillet in debugInfo.OuterCornerFillets)
                fillet.ArcCurve.Dispose();
        }

        if (debugInfo.OuterCornerAttempts is not { Count: > 0 })
            return;

        foreach (var attempt in debugInfo.OuterCornerAttempts)
        {
            if (attempt.Candidates == null)
                continue;

            foreach (var candidate in attempt.Candidates)
                candidate.Curve.Dispose();
        }
    }

    private static Point3d ComputeOuterCornerDebugPoint(
        IntersectionRealizationRequest request,
        int cornerOrder)
    {
        if (request.ProvisionalBoundary2D.CornerPoints.Count == 0)
            return Point3d.Origin;

        var index = cornerOrder;
        if (index < 0)
            index = 0;

        index %= request.ProvisionalBoundary2D.CornerPoints.Count;
        return ToRhinoPoint(request.ProvisionalBoundary2D.CornerPoints[index]);
    }

    private static ObjectAttributes BuildAttemptDebugAttributes(
        int layerIndex,
        string name,
        string analysisToken,
        OuterCornerAttempt attempt)
    {
        var attrs = BuildAttributes(
            layerIndex,
            name,
            analysisToken,
            "debug_dot",
            "debug_annotation");
        attrs.SetUserString("rook_intersection_debug_reason", attempt.Reason);
        attrs.SetUserString("rook_intersection_corner_order", attempt.CornerOrder.ToString());
        attrs.SetUserString("rook_intersection_requested_radius", RoundTo(attempt.RequestedRadius, 6).ToString("G"));
        if (attempt.ResolvedRadius.HasValue)
            attrs.SetUserString("rook_intersection_resolved_radius", RoundTo(attempt.ResolvedRadius.Value, 6).ToString("G"));
        if (attempt.CurrentEdge != null)
        {
            attrs.SetUserString("rook_intersection_current_edge_road", attempt.CurrentEdge.Road);
            attrs.SetUserString("rook_intersection_current_edge_side", attempt.CurrentEdge.Side);
            attrs.SetUserString("rook_intersection_current_edge_arm", attempt.CurrentEdge.ArmDirection);
        }
        if (attempt.NextEdge != null)
        {
            attrs.SetUserString("rook_intersection_next_edge_road", attempt.NextEdge.Road);
            attrs.SetUserString("rook_intersection_next_edge_side", attempt.NextEdge.Side);
            attrs.SetUserString("rook_intersection_next_edge_arm", attempt.NextEdge.ArmDirection);
        }

        return attrs;
    }

    private static IReadOnlyList<DebugBoundaryCurve> BuildBoundaryDebugCurves(
        Curve? exteriorLoop,
        IReadOnlyList<OuterCornerFillet>? cornerFillets,
        IReadOnlyList<Curve>? fallbackCurves,
        IReadOnlyList<DebugBoundaryCurve>? directCurves = null)
    {
        var curves = new List<DebugBoundaryCurve>();

        if (directCurves is { Count: > 0 })
        {
            foreach (var directCurve in directCurves)
            {
                var directCopy = directCurve.Curve.DuplicateCurve();
                if (directCopy == null)
                    continue;

                curves.Add(directCurve with { Curve = directCopy });
            }
        }

        if (exteriorLoop != null)
        {
            var loopCopy = exteriorLoop.DuplicateCurve();
            if (loopCopy != null)
                curves.Add(new DebugBoundaryCurve(
                    Label: "final-outer-loop",
                    Curve: loopCopy));
        }

        if (cornerFillets is { Count: > 0 })
        {
            foreach (var fillet in cornerFillets)
            {
                var arcCopy = fillet.ArcCurve.DuplicateCurve();
                if (arcCopy == null)
                    continue;

                curves.Add(new DebugBoundaryCurve(
                    Label: $"accepted-corner-{fillet.CornerOrder}",
                    Curve: arcCopy,
                    Role: "direct-corner-arc"));
            }
        }

        if (fallbackCurves is { Count: > 0 })
        {
            for (int i = 0; i < fallbackCurves.Count; i++)
            {
                var fallbackCopy = fallbackCurves[i]?.DuplicateCurve();
                if (fallbackCopy == null)
                    continue;

                curves.Add(new DebugBoundaryCurve(
                    Label: $"fallback-boundary-{i}",
                    Curve: fallbackCopy,
                    PieceIndex: i));
            }
        }

        return curves;
    }

    private static IEnumerable<DebugBoundaryCurve> BuildResolvedBoundaryDebugCurves(
        BoundaryEdgeResolution resolution,
        double tolerance)
    {
        foreach (var edge in resolution.Edges)
        {
            var edgeCopy = edge.Curve.DuplicateCurve();
            if (edgeCopy != null)
                yield return new DebugBoundaryCurve(
                    Label: $"resolved-{edge.Id}",
                    Curve: edgeCopy,
                    PieceIndex: edge.OrderIndex,
                    StartParameter: edge.StartParameter,
                    EndParameter: edge.EndParameter,
                    StartCornerIndex: edge.StartCornerIndex,
                    EndCornerIndex: edge.EndCornerIndex,
                    Role: "resolved-boundary-edge");
        }

        for (int cornerOrder = 0; cornerOrder < resolution.Corners.Count; cornerOrder++)
        {
            var incomingIndex = (cornerOrder + resolution.Edges.Count - 1) % resolution.Edges.Count;
            var outgoingIndex = cornerOrder;
            var semanticCornerOrder = resolution.Corners[cornerOrder].SemanticIndex;
            if (!TryBuildBoundaryCornerLocalSegments(
                    resolution.Edges[incomingIndex].Curve,
                    resolution.Edges[outgoingIndex].Curve,
                    tolerance,
                    out var incomingLocal,
                    out var outgoingLocal))
            {
                continue;
            }

            using (incomingLocal)
            using (outgoingLocal)
            {
                var incomingCopy = incomingLocal.DuplicateCurve();
                if (incomingCopy != null)
                {
                    yield return new DebugBoundaryCurve(
                        Label: $"corner-{semanticCornerOrder}-incoming-local",
                        Curve: incomingCopy,
                        StartCornerIndex: semanticCornerOrder,
                        CornerOrder: semanticCornerOrder,
                        Role: "corner-local-segment",
                        StartParameter: incomingLocal.Domain.Min,
                        EndParameter: incomingLocal.Domain.Max);
                }

                var outgoingCopy = outgoingLocal.DuplicateCurve();
                if (outgoingCopy != null)
                {
                    yield return new DebugBoundaryCurve(
                        Label: $"corner-{semanticCornerOrder}-outgoing-local",
                        Curve: outgoingCopy,
                        StartCornerIndex: semanticCornerOrder,
                        CornerOrder: semanticCornerOrder,
                        Role: "corner-local-segment",
                        StartParameter: outgoingLocal.Domain.Min,
                        EndParameter: outgoingLocal.Domain.Max);
                }
            }
        }
    }

    private sealed record ApproachEdgeSegment(
        string Road,
        string Side,
        string ArmDirection,
        string ObjectName,
        Curve Curve,
        Curve SourceCurve);

    private sealed record ApproachTrimPoint(
        Point3d Point,
        string ArmDirection);

    private sealed record LocalizedApproachSegment(
        string ArmDirection,
        Curve Curve);

    private sealed record SemanticBoundaryCandidate(
        Guid ObjectId,
        string Side,
        double Score,
        Curve Curve);

    private sealed record DebugDotSpec(
        TextDot Dot,
        ObjectAttributes Attributes);

    private sealed record DebugCurveSpec(
        Curve Curve,
        ObjectAttributes Attributes);

    private sealed record DebugBoundaryCurve(
        string Label,
        Curve Curve,
        string? BoundaryId = null,
        int? PieceIndex = null,
        double? StartParameter = null,
        double? EndParameter = null,
        int? StartCornerIndex = null,
        int? EndCornerIndex = null,
        string? Role = null,
        int? CornerOrder = null);

    private sealed record BoundaryCornerSample(
        double Parameter,
        Point3d Point);

    private sealed record BoundaryCornerPeak(
        int SampleIndex,
        double Parameter,
        Point3d Point,
        double RadiusToCentroid,
        double TurningAngleDegrees);

    private sealed record BoundaryCornerRef(
        int SemanticIndex,
        Point3d Point,
        double Parameter,
        double RadiusToCentroid,
        double TurningAngleDegrees);

    private sealed record ResolvedBoundaryEdge(
        string Id,
        int OrderIndex,
        int StartCornerIndex,
        int EndCornerIndex,
        double StartParameter,
        double EndParameter,
        Curve Curve);

    private sealed record BoundaryEdgeResolution(
        IReadOnlyList<BoundaryCornerRef> Corners,
        IReadOnlyList<ResolvedBoundaryEdge> Edges);

    private sealed record BoundaryCornerFilletResult(
        int CornerOrder,
        double Radius,
        Point3d CornerPoint,
        double IncomingParameter,
        double OutgoingParameter,
        Point3d IncomingPoint,
        Point3d OutgoingPoint,
        Curve ArcCurve);

    private sealed record SurfaceDebugInfo(
        string Strategy,
        IReadOnlyList<OuterCornerFillet>? OuterCornerFillets = null,
        IReadOnlyList<OuterCornerAttempt>? OuterCornerAttempts = null,
        IReadOnlyList<DebugBoundaryCurve>? BoundaryDebugCurves = null);

    private sealed record FilletPickPoint(
        string Label,
        Point3d Point);

    private sealed record FilletArcPiece(
        string Label,
        Curve Curve);

    private sealed record ArmEdgeRef(
        string Road,
        string Side,
        string ArmDirection);

    private sealed record OrderedOuterArm(
        double Angle,
        ArmEdgeRef LeftEdge,
        ArmEdgeRef RightEdge);

    private sealed record OuterCornerFillet(
        int CornerOrder,
        ArmEdgeRef CurrentEdge,
        ArmEdgeRef NextEdge,
        double Radius,
        double CurrentParameter,
        double NextParameter,
        Curve ArcCurve);

    private sealed record OuterCornerAttempt(
        int CornerOrder,
        bool Succeeded,
        string Reason,
        ArmEdgeRef? CurrentEdge,
        ArmEdgeRef? NextEdge,
        double RequestedRadius,
        double? ResolvedRadius,
        Point3d? DotPoint,
        IReadOnlyList<OuterCornerCandidate>? Candidates = null);

    private sealed record OuterCornerCandidate(
        string Label,
        double? Score,
        string Reason,
        Curve Curve);

    private sealed record ResolvedSemanticBoundaries(
        Curve? LeftEdge,
        Curve? RightEdge,
        Guid? LeftObjectId,
        Guid? RightObjectId);

    private sealed record ApproachPatch(
        string Road,
        string ArmDirection,
        string ObjectName,
        Curve BorderCurve);

}
