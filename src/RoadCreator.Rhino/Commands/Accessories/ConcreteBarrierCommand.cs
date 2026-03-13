using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Accessories;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Creates a concrete barrier with posts and horizontal rods along a curve.
/// Converts from Svodidlabetonova.rvb.
///
/// Algorithm:
///   1. Select guide curve
///   2. Enter post spacing (default 2.5m)
///   3. Select custom post object or use default box (0.35×0.35×1.4m)
///   4. Divide curve at post spacing
///   5. At each point: get tangent, place post, place rod circle profiles
///   6. Loft all lower rod profiles + all upper rod profiles into rod surfaces
/// </summary>
[System.Runtime.InteropServices.Guid("A1000003-B2C3-D4E5-F6A7-B8C9D0E1F203")]
public class ConcreteBarrierCommand : Command
{
    private sealed record ConcreteBarrierInputs(
        Curve GuideCurve,
        double PostSpacing,
        Point3d PostBase,
        Guid[] PostTemplateIds,
        bool UseDefault);

    public override string EnglishName => "RC_ConcreteBarrier";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectConcreteBarrierCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();
        if (guideCurve == null)
            return Result.Cancel;

        var getSpacing = new GetNumber();
        getSpacing.SetCommandPrompt(Strings.EnterPostSpacing);
        getSpacing.SetDefaultNumber(ConcreteBarrierComputer.DefaultPostSpacing);
        getSpacing.SetLowerLimit(0.5, false);
        if (getSpacing.Get() != GetResult.Number)
            return Result.Cancel;
        double postSpacing = getSpacing.Number();

        // Custom post or default
        Point3d postBase;
        Guid[] postTemplateIds;
        bool useDefault;

        var getPost = new GetObject();
        getPost.SetCommandPrompt(Strings.SelectConcreteBarrierPost);
        getPost.AcceptNothing(true);
        if (getPost.GetMultiple(1, 0) == GetResult.Object)
        {
            useDefault = false;
            postTemplateIds = new Guid[getPost.ObjectCount];
            for (int pi = 0; pi < getPost.ObjectCount; pi++)
                postTemplateIds[pi] = getPost.Object(pi).ObjectId;

            var getBase = new GetPoint();
            getBase.SetCommandPrompt(Strings.SelectPostBasePoint);
            if (getBase.Get() != GetResult.Point)
                return Result.Cancel;
            postBase = getBase.Point();
        }
        else
        {
            useDefault = true;
            postTemplateIds = Array.Empty<Guid>();
            var center = ConcreteBarrierComputer.DefaultPostCenter;
            postBase = new Point3d(center.X, center.Y, center.Z);
        }

        return RunCore(doc, new ConcreteBarrierInputs(
            guideCurve, postSpacing, postBase, postTemplateIds, useDefault));
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

        var guideCurve = RoadCurveResolver.ResolveCenterGuideCurve(doc, roadName);
        if (guideCurve == null)
        {
            RhinoApp.WriteLine($"No guide curve found for road '{roadName}'.");
            return Result.Failure;
        }

        var getSpacing = new GetNumber();
        getSpacing.SetCommandPrompt(Strings.EnterPostSpacing);
        getSpacing.SetDefaultNumber(ConcreteBarrierComputer.DefaultPostSpacing);
        getSpacing.SetLowerLimit(0.5, false);
        if (getSpacing.Get() != GetResult.Number)
            return Result.Cancel;
        double postSpacing = getSpacing.Number();

        var center = ConcreteBarrierComputer.DefaultPostCenter;
        return RunCore(doc, new ConcreteBarrierInputs(
            guideCurve,
            postSpacing,
            new Point3d(center.X, center.Y, center.Z),
            Array.Empty<Guid>(),
            true));
    }

    private Result RunCore(RhinoDoc doc, ConcreteBarrierInputs inputs)
    {
        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // includeEnds=true: includes both start and endpoint.
            inputs.GuideCurve.DivideByLength(inputs.PostSpacing, true, out Point3d[] divPoints);
            if (divPoints == null || divPoints.Length < 2)
            {
                RhinoApp.WriteLine(Strings.ErrorNotACurve);
                return Result.Failure;
            }

            // Layer setup
            var layers = new LayerManager(doc);
            int postLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.ConcreteBarrier),
                System.Drawing.Color.FromArgb(190, 190, 190));
            int rodLayerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.ConcreteBarrierRods),
                System.Drawing.Color.FromArgb(210, 210, 210));
            var postAttrs = new ObjectAttributes { LayerIndex = postLayerIdx };
            var rodAttrs = new ObjectAttributes { LayerIndex = rodLayerIdx };

            // Create rod profile circles (in XZ plane after 90° rotation around Y)
            var lowerRodProfiles = new List<Curve>();
            var upperRodProfiles = new List<Curve>();

            for (int i = 0; i < divPoints.Length; i++)
            {
                var pt = divPoints[i];
                inputs.GuideCurve.ClosestPoint(pt, out double t);
                var tangent = inputs.GuideCurve.TangentAt(t);
                double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

                // Determine rotation: at first/last use curve start/end direction
                double rotation;
                if (i == 0)
                {
                    var startTangent = inputs.GuideCurve.TangentAt(inputs.GuideCurve.Domain.Min);
                    rotation = 180 + System.Math.Atan2(startTangent.Y, startTangent.X) * (180.0 / System.Math.PI);
                }
                else if (i == divPoints.Length - 1)
                {
                    var endTangent = inputs.GuideCurve.TangentAt(inputs.GuideCurve.Domain.Max);
                    rotation = 180 + System.Math.Atan2(endTangent.Y, endTangent.X) * (180.0 / System.Math.PI);
                }
                else
                {
                    rotation = 180 + tangentAngle;
                }

                // Place post
                if (inputs.UseDefault)
                {
                    PlaceDefaultPost(doc, pt, rotation, postAttrs);
                }
                else
                {
                    foreach (var templateId in inputs.PostTemplateIds)
                    {
                        var templateObj = doc.Objects.FindId(templateId);
                        if (templateObj?.Geometry != null)
                        {
                            var copy = templateObj.Geometry.Duplicate();
                            var xform = Transform.Translation(pt - inputs.PostBase) *
                                Transform.Rotation(rotation * System.Math.PI / 180.0,
                                    Vector3d.ZAxis, pt);
                            copy.Transform(xform);
                            doc.Objects.Add(copy, postAttrs);
                        }
                    }
                }

                // Rod profiles (circles in XZ plane, rotated 90° around Y)
                // Lower rod at height 0.5m
                var lowerCircle = new Circle(
                    new Plane(pt + new Vector3d(0, 0, ConcreteBarrierComputer.LowerRodHeight),
                        Vector3d.YAxis),
                    ConcreteBarrierComputer.RodRadius);
                var lowerCurve = new ArcCurve(lowerCircle);
                lowerCurve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);
                lowerRodProfiles.Add(lowerCurve);

                // Upper rod at height 0.9m
                var upperCircle = new Circle(
                    new Plane(pt + new Vector3d(0, 0, ConcreteBarrierComputer.UpperRodHeight),
                        Vector3d.YAxis),
                    ConcreteBarrierComputer.RodRadius);
                var upperCurve = new ArcCurve(upperCircle);
                upperCurve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, pt);
                upperRodProfiles.Add(upperCurve);
            }

            // Loft rod profiles
            var lowerLoft = Brep.CreateFromLoft(lowerRodProfiles, Point3d.Unset, Point3d.Unset,
                LoftType.Straight, false);
            if (lowerLoft != null)
                foreach (var brep in lowerLoft)
                    doc.Objects.AddBrep(brep, rodAttrs);

            var upperLoft = Brep.CreateFromLoft(upperRodProfiles, Point3d.Unset, Point3d.Unset,
                LoftType.Straight, false);
            if (upperLoft != null)
                foreach (var brep in upperLoft)
                    doc.Objects.AddBrep(brep, rodAttrs);

            RhinoApp.WriteLine(Strings.ConcreteBarrierCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static void PlaceDefaultPost(RhinoDoc doc, Point3d position,
        double rotation, ObjectAttributes attrs)
    {
        var corners = ConcreteBarrierComputer.GetDefaultPostCorners();
        var center = ConcreteBarrierComputer.DefaultPostCenter;

        var boxBrep = Brep.CreateFromBox(new BoundingBox(
            new Point3d(corners[0].X, corners[0].Y, corners[0].Z),
            new Point3d(corners[6].X, corners[6].Y, corners[6].Z)));
        if (boxBrep != null)
        {
            boxBrep.Translate(new Vector3d(position.X - center.X,
                position.Y - center.Y, position.Z));
            boxBrep.Transform(Transform.Rotation(
                rotation * System.Math.PI / 180.0, Vector3d.ZAxis, position));
            doc.Objects.AddBrep(boxBrep, attrs);
        }
    }
}
