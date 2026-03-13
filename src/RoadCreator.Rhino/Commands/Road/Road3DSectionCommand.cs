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
/// Generates a 3D road surface for a subsection of the route.
/// Converts from RC_3DSilnice_Useky_CZ.rvb.
///
/// Unlike RC_Road3D (which models the entire route), this command lets the user
/// pick start and end points on the 3D route to model only a portion.
///
/// Algorithm:
///   1. Select route (auto-detect or manual) and road category
///   2. User picks start and/or end points on the route to define the section
///   3. Trim 3D route curve to the section
///   4. Collect important points within the section
///   5. Interpolate new transition points at section boundaries if needed
///   6. Build cross-section profiles at each station
///   7. Sweep profiles along the section curve
/// </summary>
[System.Runtime.InteropServices.Guid("E5F6A7B8-C9D0-3E4F-5A6B-7C8D9E0F1A2B")]
public class Road3DSectionCommand : Command
{
    private sealed record Road3DSectionInputs(
        string RoadName,
        Curve RouteCurve,
        Curve SectionCurve,
        RoadCategory Category,
        double CrossfallStraight,
        double CrossfallCurve,
        bool IncludeVerge,
        double VergeWidth);

    public override string EnglishName => "RC_Road3DSection";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        // --- Step 1: Select route (auto-detect or manual) ---
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

        return PromptSectionParametersAndRun(doc, roadName, routeCurve);
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

        return PromptSectionParametersAndRun(doc, roadName, routeCurve);
    }

    private Result PromptSectionParametersAndRun(RhinoDoc doc, string roadName, Curve routeCurve)
    {
        // --- Step 2: Select section boundaries ---
        var getStart = new GetPoint();
        getStart.SetCommandPrompt("Select section start point on route (Enter to skip)");
        getStart.AcceptNothing(true);
        getStart.Constrain(routeCurve, false);

        Point3d? sectionStart = null;
        if (getStart.Get() == GetResult.Point)
            sectionStart = getStart.Point();

        var getEnd = new GetPoint();
        getEnd.SetCommandPrompt("Select section end point on route (Enter to skip)");
        getEnd.AcceptNothing(true);
        getEnd.Constrain(routeCurve, false);

        Point3d? sectionEnd = null;
        if (getEnd.Get() == GetResult.Point)
            sectionEnd = getEnd.Point();

        // Trim route curve to section
        Curve sectionCurve = routeCurve;
        if (sectionStart.HasValue || sectionEnd.HasValue)
        {
            double t0 = routeCurve.Domain.T0;
            double t1 = routeCurve.Domain.T1;

            if (sectionStart.HasValue)
            {
                routeCurve.ClosestPoint(sectionStart.Value, out double ts);
                t0 = ts;
            }
            if (sectionEnd.HasValue)
            {
                routeCurve.ClosestPoint(sectionEnd.Value, out double te);
                t1 = te;
            }

            if (t0 > t1) (t0, t1) = (t1, t0); // swap if reversed

            var trimmed = routeCurve.Trim(t0, t1);
            if (trimmed != null)
                sectionCurve = trimmed;
        }

        // --- Step 3: Road category and crossfall ---
        var parameterResult = PromptForRoadParameters(
            out var category,
            out double crossfallStraight,
            out double crossfallCurve,
            out bool includeVerge,
            out double vergeWidth);
        if (parameterResult != Result.Success)
            return parameterResult;

        var inputs = new Road3DSectionInputs(
            roadName,
            routeCurve,
            sectionCurve,
            category,
            crossfallStraight,
            crossfallCurve,
            includeVerge,
            vergeWidth);

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

        var getCat = new GetOption();
        getCat.SetCommandPrompt(Strings.SelectRoadCategory);
        for (int i = 0; i < RoadCategory.All.Count; i++)
        {
            string optionName = RoadCategory.All[i].Code
                .Replace(" ", "_").Replace(".", "_").Replace("/", "_");
            getCat.AddOption(optionName);
        }
        if (getCat.Get() != GetResult.Option)
            return Result.Cancel;
        int catIdx = getCat.OptionIndex() - 1;
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

    private Result RunCore(RhinoDoc doc, Road3DSectionInputs inputs)
    {

        doc.Views.RedrawEnabled = false;

        try
        {
            // --- Step 4: Collect important points within section ---
            var stations = new List<RoadGeometryHelper.StationInfo>();

            if (!string.IsNullOrEmpty(inputs.RoadName))
            {
                string impPtsPath = LayerScheme.BuildRoadPath(inputs.RoadName,
                    LayerScheme.Route3D, LayerScheme.ImportantPoints);
                int impPtsLayerIdx = doc.Layers.FindByFullPath(impPtsPath, -1);
                if (impPtsLayerIdx >= 0)
                {
                    var layerObjects = doc.Objects.FindByLayer(doc.Layers[impPtsLayerIdx]);
                    if (layerObjects != null)
                    {
                        foreach (var obj in layerObjects)
                        {
                            if (obj.Geometry is not global::Rhino.Geometry.Point pt) continue;
                            var loc = pt.Location;

                            // Check if this point is within the section
                            if (!inputs.SectionCurve.ClosestPoint(loc, out double t)) continue;
                            var closestPt = inputs.SectionCurve.PointAt(t);
                            if (closestPt.DistanceTo(loc) > 1.0) continue; // not on section

                            string ptName = obj.Attributes.Name ?? "";
                            var parts = ptName.Split(' ');
                            string direction = "R";
                            double chainage = 0;
                            if (parts.Length >= 3)
                            {
                                direction = parts[1];
                                double.TryParse(parts[2],
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out chainage);
                            }
                            else if (parts.Length >= 2)
                            {
                                direction = parts[1];
                            }

                            stations.Add(new RoadGeometryHelper.StationInfo(loc, direction, chainage));
                        }
                    }
                }
            }

            // Add section boundary points if no important points are near them
            AddBoundaryStation(inputs.SectionCurve, inputs.SectionCurve.PointAtStart, stations);
            AddBoundaryStation(inputs.SectionCurve, inputs.SectionCurve.PointAtEnd, stations);

            // Sort by chainage
            stations.Sort((a, b) => a.Chainage.CompareTo(b.Chainage));

            if (stations.Count < 2)
            {
                // Fallback: add start and end points
                stations.Clear();
                stations.Add(new RoadGeometryHelper.StationInfo(inputs.SectionCurve.PointAtStart, "R", 0));
                stations.Add(new RoadGeometryHelper.StationInfo(inputs.SectionCurve.PointAtEnd, "R",
                    inputs.SectionCurve.GetLength()));
            }

            // --- Step 5: Build profiles and sweep ---
            var layers = new LayerManager(doc);
            string sectionPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName, "Sections");
            int sectionLayerIdx = layers.EnsureLayer(sectionPath,
                System.Drawing.Color.FromArgb(255, 127, 0));

            string profilesPath = LayerScheme.BuildOptionalRoadPath(inputs.RoadName,
                "Sections", LayerScheme.CrossSectionProfiles);
            int profilesLayerIdx = layers.EnsureLayer(profilesPath, System.Drawing.Color.Red);

            var profileCurves = new List<Curve>();
            var profileAttrs = new ObjectAttributes { LayerIndex = profilesLayerIdx };

            foreach (var station in stations)
            {
                double curveDirection = RoadGeometryHelper.ParseCurveDirection(station.Direction);
                double radius = RoadGeometryHelper.GetRadiusAtPoint(inputs.SectionCurve, station.Point);

                double widening = (curveDirection != 0)
                    ? WideningTable.ComputeWidening(radius, inputs.Category)
                    : 0;

                var profilePts = CrossSectionComputer.ComputeProfilePoints(
                    inputs.Category, widening, inputs.CrossfallStraight, inputs.CrossfallCurve,
                    curveDirection, inputs.IncludeVerge, inputs.VergeWidth);

                var rhinoPts = new List<Point3d>();
                foreach (var p in profilePts)
                    rhinoPts.Add(new Point3d(p.X, 0, p.Y));

                var polyline = new Polyline(rhinoPts);
                var polyCurve = polyline.ToPolylineCurve();

                if (!inputs.SectionCurve.ClosestPoint(station.Point, out double t))
                    continue;
                var tangent = inputs.SectionCurve.TangentAt(t);
                var routePoint = inputs.SectionCurve.PointAt(t);

                tangent.Unitize();
                var lateral = Vector3d.CrossProduct(Vector3d.ZAxis, tangent);
                if (lateral.IsTiny(1e-10))
                    lateral = Vector3d.XAxis;
                lateral.Unitize();
                var up = Vector3d.CrossProduct(tangent, lateral);
                up.Unitize();
                var xform = Transform.PlaneToPlane(Plane.WorldXY,
                    new Plane(routePoint, lateral, up));
                polyCurve.Transform(xform);

                doc.Objects.AddCurve(polyCurve, profileAttrs);
                profileCurves.Add(polyCurve);
            }

            if (profileCurves.Count < 2)
            {
                RhinoApp.WriteLine(Strings.NotEnoughProfiles);
                return Result.Failure;
            }

            // Sweep profiles along section curve
            var sweep = new SweepOneRail();
            var breps = sweep.PerformSweep(inputs.SectionCurve, profileCurves);

            if (breps == null || breps.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SweepFailed);
                return Result.Failure;
            }

            foreach (var brep in breps)
            {
                doc.Objects.AddBrep(brep, new ObjectAttributes
                {
                    LayerIndex = sectionLayerIdx,
                    Name = $"{inputs.RoadName} 3D_section"
                });

                // Extract boundary edges
                var edges = brep.DuplicateEdgeCurves(true);
                if (edges != null && edges.Length > 0)
                {
                    System.Array.Sort(edges, (a, b) => b.GetLength().CompareTo(a.GetLength()));
                    int boundaryCount = System.Math.Min(2, edges.Length);
                    for (int e = 0; e < boundaryCount; e++)
                    {
                        doc.Objects.AddCurve(edges[e], new ObjectAttributes
                        {
                            LayerIndex = sectionLayerIdx,
                            Name = $"{inputs.RoadName} section_boundary"
                        });
                    }
                }
            }

            layers.LockLayer(profilesPath);

            RhinoApp.WriteLine($"3D road section created: {inputs.Category.Code}, " +
                $"{stations.Count} profiles, length = {inputs.SectionCurve.GetLength():F1}m.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    /// <summary>
    /// Add a boundary point as a station if no existing station is near it.
    /// Uses "R" (straight) direction for interpolated boundary points.
    /// </summary>
    private static void AddBoundaryStation(Curve sectionCurve, Point3d boundaryPt,
        List<RoadGeometryHelper.StationInfo> stations)
    {
        const double ProximityThreshold = 0.5;
        foreach (var st in stations)
        {
            if (st.Point.DistanceTo(boundaryPt) < ProximityThreshold)
                return; // already covered
        }

        // Compute chainage along the section curve
        sectionCurve.ClosestPoint(boundaryPt, out double t);
        double chainage = sectionCurve.GetLength(
            new Interval(sectionCurve.Domain.T0, t));
        stations.Add(new RoadGeometryHelper.StationInfo(boundaryPt, "R", chainage));
    }
}
