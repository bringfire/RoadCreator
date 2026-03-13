using RoadCreator.Core.Alignment;
using RoadCreator.Core.Math;
using Xunit;

namespace RoadCreator.Core.Tests.Alignment;

public class Route3DAssemblerTests
{
    // --- Assemble ---

    [Fact]
    public void Assemble_CombinesHorizontalAndElevation()
    {
        var horizontal = new Point3[]
        {
            new(0, 0, 0),
            new(10, 5, 0),
            new(20, 10, 0)
        };
        var elevations = new double[] { 100, 103, 106 };

        var result = Route3DAssembler.Assemble(horizontal, elevations);

        Assert.Equal(3, result.Length);
        Assert.Equal(0, result[0].X);
        Assert.Equal(0, result[0].Y);
        Assert.Equal(100, result[0].Z);
        Assert.Equal(10, result[1].X);
        Assert.Equal(5, result[1].Y);
        Assert.Equal(103, result[1].Z);
        Assert.Equal(20, result[2].X);
        Assert.Equal(10, result[2].Y);
        Assert.Equal(106, result[2].Z);
    }

    [Fact]
    public void Assemble_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Route3DAssembler.Assemble(null!, new double[] { 1 }));
        Assert.Throws<ArgumentNullException>(() =>
            Route3DAssembler.Assemble(new Point3[] { new(0, 0, 0) }, null!));
    }

    [Fact]
    public void Assemble_ThrowsOnLengthMismatch()
    {
        var horizontal = new Point3[] { new(0, 0, 0), new(10, 0, 0) };
        var elevations = new double[] { 100 };

        Assert.Throws<ArgumentException>(() =>
            Route3DAssembler.Assemble(horizontal, elevations));
    }

    [Fact]
    public void Assemble_SinglePoint()
    {
        var horizontal = new Point3[] { new(5, 3, 0) };
        var elevations = new double[] { 99 };

        var result = Route3DAssembler.Assemble(horizontal, elevations);

        Assert.Single(result);
        Assert.Equal(5, result[0].X);
        Assert.Equal(3, result[0].Y);
        Assert.Equal(99, result[0].Z);
    }

    // --- SampleElevation ---

    [Fact]
    public void SampleElevation_InterpolatesBetweenPoints()
    {
        var profile = new Point3[]
        {
            new(0, 100, 0),
            new(100, 110, 0),
            new(200, 120, 0)
        };

        double elev = Route3DAssembler.SampleElevation(profile, 50);
        Assert.Equal(105, elev, 1e-10);
    }

    [Fact]
    public void SampleElevation_ExactMatch()
    {
        var profile = new Point3[]
        {
            new(0, 100, 0),
            new(100, 110, 0)
        };

        Assert.Equal(100, Route3DAssembler.SampleElevation(profile, 0), 1e-10);
        Assert.Equal(110, Route3DAssembler.SampleElevation(profile, 100), 1e-10);
    }

    [Fact]
    public void SampleElevation_ExtrapolatesBefore()
    {
        var profile = new Point3[]
        {
            new(100, 200, 0),
            new(200, 210, 0)
        };

        // Slope = (210-200)/(200-100) = 0.1
        // At chainage 50: 200 + 0.1 * (50-100) = 200 - 5 = 195
        double elev = Route3DAssembler.SampleElevation(profile, 50);
        Assert.Equal(195, elev, 1e-10);
    }

    [Fact]
    public void SampleElevation_ExtrapolatesAfter()
    {
        var profile = new Point3[]
        {
            new(0, 100, 0),
            new(100, 110, 0)
        };

        // Slope = 0.1, at chainage 150: 110 + 0.1 * 50 = 115
        double elev = Route3DAssembler.SampleElevation(profile, 150);
        Assert.Equal(115, elev, 1e-10);
    }

    [Fact]
    public void SampleElevation_SinglePoint()
    {
        var profile = new Point3[] { new(100, 200, 0) };

        Assert.Equal(200, Route3DAssembler.SampleElevation(profile, 100), 1e-10);
        Assert.Equal(200, Route3DAssembler.SampleElevation(profile, 50), 1e-10);
    }

    [Fact]
    public void SampleElevation_ThrowsOnEmptyProfile()
    {
        Assert.Throws<ArgumentException>(() =>
            Route3DAssembler.SampleElevation(Array.Empty<Point3>(), 50));
    }

    [Fact]
    public void SampleElevation_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() =>
            Route3DAssembler.SampleElevation(null!, 50));
    }

    [Fact]
    public void SampleElevation_CoincidentChainages_ReturnsFirstElevation()
    {
        // Two profile points with same X (chainage) — should return lo point's Y, not NaN
        var profile = new Point3[]
        {
            new(0, 100, 0),
            new(50, 110, 0),
            new(50, 120, 0),
            new(100, 130, 0)
        };

        double elev = Route3DAssembler.SampleElevation(profile, 50);
        // dx = 0 guard returns profilePoints[lo].Y
        Assert.False(double.IsNaN(elev));
    }

    [Fact]
    public void SampleElevation_MultipleSegments()
    {
        var profile = new Point3[]
        {
            new(0, 100, 0),
            new(50, 105, 0),
            new(100, 115, 0),
            new(200, 125, 0)
        };

        // Between second and third: (75 - 50)/(100 - 50) = 0.5
        // 105 + 0.5 * (115 - 105) = 110
        Assert.Equal(110, Route3DAssembler.SampleElevation(profile, 75), 1e-10);

        // Between third and fourth: (150 - 100)/(200 - 100) = 0.5
        // 115 + 0.5 * (125 - 115) = 120
        Assert.Equal(120, Route3DAssembler.SampleElevation(profile, 150), 1e-10);
    }

    // --- ComputeChainages ---

    [Fact]
    public void ComputeChainages_StraightLine()
    {
        var points = new Point3[]
        {
            new(0, 0, 0),
            new(10, 0, 0),
            new(30, 0, 0)
        };

        var chainages = Route3DAssembler.ComputeChainages(points);

        Assert.Equal(3, chainages.Length);
        Assert.Equal(0, chainages[0], 1e-10);
        Assert.Equal(10, chainages[1], 1e-10);
        Assert.Equal(30, chainages[2], 1e-10);
    }

    [Fact]
    public void ComputeChainages_DiagonalSegments()
    {
        var points = new Point3[]
        {
            new(0, 0, 0),
            new(3, 4, 0),   // distance = 5
            new(6, 8, 0)    // distance = 5
        };

        var chainages = Route3DAssembler.ComputeChainages(points);

        Assert.Equal(0, chainages[0], 1e-10);
        Assert.Equal(5, chainages[1], 1e-10);
        Assert.Equal(10, chainages[2], 1e-10);
    }

    [Fact]
    public void ComputeChainages_SinglePoint()
    {
        var points = new Point3[] { new(5, 3, 0) };
        var chainages = Route3DAssembler.ComputeChainages(points);

        Assert.Single(chainages);
        Assert.Equal(0, chainages[0], 1e-10);
    }

    [Fact]
    public void ComputeChainages_Uses2DDistance()
    {
        // Z values should be ignored in chainage calculation
        var points = new Point3[]
        {
            new(0, 0, 100),
            new(10, 0, 200)
        };

        var chainages = Route3DAssembler.ComputeChainages(points);
        Assert.Equal(10, chainages[1], 1e-10);
    }

    [Fact]
    public void ComputeChainages_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Route3DAssembler.ComputeChainages(Array.Empty<Point3>()));
    }

    [Fact]
    public void ComputeChainages_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() =>
            Route3DAssembler.ComputeChainages(null!));
    }

    // --- Integration: Full pipeline ---

    [Fact]
    public void FullPipeline_AssembleWithSampledElevations()
    {
        // Simulate a horizontal alignment
        var horizontal = new Point3[]
        {
            new(0, 0, 0),
            new(50, 0, 0),
            new(100, 0, 0)
        };

        // Profile: linear grade from elevation 200 to 210 over 100m
        var profile = new Point3[]
        {
            new(0, 200, 0),
            new(100, 210, 0)
        };

        // Compute chainages and sample elevations
        var chainages = Route3DAssembler.ComputeChainages(horizontal);
        var elevations = new double[horizontal.Length];
        for (int i = 0; i < horizontal.Length; i++)
            elevations[i] = Route3DAssembler.SampleElevation(profile, chainages[i]);

        var result = Route3DAssembler.Assemble(horizontal, elevations);

        Assert.Equal(3, result.Length);
        Assert.Equal(200, result[0].Z, 1e-10);
        Assert.Equal(205, result[1].Z, 1e-10);
        Assert.Equal(210, result[2].Z, 1e-10);
    }
}
