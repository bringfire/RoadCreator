namespace RoadCreator.Core.Math;

/// <summary>
/// Curve sampling and analysis utilities for polyline-based curves.
/// </summary>
public static class CurveUtils
{
    /// <summary>
    /// Sample a polyline at equidistant intervals, returning the sampled points.
    /// Mirrors DivideCurveEquidistant from VBScript.
    /// </summary>
    public static List<Point3> DividePolylineEquidistant(IReadOnlyList<Point3> polyline, double interval)
    {
        if (polyline.Count < 2 || interval <= 0)
            return new List<Point3>(polyline);

        var result = new List<Point3> { polyline[0] };
        double accumulated = 0;
        int segIndex = 0;
        Point3 current = polyline[0];

        while (segIndex < polyline.Count - 1)
        {
            var segEnd = polyline[segIndex + 1];
            double segRemaining = current.DistanceTo(segEnd);

            double needed = interval - accumulated;

            if (segRemaining >= needed - 1e-10)
            {
                double t = needed / segRemaining;
                if (t >= 1.0 - 1e-10)
                {
                    // Landed on or past segment end — advance to next segment
                    current = segEnd;
                    accumulated = segRemaining - needed;
                    if (accumulated < 0) accumulated = 0;
                    segIndex++;
                }
                else
                {
                    current = GeometryMath.Lerp(current, segEnd, t);
                    accumulated = 0;
                }
                result.Add(current);
            }
            else
            {
                accumulated += segRemaining;
                current = segEnd;
                segIndex++;
            }
        }

        return result;
    }

    /// <summary>
    /// Compute the tangent angle (in degrees from +X axis) at each point of a polyline.
    /// At interior points, uses the average of adjacent segment directions.
    /// </summary>
    public static List<double> ComputeTangentAngles(IReadOnlyList<Point3> points)
    {
        var angles = new List<double>(points.Count);
        if (points.Count < 2)
        {
            if (points.Count == 1) angles.Add(0);
            return angles;
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                angles.Add(AngleUtils.AngleBetweenPoints(points[0], points[1]));
            }
            else if (i == points.Count - 1)
            {
                angles.Add(AngleUtils.AngleBetweenPoints(points[i - 1], points[i]));
            }
            else
            {
                double a1 = AngleUtils.AngleBetweenPoints(points[i - 1], points[i]);
                double a2 = AngleUtils.AngleBetweenPoints(points[i], points[i + 1]);
                double avg = a1 + AngleUtils.AngleDifference(a1, a2) / 2.0;
                angles.Add(AngleUtils.Normalize360(avg));
            }
        }

        return angles;
    }

    /// <summary>
    /// Compute the total length of a polyline.
    /// </summary>
    public static double PolylineLength(IReadOnlyList<Point3> points)
    {
        double len = 0;
        for (int i = 1; i < points.Count; i++)
            len += points[i - 1].DistanceTo(points[i]);
        return len;
    }

    /// <summary>
    /// Estimate the radius of curvature at a point using three consecutive points.
    /// Returns double.MaxValue for nearly straight segments.
    /// </summary>
    public static double EstimateRadius(Point3 p0, Point3 p1, Point3 p2)
    {
        double a = p0.DistanceTo(p1);
        double b = p1.DistanceTo(p2);
        double c = p0.DistanceTo(p2);

        double s = (a + b + c) / 2.0;
        double area = System.Math.Sqrt(System.Math.Max(0, s * (s - a) * (s - b) * (s - c)));

        if (area < 1e-12) return double.MaxValue;

        return (a * b * c) / (4.0 * area);
    }
}
