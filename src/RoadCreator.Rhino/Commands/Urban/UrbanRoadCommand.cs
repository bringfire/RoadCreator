using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Urban;

/// <summary>
/// Creates a 3D urban road surface by sweeping cross-section profiles along a route.
/// Converts from Silnicezprofilumesto.rvb.
///
/// This is a simplified version of Road3DCommand — just a SweepOneRail
/// with user-selected profiles. The profiles are created manually by the user
/// (not generated from road category standards).
///
/// Algorithm:
///   1. Select route curve
///   2. Select cross-section profile curves
///   3. Sweep profiles along route
///   4. Extract edge curves as road boundaries
/// </summary>
[System.Runtime.InteropServices.Guid("D4E5F6A7-B8C9-0123-DEF0-123456789ABC")]
public class UrbanRoadCommand : Command
{
    public override string EnglishName => "RC_UrbanRoad";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select route curve
        var getRoute = new GetObject();
        getRoute.SetCommandPrompt(Strings.SelectRouteForUrbanRoad);
        getRoute.GeometryFilter = ObjectType.Curve;
        if (getRoute.Get() != GetResult.Object)
            return Result.Cancel;
        var route = getRoute.Object(0).Curve();

        // Select cross-section profiles
        var getProfiles = new GetObject();
        getProfiles.SetCommandPrompt(Strings.SelectUrbanProfiles);
        getProfiles.GeometryFilter = ObjectType.Curve;
        getProfiles.EnablePreSelect(false, true);
        if (getProfiles.GetMultiple(1, 0) != GetResult.Object)
            return Result.Cancel;

        var profiles = new Curve[getProfiles.ObjectCount];
        for (int i = 0; i < getProfiles.ObjectCount; i++)
            profiles[i] = getProfiles.Object(i).Curve();

        doc.Views.RedrawEnabled = false;

        try
        {
            // Layer setup
            var layers = new LayerManager(doc);
            string roadPath = LayerScheme.BuildPath(LayerScheme.Road3D);
            int roadLayerIdx = layers.EnsureLayer(roadPath,
                System.Drawing.Color.FromArgb(0, 0, 0));
            var attrs = new ObjectAttributes { LayerIndex = roadLayerIdx };

            // Sweep profiles along route
            var sweep = new SweepOneRail();
            var breps = sweep.PerformSweep(route, profiles);
            if (breps == null || breps.Length == 0)
            {
                RhinoApp.WriteLine(Strings.SweepFailed);
                return Result.Failure;
            }

            foreach (var brep in breps)
            {
                doc.Objects.AddBrep(brep, attrs);

                // Extract naked edge curves only (VBScript: DuplicateEdgeCurves)
                // Filter to naked edges to avoid internal seam curves
                foreach (var edge in brep.Edges)
                {
                    if (edge.Valence != EdgeAdjacency.Naked) continue;
                    var edgeCurve = edge.DuplicateCurve();
                    if (edgeCurve != null)
                        doc.Objects.AddCurve(edgeCurve, attrs);
                }
            }

            RhinoApp.WriteLine(Strings.UrbanRoadCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
