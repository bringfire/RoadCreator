using global::Rhino;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Geometry.Intersect;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino;

/// <summary>Result of the route selection prompt.</summary>
public enum RoutePromptResult { NoRoads, RoadSelected, Alternative, Cancelled }

/// <summary>
/// Auto-detection utilities for finding road alignment components by layer hierarchy.
/// Implements the RC2 route selection and component discovery patterns.
///
/// VBScript auto-detection:
///   1. List top-level layers, exclude system layers (Terén, Default, etc.)
///   2. User selects a road from the filtered list
///   3. Find sublayer objects: axis from Tečnový polygon, niveleta from Niveleta, etc.
///
/// C# equivalent:
///   1. Find child layers of "RoadCreator" that match "Road_N" pattern
///   2. Present selection to user
///   3. Find sublayer objects by LayerScheme path
/// </summary>
public static class RouteDiscovery
{
    /// <summary>
    /// System layer names that should be excluded from road selection.
    /// Maps to VBScript: Terén, Default, 3D model terénu, RC 3D modely, Svahy, RC_Databaze
    /// </summary>
    private static readonly HashSet<string> SystemLayerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoadCreator",         // Root layer (contains road sublayers)
        LayerScheme.Terrain,   // "TERRAIN"
        LayerScheme.Database,  // "RC_Database"
        "Default",
        "DATABAZE",            // VBScript legacy database layer
        "Stromy databaze",     // Tree database
        "Znacky",              // Traffic signs
    };

    /// <summary>
    /// Get all available road names from the document.
    /// Finds layers matching "RoadCreator::Road_N" pattern.
    /// </summary>
    public static string[] GetAvailableRoads(RhinoDoc doc)
    {
        var roads = new List<string>();
        int rootIdx = doc.Layers.FindByFullPath(LayerScheme.Root, -1);
        if (rootIdx < 0)
            return Array.Empty<string>();

        var rootLayer = doc.Layers[rootIdx];
        var childLayers = rootLayer.GetChildren();
        if (childLayers == null)
            return Array.Empty<string>();

        foreach (var child in childLayers)
        {
            if (child.Name.StartsWith(RoadObjectNaming.RoadPrefix, StringComparison.Ordinal))
                roads.Add(child.Name);
        }

        roads.Sort(StringComparer.Ordinal);
        return roads.ToArray();
    }

    /// <summary>
    /// Find the axis curve (last tangent polygon segment) for a road.
    /// VBScript: finds object in Tečnový polygon where nameparts[1] == max segment count.
    /// C#: finds the segment with the highest index in the Tangent Polygon layer.
    /// </summary>
    public static Curve? FindAxisCurve(RhinoDoc doc, string roadName)
    {
        string tpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon);
        int layerIdx = doc.Layers.FindByFullPath(tpPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        // Find the segment with the highest index
        Curve? lastSegment = null;
        int maxIndex = -1;

        foreach (var obj in objects)
        {
            if (obj.Geometry is not Curve curve)
                continue;
            var name = obj.Attributes.Name;
            if (string.IsNullOrEmpty(name))
                continue;

            var parts = name.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int idx) && idx > maxIndex)
            {
                maxIndex = idx;
                lastSegment = curve;
            }
        }

        return lastSegment;
    }

    /// <summary>
    /// Find all tangent polygon segments as a joined curve (the full axis).
    /// Returns the curves sorted by segment index and joined.
    /// </summary>
    public static Curve? FindFullAxisCurve(RhinoDoc doc, string roadName)
    {
        string tpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon);
        int layerIdx = doc.Layers.FindByFullPath(tpPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        var segments = new SortedDictionary<int, Curve>();
        foreach (var obj in objects)
        {
            if (obj.Geometry is not Curve curve)
                continue;
            var name = obj.Attributes.Name;
            if (string.IsNullOrEmpty(name))
                continue;
            var parts = name.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int idx))
                segments[idx] = curve;
        }

        if (segments.Count == 0)
            return null;

        var curves = segments.Values.ToArray();
        if (curves.Length == 1)
            return curves[0];

        var joined = Curve.JoinCurves(curves, 0.1);
        return joined?.Length > 0 ? joined[0] : null;
    }

    /// <summary>
    /// Find the grade line (niveleta) curve for a road.
    /// VBScript: finds named curve in {road}::Niveleta layer.
    /// C#: finds any named curve in {road}::Grade Line layer.
    /// </summary>
    public static Curve? FindGradeLine(RhinoDoc doc, string roadName)
    {
        string glPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.GradeLine);
        int layerIdx = doc.Layers.FindByFullPath(glPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        // Return the first named curve (there should be exactly one grade line)
        foreach (var obj in objects)
        {
            if (obj.Geometry is Curve curve && !string.IsNullOrEmpty(obj.Attributes.Name))
                return curve;
        }

        // Fallback: return any curve on the layer
        foreach (var obj in objects)
        {
            if (obj.Geometry is Curve curve)
                return curve;
        }

        return null;
    }

    /// <summary>
    /// Find the reference elevation (datum) from the longitudinal profile layer.
    /// VBScript: finds a point named "{road} Podélný_profil {datum}" and parses the datum.
    /// C#: finds a point named "{road} LongProfile {datum}" on the Longitudinal Profile layer.
    /// </summary>
    public static double? FindProfileDatum(RhinoDoc doc, string roadName)
    {
        string lpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.LongitudinalProfile);
        int layerIdx = doc.Layers.FindByFullPath(lpPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        foreach (var obj in objects)
        {
            if (obj.Geometry is not global::Rhino.Geometry.Point)
                continue;
            if (RoadObjectNaming.TryParseLongProfileDatum(obj.Attributes.Name, out double datum))
                return datum;
        }

        return null;
    }

    /// <summary>
    /// Find stationing (important) points from the Stationing Points sublayer.
    /// Returns array of (name, location) tuples.
    /// </summary>
    public static (string Name, Point3d Location)[] FindStationingPoints(RhinoDoc doc, string roadName)
    {
        string stPtsPath = LayerScheme.BuildRoadPath(roadName,
            LayerScheme.TangentPolygon, LayerScheme.Stationing, LayerScheme.StationingPoints);
        int layerIdx = doc.Layers.FindByFullPath(stPtsPath, -1);
        if (layerIdx < 0)
            return Array.Empty<(string, Point3d)>();

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return Array.Empty<(string, Point3d)>();

        var result = new List<(string, Point3d)>();
        foreach (var obj in objects)
        {
            if (obj.Geometry is global::Rhino.Geometry.Point pt &&
                !string.IsNullOrEmpty(obj.Attributes.Name))
            {
                result.Add((obj.Attributes.Name, pt.Location));
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Check whether a 3D route already exists for a road.
    /// VBScript: completeness check before running.
    /// </summary>
    public static bool HasRoute3D(RhinoDoc doc, string roadName)
    {
        string routePath = LayerScheme.BuildRoadPath(roadName, LayerScheme.Route3D);
        int layerIdx = doc.Layers.FindByFullPath(routePath, -1);
        if (layerIdx < 0)
            return false;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        return objects != null && objects.Length > 0;
    }

    /// <summary>
    /// Find the profile origin point from the Longitudinal Profile layer.
    /// This is the point where the profile drawing starts (used for coordinate transform).
    /// </summary>
    public static Point3d? FindProfileOrigin(RhinoDoc doc, string roadName)
    {
        string lpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.LongitudinalProfile);
        int layerIdx = doc.Layers.FindByFullPath(lpPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        foreach (var obj in objects)
        {
            if (obj.Geometry is global::Rhino.Geometry.Point pt &&
                !string.IsNullOrEmpty(obj.Attributes.Name) &&
                obj.Attributes.Name.Contains(RoadObjectNaming.LongProfileSuffix))
            {
                return pt.Location;
            }
        }

        return null;
    }

    /// <summary>
    /// Present a route selection dialog showing available roads.
    /// </summary>
    /// <param name="doc">Rhino document</param>
    /// <param name="alternativeOption">Extra option label (e.g. "Manual", "Standalone"), or null for none</param>
    /// <returns>Result enum and selected road name (empty if not selected)</returns>
    public static (RoutePromptResult Result, string RoadName) PromptRouteSelection(
        RhinoDoc doc, string? alternativeOption = "Manual")
    {
        var availableRoads = GetAvailableRoads(doc);
        if (availableRoads.Length == 0)
            return (RoutePromptResult.NoRoads, "");

        var getRoute = new GetOption();
        getRoute.SetCommandPrompt(Strings.SelectRoute);
        foreach (var road in availableRoads)
            getRoute.AddOption(road);
        if (alternativeOption != null)
            getRoute.AddOption(alternativeOption);

        if (getRoute.Get() != GetResult.Option)
            return (RoutePromptResult.Cancelled, "");

        int routeIdx = getRoute.Option().Index - 1;
        if (routeIdx >= 0 && routeIdx < availableRoads.Length)
            return (RoutePromptResult.RoadSelected, availableRoads[routeIdx]);

        return (RoutePromptResult.Alternative, "");
    }

    /// <summary>
    /// Find the 3D route curve for a road from the Route 3D layer.
    /// Looks for a curve whose name contains the Route3DSuffix.
    /// </summary>
    public static Curve? FindRoute3DCurve(RhinoDoc doc, string roadName)
    {
        string route3DPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.Route3D);
        int layerIdx = doc.Layers.FindByFullPath(route3DPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        foreach (var obj in objects)
        {
            if (obj.Geometry is Curve c &&
                obj.Attributes.Name?.Contains(RoadObjectNaming.Route3DSuffix) == true)
                return c;
        }
        return null;
    }

    /// <summary>
    /// Sample elevation from a profile-space curve at a given chainage.
    /// Profile curves use X = chainage, Y = (elevation - datum) × VerticalExaggeration.
    /// Returns the real-world elevation, or null if no intersection found.
    /// </summary>
    public static double? SampleProfileElevation(Curve profileCurve, double chainage,
        double datum, double tolerance)
    {
        var vertLine = new LineCurve(
            new Point3d(chainage, -100000, 0),
            new Point3d(chainage, 100000, 0));
        var intersections = Intersection.CurveCurve(profileCurve, vertLine, tolerance, 0);
        if (intersections != null && intersections.Count > 0)
            return datum + intersections[0].PointA.Y / ProfileConstants.VerticalExaggeration;
        return null;
    }
}
