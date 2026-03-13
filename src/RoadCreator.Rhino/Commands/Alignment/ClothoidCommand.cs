using RoadCreator.Core.Alignment;
using CorePoint3 = RoadCreator.Core.Math.Point3;

namespace RoadCreator.Rhino.Commands.Alignment;

/// <summary>
/// Inserts a symmetric clothoid transition curve between two tangent segments.
/// Converts from RC2_PrechodniceKlotoida_CZ.rvb.
/// </summary>
[System.Runtime.InteropServices.Guid("D4E5F6A7-B8C9-0D1E-2F3A-4B5C6D7E8F90")]
public class ClothoidCommand : TransitionCommandBase
{
    public override string EnglishName => "RC_Clothoid";

    protected override string CurveTypeName => "clothoid";
    protected override double DefaultL => 70;
    protected override double DefaultR => 150;

    protected override double GetLargeTangent(double L, double R, double deflectionAngleDeg)
        => ClothoidTransition.ComputeLargeTangent(L, R, deflectionAngleDeg);

    protected override double GetShift(double L, double R)
        => ClothoidTransition.ComputeShift(L, R);

    protected override CorePoint3[] GetTransitionPoints(double L, double R)
        => ClothoidTransition.ComputePoints(L, R);
}
