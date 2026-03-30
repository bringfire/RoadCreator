using RoadCreator.Core.Math;

namespace RoadCreator.Core.Profiles;

/// <summary>
/// Converts a RoadProfileDefinition into local cross-section point arrays
/// using the same coordinate convention as CrossSectionComputer:
///   X = lateral offset from centerline
///   Y = elevation relative to route
///   Z = 0
///
/// Pure computation — no Rhino dependency.
///
/// v1 scope: realizes only the pavement surface. Sidewalk, shoulder, and
/// median surfaces are not included in the section output.
/// </summary>
public static class ProfileCrossSectionBuilder
{
    /// <summary>
    /// Compute cross-section points from a road profile definition.
    ///
    /// Validates realizability before computing. Throws ArgumentException
    /// with a clear message if the profile cannot produce a valid road section.
    /// </summary>
    /// <param name="profile">The road profile definition.</param>
    /// <param name="crossfallStraightOverride">Optional override for straight crossfall (%).</param>
    /// <param name="crossfallCurveOverride">Optional override for curve crossfall (%).</param>
    /// <param name="curveDirection">Curve direction: 0=straight, ±1=full curve. Clamped to [-1,1].</param>
    /// <returns>Point3[] in local cross-section space (X=lateral, Y=elevation, Z=0).</returns>
    public static Point3[] ComputeSectionPoints(
        RoadProfileDefinition profile,
        double? crossfallStraightOverride,
        double? crossfallCurveOverride,
        double curveDirection)
    {
        // ── Realizability gate ────────────────────────────────────────────

        var validation = RoadProfileValidator.Validate(profile);
        if (!validation.FootprintReady)
            throw new System.ArgumentException(
                "Profile is not footprint-ready: requires at least one negative " +
                "and one positive offset feature. " +
                string.Join("; ", validation.Errors));

        // ── Resolve pavement span ────────────────────────────────────────

        var (leftOffset, rightOffset, surface) = ResolvePavementSpan(profile);

        // ── Resolve crossfall ────────────────────────────────────────────

        double p = crossfallStraightOverride
            ?? profile.CrossSectionDefaults?.CrossfallStraight
            ?? surface?.Crossfall
            ?? 0.0;

        double pmax = crossfallCurveOverride
            ?? profile.CrossSectionDefaults?.CrossfallCurve
            ?? surface?.Crossfall
            ?? 0.0;

        // ── Compute section ──────────────────────────────────────────────

        double clampedDir = System.Math.Max(-1.0, System.Math.Min(1.0, curveDirection));
        double M = 1.0 - System.Math.Abs(clampedDir);
        double Z = clampedDir;

        double leftEdgeWidth = System.Math.Abs(leftOffset);
        double rightEdgeWidth = System.Math.Abs(rightOffset);

        double zLeft = ComputeEdgeElevation(p, pmax, M, Z, leftEdgeWidth, isLeft: true);
        double zRight = ComputeEdgeElevation(p, pmax, M, Z, rightEdgeWidth, isLeft: false);

        return new[]
        {
            new Point3(leftOffset, zLeft, 0),
            new Point3(0, 0, 0),
            new Point3(rightOffset, zRight, 0),
        };
    }

    /// <summary>
    /// Reuse the exact VBScript crossfall formula from CrossSectionComputer:
    ///   Left:  ((M × (-p)) + (-pmax × Z)) / 100 × edgeWidth
    ///   Right: ((M × (-p)) + (+pmax × Z)) / 100 × edgeWidth
    /// </summary>
    private static double ComputeEdgeElevation(
        double p, double pmax, double M, double Z,
        double edgeWidth, bool isLeft)
    {
        double crossfallSign = isLeft ? -1.0 : 1.0;
        return ((M * (-p)) + (crossfallSign * pmax * Z)) / 100.0 * edgeWidth;
    }

    /// <summary>
    /// Resolve the pavement span from the profile.
    ///
    /// Priority:
    ///   1. Exactly one surface with type "pavement" — use its boundary features
    ///   2. Semantic fallback: find edge_of_pavement or carriageway_edge features
    ///   3. Error if neither yields a valid span
    ///
    /// Multiple pavement surfaces are an error in v1.
    /// Same-sign boundaries are an error.
    /// </summary>
    private static (double leftOffset, double rightOffset, ProfileSurface? surface)
        ResolvePavementSpan(RoadProfileDefinition profile)
    {
        // Try explicit pavement surface
        var pavementSurfaces = profile.Surfaces
            .Where(s => string.Equals(s.Type, ProfileSurfaceTypes.Pavement,
                System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pavementSurfaces.Count > 1)
            throw new System.ArgumentException(
                $"Profile has {pavementSurfaces.Count} pavement surfaces — " +
                "exactly one is required in v1");

        if (pavementSurfaces.Count == 1)
        {
            var surface = pavementSurfaces[0];
            if (surface.Between.Count != 2)
                throw new System.ArgumentException(
                    $"Pavement surface '{surface.Id}' must have exactly 2 boundary features");

            var featureMap = profile.Features.ToDictionary(
                f => f.Id, f => f, System.StringComparer.OrdinalIgnoreCase);

            if (!featureMap.TryGetValue(surface.Between[0], out var f0))
                throw new System.ArgumentException(
                    $"Pavement surface references unknown feature '{surface.Between[0]}'");
            if (!featureMap.TryGetValue(surface.Between[1], out var f1))
                throw new System.ArgumentException(
                    $"Pavement surface references unknown feature '{surface.Between[1]}'");

            double left = System.Math.Min(f0.Offset, f1.Offset);
            double right = System.Math.Max(f0.Offset, f1.Offset);

            ValidateSpan(left, right);
            return (left, right, surface);
        }

        // Semantic fallback: edge_of_pavement or carriageway_edge
        var semanticFeatures = profile.Features
            .Where(f => string.Equals(f.Type, RoadProfileFeatureTypes.EdgeOfPavement,
                            System.StringComparison.OrdinalIgnoreCase)
                     || string.Equals(f.Type, RoadProfileFeatureTypes.CarriagewayEdge,
                            System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (semanticFeatures.Count < 2)
            throw new System.ArgumentException(
                "Profile has no pavement surface and fewer than 2 " +
                "edge_of_pavement/carriageway_edge features — " +
                "cannot determine road surface boundaries");

        double semanticLeft = semanticFeatures.Min(f => f.Offset);
        double semanticRight = semanticFeatures.Max(f => f.Offset);

        ValidateSpan(semanticLeft, semanticRight);
        return (semanticLeft, semanticRight, null);
    }

    private static void ValidateSpan(double left, double right)
    {
        if (left >= 0)
            throw new System.ArgumentException(
                $"Left boundary offset ({left}) must be negative");
        if (right <= 0)
            throw new System.ArgumentException(
                $"Right boundary offset ({right}) must be positive");
    }
}
