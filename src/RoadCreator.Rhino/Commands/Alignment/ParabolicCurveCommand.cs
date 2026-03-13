using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Geometry.Intersect;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Alignment;

/// <summary>
/// Inserts a parabolic vertical curve between two grade segments.
/// Converts from RC2_Parabolickyoblouk_CZ.rvb / parabolickyoblouk.rvb.
///
/// The grade line is drawn in profile space (X = chainage, Y = elevation × 10).
/// The parabolic offset y(x) = x² / (2R) is applied with 10:1 exaggeration.
/// </summary>
[System.Runtime.InteropServices.Guid("F1A2B3C4-D5E6-7F8A-9B0C-1D2E3F4A5B6C")]
public class ParabolicCurveCommand : Command
{
    public override string EnglishName => "RC_ParabolicCurve";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Select first grade segment
        var getGrade1 = new GetObject();
        getGrade1.SetCommandPrompt(Strings.SelectFirstGradeSegment);
        getGrade1.GeometryFilter = ObjectType.Curve;
        if (getGrade1.Get() != GetResult.Object)
            return Result.Cancel;
        var grade1Ref = getGrade1.Object(0);
        var grade1Obj = grade1Ref.Object();
        var grade1 = grade1Ref.Curve();

        // Select second grade segment
        var getGrade2 = new GetObject();
        getGrade2.SetCommandPrompt(Strings.SelectSecondGradeSegment);
        getGrade2.GeometryFilter = ObjectType.Curve;
        if (getGrade2.Get() != GetResult.Object)
            return Result.Cancel;
        var grade2Ref = getGrade2.Object(0);
        var grade2Obj = grade2Ref.Object();
        var grade2 = grade2Ref.Curve();

        // Parse road name from object name
        string roadName = "";
        var name1 = grade1Obj.Attributes.Name ?? "";
        var parts1 = name1.Split(' ');
        if (parts1.Length >= 1)
            roadName = parts1[0];

        // Get parabolic radius
        var getR = new GetNumber();
        getR.SetCommandPrompt(Strings.EnterParabolicRadius);
        getR.SetDefaultNumber(2000);
        getR.SetLowerLimit(50, false);
        getR.SetUpperLimit(100000, false);
        if (getR.Get() != GetResult.Number)
            return Result.Cancel;
        double R = getR.Number();

        doc.Views.RedrawEnabled = false;

        try
        {
            // Find vertex (intersection of the two grade segments)
            var intersections = Intersection.CurveCurve(grade1, grade2, doc.ModelAbsoluteTolerance, 0);
            if (intersections == null || intersections.Count == 0)
            {
                RhinoApp.WriteLine(Strings.ErrorParallelLines);
                return Result.Failure;
            }
            var vertex = intersections[0].PointA;

            // Determine direction points using circle intersection (same as horizontal)
            var circle = new ArcCurve(new Circle(vertex, 1.0));
            var ccx1 = Intersection.CurveCurve(grade1, circle, doc.ModelAbsoluteTolerance, 0);
            var ccx2 = Intersection.CurveCurve(grade2, circle, doc.ModelAbsoluteTolerance, 0);

            if (ccx1 == null || ccx1.Count == 0 || ccx2 == null || ccx2.Count == 0)
            {
                RhinoApp.WriteLine("Could not determine grade directions.");
                return Result.Failure;
            }

            var p1 = ccx1[0].PointA;
            var p2 = ccx2[0].PointA;

            // Compute grades (in profile space, Y is exaggerated by 10)
            double dx1 = vertex.X - p1.X;
            double dx2 = p2.X - vertex.X;
            if (System.Math.Abs(dx1) < 1e-10 || System.Math.Abs(dx2) < 1e-10)
            {
                RhinoApp.WriteLine("Grade segment is vertical; cannot compute slope.");
                return Result.Failure;
            }

            double dy1 = (vertex.Y - p1.Y) / ProfileConstants.VerticalExaggeration;
            double slope1 = (dy1 / dx1) * 100.0;

            double dy2 = (p2.Y - vertex.Y) / ProfileConstants.VerticalExaggeration;
            double slope2 = (dy2 / dx2) * 100.0;

            // Compute tangent length
            double t = ParabolicVerticalCurve.ComputeTangentLength(R, slope1, slope2);
            double ymax = ParabolicVerticalCurve.ComputeMaxOrdinate(R, t);
            bool isSag = ParabolicVerticalCurve.IsSagCurve(slope1, slope2);
            double sign = isSag ? 1.0 : -1.0;

            RhinoApp.WriteLine($"t: {t:F2} m, ymax: {ymax:F4} m, type: {(isSag ? "sag" : "crest")}");

            // Find ZZ (start) and KZ (end) points on the grade segments
            var zzLine = new LineCurve(
                new Point3d(vertex.X - t, vertex.Y - 1000, 0),
                new Point3d(vertex.X - t, vertex.Y + 1000, 0));
            var kzLine = new LineCurve(
                new Point3d(vertex.X + t, vertex.Y - 1000, 0),
                new Point3d(vertex.X + t, vertex.Y + 1000, 0));

            var zzIntersect = Intersection.CurveCurve(grade1, zzLine, doc.ModelAbsoluteTolerance, 0);
            if (zzIntersect == null || zzIntersect.Count == 0)
            {
                RhinoApp.WriteLine("First grade segment is too short for the specified radius.");
                return Result.Failure;
            }
            var zzPoint = zzIntersect[0].PointA;

            var kzIntersect = Intersection.CurveCurve(grade2, kzLine, doc.ModelAbsoluteTolerance, 0);
            if (kzIntersect == null || kzIntersect.Count == 0)
            {
                RhinoApp.WriteLine("Second grade segment is too short for the specified radius.");
                return Result.Failure;
            }
            var kzPoint = kzIntersect[0].PointA;

            // Split grade segments at ZZ and KZ
            grade1.ClosestPoint(zzPoint, out double splitParam1);
            var grade1Pieces = grade1.Split(splitParam1);

            grade2.ClosestPoint(kzPoint, out double splitParam2);
            var grade2Pieces = grade2.Split(splitParam2);

            // Create extended tangent line from ZZ through vertex for sampling
            var tangentLine = new LineCurve(zzPoint, vertex);
            tangentLine.Transform(Transform.Scale(tangentLine.PointAtNormalizedLength(0.5), 4.0));

            // Compute parabolic curve points in profile space
            double fullLength = 2.0 * t;
            int intSamples = (int)fullLength + 1;
            var curvePoints = new List<Point3d>();

            // Sample at 1m intervals, plus exact KZ endpoint if needed
            bool needsKzEndpoint = fullLength - (intSamples - 1) > 1e-10;
            int totalSamples = intSamples + (needsKzEndpoint ? 1 : 0);

            for (int i = 0; i < totalSamples; i++)
            {
                double x = (i < intSamples) ? i : fullLength;
                double y = ParabolicVerticalCurve.ComputeOffset(x, R);

                // Find the point on the tangent line at this chainage
                var vertLine = new LineCurve(
                    new Point3d(zzPoint.X + x, zzPoint.Y - 1000, 0),
                    new Point3d(zzPoint.X + x, zzPoint.Y + 1000, 0));
                var tangentIntersect = Intersection.CurveCurve(tangentLine, vertLine, doc.ModelAbsoluteTolerance, 0);

                if (tangentIntersect != null && tangentIntersect.Count > 0)
                {
                    var tangentPt = tangentIntersect[0].PointA;
                    curvePoints.Add(new Point3d(tangentPt.X, tangentPt.Y + sign * y * ProfileConstants.VerticalExaggeration, 0));
                }
            }

            if (curvePoints.Count < 2)
            {
                RhinoApp.WriteLine("Could not generate enough parabolic curve points.");
                return Result.Failure;
            }

            var parabolicCurve = Curve.CreateInterpolatedCurve(curvePoints, 3);

            // Layer setup
            var layers = new LayerManager(doc);
            string glPath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.GradeLine);
            int glLayerIdx = layers.EnsureLayer(glPath);

            string ipPath = LayerScheme.BuildOptionalRoadPath(roadName, LayerScheme.GradeLine, LayerScheme.ImportantPoints);
            int ipLayerIdx = layers.EnsureLayer(ipPath, System.Drawing.Color.Gray);

            // Join curves: grade1_before_ZZ + parabola + grade2_after_KZ
            var curvesToJoin = new List<Curve>();
            if (grade1Pieces != null && grade1Pieces.Length > 0)
                curvesToJoin.Add(grade1Pieces[0]);
            curvesToJoin.Add(parabolicCurve);
            if (grade2Pieces != null && grade2Pieces.Length > 0)
                curvesToJoin.Add(grade2Pieces[^1]);

            var joinAttrs = new ObjectAttributes
            {
                LayerIndex = glLayerIdx,
                Name = $"{roadName} parabolic_R_{R}"
            };
            var joined = Curve.JoinCurves(curvesToJoin);
            if (joined != null && joined.Length > 0)
            {
                doc.Objects.AddCurve(joined[0], joinAttrs);
            }
            else
            {
                doc.Objects.AddCurve(parabolicCurve, joinAttrs);
            }

            // Delete original grade segments
            doc.Objects.Delete(grade1Obj, true);
            doc.Objects.Delete(grade2Obj, true);

            // Add important points: ZZ, V, KZ
            var zzAttrs = new ObjectAttributes { LayerIndex = ipLayerIdx, Name = $"ParabolicArc ZZ {R}" };
            doc.Objects.AddPoint(zzPoint, zzAttrs);
            var vAttrs = new ObjectAttributes { LayerIndex = ipLayerIdx, Name = $"ParabolicArc V {R}" };
            doc.Objects.AddPoint(vertex, vAttrs);
            var kzAttrs = new ObjectAttributes { LayerIndex = ipLayerIdx, Name = $"ParabolicArc KZ {R}" };
            doc.Objects.AddPoint(kzPoint, kzAttrs);

            // Add ZZ/KZ vertical reference lines and circles
            doc.Objects.AddLine(
                new Line(zzPoint, new Point3d(zzPoint.X, zzPoint.Y + 80, 0)),
                new ObjectAttributes { LayerIndex = glLayerIdx });
            doc.Objects.AddLine(
                new Line(kzPoint, new Point3d(kzPoint.X, kzPoint.Y + 80, 0)),
                new ObjectAttributes { LayerIndex = glLayerIdx });
            doc.Objects.AddCircle(new Circle(zzPoint, 1), new ObjectAttributes { LayerIndex = glLayerIdx });
            doc.Objects.AddCircle(new Circle(kzPoint, 1), new ObjectAttributes { LayerIndex = glLayerIdx });

            // Legend text
            var legendAttrs = new ObjectAttributes { LayerIndex = glLayerIdx };
            var legendCircle = new Circle(new Point3d(vertex.X, vertex.Y + 80, 0), 3);
            doc.Objects.AddCircle(legendCircle, legendAttrs);

            var legendR = new TextEntity
            {
                Plane = new Plane(new Point3d(vertex.X - 10, vertex.Y + 100, 0), Vector3d.ZAxis),
                PlainText = $"R = {R} m",
                TextHeight = 3.0
            };
            doc.Objects.AddText(legendR, legendAttrs);

            var legendT = new TextEntity
            {
                Plane = new Plane(new Point3d(vertex.X - 10, vertex.Y + 96, 0), Vector3d.ZAxis),
                PlainText = $"t = {t:F2} m",
                TextHeight = 3.0
            };
            doc.Objects.AddText(legendT, legendAttrs);

            var legendYmax = new TextEntity
            {
                Plane = new Plane(new Point3d(vertex.X - 10, vertex.Y + 92, 0), Vector3d.ZAxis),
                PlainText = $"Ymax = {sign * ymax:F2} m",
                TextHeight = 3.0
            };
            doc.Objects.AddText(legendYmax, legendAttrs);

            layers.LockLayer(ipPath);

            RhinoApp.WriteLine($"Parabolic vertical curve created: R = {R}m, t = {t:F2}m, ymax = {ymax:F4}m ({(isSag ? "sag" : "crest")})");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
