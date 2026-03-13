using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Geometry.Intersect;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Terrain;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Terrain;

/// <summary>
/// Generates contour lines from a terrain surface or mesh.
/// Converts from RC2_Vrstevnice_CZ.rvb.
///
/// Algorithm:
///   1. Select terrain (mesh or NURBS surface)
///   2. Get bounding box to determine Z range
///   3. Create horizontal cutting plane at each elevation level
///   4. Intersect terrain with plane to get contour curves
///   5. Classify contours: 10m = main, 5m = secondary, 2m = minor, odd = discard
///   6. Assign to appropriate layers
/// </summary>
[System.Runtime.InteropServices.Guid("C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F")]
public class ContourLinesCommand : Command
{
    public override string EnglishName => "RC_ContourLines";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select terrain (mesh or surface/polysurface)
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectTerrain);
        getTerrain.GeometryFilter = ObjectType.Mesh | ObjectType.Surface | ObjectType.Brep;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;

        var terrainRef = getTerrain.Object(0);
        var terrainGeo = terrainRef.Geometry();
        bool isMesh = terrainGeo is Mesh;

        // Get bounding box for Z range
        var bbox = terrainGeo.GetBoundingBox(true);
        if (!bbox.IsValid)
        {
            RhinoApp.WriteLine("Could not compute terrain bounding box.");
            return Result.Failure;
        }

        double minZ = bbox.Min.Z;
        double maxZ = bbox.Max.Z;

        // RC2: Print terrain specs (type, min/max heights)
        string terrainType = isMesh ? "Mesh" : "NURBS Surface";
        RhinoApp.WriteLine(string.Format(Strings.ContourStats, terrainType, minZ, maxZ));

        double startElev = ContourClassifier.ComputeStartElevation(minZ);

        doc.Views.RedrawEnabled = false;

        try
        {
            // Layer setup
            var layers = new LayerManager(doc);
            string contourPath = LayerScheme.BuildPath(LayerScheme.Terrain, LayerScheme.Contours);
            layers.EnsureLayer(contourPath);

            string mainPath = LayerScheme.BuildPath(LayerScheme.Terrain, LayerScheme.Contours, LayerScheme.MainContours);
            int mainLayerIdx = layers.EnsureLayer(mainPath, System.Drawing.Color.FromArgb(255, 191, 0));

            string sec5Path = LayerScheme.BuildPath(LayerScheme.Terrain, LayerScheme.Contours, LayerScheme.SecondaryContours5m);
            int sec5LayerIdx = layers.EnsureLayer(sec5Path, System.Drawing.Color.FromArgb(90, 90, 90));

            string minor2Path = LayerScheme.BuildPath(LayerScheme.Terrain, LayerScheme.Contours, LayerScheme.MinorContours2m);
            int minor2LayerIdx = layers.EnsureLayer(minor2Path, System.Drawing.Color.FromArgb(90, 60, 90));

            int totalContours = 0;
            int heightIndex = 0;

            for (double elev = startElev; elev <= maxZ; elev += 1.0)
            {
                var type = ContourClassifier.Classify(heightIndex);
                heightIndex++;

                if (type == ContourClassifier.ContourType.Discard)
                    continue;

                int layerIdx = type switch
                {
                    ContourClassifier.ContourType.Main10m => mainLayerIdx,
                    ContourClassifier.ContourType.Secondary5m => sec5LayerIdx,
                    ContourClassifier.ContourType.Minor2m => minor2LayerIdx,
                    _ => minor2LayerIdx
                };

                string namePrefix = type switch
                {
                    ContourClassifier.ContourType.Main10m => "RC_Main",
                    ContourClassifier.ContourType.Secondary5m => "RC_5m",
                    ContourClassifier.ContourType.Minor2m => "RC_2m",
                    _ => "RC_Contour"
                };

                // Create cutting plane at this elevation
                var plane = new Plane(new Point3d(0, 0, elev), Vector3d.ZAxis);
                Curve[]? contourCurves = null;

                if (isMesh && terrainGeo is Mesh mesh)
                {
                    var polylines = Intersection.MeshPlane(mesh, plane);
                    if (polylines != null)
                    {
                        contourCurves = new Curve[polylines.Length];
                        for (int j = 0; j < polylines.Length; j++)
                            contourCurves[j] = polylines[j].ToPolylineCurve();
                    }
                }
                else
                {
                    var brep = terrainGeo as Brep ?? (terrainGeo as Surface)?.ToBrep();
                    if (brep == null) continue;

                    if (Intersection.BrepPlane(brep, plane, doc.ModelAbsoluteTolerance,
                        out Curve[] intCurves, out _))
                    {
                        contourCurves = intCurves;
                    }
                }

                if (contourCurves == null || contourCurves.Length == 0)
                    continue;

                foreach (var curve in contourCurves)
                {
                    var attrs = new ObjectAttributes
                    {
                        LayerIndex = layerIdx,
                        Name = $"{namePrefix} {elev:F0}"
                    };
                    doc.Objects.AddCurve(curve, attrs);
                    totalContours++;
                }
            }

            // Lock all contour layers (matching VBScript behavior)
            layers.LockLayer(contourPath);
            layers.LockLayer(mainPath);
            layers.LockLayer(sec5Path);
            layers.LockLayer(minor2Path);

            // Hide 5m contours by default (matching VBScript behavior)
            layers.SetLayerVisible(sec5Path, false);

            RhinoApp.WriteLine($"Generated {totalContours} contour curves from {minZ:F1}m to {maxZ:F1}m.");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
