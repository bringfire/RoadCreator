namespace RoadCreator.Core.Math;

/// <summary>
/// Lightweight 3D vector type for Core library (no RhinoCommon dependency).
/// </summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Vector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 UnitX => new(1, 0, 0);
    public static Vector3 UnitY => new(0, 1, 0);
    public static Vector3 UnitZ => new(0, 0, 1);

    public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3 Normalized
    {
        get
        {
            double len = Length;
            if (len < 1e-15) return Zero;
            return new Vector3(X / len, Y / len, Z / len);
        }
    }

    public static double Dot(Vector3 a, Vector3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3 Cross(Vector3 a, Vector3 b) =>
        new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator *(double s, Vector3 v) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);

    public bool Equals(Vector3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(Vector3 left, Vector3 right) => left.Equals(right);
    public static bool operator !=(Vector3 left, Vector3 right) => !left.Equals(right);

    public override string ToString() => $"<{X:F3}, {Y:F3}, {Z:F3}>";
}
