using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;
using RoadCreator.Rhino.Terrain;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// Utility pole placement along a curve with terrain projection.
/// Converts from pokus.rvb.
///
/// Algorithm:
///   1. Select pole template object from database (named "stozar")
///   2. Select terrain
///   3. Draw/select polyline for pole route
///   4. Divide at 10m intervals
///   5. At each point: project to terrain, copy pole, rotate by tangent
/// </summary>
[System.Runtime.InteropServices.Guid("B1000009-C2D3-E4F5-A6B7-C8D9E0F1A209")]
public class UtilityPoleCommand : Command
{
    private const double PoleSpacing = 10.0;

    public override string EnglishName => "RC_UtilityPoles";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select pole template
        var getPole = new GetObject();
        getPole.SetCommandPrompt(Strings.SelectPoleObject);
        if (getPole.GetMultiple(1, 0) != GetResult.Object)
            return Result.Cancel;

        var poleIds = new Guid[getPole.ObjectCount];
        for (int i = 0; i < getPole.ObjectCount; i++)
            poleIds[i] = getPole.Object(i).ObjectId;

        // Select base point of pole
        var getBase = new GetPoint();
        getBase.SetCommandPrompt(Strings.SelectBasePoint);
        if (getBase.Get() != GetResult.Point)
            return Result.Cancel;
        var poleBase = getBase.Point();

        // Select terrain
        var getTerrain = new GetObject();
        getTerrain.SetCommandPrompt(Strings.SelectForestTerrain);
        getTerrain.GeometryFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Mesh;
        if (getTerrain.Get() != GetResult.Object)
            return Result.Cancel;
        var terrain = TerrainFactory.FromRhinoObject(getTerrain.Object(0).Object());
        if (terrain == null)
        {
            RhinoApp.WriteLine(Strings.ErrorInvalidTerrain);
            return Result.Failure;
        }

        // Select guide curve
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectPoleCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();

        doc.Views.RedrawEnabled = false;

        try
        {
            var layers = new LayerManager(doc);
            int poleLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.RoadPoles),
                System.Drawing.Color.FromArgb(100, 100, 100));
            var poleAttrs = new ObjectAttributes { LayerIndex = poleLayerIdx };

            // Divide curve at 10m intervals
            // includeEnds=true: includes both start and endpoint.
            guideCurve.DivideByLength(PoleSpacing, true, out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length == 0)
                return Result.Failure;

            int placedCount = 0;

            for (int i = 0; i < divPoints.Length; i++)
            {
                var pt = divPoints[i];

                // Project to terrain
                var projected = terrain.ProjectPointDown(
                    new Core.Math.Point3(pt.X, pt.Y, pt.Z + 1000));
                if (projected == null) continue;
                var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

                // Get tangent for rotation
                guideCurve.ClosestPoint(pt, out double t);
                var tangent = guideCurve.TangentAt(t);
                // VBScript uses Asin(tangent.X) rather than Atan2(tangent.Y, tangent.X).
                // This is an unusual heading formula preserved for VBScript compatibility.
                // It produces correct rotation for the original pole models.
                double angle = System.Math.Asin(
                    System.Math.Clamp(tangent.X, -1.0, 1.0)) * (180.0 / System.Math.PI);

                var moveXform = Transform.Translation(placePt - poleBase);
                var rotXform = Transform.Rotation(angle * System.Math.PI / 180.0,
                    Vector3d.ZAxis, placePt);
                var xform = rotXform * moveXform;

                foreach (var poleId in poleIds)
                {
                    var obj = doc.Objects.FindId(poleId);
                    if (obj?.Geometry == null) continue;
                    var copy = obj.Geometry.Duplicate();
                    copy.Transform(xform);
                    doc.Objects.Add(copy, poleAttrs);
                }

                placedCount++;
            }

            RhinoApp.WriteLine(string.Format(Strings.UtilityPolesCreated, placedCount));
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
