using RoadCreator.Core.Math;

namespace RoadCreator.Core.Accessories;

/// <summary>
/// Delta Blok concrete barrier profile data for 4 variants.
/// Converts from SvodidlaDelta.rvb.
///
/// Each variant has 3 profile types:
///   - Main: full cross-section at regular post positions
///   - EndCap: flattened top (capped at shoulder height z2)
///   - FullEndCap: nearly flat (just above base, for start/end termination)
///
/// All profiles are 8-point polylines in the YZ plane (X=0).
/// Block spacing is 4m, with a computed transition distance for end-cap placement.
/// </summary>
public static class DeltaBlokProfileComputer
{
    /// <summary>Block spacing along the curve (meters).</summary>
    public const double BlockSpacing = 4.0;

    /// <summary>
    /// Get the main cross-section profile for a variant.
    /// </summary>
    public static Point3[] GetMainProfile(DeltaBlokVariant variant)
    {
        return variant switch
        {
            DeltaBlokVariant.Blok80 => new[]
            {
                new Point3(0, -0.31, -0.05), new Point3(0, -0.29, 0.11),
                new Point3(0, -0.14, 0.29),  new Point3(0, -0.06, 0.80),
                new Point3(0, 0.06, 0.80),   new Point3(0, 0.14, 0.29),
                new Point3(0, 0.29, 0.11),   new Point3(0, 0.31, -0.05),
            },
            DeltaBlokVariant.Blok100S => new[]
            {
                new Point3(0, -0.33, -0.05), new Point3(0, -0.31, 0.13),
                new Point3(0, -0.14, 0.35),  new Point3(0, -0.06, 1.00),
                new Point3(0, 0.06, 1.00),   new Point3(0, 0.14, 0.35),
                new Point3(0, 0.31, 0.13),   new Point3(0, 0.33, -0.05),
            },
            DeltaBlokVariant.Blok100 => new[]
            {
                new Point3(0, -0.35, -0.05), new Point3(0, -0.34, 0.10),
                new Point3(0, -0.17, 0.39),  new Point3(0, -0.11, 1.00),
                new Point3(0, 0.11, 1.00),   new Point3(0, 0.17, 0.39),
                new Point3(0, 0.34, 0.10),   new Point3(0, 0.35, -0.05),
            },
            DeltaBlokVariant.Blok120 => new[]
            {
                new Point3(0, -0.42, -0.05), new Point3(0, -0.38, 0.08),
                new Point3(0, -0.18, 0.39),  new Point3(0, -0.12, 1.20),
                new Point3(0, 0.12, 1.20),   new Point3(0, 0.18, 0.39),
                new Point3(0, 0.38, 0.08),   new Point3(0, 0.42, -0.05),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
    }

    /// <summary>
    /// Get the end-cap profile (top flattened to shoulder height z2).
    /// Derived from main profile per VBScript formula.
    /// </summary>
    public static Point3[] GetEndCapProfile(DeltaBlokVariant variant)
    {
        var m = GetMainProfile(variant);
        return new[]
        {
            m[0],
            m[1],
            new Point3(m[2].X, m[2].Y - 0.01, m[2].Z),
            new Point3(m[3].X, m[2].Y, m[2].Z),
            new Point3(m[4].X, m[4].Y, m[2].Z),
            new Point3(m[5].X, m[4].Y + 0.01, m[2].Z),
            m[6],
            m[7],
        };
    }

    /// <summary>
    /// Get the full end-cap profile (nearly flat, for start/end termination).
    /// Derived from main profile per VBScript formula.
    /// </summary>
    public static Point3[] GetFullEndCapProfile(DeltaBlokVariant variant)
    {
        var m = GetMainProfile(variant);
        return new[]
        {
            m[0],
            m[1],
            new Point3(m[1].X, m[1].Y + 0.005, m[1].Z),
            new Point3(m[1].X, m[1].Y + 0.01, m[1].Z),
            new Point3(m[6].X, m[6].Y - 0.01, m[6].Z),
            new Point3(m[6].X, m[6].Y - 0.005, m[6].Z),
            m[6],
            m[7],
        };
    }

    /// <summary>
    /// Compute the transition distance from full end-cap to end-cap profile.
    /// VBScript formula: 4 * ((z2 - z1) / (z3 - z1))
    /// </summary>
    public static double GetTransitionDistance(DeltaBlokVariant variant)
    {
        var m = GetMainProfile(variant);
        double z1 = m[1].Z;
        double z2 = m[2].Z;
        double z3 = m[3].Z;
        return BlockSpacing * ((z2 - z1) / (z3 - z1));
    }
}

/// <summary>
/// Delta Blok barrier variants (Czech standard sizes).
/// </summary>
public enum DeltaBlokVariant
{
    Blok80,
    Blok100S,
    Blok100,
    Blok120,
}
