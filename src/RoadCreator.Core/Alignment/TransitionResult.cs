using RoadCreator.Core.Math;

namespace RoadCreator.Core.Alignment;

/// <summary>
/// Result of computing a symmetric transition curve pair with connecting arc.
/// Both clothoid and cubic parabola produce this same output structure.
/// </summary>
public class TransitionResult
{
    /// <summary>Points of the first transition curve (in local coords, origin at tangent start).</summary>
    public required Point3[] Transition1Points { get; init; }

    /// <summary>Points of the second transition curve (mirrored).</summary>
    public required Point3[] Transition2Points { get; init; }

    /// <summary>Large tangent distance from vertex to transition start.</summary>
    public double LargeTangent { get; init; }

    /// <summary>Shift parameter m.</summary>
    public double Shift { get; init; }

    /// <summary>Xs parameter (half-length projection on tangent).</summary>
    public double Xs { get; init; }

    /// <summary>Deflection angle between the two tangents (degrees).</summary>
    public double DeflectionAngle { get; init; }

    /// <summary>Arc length of the connecting circular arc.</summary>
    public double ArcLength { get; init; }

    /// <summary>Whether the curve turns right (true) or left (false).</summary>
    public bool IsRightTurn { get; init; }
}

/// <summary>
/// Stationing points generated for an alignment transition.
/// </summary>
public record StationingPoint(
    string Label,   // ZP, PO, OP, KP, ZU, KU, ZZ, KZ, V
    double Chainage, // Distance from route start in meters
    Point3 Position  // 2D position
);
