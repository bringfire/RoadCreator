using RoadCreator.Core.Alignment;
using CorePoint3 = RoadCreator.Core.Math.Point3;

namespace RoadCreator.Rhino.Commands.Alignment;

/// <summary>
/// Inserts a symmetric cubic parabola transition curve between two tangent segments.
/// Converts from PrechodniceKubParabola.rvb.
/// </summary>
[System.Runtime.InteropServices.Guid("E5F6A7B8-C9D0-1E2F-3A4B-5C6D7E8F9012")]
public class CubicParabolaCommand : TransitionCommandBase
{
    public override string EnglishName => "RC_CubicParabola";

    protected override string CurveTypeName => "cubic parabola";
    protected override double DefaultL => 60;
    protected override double DefaultR => 200;

    protected override string? ValidateParameters(double L, double R)
    {
        if (L / (2.0 * R) >= 1.0)
            return "Transition length L must be less than 2*R for cubic parabola.";
        return null;
    }

    protected override double GetLargeTangent(double L, double R, double deflectionAngleDeg)
        => CubicParabolaTransition.ComputeLargeTangent(L, R, deflectionAngleDeg);

    protected override double GetShift(double L, double R)
        => CubicParabolaTransition.ComputeShift(L, R);

    protected override CorePoint3[] GetTransitionPoints(double L, double R)
        => CubicParabolaTransition.ComputePoints(L, R);
}
