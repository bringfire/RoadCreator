using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Terrain;
using RoadCreator.Rhino.Layers;
using RoadCreator.Rhino.Terrain;

namespace RoadCreator.Rhino.Commands.Terrain;

/// <summary>
/// Generates a longitudinal profile by projecting a route axis onto terrain.
/// Converts from RC2_PodelnyProfil_CZ.rvb.
///
/// RC2 enhancements over gen-1:
///   - Auto-detect route via RouteDiscovery (with manual fallback)
///   - Projects important points (ZP, PO, OP, KP, ZU, KU) onto profile
///   - Creates "Important Points" sublayer for stationing markers on profile
///   - Creates KU (end-of-section) stationing marker on tangent polygon
///
/// Algorithm:
///   1. Select route axis curve and terrain (mesh or surface)
///   2. Divide axis into 2m stations
///   3. Project each station point onto terrain to get elevation
///   4. Compute reference datum from minimum elevation
///   5. Draw profile in 2D: X = chainage, Y = (elevation - datum) × 10
///   6. Add elevation labels at specified spacing
///   7. Plot important points on the terrain profile
///   8. Store datum in object name for downstream use
/// </summary>
[System.Runtime.InteropServices.Guid("D4E5F6A7-B8C9-0D1E-2F3A-4B5C6D7E8FA1")]
public class LongitudinalProfileCommand : Command
{
    public override string EnglishName => "RC_LongitudinalProfile";

    private const double StationSpacing = 2.0;
    private const double DefaultLabelSpacing = 20.0;
    private const double LabelTextHeight = 5.0;
    private const double AxisLabelTextHeight = 7.0;
    private const double KmDivisor = 1000.0;

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // --- Step 1: Select route (auto-detect or manual) ---
        string roadName = "";
        Curve? axisCurve = null;

        var (choice, selectedRoad) = RouteDiscovery.PromptRouteSelection(doc, "Manual");
        if (choice == RoutePromptResult.Cancelled)
            return Result.Cancel;
        if (choice == RoutePromptResult.RoadSelected)
        {
            roadName = selectedRoad;
            axisCurve = RouteDiscovery.FindFullAxisCurve(doc, roadName);
            if (axisCurve == null)
            {
                RhinoApp.WriteLine(string.Format(Strings.AxisNotFound, roadName));
                return Result.Failure;
            }
        }

        if (axisCurve == null)
        {
            var getAxis = new GetObject();
            getAxis.SetCommandPrompt(Strings.SelectRouteAxis);
            getAxis.GeometryFilter = ObjectType.Curve;
            if (getAxis.Get() != GetResult.Object)
                return Result.Cancel;
            axisCurve = getAxis.Object(0).Curve();
            if (string.IsNullOrEmpty(roadName))
                roadName = RoadObjectNaming.ParseRoadName(
                    getAxis.Object(0).Object().Attributes.Name) ?? "";
        }

        // Select terrain
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectTerrain);
        getTerrain.GeometryFilter = ObjectType.Mesh | ObjectType.Surface | ObjectType.Brep;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;
        var terrainGeo = getTerrain.Object(0).Geometry();

        var terrain = TerrainFactory.FromGeometry(terrainGeo, doc.ModelAbsoluteTolerance);
        if (terrain == null)
        {
            RhinoApp.WriteLine(Strings.ErrorInvalidTerrain);
            return Result.Failure;
        }

        // Get label spacing
        var getLabelSpacing = new GetNumber();
        getLabelSpacing.SetCommandPrompt(Strings.EnterElevationLabelSpacing);
        getLabelSpacing.SetDefaultNumber(DefaultLabelSpacing);
        getLabelSpacing.SetLowerLimit(5, false);
        getLabelSpacing.SetUpperLimit(200, false);
        if (getLabelSpacing.Get() != GetResult.Number)
            return Result.Cancel;
        double labelSpacing = getLabelSpacing.Number();

        // Get origin point for profile drawing
        var getOrigin = new GetPoint();
        getOrigin.SetCommandPrompt(Strings.SelectProfileOrigin);
        if (getOrigin.Get() != GetResult.Point)
            return Result.Cancel;
        var origin = getOrigin.Point();

        doc.Views.RedrawEnabled = false;

        try
        {
            double curveLength = axisCurve.GetLength();

            // Sample terrain elevations along the route
            int stationCount = (int)(curveLength / StationSpacing) + 1;
            var chainages = new List<double>();
            var elevations = new List<double>();

            for (int i = 0; i < stationCount; i++)
            {
                double dist = i * StationSpacing;
                if (dist > curveLength) dist = curveLength;

                if (!axisCurve.LengthParameter(dist, out double param))
                    continue;
                var pt3d = axisCurve.PointAt(param);

                double? elevation = terrain.GetElevationAt(pt3d.X, pt3d.Y);
                if (elevation == null)
                    continue;

                chainages.Add(dist);
                elevations.Add(elevation.Value);
            }

            // Add endpoint if not reached
            if ((stationCount - 1) * StationSpacing < curveLength - 1e-10)
            {
                if (axisCurve.LengthParameter(curveLength, out double lastParam))
                {
                    var lastPt = axisCurve.PointAt(lastParam);
                    double? lastElev = terrain.GetElevationAt(lastPt.X, lastPt.Y);
                    if (lastElev != null)
                    {
                        chainages.Add(curveLength);
                        elevations.Add(lastElev.Value);
                    }
                }
            }

            if (chainages.Count < 2)
            {
                RhinoApp.WriteLine("Could not sample enough terrain elevations.");
                return Result.Failure;
            }

            var chainageArr = chainages.ToArray();
            var elevationArr = elevations.ToArray();

            // Compute reference datum
            double minElev = LongitudinalProfileComputer.FindMinElevation(elevationArr);
            double datum = LongitudinalProfileComputer.ComputeReferenceDatum(minElev);

            // Compute profile points
            var profilePoints = LongitudinalProfileComputer.ComputeProfilePoints(
                chainageArr, elevationArr, datum);

            // Layer setup
            var layers = new LayerManager(doc);
            string profPath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.LongitudinalProfile);
            int profLayerIdx = layers.EnsureLayer(profPath, System.Drawing.Color.Black);

            string terrainProfPath = LayerScheme.BuildOptionalRoadPath(roadName,
                LayerScheme.LongitudinalProfile, "Terrain Profile");
            int terrainProfLayerIdx = layers.EnsureLayer(terrainProfPath, System.Drawing.Color.FromArgb(0, 100, 0));

            string labelsPath = LayerScheme.BuildOptionalRoadPath(roadName,
                LayerScheme.LongitudinalProfile, "Elevation Labels");
            int labelsLayerIdx = layers.EnsureLayer(labelsPath, System.Drawing.Color.Black);

            // Draw baseline (X axis)
            doc.Objects.AddLine(
                new Line(origin, new Point3d(origin.X + curveLength, origin.Y, 0)),
                new ObjectAttributes { LayerIndex = profLayerIdx });

            // Draw terrain profile curve
            var rhPoints = new List<Point3d>();
            for (int i = 0; i < profilePoints.Length; i++)
            {
                rhPoints.Add(new Point3d(
                    origin.X + profilePoints[i].X,
                    origin.Y + profilePoints[i].Y,
                    0));
            }

            var profileCurve = Curve.CreateInterpolatedCurve(rhPoints, 3);
            doc.Objects.AddCurve(profileCurve, new ObjectAttributes
            {
                LayerIndex = terrainProfLayerIdx,
                Name = $"{roadName} TerrainProfile"
            });

            // Draw elevation labels at regular intervals
            var labelAttrs = new ObjectAttributes { LayerIndex = labelsLayerIdx };
            for (int i = 0; i < chainageArr.Length; i++)
            {
                double ch = chainageArr[i];
                bool isEndpoint = (i == 0 || i == chainageArr.Length - 1);
                double remainder = ch % labelSpacing;
                bool isLabelPoint = isEndpoint ||
                    (remainder < 1e-6 || System.Math.Abs(remainder - labelSpacing) < 1e-6);

                if (!isLabelPoint) continue;

                DrawElevationLabel(doc, origin, ch, elevationArr[i], profilePoints[i].Y,
                    labelsLayerIdx);
            }

            // Add km markers along baseline
            AddKmMarkers(doc, origin, curveLength, profLayerIdx);

            // --- RC2: Plot important points on profile ---
            if (!string.IsNullOrEmpty(roadName))
            {
                var stationingPts = RouteDiscovery.FindStationingPoints(doc, roadName);
                if (stationingPts.Length > 0)
                {
                    string impPtsPath = LayerScheme.BuildOptionalRoadPath(roadName,
                        LayerScheme.LongitudinalProfile, "Terrain Profile", LayerScheme.ImportantPoints);
                    int impPtsLayerIdx = layers.EnsureLayer(impPtsPath,
                        System.Drawing.Color.Red);

                    foreach (var (name, location) in stationingPts)
                    {
                        // Find chainage of this point on the axis curve
                        if (!axisCurve.ClosestPoint(
                                new Point3d(location.X, location.Y, 0), out double t))
                            continue;

                        double ptChainage = axisCurve.GetLength(
                            new Interval(axisCurve.Domain.T0, t));

                        // Get terrain elevation at this point
                        double? ptElev = terrain.GetElevationAt(location.X, location.Y);
                        if (ptElev == null) continue;

                        double profileY = (ptElev.Value - datum) *
                            ProfileConstants.VerticalExaggeration;

                        // Draw the important point label on the profile
                        DrawElevationLabel(doc, origin, ptChainage, ptElev.Value,
                            profileY, impPtsLayerIdx);
                    }
                }
            }

            // Store datum point
            var datumPtAttrs = new ObjectAttributes
            {
                LayerIndex = profLayerIdx,
                Name = RoadObjectNaming.BuildLongProfileName(roadName, datum)
            };
            doc.Objects.AddPoint(origin, datumPtAttrs);

            // Add datum text
            var datumText = new TextEntity
            {
                Plane = new Plane(new Point3d(origin.X, origin.Y - 110, 0), Vector3d.ZAxis),
                PlainText = $"Reference datum: {datum:F0} m",
                TextHeight = AxisLabelTextHeight
            };
            doc.Objects.AddText(datumText, new ObjectAttributes { LayerIndex = profLayerIdx });

            // Lock layers
            layers.LockLayer(terrainProfPath);
            layers.LockLayer(labelsPath);

            RhinoApp.WriteLine($"Longitudinal profile created: {chainages.Count} points, datum = {datum:F0}m, length = {curveLength:F1}m.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    /// <summary>
    /// Draw an elevation label at a specific chainage on the profile.
    /// Vertical lines from baseline to profile, with rotated elevation text.
    /// </summary>
    private static void DrawElevationLabel(RhinoDoc doc, Point3d origin,
        double chainage, double elevation, double profileY, int layerIdx)
    {
        var attrs = new ObjectAttributes { LayerIndex = layerIdx };
        double px = origin.X + chainage;

        // Vertical lines from baseline toward profile
        doc.Objects.AddLine(
            new Line(new Point3d(px, origin.Y, 0), new Point3d(px, origin.Y + 20, 0)),
            attrs);
        doc.Objects.AddLine(
            new Line(new Point3d(px, origin.Y + 50, 0),
                new Point3d(px, origin.Y + profileY, 0)),
            attrs);

        // Elevation text (rotated 90 degrees)
        var elevText = new TextEntity
        {
            Plane = new Plane(new Point3d(px, origin.Y + 35, 0), Vector3d.ZAxis),
            PlainText = $"{elevation:F2}",
            TextHeight = LabelTextHeight
        };
        var textId = doc.Objects.AddText(elevText, attrs);
        if (textId != System.Guid.Empty)
        {
            var xform = Transform.Rotation(
                System.Math.PI / 2,
                Vector3d.ZAxis,
                new Point3d(px, origin.Y + 35, 0));
            doc.Objects.Transform(textId, xform, true);
        }
    }

    private static void AddKmMarkers(RhinoDoc doc, Point3d origin, double curveLength, int layerIdx)
    {
        var attrs = new ObjectAttributes { LayerIndex = layerIdx };
        double interval = curveLength > 1000 ? 100 : 50;

        int i = 0;
        while (i * interval < curveLength)
        {
            double x = origin.X + i * interval;
            doc.Objects.AddCircle(new Circle(new Point3d(x, origin.Y, 0), 2.5), attrs);

            string label = $"{(i * interval / KmDivisor):F3} km";
            double yOffset = i % 10 == 0 ? 6.5 : 6.0;
            var text = new TextEntity
            {
                Plane = new Plane(new Point3d(x, origin.Y - yOffset, 0), Vector3d.ZAxis),
                PlainText = label,
                TextHeight = i % 10 == 0 ? AxisLabelTextHeight : LabelTextHeight
            };
            doc.Objects.AddText(text, attrs);
            i++;
        }
    }
}
