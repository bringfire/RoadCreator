using RoadCreator.Core.CrossSection;
using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.CrossSection;

public class CrossSectionComputerTests
{
    // --- Undivided road, straight section, no verge ---

    [Fact]
    public void UndividedStraight_NoVerge_Returns3Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0, includeVerge: false, vergeWidth: 0);

        Assert.Equal(3, pts.Length);

        // Center point is origin
        Assert.Equal(0, pts[1].X, 1e-10);
        Assert.Equal(0, pts[1].Y, 1e-10);

        // Left and right edges are symmetric in straight (M=1, Z=0)
        // Z_left = ((1 × (-2.5)) + (-4 × 0)) / 100 × 3.25 = -2.5/100 × 3.25 = -0.08125
        // Z_right = ((1 × (-2.5)) + (+4 × 0)) / 100 × 3.25 = -2.5/100 × 3.25 = -0.08125
        Assert.Equal(-3.25, pts[0].X, 1e-10);
        Assert.Equal(-0.08125, pts[0].Y, 1e-10);
        Assert.Equal(3.25, pts[2].X, 1e-10);
        Assert.Equal(-0.08125, pts[2].Y, 1e-10);
    }

    // --- Undivided road, left curve, no verge ---

    [Fact]
    public void UndividedLeftCurve_NoVerge_AsymmetricCrossfall()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0.2, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 1.0, includeVerge: false, vergeWidth: 0);

        Assert.Equal(3, pts.Length);

        double edgeWidth = 3.25 + 0.2; // 3.45
        // M=0, Z=1 (left curve)
        // Z_left = ((0 × (-2.5)) + (-4 × 1)) / 100 × 3.45 = -4/100 × 3.45 = -0.138
        // Z_right = ((0 × (-2.5)) + (+4 × 1)) / 100 × 3.45 = +4/100 × 3.45 = +0.138
        Assert.Equal(-edgeWidth, pts[0].X, 1e-10);
        Assert.Equal(-0.138, pts[0].Y, 1e-10);
        Assert.Equal(edgeWidth, pts[2].X, 1e-10);
        Assert.Equal(0.138, pts[2].Y, 1e-10);
    }

    // --- Undivided road, right curve, no verge ---

    [Fact]
    public void UndividedRightCurve_NoVerge_OppositeSign()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0.2, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: -1.0, includeVerge: false, vergeWidth: 0);

        Assert.Equal(3, pts.Length);

        // edgeWidth = 3.45, M=0, Z=-1 (right curve)
        // Z_left = ((0 × (-2.5)) + (-4 × (-1))) / 100 × 3.45 = +4/100 × 3.45 = +0.138
        // Z_right = ((0 × (-2.5)) + (+4 × (-1))) / 100 × 3.45 = -4/100 × 3.45 = -0.138
        Assert.Equal(0.138, pts[0].Y, 1e-10);
        Assert.Equal(-0.138, pts[2].Y, 1e-10);
    }

    // --- Undivided road with verge ---

    [Fact]
    public void UndividedStraight_WithVerge_Returns5Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0, includeVerge: true, vergeWidth: 0.75);

        Assert.Equal(5, pts.Length);

        // Outer verge edges
        Assert.Equal(-(3.25 + 0.75), pts[0].X, 1e-10);
        Assert.Equal(3.25 + 0.75, pts[4].X, 1e-10);

        // Verge drops 8% from road edge
        double roadEdgeZ = -0.08125; // same as straight crossfall test
        double vergeDropZ = -8.0 / 100.0 * 0.75;
        Assert.Equal(roadEdgeZ + vergeDropZ, pts[0].Y, 1e-10);
    }

    // --- Divided road, no verge ---

    [Fact]
    public void DividedStraight_NoVerge_Returns4Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S2075, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0, includeVerge: false, vergeWidth: 0);

        Assert.Equal(4, pts.Length);

        // Median edges at ±halfMedian
        double halfMedian = 1.25 / 2.0;
        Assert.Equal(-halfMedian, pts[1].X, 1e-10);
        Assert.Equal(0, pts[1].Y, 1e-10);
        Assert.Equal(halfMedian, pts[2].X, 1e-10);
        Assert.Equal(0, pts[2].Y, 1e-10);
    }

    [Fact]
    public void DividedStraight_WithVerge_Returns6Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S2075, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0, includeVerge: true, vergeWidth: 1.5);

        Assert.Equal(6, pts.Length);
    }

    // --- Widening affects edge width ---

    [Fact]
    public void Widening_IncreasesEdgeWidth()
    {
        var ptsNoW = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 1.0, includeVerge: false, vergeWidth: 0);

        var ptsWithW = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0.5, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 1.0, includeVerge: false, vergeWidth: 0);

        // With widening, outer edges should be further from center
        Assert.True(System.Math.Abs(ptsWithW[2].X) > System.Math.Abs(ptsNoW[2].X));
    }

    // --- Null category throws ---

    [Fact]
    public void ComputeProfilePoints_NullCategory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CrossSectionComputer.ComputeProfilePoints(
                null!, 0, 2.5, 4.0, 0, false, 0));
    }

    // --- ComputeVergeWidth ---

    [Fact]
    public void ComputeVergeWidth_Guardrail()
    {
        Assert.Equal(1.5, CrossSectionComputer.ComputeVergeWidth(true));
    }

    [Fact]
    public void ComputeVergeWidth_RoadPoles()
    {
        Assert.Equal(0.75, CrossSectionComputer.ComputeVergeWidth(false));
    }

    // --- Profile points are in correct order (left to right) ---

    [Fact]
    public void ProfilePoints_AreOrderedLeftToRight()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S95, widening: 0.3, crossfallStraight: 2.5,
            crossfallCurve: 5.0, curveDirection: 1.0, includeVerge: true, vergeWidth: 1.0);

        for (int i = 1; i < pts.Length; i++)
        {
            Assert.True(pts[i].X > pts[i - 1].X,
                $"Point {i} (X={pts[i].X}) should be right of point {i - 1} (X={pts[i - 1].X})");
        }
    }

    // --- Symmetry in straight section ---

    [Fact]
    public void Straight_ProfileIsSymmetric()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0, includeVerge: false, vergeWidth: 0);

        // Left and right edges should be mirror images
        Assert.Equal(-pts[0].X, pts[2].X, 1e-10);
        Assert.Equal(pts[0].Y, pts[2].Y, 1e-10);
    }

    // --- Fractional curveDirection (LM/PM transitions) ---

    [Fact]
    public void TransitionLeftHalf_BlendsCrossfall()
    {
        // curveDirection = +0.5 (LM transition): M = 0.5, Z = 0.5
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0.1, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 0.5, includeVerge: false, vergeWidth: 0);

        Assert.Equal(3, pts.Length);

        // edgeWidth = 3.25 + 0.1 = 3.35
        // Z_left = ((0.5 × (-2.5)) + (-4 × 0.5)) / 100 × 3.35
        //        = (-1.25 + -2.0) / 100 × 3.35 = -3.25/100 × 3.35 = -0.108875
        Assert.Equal(-0.108875, pts[0].Y, 1e-10);
        // Z_right = ((0.5 × (-2.5)) + (+4 × 0.5)) / 100 × 3.35
        //         = (-1.25 + 2.0) / 100 × 3.35 = 0.75/100 × 3.35 = 0.025125
        Assert.Equal(0.025125, pts[2].Y, 1e-10);
    }

    [Fact]
    public void TransitionRightHalf_BlendsCrossfall()
    {
        // curveDirection = -0.5 (PM transition): M = 0.5, Z = -0.5
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, widening: 0.1, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: -0.5, includeVerge: false, vergeWidth: 0);

        Assert.Equal(3, pts.Length);

        // edgeWidth = 3.35
        // Z_left = ((0.5 × (-2.5)) + (-4 × (-0.5))) / 100 × 3.35
        //        = (-1.25 + 2.0) / 100 × 3.35 = 0.75/100 × 3.35 = 0.025125
        Assert.Equal(0.025125, pts[0].Y, 1e-10);
        // Z_right = ((0.5 × (-2.5)) + (+4 × (-0.5))) / 100 × 3.35
        //         = (-1.25 + -2.0) / 100 × 3.35 = -3.25/100 × 3.35 = -0.108875
        Assert.Equal(-0.108875, pts[2].Y, 1e-10);
    }

    [Fact]
    public void TransitionIsBetweenStraightAndCurve()
    {
        // Verify that transition elevation is between straight and full-curve values
        var straight = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, 0, 2.5, 4.0, 0, false, 0);
        var fullCurve = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, 0, 2.5, 4.0, 1.0, false, 0);
        var transition = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, 0, 2.5, 4.0, 0.5, false, 0);

        // Left edge: transition Y should be between straight Y and full-curve Y
        double minYLeft = System.Math.Min(straight[0].Y, fullCurve[0].Y);
        double maxYLeft = System.Math.Max(straight[0].Y, fullCurve[0].Y);
        Assert.True(transition[0].Y >= minYLeft && transition[0].Y <= maxYLeft,
            $"Transition left Y={transition[0].Y} not between {minYLeft} and {maxYLeft}");

        // Right edge: same check
        double minYRight = System.Math.Min(straight[2].Y, fullCurve[2].Y);
        double maxYRight = System.Math.Max(straight[2].Y, fullCurve[2].Y);
        Assert.True(transition[2].Y >= minYRight && transition[2].Y <= maxYRight,
            $"Transition right Y={transition[2].Y} not between {minYRight} and {maxYRight}");
    }

    // --- curveDirection clamping ---

    [Fact]
    public void CurveDirectionAboveOne_IsClamped()
    {
        // curveDirection = 2.0 should be clamped to 1.0 (same as full left curve)
        var ptsClamped = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, 0, 2.5, 4.0, 2.0, false, 0);
        var ptsFullCurve = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S75, 0, 2.5, 4.0, 1.0, false, 0);

        Assert.Equal(ptsFullCurve[0].Y, ptsClamped[0].Y, 1e-10);
        Assert.Equal(ptsFullCurve[2].Y, ptsClamped[2].Y, 1e-10);
    }

    // --- Divided road with curves ---

    [Fact]
    public void DividedLeftCurve_NoVerge_Returns4Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S2075, widening: 0.2, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: 1.0, includeVerge: false, vergeWidth: 0);

        Assert.Equal(4, pts.Length);

        double halfMedian = 1.25 / 2.0;
        double edgeWidth = 10.25 + 0.2 + halfMedian; // 11.075
        // M=0, Z=1 (left curve)
        // Z_left = ((0 × (-2.5)) + (-4 × 1)) / 100 × 11.075 = -4/100 × 11.075 = -0.443
        // Z_right = ((0 × (-2.5)) + (+4 × 1)) / 100 × 11.075 = +4/100 × 11.075 = +0.443
        Assert.Equal(-edgeWidth, pts[0].X, 1e-10);
        Assert.Equal(-0.443, pts[0].Y, 1e-10);
        Assert.Equal(edgeWidth, pts[3].X, 1e-10);
        Assert.Equal(0.443, pts[3].Y, 1e-10);
    }

    [Fact]
    public void DividedRightCurve_WithVerge_Returns6Points()
    {
        var pts = CrossSectionComputer.ComputeProfilePoints(
            RoadCategory.S2075, widening: 0.3, crossfallStraight: 2.5,
            crossfallCurve: 4.0, curveDirection: -1.0, includeVerge: true, vergeWidth: 1.5);

        Assert.Equal(6, pts.Length);

        // Outer verge edges should be further out than road edges
        Assert.True(System.Math.Abs(pts[0].X) > System.Math.Abs(pts[1].X));
        Assert.True(System.Math.Abs(pts[5].X) > System.Math.Abs(pts[4].X));
    }
}
