using global::Rhino;
using global::Rhino.Geometry;
using RoadCreator.Rhino;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Resolve commonly used road curves from the document for scripted accessory commands.
/// </summary>
internal static class RoadCurveResolver
{
    public static Curve? ResolveNearestBoundaryCurve(
        RhinoDoc doc, string roadName, Point3d referencePoint)
    {
        foreach (var layerPath in new[]
        {
            LayerScheme.BuildRoadPath(roadName, LayerScheme.Road3D),
            LayerScheme.BuildRoadPath(roadName, "Sections"),
        })
        {
            int layerIdx = doc.Layers.FindByFullPath(layerPath, -1);
            if (layerIdx < 0)
                continue;

            var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (objects == null || objects.Length == 0)
                continue;

            Curve? bestCurve = null;
            double bestDistance = double.MaxValue;

            foreach (var obj in objects)
            {
                if (obj.Geometry is not Curve curve)
                    continue;

                var name = obj.Attributes.Name ?? "";
                if (!name.Contains("boundary", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!curve.ClosestPoint(referencePoint, out double t))
                    continue;

                double distance = curve.PointAt(t).DistanceTo(referencePoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCurve = curve;
                }
            }

            if (bestCurve != null)
                return bestCurve;
        }

        return null;
    }

    public static Curve[] ResolveBoundaryCurves(RhinoDoc doc, string roadName)
    {
        foreach (var layerPath in new[]
        {
            LayerScheme.BuildRoadPath(roadName, LayerScheme.Road3D),
            LayerScheme.BuildRoadPath(roadName, "Sections"),
        })
        {
            int layerIdx = doc.Layers.FindByFullPath(layerPath, -1);
            if (layerIdx < 0)
                continue;

            var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (objects == null || objects.Length == 0)
                continue;

            var curves = new List<Curve>();
            foreach (var obj in objects)
            {
                if (obj.Geometry is not Curve curve)
                    continue;

                var name = obj.Attributes.Name ?? "";
                if (name.Contains("boundary", StringComparison.OrdinalIgnoreCase))
                    curves.Add(curve);
            }

            if (curves.Count >= 2)
                return curves.ToArray();
        }

        return Array.Empty<Curve>();
    }

    public static Curve? ResolveCenterGuideCurve(RhinoDoc doc, string roadName)
    {
        return RouteDiscovery.FindRoute3DCurve(doc, roadName)
            ?? RouteDiscovery.FindFullAxisCurve(doc, roadName);
    }
}
