namespace RoadCreator.Core.Math;

/// <summary>
/// Lightweight 3D point type for Core library (no RhinoCommon dependency).
/// Convert to/from Rhino.Geometry.Point3d at the Rhino project boundary.
/// </summary>
public readonly struct Point3 : IEquatable<Point3>
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Point3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Point3 Origin => new(0, 0, 0);

    public double DistanceTo(Point3 other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double DistanceTo2D(Point3 other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    public Point3 WithZ(double z) => new(X, Y, z);

    public static Point3 operator +(Point3 a, Vector3 v) => new(a.X + v.X, a.Y + v.Y, a.Z + v.Z);
    public static Point3 operator -(Point3 a, Vector3 v) => new(a.X - v.X, a.Y - v.Y, a.Z - v.Z);
    public static Vector3 operator -(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public bool Equals(Point3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Point3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(Point3 left, Point3 right) => left.Equals(right);
    public static bool operator !=(Point3 left, Point3 right) => !left.Equals(right);

    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
}
