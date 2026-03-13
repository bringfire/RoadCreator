using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;
using RoadCreator.Core.Nature;
using RoadCreator.Rhino.Layers;
using RoadCreator.Rhino.Terrain;

namespace RoadCreator.Rhino.Commands.Nature;

/// <summary>
/// Interactive multi-object placement: each click places a random tree from
/// a set of user-selected templates with random rotation and scale.
/// Converts from Magiccopymore.rvb / MagiccopymoreMesh.rvb (unified via ITerrain).
///
/// Algorithm:
///   1. Select terrain
///   2. Enter number of tree templates
///   3. For each template: select objects, base point, scale variance
///   4. Loop: click to place → random template, rotation, scale → project to terrain
///   5. Escape to finish
/// </summary>
[System.Runtime.InteropServices.Guid("B1000006-C2D3-E4F5-A6B7-C8D9E0F1A206")]
public class MagicCopyMultiCommand : Command
{
    public override string EnglishName => "RC_MagicCopyMulti";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
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

        // Number of tree templates
        var getCount = new GetInteger();
        getCount.SetCommandPrompt(Strings.EnterTreeCount);
        getCount.SetDefaultInteger(2);
        getCount.SetLowerLimit(1, false);
        getCount.SetUpperLimit(20, false);
        if (getCount.Get() != GetResult.Number)
            return Result.Cancel;
        int templateCount = getCount.Number();

        // Collect each template
        var templates = new List<ManualTemplate>();
        for (int i = 0; i < templateCount; i++)
        {
            var getObjects = new GetObject();
            getObjects.SetCommandPrompt(string.Format(Strings.SelectTreeTemplate, i + 1));
            getObjects.EnablePreSelect(false, true);
            if (getObjects.GetMultiple(1, 0) != GetResult.Object)
                return Result.Cancel;

            var objectIds = new Guid[getObjects.ObjectCount];
            for (int j = 0; j < getObjects.ObjectCount; j++)
                objectIds[j] = getObjects.Object(j).ObjectId;

            var getBase = new GetPoint();
            getBase.SetCommandPrompt(string.Format(Strings.SelectTreeBasePoint, i + 1));
            if (getBase.Get() != GetResult.Point)
                return Result.Cancel;
            var basePoint = getBase.Point();

            var getScale = new GetNumber();
            getScale.SetCommandPrompt(Strings.EnterScaleVariance);
            getScale.SetDefaultNumber(20.0);
            getScale.SetLowerLimit(0, true);
            getScale.SetUpperLimit(100, false);
            if (getScale.Get() != GetResult.Number)
                return Result.Cancel;
            double scalePercent = getScale.Number();

            templates.Add(new ManualTemplate(objectIds, basePoint, scalePercent));
        }

        var layers = new LayerManager(doc);
        int treeLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.Trees),
            System.Drawing.Color.FromArgb(0, 190, 0));
        var treeAttrs = new ObjectAttributes { LayerIndex = treeLayerIdx };

        var rng = new Random();
        int copyCount = 0;

        // Interactive placement loop
        while (true)
        {
            var getPoint = new GetPoint();
            getPoint.SetCommandPrompt(Strings.ClickToPlace);
            getPoint.AcceptNothing(true);
            var result = getPoint.Get();

            if (result == GetResult.Nothing || result != GetResult.Point)
                break;

            var clickPt = getPoint.Point();

            // Project to terrain
            var projected = terrain.ProjectPointDown(
                new Core.Math.Point3(clickPt.X, clickPt.Y, clickPt.Z + 1000));
            if (projected == null) continue;
            var placePt = new Point3d(projected.Value.X, projected.Value.Y, projected.Value.Z);

            int templateIdx = RandomPlacementComputer.SelectTreeIndex(templates.Count, rng.NextDouble());
            var template = templates[templateIdx];
            double rotation = RandomPlacementComputer.ComputeRotationDegrees(rng.NextDouble());
            double scale = RandomPlacementComputer.ComputeScale(template.ScalePercent, rng.NextDouble());

            var moveXform = Transform.Translation(placePt - template.BasePoint);
            var rotXform = Transform.Rotation(rotation * System.Math.PI / 180.0,
                Vector3d.ZAxis, placePt);
            var scaleXform = Transform.Scale(placePt, scale);
            var xform = scaleXform * rotXform * moveXform;

            foreach (var objId in template.ObjectIds)
            {
                var obj = doc.Objects.FindId(objId);
                if (obj?.Geometry == null) continue;
                var copy = obj.Geometry.Duplicate();
                copy.Transform(xform);
                doc.Objects.Add(copy, treeAttrs);
            }

            copyCount++;
            doc.Views.Redraw();
        }

        RhinoApp.WriteLine(string.Format(Strings.MagicCopyDone, copyCount));
        return Result.Success;
    }

    private record ManualTemplate(Guid[] ObjectIds, Point3d BasePoint, double ScalePercent);
}
