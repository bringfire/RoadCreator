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
/// Creates a Delta Blok concrete barrier along a curve.
/// Converts from SvodidlaDelta.rvb.
///
/// Algorithm:
///   1. Select guide curve
///   2. Select variant (80/100S/100/120)
///   3. Divide curve at 4m block spacing
///   4. At first/last: place full end-cap + end-cap profile (with edge surface cap)
///   5. At mid-points: place main profile
///   6. All profiles oriented by curve tangent
///   7. Loft all profiles into barrier surface
/// </summary>
[System.Runtime.InteropServices.Guid("A1000004-B2C3-D4E5-F6A7-B8C9D0E1F204")]
public class DeltaBlokBarrierCommand : Command
{
    private sealed record DeltaBlokInputs(
        Curve GuideCurve,
        DeltaBlokVariant Variant);

    public override string EnglishName => "RC_DeltaBlokBarrier";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectDeltaBlokCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();
        if (guideCurve == null)
            return Result.Cancel;

        var variantResult = PromptForVariant(out var variant);
        if (variantResult != Result.Success)
            return variantResult;

        return RunCore(doc, new DeltaBlokInputs(guideCurve, variant));
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

        var variantResult = PromptForVariant(out var variant);
        if (variantResult != Result.Success)
            return variantResult;

        return RunCore(doc, new DeltaBlokInputs(guideCurve, variant));
    }

    private Result PromptForVariant(out DeltaBlokVariant variant)
    {
        variant = DeltaBlokVariant.Blok80;

        // Select variant via command-line options
        var getVariant = new GetOption();
        getVariant.SetCommandPrompt(Strings.SelectDeltaBlokVariant);
        getVariant.AddOption("DeltaBlok80");
        getVariant.AddOption("DeltaBlok100S");
        getVariant.AddOption("DeltaBlok100");
        getVariant.AddOption("DeltaBlok120");
        if (getVariant.Get() != GetResult.Option)
            return Result.Cancel;

        variant = getVariant.Option().Index switch
        {
            1 => DeltaBlokVariant.Blok80,
            2 => DeltaBlokVariant.Blok100S,
            3 => DeltaBlokVariant.Blok100,
            4 => DeltaBlokVariant.Blok120,
            _ => DeltaBlokVariant.Blok80,
        };

        return Result.Success;
    }

    private Result RunCore(RhinoDoc doc, DeltaBlokInputs inputs)
    {

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Layer setup
            var layers = new LayerManager(doc);
            int layerIdx = layers.EnsureLayer(LayerScheme.BuildPath(LayerScheme.DeltaBlok),
                System.Drawing.Color.FromArgb(176, 150, 180));
            var attrs = new ObjectAttributes { LayerIndex = layerIdx };

            // Profiles
            var mainProfile = DeltaBlokProfileComputer.GetMainProfile(inputs.Variant);
            var endCapProfile = DeltaBlokProfileComputer.GetEndCapProfile(inputs.Variant);
            var fullEndCapProfile = DeltaBlokProfileComputer.GetFullEndCapProfile(inputs.Variant);
            double transitionDist = DeltaBlokProfileComputer.GetTransitionDistance(inputs.Variant);

            // Divide curve at block spacing.
            // includeEnds=true: includes both start and endpoint (may produce shorter final segment).
            inputs.GuideCurve.DivideByLength(DeltaBlokProfileComputer.BlockSpacing, true,
                out Point3d[] blockPoints);
            if (blockPoints == null || blockPoints.Length < 2)
                return Result.Failure;

            // Build profile list with additional transition profiles at start/end
            // VBScript: profil array has UBound(blokpoints) + 2 extra entries
            var allProfiles = new List<Curve>();

            for (int i = 0; i < blockPoints.Length; i++)
            {
                var pt = blockPoints[i];
                double tangentAngle = GetTangentAngle(inputs.GuideCurve, pt);

                if (i == 0)
                {
                    // Full end-cap at start
                    double startRotation = 180 + tangentAngle;
                    allProfiles.Add(CreateProfileCurve(fullEndCapProfile, pt, startRotation));

                    // End-cap at transition distance ahead
                    // VBScript uses chord angle (block point → transition point), not tangent
                    var transitionPt = FindPointAtDistanceAlong(inputs.GuideCurve, pt, transitionDist);
                    if (transitionPt.HasValue)
                    {
                        double transAngle = 180 + ChordAngle(pt, transitionPt.Value);
                        allProfiles.Add(CreateProfileCurve(endCapProfile, transitionPt.Value, transAngle));
                    }

                    // Create end-cap surface (close the flat end)
                    var endCapCurve = allProfiles[0];
                    var capLine = new LineCurve(endCapCurve.PointAtStart, endCapCurve.PointAtEnd);
                    var edgeSrf = Brep.CreateEdgeSurface(new Curve[] { capLine, endCapCurve });
                    if (edgeSrf != null)
                        doc.Objects.AddBrep(edgeSrf, attrs);
                }
                else if (i == blockPoints.Length - 1)
                {
                    // End-cap at transition distance before end
                    // VBScript uses chord angle (block point → transition point)
                    var transitionPt = FindPointAtDistanceAlong(inputs.GuideCurve, pt, -transitionDist);
                    if (transitionPt.HasValue)
                    {
                        double transAngle = ChordAngle(pt, transitionPt.Value);
                        allProfiles.Add(CreateProfileCurve(endCapProfile, transitionPt.Value, transAngle));
                    }

                    // Full end-cap at end (reversed direction)
                    double endRotation = tangentAngle; // 360 + alfa ≡ alfa
                    allProfiles.Add(CreateProfileCurve(fullEndCapProfile, pt, endRotation));

                    // Create end-cap surface
                    var lastProfile = allProfiles[^1];
                    var capLine = new LineCurve(lastProfile.PointAtStart, lastProfile.PointAtEnd);
                    var edgeSrf = Brep.CreateEdgeSurface(new Curve[] { capLine, lastProfile });
                    if (edgeSrf != null)
                        doc.Objects.AddBrep(edgeSrf, attrs);
                }
                else
                {
                    // Main profile at mid-points
                    double rotation = 180 + tangentAngle;
                    allProfiles.Add(CreateProfileCurve(mainProfile, pt, rotation));
                }
            }

            // Loft all profiles
            if (allProfiles.Count >= 2)
            {
                var loft = Brep.CreateFromLoft(allProfiles, Point3d.Unset, Point3d.Unset,
                    LoftType.Straight, false);
                if (loft != null)
                    foreach (var brep in loft)
                        doc.Objects.AddBrep(brep, attrs);
            }

            RhinoApp.WriteLine(Strings.DeltaBlokCreated);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static double ChordAngle(Point3d from, Point3d to)
    {
        return System.Math.Atan2(to.Y - from.Y, to.X - from.X) * (180.0 / System.Math.PI);
    }

    private static double GetTangentAngle(Curve curve, Point3d pt)
    {
        curve.ClosestPoint(pt, out double t);
        var tangent = curve.TangentAt(t);
        return System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);
    }

    private static Curve CreateProfileCurve(Core.Math.Point3[] profile, Point3d position, double rotation)
    {
        var pts = new Point3d[profile.Length];
        for (int j = 0; j < profile.Length; j++)
            pts[j] = new Point3d(profile[j].X, profile[j].Y, profile[j].Z);

        var polyline = new Polyline(pts);
        var curve = polyline.ToPolylineCurve();
        curve.Translate(new Vector3d(position));
        curve.Rotate(rotation * System.Math.PI / 180.0, Vector3d.ZAxis, position);
        return curve;
    }

    private static Point3d? FindPointAtDistanceAlong(Curve curve, Point3d fromPt, double distance)
    {
        curve.ClosestPoint(fromPt, out double t);
        double length = curve.GetLength(new Interval(curve.Domain.Min, t));
        double targetLength = length + distance;
        if (targetLength < 0 || targetLength > curve.GetLength())
            return null;
        if (!curve.LengthParameter(targetLength, out double targetT))
            return null;
        return curve.PointAt(targetT);
    }
}
