using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.CrossSection;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Road;

/// <summary>
/// Generates a 3D road surface by sweeping cross-section profiles along a 3D route.
/// Converts from RC2_3DSilnice_CZ.rvb.
///
/// Algorithm:
///   1. Select 3D route curve and important points (from Phase 2 output)
///   2. Choose road category (S 6.5 through D 4/8)
///   3. Set crossfall parameters (straight and curve)
///   4. Optionally include road verges
///   5. At each important point, compute cross-section profile:
///      - Determine curve direction (L/R/straight) from point name
///      - Look up widening from curve radius
///      - Build profile polyline with crossfall
///      - Orient profile perpendicular to route at that station
///   6. Sweep all profiles along the 3D route (Rhino.AddSweep1)
///   7. Extract edge curves as road boundaries
/// </summary>
[System.Runtime.InteropServices.Guid("E5F6A7B8-C9D0-2E3F-4A5B-6C7D8E9F0A1B")]
public class Road3DCommand : Command
{
    private sealed record Road3DInputs(
        string RoadName,
        Curve RouteCurve,
        RoadCategory Category,
        double CrossfallStraight,
        double CrossfallCurve,
        bool IncludeVerge,
        double VergeWidth,
        IReadOnlyList<RoadGeometryHelper.StationInfo> Stations);

    public override string EnglishName => "RC_Road3D";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        // RC2: Auto-detect 3D route via RouteDiscovery, with manual fallback
        string roadName = "";
        Curve? routeCurve = null;

        var (choice, selectedRoad) = RouteDiscovery.PromptRouteSelection(doc, "Manual");
        if (choice == RoutePromptResult.Cancelled)
            return Result.Cancel;
        if (choice == RoutePromptResult.RoadSelected)
        {
            roadName = selectedRoad;
            routeCurve = RouteDiscovery.FindRoute3DCurve(doc, roadName);
        }

        if (routeCurve == null)
        {
            var getRoute = new GetObject();
            getRoute.SetCommandPrompt(Strings.Select3DRoute);
            getRoute.GeometryFilter = ObjectType.Curve;
            if (getRoute.Get() != GetResult.Object)
                return Result.Cancel;
            routeCurve = getRoute.Object(0).Curve();
            var routeObject = getRoute.Object(0).Object();
            if (routeCurve == null || routeObject == null)
                return Result.Cancel;
            if (string.IsNullOrEmpty(roadName))
                roadName = RoadObjectNaming.ParseRoadName(routeObject.Attributes.Name) ?? "";
        }

        var parameterResult = PromptForRoadParameters(
            out var category,
            out double crossfallStraight,
            out double crossfallCurve,
            out bool includeVerge,
            out double vergeWidth);
        if (parameterResult != Result.Success)
            return parameterResult;

        var stationResult = ResolveStationsInteractive(doc, roadName, out var stations);
        if (stationResult != Result.Success)
            return stationResult;

        var inputs = new Road3DInputs(
            roadName,
            routeCurve,
            category,
            crossfallStraight,
            crossfallCurve,
            includeVerge,
            vergeWidth,
            stations);

        return RunCore(doc, inputs);
    }

    private Result RunScripted(RhinoDoc doc)
    {
        var getRoad = new GetString();
        getRoad.SetCommandPrompt("Road name");
        if (getRoad.Get() != GetResult.String)
            return Result.Cancel;

        string roadName = getRoad.StringResult().Trim();
        if (string.IsNullOrEmpty(roadName))
        {
            RhinoApp.WriteLine("Road name is required in scripted mode.");
            return Result.Failure;
        }

        var routeCurve = RouteDiscovery.FindRoute3DCurve(doc, roadName);
        if (routeCurve == null)
        {
            RhinoApp.WriteLine($"No 3D route found for road '{roadName}'.");
            return Result.Failure;
        }

        var parameterResult = PromptForRoadParameters(
            out var category,
            out double crossfallStraight,
            out double crossfallCurve,
            out bool includeVerge,
            out double vergeWidth);
        if (parameterResult != Result.Success)
            return parameterResult;

        var stations = ResolveStationsFromRouteLayer(doc, roadName);
        if (stations.Count < 2)
        {
            RhinoApp.WriteLine(
                $"Need at least 2 important points on '{LayerScheme.BuildRoadPath(roadName, LayerScheme.Route3D, LayerScheme.ImportantPoints)}' for scripted RC_Road3D.");
            return Result.Failure;
        }

        var inputs = new Road3DInputs(
            roadName,
            routeCurve,
            category,
            crossfallStraight,
            crossfallCurve,
            includeVerge,
            vergeWidth,
            stations);

        return RunCore(doc, inputs);
    }

    private Result PromptForRoadParameters(
        out RoadCategory category,
        out double crossfallStraight,
        out double crossfallCurve,
        out bool includeVerge,
        out double vergeWidth)
    {
        category = RoadCategory.S65;
        crossfallStraight = 0;
        crossfallCurve = 0;
        includeVerge = false;
        vergeWidth = 0;

        var categoryNames = new string[RoadCategory.All.Count];
        for (int i = 0; i < RoadCategory.All.Count; i++)
            categoryNames[i] = RoadCategory.All[i].Code;

        var getCat = new GetOption();
        getCat.SetCommandPrompt(Strings.SelectRoadCategory);
        for (int i = 0; i < categoryNames.Length; i++)
        {
            string optionName = categoryNames[i].Replace(" ", "_").Replace(".", "_").Replace("/", "_");
            getCat.AddOption(optionName);
        }
        if (getCat.Get() != GetResult.Option)
            return Result.Cancel;
        int catIdx = getCat.OptionIndex() - 1; // 1-based
        if (catIdx < 0 || catIdx >= RoadCategory.All.Count)
            return Result.Cancel;
        category = RoadCategory.All[catIdx];

        var getCrossfallStraight = new GetNumber();
        getCrossfallStraight.SetCommandPrompt(Strings.EnterCrossfallStraight);
        getCrossfallStraight.SetDefaultNumber(2.5);
        getCrossfallStraight.SetLowerLimit(2.0, false);
        getCrossfallStraight.SetUpperLimit(4.0, false);
        if (getCrossfallStraight.Get() != GetResult.Number)
            return Result.Cancel;
        crossfallStraight = getCrossfallStraight.Number();

        var getCrossfallCurve = new GetNumber();
        getCrossfallCurve.SetCommandPrompt(Strings.EnterCrossfallCurve);
        getCrossfallCurve.SetDefaultNumber(4.0);
        getCrossfallCurve.SetLowerLimit(4.0, false);
        getCrossfallCurve.SetUpperLimit(20.0, false);
        if (getCrossfallCurve.Get() != GetResult.Number)
            return Result.Cancel;
        crossfallCurve = getCrossfallCurve.Number();

        var getVerge = new GetOption();
        getVerge.SetCommandPrompt(Strings.IncludeVerge);
        int yesIdx = getVerge.AddOption("Yes");
        getVerge.AddOption("No");
        if (getVerge.Get() != GetResult.Option)
            return Result.Cancel;
        includeVerge = getVerge.OptionIndex() == yesIdx;

        if (includeVerge)
        {
            var getEquip = new GetOption();
            getEquip.SetCommandPrompt(Strings.VergeEquipment);
            int guardIdx = getEquip.AddOption("Guardrail");
            getEquip.AddOption("RoadPoles");
            if (getEquip.Get() != GetResult.Option)
                return Result.Cancel;
            bool hasGuardrail = getEquip.OptionIndex() == guardIdx;
            vergeWidth = CrossSectionComputer.ComputeVergeWidth(hasGuardrail);
        }

        return Result.Success;
    }

    private Result ResolveStationsInteractive(
        RhinoDoc doc,
        string roadName,
        out IReadOnlyList<RoadGeometryHelper.StationInfo> stations)
    {
        stations = ResolveStationsFromRouteLayer(doc, roadName);
        if (stations.Count >= 2)
        {
            RhinoApp.WriteLine($"Auto-detected {stations.Count} important points from route.");
            return Result.Success;
        }

        var manualPoints = new List<(string Name, Point3d Location)>();

        doc.Views.RedrawEnabled = true;
        var getPoints = new GetObject();
        getPoints.SetCommandPrompt(Strings.SelectImportantPoints);
        getPoints.GeometryFilter = ObjectType.Point;
        getPoints.EnablePreSelect(true, true);
        getPoints.GetMultiple(2, 0);
        doc.Views.RedrawEnabled = false;

        if (getPoints.CommandResult() != Result.Success || getPoints.ObjectCount < 2)
        {
            RhinoApp.WriteLine(Strings.NeedAtLeast2Points);
            return Result.Cancel;
        }

        for (int i = 0; i < getPoints.ObjectCount; i++)
        {
            var ptObj = getPoints.Object(i).Object();
            var ptGeo = getPoints.Object(i).Point();
            if (ptObj == null || ptGeo == null)
                continue;

            manualPoints.Add((ptObj.Attributes.Name ?? "", ptGeo.Location));
        }

        stations = ParseStationInfos(manualPoints);
        if (stations.Count < 2)
        {
            RhinoApp.WriteLine(Strings.NeedAtLeast2Points);
            return Result.Failure;
        }

        return Result.Success;
    }

    private Result RunCore(RhinoDoc doc, Road3DInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            var layers = new LayerManager(doc);
            string modelPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName, LayerScheme.Road3D);
            int modelLayerIdx = layers.EnsureLayer(modelPath, System.Drawing.Color.FromArgb(255, 127, 0));

            string profilesPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName,
                LayerScheme.Road3D, LayerScheme.CrossSectionProfiles);
            int profilesLayerIdx = layers.EnsureLayer(profilesPath, System.Drawing.Color.Red);

            var profileCurves = new List<Curve>();
            var profileAttrs = new ObjectAttributes { LayerIndex = profilesLayerIdx };

            foreach (var station in inputs.Stations)
            {
                double curveDirection = RoadGeometryHelper.ParseCurveDirection(station.Direction);
                double radius = RoadGeometryHelper.GetRadiusAtPoint(inputs.RouteCurve, station.Point);
                double widening = curveDirection != 0
                    ? WideningTable.ComputeWidening(radius, inputs.Category)
                    : 0;

                var profilePts = CrossSectionComputer.ComputeProfilePoints(
                    inputs.Category,
                    widening,
                    inputs.CrossfallStraight,
                    inputs.CrossfallCurve,
                    curveDirection,
                    inputs.IncludeVerge,
                    inputs.VergeWidth);

                var rhinoPts = new List<Point3d>();
                foreach (var p in profilePts)
                    rhinoPts.Add(new Point3d(p.X, 0, p.Y));

                var polyline = new Polyline(rhinoPts);
                var polyCurve = polyline.ToPolylineCurve();

                if (!inputs.RouteCurve.ClosestPoint(station.Point, out double t))
                    continue;
                var tangent = inputs.RouteCurve.TangentAt(t);
                var routePoint = inputs.RouteCurve.PointAt(t);

                tangent.Unitize();
                var lateral = Vector3d.CrossProduct(Vector3d.ZAxis, tangent);
                if (lateral.IsTiny(1e-10))
                    lateral = Vector3d.XAxis;
                lateral.Unitize();
                var up = Vector3d.CrossProduct(tangent, lateral);
                up.Unitize();
                var sourcePlane = Plane.WorldXY;
                var targetPlane = new Plane(routePoint, lateral, up);
                var xform = Transform.PlaneToPlane(sourcePlane, targetPlane);
                polyCurve.Transform(xform);

                doc.Objects.AddCurve(polyCurve, profileAttrs);
                profileCurves.Add(polyCurve);
            }

            if (profileCurves.Count < 2)
            {
                RhinoApp.WriteLine(Strings.NotEnoughProfiles);
                return Result.Failure;
            }

            var sweep = new global::Rhino.Geometry.SweepOneRail();
            var breps = sweep.PerformSweep(inputs.RouteCurve, profileCurves);

            if (breps == null || breps.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SweepFailed);
                return Result.Failure;
            }

            foreach (var brep in breps)
            {
                var modelAttrs = new ObjectAttributes
                {
                    LayerIndex = modelLayerIdx,
                    Name = $"{inputs.RoadName} 3D_model"
                };
                doc.Objects.AddBrep(brep, modelAttrs);

                var edges = brep.DuplicateEdgeCurves(true);
                if (edges == null || edges.Length == 0)
                    continue;

                System.Array.Sort(edges, (a, b) => b.GetLength().CompareTo(a.GetLength()));
                int boundaryCount = System.Math.Min(2, edges.Length);
                for (int e = 0; e < boundaryCount; e++)
                {
                    doc.Objects.AddCurve(edges[e], new ObjectAttributes
                    {
                        LayerIndex = modelLayerIdx,
                        Name = $"{inputs.RoadName} boundary"
                    });
                }
            }

            layers.LockLayer(profilesPath);

            RhinoApp.WriteLine(
                $"3D road model created: {inputs.Category.Code}, {inputs.Stations.Count} profiles, verge={inputs.IncludeVerge}.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static IReadOnlyList<RoadGeometryHelper.StationInfo> ResolveStationsFromRouteLayer(
        RhinoDoc doc, string roadName)
    {
        if (string.IsNullOrEmpty(roadName))
            return Array.Empty<RoadGeometryHelper.StationInfo>();

        string impPtsPath = LayerScheme.BuildRoadPath(
            roadName, LayerScheme.Route3D, LayerScheme.ImportantPoints);
        int impPtsLayerIdx = doc.Layers.FindByFullPath(impPtsPath, -1);
        if (impPtsLayerIdx < 0)
            return Array.Empty<RoadGeometryHelper.StationInfo>();

        var layerObjects = doc.Objects.FindByLayer(doc.Layers[impPtsLayerIdx]);
        if (layerObjects == null)
            return Array.Empty<RoadGeometryHelper.StationInfo>();

        var points = new List<(string Name, Point3d Location)>();
        foreach (var obj in layerObjects)
        {
            if (obj.Geometry is global::Rhino.Geometry.Point pt)
                points.Add((obj.Attributes.Name ?? "", pt.Location));
        }

        return ParseStationInfos(points);
    }

    private static IReadOnlyList<RoadGeometryHelper.StationInfo> ParseStationInfos(
        IEnumerable<(string Name, Point3d Location)> points)
    {
        var stations = new List<RoadGeometryHelper.StationInfo>();
        foreach (var (name, location) in points)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string direction = "R";
            double chainage = 0;
            if (parts.Length >= 3)
            {
                direction = parts[1];
                double.TryParse(
                    parts[2],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out chainage);
            }
            else if (parts.Length >= 2)
            {
                direction = parts[1];
            }

            stations.Add(new RoadGeometryHelper.StationInfo(location, direction, chainage));
        }

        stations.Sort((a, b) => a.Chainage.CompareTo(b.Chainage));
        return stations;
    }
}
