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
using CorePoint3 = RoadCreator.Core.Math.Point3;

// RC2 enhancement: Visual stationing markers (tick lines + text labels) at ZP, PO, OP, KP

namespace RoadCreator.Rhino.Commands.Alignment;

/// <summary>
/// Shared workflow for symmetric transition curve commands (clothoid, cubic parabola).
/// Subclasses provide curve-specific computation and validation.
/// </summary>
public abstract class TransitionCommandBase : Command
{
    private sealed record ResolvedTangents(
        string RoadName,
        int TangentIndex,
        RhinoObject FirstObject,
        Curve FirstCurve,
        RhinoObject SecondObject,
        Curve SecondCurve);

    protected abstract string CurveTypeName { get; }
    protected abstract double DefaultL { get; }
    protected abstract double DefaultR { get; }

    /// <summary>Optional extra validation after L and R are entered. Return null if valid.</summary>
    protected virtual string? ValidateParameters(double L, double R) => null;

    protected abstract double GetLargeTangent(double L, double R, double deflectionAngleDeg);
    protected abstract double GetShift(double L, double R);
    protected abstract CorePoint3[] GetTransitionPoints(double L, double R);

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        return mode == RunMode.Scripted
            ? RunScripted(doc)
            : RunInteractive(doc);
    }

    private Result RunInteractive(RhinoDoc doc)
    {
        // Select first tangent
        var getTangent1 = new GetObject();
        getTangent1.SetCommandPrompt(Strings.SelectFirstTangent);
        getTangent1.GeometryFilter = ObjectType.Curve;
        if (getTangent1.Get() != GetResult.Object)
            return Result.Cancel;
        var tangent1Ref = getTangent1.Object(0);
        var tangent1Obj = tangent1Ref.Object();
        var tangent1 = tangent1Ref.Curve();

        // Select second tangent
        var getTangent2 = new GetObject();
        getTangent2.SetCommandPrompt(Strings.SelectSecondTangent);
        getTangent2.GeometryFilter = ObjectType.Curve;
        if (getTangent2.Get() != GetResult.Object)
            return Result.Cancel;
        var tangent2Ref = getTangent2.Object(0);
        var tangent2Obj = tangent2Ref.Object();
        var tangent2 = tangent2Ref.Curve();

        if (tangent1Obj == null || tangent1 == null || tangent2Obj == null || tangent2 == null)
            return Result.Cancel;

        // Parse road name and tangent index from object name (format: "Road_1 2")
        ParseTangentIdentity(tangent1Obj.Attributes.Name, out string roadName, out int tangentIndex);

        var resolved = new ResolvedTangents(
            roadName,
            tangentIndex,
            tangent1Obj,
            tangent1,
            tangent2Obj,
            tangent2);

        return PromptForParametersAndRun(doc, resolved);
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

        var resolved = ResolveTangentsForRoad(doc, roadName);
        if (resolved == null)
        {
            RhinoApp.WriteLine(
                $"Could not find two tangent polygon segments for road '{roadName}'.");
            return Result.Failure;
        }

        return PromptForParametersAndRun(doc, resolved);
    }

    private Result PromptForParametersAndRun(RhinoDoc doc, ResolvedTangents tangents)
    {
        var parameterResult = PromptForParameters(out double L, out double R);
        if (parameterResult != Result.Success)
            return parameterResult;

        return RunCore(doc, tangents, L, R);
    }

    private Result PromptForParameters(out double L, out double R)
    {
        L = 0;
        R = 0;

        // Get transition length
        var getL = new GetNumber();
        getL.SetCommandPrompt(Strings.EnterTransitionLength);
        getL.SetDefaultNumber(DefaultL);
        getL.SetLowerLimit(1, false);
        if (getL.Get() != GetResult.Number)
            return Result.Cancel;
        L = getL.Number();

        // Get radius
        var getR = new GetNumber();
        getR.SetCommandPrompt(Strings.EnterRadius);
        getR.SetDefaultNumber(DefaultR);
        getR.SetLowerLimit(5, false);
        getR.SetUpperLimit(10000, false);
        if (getR.Get() != GetResult.Number)
            return Result.Cancel;
        R = getR.Number();

        // Subclass-specific parameter validation
        var validationError = ValidateParameters(L, R);
        if (validationError != null)
        {
            RhinoApp.WriteLine(validationError);
            return Result.Failure;
        }

        return Result.Success;
    }

    private Result RunCore(RhinoDoc doc, ResolvedTangents tangents, double L, double R)
    {
        string roadName = tangents.RoadName;
        int tangentIndex = tangents.TangentIndex;
        var tangent1Obj = tangents.FirstObject;
        var tangent1 = tangents.FirstCurve;
        var tangent2Obj = tangents.SecondObject;
        var tangent2 = tangents.SecondCurve;

        doc.Views.RedrawEnabled = false;

        try
        {
            // Find vertex (intersection of the two tangents)
            var intersections = Intersection.CurveCurve(tangent1, tangent2, doc.ModelAbsoluteTolerance, 0);
            if (intersections == null || intersections.Count == 0)
            {
                RhinoApp.WriteLine(Strings.ErrorParallelLines);
                return Result.Failure;
            }
            var vertex = intersections[0].PointA;

            // Compute deflection angle via circle intersection approach
            var circleCurve = new ArcCurve(new Circle(vertex, 1.0));
            var ccx1 = Intersection.CurveCurve(tangent1, circleCurve, doc.ModelAbsoluteTolerance, 0);
            var ccx2 = Intersection.CurveCurve(tangent2, circleCurve, doc.ModelAbsoluteTolerance, 0);

            if (ccx1 == null || ccx1.Count == 0 || ccx2 == null || ccx2.Count == 0)
            {
                RhinoApp.WriteLine("Could not determine tangent angles.");
                return Result.Failure;
            }

            var p1 = ccx1[0].PointA;
            var p2 = ccx2[0].PointA;

            var v1 = p1 - vertex;
            var v2 = p2 - vertex;
            double angle1 = Math.Atan2(v1.Y, v1.X) * 180.0 / Math.PI;
            double angle2 = Math.Atan2(v2.Y, v2.X) * 180.0 / Math.PI;
            double rawAngle = angle2 - angle1;
            if (rawAngle < 0) rawAngle += 360;
            double deflectionAngle = 180.0 - rawAngle;
            if (deflectionAngle < 0) deflectionAngle += 360;

            // I6: Validate deflection angle is in a usable range
            if (deflectionAngle < 0.1 || deflectionAngle > 179.9)
            {
                RhinoApp.WriteLine($"Deflection angle {deflectionAngle:F2} deg is out of usable range (0.1-179.9 deg).");
                return Result.Failure;
            }

            double betaAngle = Math.Atan2(vertex.Y - p1.Y, vertex.X - p1.X) * 180.0 / Math.PI;

            // Compute large tangent
            double largeTangent = GetLargeTangent(L, R, deflectionAngle);
            double m = GetShift(L, R);

            RhinoApp.WriteLine($"m: {m:F4}");
            RhinoApp.WriteLine($"T: {largeTangent:F4}");

            // Find transition start points on tangents
            var largeTangentCircle = new ArcCurve(new Circle(vertex, largeTangent));
            var startIntersect1 = Intersection.CurveCurve(tangent1, largeTangentCircle, doc.ModelAbsoluteTolerance, 0);
            var startIntersect2 = Intersection.CurveCurve(tangent2, largeTangentCircle, doc.ModelAbsoluteTolerance, 0);

            if (startIntersect1 == null || startIntersect1.Count == 0)
            {
                RhinoApp.WriteLine("First tangent is too short for the specified parameters.");
                return Result.Failure;
            }
            if (startIntersect2 == null || startIntersect2.Count == 0)
            {
                RhinoApp.WriteLine("Second tangent is too short for the specified parameters.");
                return Result.Failure;
            }

            var transStart1 = startIntersect1[0].PointA;
            var transStart2 = startIntersect2[0].PointA;

            // Determine left/right turn
            var midpoint = (transStart1 + transStart2) / 2.0;
            var perpDir = new Vector3d(0, -1, 0); // S2: unit vector instead of magnitude 10
            perpDir.Transform(Transform.Rotation(betaAngle * Math.PI / 180.0, Vector3d.ZAxis, Point3d.Origin));
            var perpEnd = vertex + perpDir;

            var smerV = midpoint - vertex;
            var kolmV = perpEnd - vertex;
            double checkAngle = Math.Atan2(kolmV.Y, kolmV.X) - Math.Atan2(smerV.Y, smerV.X);
            if (checkAngle < 0) checkAngle += 2 * Math.PI;
            bool isRightTurn = checkAngle * 180.0 / Math.PI > 270;

            // Compute transition points
            var localPoints = GetTransitionPoints(L, R);
            if (isRightTurn)
                localPoints = TransitionUtils.MirrorY(localPoints);

            // Transform transition 1: rotate to tangent direction, then translate to start point
            var rhinoPoints1 = new List<Point3d>();
            foreach (var cp in localPoints)
            {
                var pt = new Point3d(cp.X, cp.Y, 0);
                pt.Transform(Transform.Rotation(betaAngle * Math.PI / 180.0, Vector3d.ZAxis, Point3d.Origin));
                pt.Transform(Transform.Translation(new Vector3d(transStart1)));
                rhinoPoints1.Add(pt);
            }

            var trans1Curve = Curve.CreateInterpolatedCurve(rhinoPoints1, 3);
            var transition1 = doc.Objects.AddCurve(trans1Curve);

            // Mirror transition 1 about the vertex-midpoint line to create transition 2
            var trans2Curve = trans1Curve.DuplicateCurve();
            trans2Curve.Transform(Transform.Mirror(vertex, midpoint - vertex));
            var transition2 = doc.Objects.AddCurve(trans2Curve);

            // Create connecting arc
            var endPt1 = trans1Curve.PointAtEnd;
            var endPt2 = trans2Curve.PointAtEnd;

            var arcCircle1 = new ArcCurve(new Circle(endPt1, R));
            var arcCircle2 = new ArcCurve(new Circle(endPt2, R));
            var arcCenters = Intersection.CurveCurve(arcCircle1, arcCircle2, doc.ModelAbsoluteTolerance, 0);

            Guid arcId = Guid.Empty;
            Point3d arcMidPoint = (endPt1 + endPt2) / 2.0; // fallback
            if (arcCenters != null && arcCenters.Count > 0)
            {
                var transEndMid = (endPt1 + endPt2) / 2.0;
                var helperLine = new LineCurve(vertex, transEndMid);

                for (int i = 0; i < arcCenters.Count; i++)
                {
                    var testCircle = new ArcCurve(new Circle(arcCenters[i].PointA, R));
                    var testIntersect = Intersection.CurveCurve(testCircle, helperLine, doc.ModelAbsoluteTolerance, 0);
                    if (testIntersect != null && testIntersect.Count > 0)
                    {
                        arcMidPoint = testIntersect[0].PointA; // I7: actual arc midpoint
                        var arc = new Arc(endPt1, arcMidPoint, endPt2);
                        arcId = doc.Objects.AddArc(arc);
                        break;
                    }
                }
            }

            // Layer setup
            var layers = new LayerManager(doc);
            string tpPath = string.IsNullOrEmpty(roadName)
                ? LayerScheme.BuildPath(LayerScheme.TangentPolygon)
                : LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon);
            int tpLayerIdx = layers.EnsureLayer(tpPath);

            var transAttrs = new ObjectAttributes { LayerIndex = tpLayerIdx };
            doc.Objects.ModifyAttributes(transition1, transAttrs, true);
            doc.Objects.ModifyAttributes(transition2, transAttrs, true);
            if (arcId != Guid.Empty)
                doc.Objects.ModifyAttributes(arcId, transAttrs, true);

            // Split tangents at transition start points and join all pieces
            tangent1.ClosestPoint(transStart1, out double splitParam1);
            var tangent1Pieces = tangent1.Split(splitParam1);

            tangent2.ClosestPoint(transStart2, out double splitParam2);
            var tangent2Pieces = tangent2.Split(splitParam2);

            var curvesToJoin = new List<Curve>();
            if (tangent1Pieces != null && tangent1Pieces.Length > 0)
                curvesToJoin.Add(tangent1Pieces[0]);
            curvesToJoin.Add(trans1Curve);
            if (arcId != Guid.Empty)
            {
                var arcObj = doc.Objects.FindId(arcId);
                if (arcObj?.Geometry is Curve arcCurve)
                    curvesToJoin.Add(arcCurve);
            }
            curvesToJoin.Add(trans2Curve);
            if (tangent2Pieces != null && tangent2Pieces.Length > 0)
                curvesToJoin.Add(tangent2Pieces[^1]);

            var joined = Curve.JoinCurves(curvesToJoin);
            if (joined != null && joined.Length > 0)
            {
                string side = isRightTurn ? "R" : "L";
                var joinAttrs = new ObjectAttributes
                {
                    LayerIndex = tpLayerIdx,
                    Name = $"{roadName} {tangentIndex} {side}_{CurveTypeName}_R_{R}"
                };
                doc.Objects.AddCurve(joined[0], joinAttrs);
            }

            // Delete original tangent lines
            doc.Objects.Delete(tangent1Obj, true);
            doc.Objects.Delete(tangent2Obj, true);

            // Add stationing points and visual markers (RC2 enhancement)
            string stPtsPath = string.IsNullOrEmpty(roadName)
                ? LayerScheme.BuildPath(LayerScheme.TangentPolygon, LayerScheme.Stationing, LayerScheme.StationingPoints)
                : LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, LayerScheme.Stationing, LayerScheme.StationingPoints);
            int stPtsLayerIdx = layers.EnsureLayer(stPtsPath);

            string stPath = string.IsNullOrEmpty(roadName)
                ? LayerScheme.BuildPath(LayerScheme.TangentPolygon, LayerScheme.Stationing)
                : LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, LayerScheme.Stationing);
            int stLayerIdx = layers.EnsureLayer(stPath);

            double tangent1Length = tangent1.GetLength();
            double priorChainage = tangent1Length - largeTangent;
            string sideLabel = isRightTurn ? "R" : "L";

            // I7: compute arc length from the actual constructed arc geometry
            double arcLength = 0;
            if (arcId != Guid.Empty)
            {
                var arc = new Arc(endPt1, arcMidPoint, endPt2);
                arcLength = arc.Length;
            }

            var stationingData = new[]
            {
                ("ZP", priorChainage, transStart1),
                ("PO", priorChainage + L, endPt1),
                ("OP", priorChainage + L + arcLength, endPt2),
                ("KP", priorChainage + 2 * L + arcLength, transStart2),
            };

            // Get the joined curve for tangent direction computation
            Curve? joinedCurve = (joined != null && joined.Length > 0) ? joined[0] : null;

            foreach (var (label, chainage, pos) in stationingData)
            {
                // Stationing point object
                var ptAttrs = new ObjectAttributes
                {
                    LayerIndex = stPtsLayerIdx,
                    Name = RoadObjectNaming.BuildStationingName(
                        roadName, tangentIndex, sideLabel, label, chainage)
                };
                doc.Objects.AddPoint(pos, ptAttrs);

                // RC2: Visual tick mark and text label on Stationing layer
                double tangentAngle = 0;
                if (joinedCurve != null && joinedCurve.ClosestPoint(pos, out double cp))
                {
                    var tangentDir = joinedCurve.TangentAt(cp);
                    tangentAngle = Math.Atan2(tangentDir.Y, tangentDir.X) * 180.0 / Math.PI;
                }

                // Tick line (33 units, perpendicular to curve)
                const double TickLength = 33.0;
                var tickLine = new Line(Point3d.Origin, new Point3d(TickLength, 0, 0));
                var tickCurve = new LineCurve(tickLine);
                var tickXform = Transform.Rotation(
                    (tangentAngle - 90) * Math.PI / 180.0,
                    Vector3d.ZAxis, Point3d.Origin);
                tickXform = Transform.Translation(new Vector3d(pos)) * tickXform;
                tickCurve.Transform(tickXform);
                doc.Objects.AddCurve(tickCurve, new ObjectAttributes { LayerIndex = stLayerIdx });

                // Text label (e.g. "ZP km 0.070000")
                double chainageKm = chainage / 1000.0;
                var textEntity = new TextEntity
                {
                    Plane = new Plane(
                        new Point3d(pos.X - 2, pos.Y - 0.5, 0), Vector3d.ZAxis),
                    PlainText = $"{label} km {chainageKm:F6}",
                    TextHeight = 3.0
                };
                var textId = doc.Objects.AddText(textEntity,
                    new ObjectAttributes { LayerIndex = stLayerIdx });
                if (textId != Guid.Empty)
                {
                    // Move to position and rotate to follow curve direction
                    var textXform = Transform.Rotation(
                        tangentAngle * Math.PI / 180.0,
                        Vector3d.ZAxis, pos);
                    doc.Objects.Transform(textId, textXform, true);
                }
            }

            // Lock stationing layers
            layers.LockLayer(stPtsPath);
            layers.LockLayer(stPath);

            // Legend text
            string legendPath = string.IsNullOrEmpty(roadName)
                ? LayerScheme.BuildPath(LayerScheme.TangentPolygon, "Legend")
                : LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon, "Legend");
            int legendLayerIdx = layers.EnsureLayer(legendPath);

            var legendAttrs = new ObjectAttributes { LayerIndex = legendLayerIdx };
            var legendPt = new Point3d(midpoint.X, midpoint.Y + 5, 0);
            var legend = new TextEntity
            {
                Plane = new Plane(legendPt, Vector3d.ZAxis),
                PlainText = $"R = {R}m; {CurveTypeName}\nalpha = {deflectionAngle:F2} deg; T = {largeTangent:F3} m\nLk = {L} m",
                TextHeight = 3.0
            };
            doc.Objects.AddText(legend, legendAttrs);

            RhinoApp.WriteLine($"Arc created with symmetric {CurveTypeName} transition, R = {R}m, L = {L}m");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static void ParseTangentIdentity(string? objectName, out string roadName, out int tangentIndex)
    {
        roadName = RoadObjectNaming.ParseRoadName(objectName) ?? "";
        tangentIndex = 0;

        if (string.IsNullOrWhiteSpace(objectName))
            return;

        var parts = objectName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
            int.TryParse(parts[1], out tangentIndex);
    }

    private ResolvedTangents? ResolveTangentsForRoad(RhinoDoc doc, string roadName)
    {
        string tpPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.TangentPolygon);
        int layerIdx = doc.Layers.FindByFullPath(tpPath, -1);
        if (layerIdx < 0)
            return null;

        var objects = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
        if (objects == null)
            return null;

        var tangentSegments = new List<(int Index, RhinoObject Object, Curve Curve)>();
        foreach (var obj in objects)
        {
            if (obj.Geometry is not Curve curve)
                continue;

            var name = obj.Attributes.Name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !string.Equals(parts[0], roadName, StringComparison.Ordinal))
                continue;

            if (!int.TryParse(parts[1], out int index))
                continue;

            tangentSegments.Add((index, obj, curve));
        }

        if (tangentSegments.Count < 2)
            return null;

        tangentSegments.Sort((a, b) => a.Index.CompareTo(b.Index));
        var first = tangentSegments[^2];
        var second = tangentSegments[^1];

        return new ResolvedTangents(
            roadName,
            first.Index,
            first.Object,
            first.Curve,
            second.Object,
            second.Curve);
    }
}
