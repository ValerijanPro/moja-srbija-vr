using System;

public static class RouteReplayMath
{
    public static double[] Distances(RouteGeometry.Point[] points)
    {
        var cumulative = new double[points.Length];
        for (int i = 1; i < points.Length; i++)
            cumulative[i] = cumulative[i - 1] + Math.Sqrt(RouteGeometry.DistanceSquared(points[i - 1], points[i]));
        return cumulative;
    }

    public static bool Sample(double[] cumulative, double distance, out int segment, out double t)
    {
        segment = 0; t = 0;
        if (cumulative.Length < 2 || cumulative[cumulative.Length - 1] <= 0) return false;
        distance = Math.Max(0, Math.Min(cumulative[cumulative.Length - 1], distance));
        int lo = 1, hi = cumulative.Length - 1;
        while (lo < hi)
        {
            int middle = (lo + hi) / 2;
            if (cumulative[middle] <= distance) lo = middle + 1;
            else hi = middle;
        }
        segment = lo - 1;
        double length = cumulative[lo] - cumulative[segment];
        t = length > 0 ? (distance - cumulative[segment]) / length : 0;
        return true;
    }

    public static double Advance(double distance, double speed, double seconds, double total) =>
        Math.Max(0, Math.Min(Math.Max(0, total), distance + Math.Max(0, speed) * Math.Max(0, seconds)));
}
