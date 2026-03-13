using System.Drawing;
using global::Rhino;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Footprint;

/// <summary>
/// Result of a single applied footprint line.
/// </summary>
public sealed record FootprintLine(string FeatureId, string Role, Guid ObjectId);

/// <summary>
/// Applies an OffsetProfile + StyleSet to a centerline curve, producing
/// offset curves in the Rhino document.
///
/// Offset direction convention (right-hand rule, top-down XY view):
///   negative offset → left of curve direction  (WorldXY plane, normal +Z)
///   positive offset → right of curve direction (inverted plane, normal -Z)
///   zero            → the centerline itself (no offset computed)
///
/// The centerline is projected to Z=0 before offsetting. This is intentional:
/// RC_RoadFootprint is a 2D plan linework tool. 3D surface generation uses
/// RC_Road3D / RC_UrbanRoad instead.
/// </summary>
public static class FootprintApplicator
{
    // Offset planes for Curve.Offset(plane, distance, tol, style):
    //   Curve.Offset offsets to the direction given by (tangent × plane.Normal).
    //   WorldXY (normal = +Z): tangent × (+Z) = right-hand cross → offsets LEFT of travel.
    //   RightPlane (normal = -Z): tangent × (-Z) → offsets RIGHT of travel.
    // Our API contract: negative offset = left, positive = right.
    private static readonly Plane LeftPlane  = Plane.WorldXY;
    private static readonly Plane RightPlane =
        new(Point3d.Origin, Vector3d.XAxis, -Vector3d.YAxis); // normal = XAxis × (-YAxis) = -Z

    /// <summary>
    /// Apply the profile to the centerline and add all offset curves to the document.
    /// Returns one FootprintLine record per created object, in left-to-right order.
    /// </summary>
    public static IReadOnlyList<FootprintLine> Apply(
        RhinoDoc doc,
        Curve centerline,
        OffsetProfile profile,
        StyleSet styleSet)
    {
        // Project centerline to XY (Z=0) for 2D offset
        var flat = ProjectToXY(centerline);
        double tol = doc.ModelAbsoluteTolerance;
        var layers = new LayerManager(doc);

        var result = new List<FootprintLine>();

        foreach (var feature in profile.OrderedFeatures)
        {
            double absOffset = profile.ResolveAbsoluteOffset(feature);
            var style = styleSet.FindStyleOrDefault(feature.StyleRef, DefaultStyles.Fallback);

            Curve? curve = absOffset == 0.0
                ? flat.DuplicateCurve()
                : OffsetCurve(flat, absOffset, tol);

            if (curve == null)
            {
                RhinoApp.WriteLine($"RC_RoadFootprint: offset failed for feature '{feature.Id}' at {absOffset:F3} m.");
                continue;
            }

            var attrs = BuildAttributes(doc, style, layers);
            attrs.Name = feature.Id;

            var id = doc.Objects.AddCurve(curve, attrs);
            if (id != Guid.Empty)
                result.Add(new FootprintLine(feature.Id, feature.Role, id));
        }

        return result;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static Curve ProjectToXY(Curve curve)
    {
        var dup = curve.DuplicateCurve();
        dup.Transform(Transform.PlanarProjection(Plane.WorldXY));
        return dup;
    }

    private static Curve? OffsetCurve(Curve flat, double signedOffset, double tol)
    {
        // Positive offset = right (normal -Z), negative offset = left (normal +Z)
        var plane = signedOffset > 0 ? RightPlane : LeftPlane;
        double dist = Math.Abs(signedOffset);

        var offsets = flat.Offset(plane, dist, tol, CurveOffsetCornerStyle.Sharp);
        if (offsets == null || offsets.Length == 0)
            return null;

        // Join multiple segments if the offset produced more than one curve
        if (offsets.Length == 1)
            return offsets[0];

        var joined = Curve.JoinCurves(offsets, tol);
        return joined?.Length > 0 ? joined[0] : offsets[0];
    }

    private static ObjectAttributes BuildAttributes(RhinoDoc doc, StyleEntry style,
        LayerManager layers)
    {
        var attrs = new ObjectAttributes();

        // Layer — delegate to LayerManager (canonical hierarchy builder)
        attrs.LayerIndex = layers.EnsureLayer(style.Layer);

        // Linetype
        int ltIdx = doc.Linetypes.Find(style.Linetype);
        if (ltIdx >= 0)
        {
            attrs.LinetypeIndex = ltIdx;
            attrs.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;
        }
        // else: inherits from layer

        // Print weight
        if (style.PrintWidthMm > 0)
        {
            attrs.PlotWeight = style.PrintWidthMm;
            attrs.PlotWeightSource = ObjectPlotWeightSource.PlotWeightFromObject;
        }

        // Object color override
        if (!string.IsNullOrEmpty(style.Color))
        {
            var color = ParseColor(style.Color);
            if (color.HasValue)
            {
                attrs.ObjectColor = color.Value;
                attrs.ColorSource = ObjectColorSource.ColorFromObject;
            }
        }

        return attrs;
    }

    private static Color? ParseColor(string colorStr)
    {
        if (string.IsNullOrEmpty(colorStr)) return null;
        try
        {
            if (colorStr.StartsWith('#') && colorStr.Length == 7)
            {
                int r = Convert.ToInt32(colorStr[1..3], 16);
                int g = Convert.ToInt32(colorStr[3..5], 16);
                int b = Convert.ToInt32(colorStr[5..7], 16);
                return Color.FromArgb(r, g, b);
            }
            // Try named colour
            var named = Color.FromName(colorStr);
            return named.IsKnownColor ? named : (Color?)null;
        }
        catch
        {
            return null;
        }
    }
}
