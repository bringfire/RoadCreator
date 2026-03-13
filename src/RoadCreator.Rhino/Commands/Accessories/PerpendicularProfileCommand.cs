using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Localization;

namespace RoadCreator.Rhino.Commands.Accessories;

/// <summary>
/// Places a profile curve perpendicular to a guide curve at a specified point.
/// Converts from kolmyprofil.rvb.
///
/// Algorithm:
///   1. Select guide curve
///   2. Select profile curve
///   3. Pick placement point on curve (default: curve start)
///   4. Find tangent angle at placement point
///   5. Move profile from its start point to placement point
///   6. Rotate by tangent angle
/// </summary>
[System.Runtime.InteropServices.Guid("A1000007-B2C3-D4E5-F6A7-B8C9D0E1F207")]
public class PerpendicularProfileCommand : Command
{
    public override string EnglishName => "RC_PerpendicularProfile";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt(Strings.SelectProfileCurve);
        getCurve.GeometryFilter = ObjectType.Curve;
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;
        var guideCurve = getCurve.Object(0).Curve();

        var getProfile = new GetObject();
        getProfile.SetCommandPrompt(Strings.SelectProfileToPlace);
        getProfile.GeometryFilter = ObjectType.Curve;
        getProfile.EnablePreSelect(false, true);
        if (getProfile.Get() != GetResult.Object)
            return Result.Cancel;
        var profileRef = getProfile.Object(0);
        var profileId = profileRef.ObjectId;

        // Pick placement point (optional — default to curve start)
        var getPoint = new GetPoint();
        getPoint.SetCommandPrompt(Strings.SelectProfilePlacementPoint);
        getPoint.AcceptNothing(true);
        Point3d placementPt;
        var ptResult = getPoint.Get();
        if (ptResult == GetResult.Point)
        {
            placementPt = getPoint.Point();
        }
        else
        {
            placementPt = guideCurve.PointAtStart;
        }

        doc.Views.RedrawEnabled = false;

        try
        {
            double tolerance = doc.ModelAbsoluteTolerance;

            // Get tangent at placement point
            guideCurve.ClosestPoint(placementPt, out double t);
            var tangent = guideCurve.TangentAt(t);
            double tangentAngle = System.Math.Atan2(tangent.Y, tangent.X) * (180.0 / System.Math.PI);

            // Get profile start point
            var profileCurve = profileRef.Curve();
            var profileStart = profileCurve.PointAtStart;

            // Transform the profile object in-place: move then rotate
            var moveXform = Transform.Translation(placementPt - profileStart);
            var rotateXform = Transform.Rotation(
                tangentAngle * System.Math.PI / 180.0, Vector3d.ZAxis, placementPt);
            var combined = rotateXform * moveXform;

            doc.Objects.Transform(profileId, combined, true);

            RhinoApp.WriteLine(Strings.PerpendicularProfilePlaced);
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
