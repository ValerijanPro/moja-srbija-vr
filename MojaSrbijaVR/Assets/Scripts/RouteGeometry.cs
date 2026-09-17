using System;
using System.Collections.Generic;

// Geometry only: metres for grouping, view-plane coordinates for directional picking.
// No Unity objects, so the same rules can be checked without launching the editor.
public static class RouteGeometry
{
    public struct Point
    {
        public double x, y;
        public Point(double x, double y) { this.x = x; this.y = y; }
        public static Point Lerp(Point a, Point b, double t) =>
            new Point(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }

    public sealed class Path
    {
        public readonly Point[] points;
        public readonly Point[] samples;
        public readonly string sport;
        public readonly double length;
        public Path(Point[] points, string sport)
        {
            this.points = points;
            this.sport = sport;
            for (int i = 1; i < points.Length; i++) length += Math.Sqrt(DistanceSquared(points[i - 1], points[i]));
            samples = Resample(points, length, 96);
        }
    }

    public static double DistanceSquared(Point a, Point b)
    {
        double x = a.x - b.x, y = a.y - b.y;
        return x * x + y * y;
    }

    static Point[] Resample(Point[] points, double length, int count)
    {
        if (points.Length < 2 || length <= 1e-9) return points;
        var result = new Point[count];
        int segment = 1;
        double start = 0, end = Math.Sqrt(DistanceSquared(points[0], points[1]));
        for (int i = 0; i < count; i++)
        {
            double target = length * i / (count - 1);
            while (segment < points.Length - 1 && end < target)
            {
                start = end;
                segment++;
                end += Math.Sqrt(DistanceSquared(points[segment - 1], points[segment]));
            }
            result[i] = Point.Lerp(points[segment - 1], points[segment],
                end > start ? Math.Max(0, Math.Min(1, (target - start) / (end - start))) : 0);
        }
        return result;
    }

    public static double Nearest(Point[] points, Point query, out int segment, out double t)
    {
        segment = -1; t = 0;
        double best = double.PositiveInfinity;
        for (int i = 1; i < points.Length; i++)
        {
            Point a = points[i - 1], b = points[i];
            double dx = b.x - a.x, dy = b.y - a.y;
            double denom = dx * dx + dy * dy;
            double u = denom > 1e-15 ? ((query.x - a.x) * dx + (query.y - a.y) * dy) / denom : 0;
            u = Math.Max(0, Math.Min(1, u));
            double d = DistanceSquared(query, Point.Lerp(a, b, u));
            if (d < best) { best = d; segment = i - 1; t = u; }
        }
        return best;
    }

    static bool Covered(Path a, Path b, double tolerance, double coverage)
    {
        int inside = 0;
        double threshold = tolerance * tolerance;
        foreach (var p in a.samples)
        {
            double distance = Nearest(b.points, p, out _, out _);
            // A substantial detour must not disappear into the common part of a route.
            if (distance > threshold * 9) return false;
            if (distance <= threshold) inside++;
        }
        return inside >= Math.Ceiling(a.samples.Length * coverage);
    }

    public static bool Similar(Path a, Path b, double tolerance = 150, double coverage = 0.9,
        double minimumLengthRatio = 0.8)
    {
        if (a.sport != b.sport || a.length < 1 || b.length < 1) return false;
        if (Math.Min(a.length, b.length) / Math.Max(a.length, b.length) < minimumLengthRatio) return false;
        return Covered(a, b, tolerance, coverage) && Covered(b, a, tolerance, coverage);
    }

    public static List<List<int>> Group(IReadOnlyList<Path> paths, double tolerance = 150,
        double coverage = 0.9, double minimumLengthRatio = 0.8)
    {
        var groups = new List<List<int>>();
        for (int i = 0; i < paths.Count; i++)
        {
            List<int> match = null;
            foreach (var group in groups)
            {
                bool fits = true;
                // Complete linkage avoids A~B~C merging even though A and C differ.
                foreach (int member in group)
                    if (!Similar(paths[i], paths[member], tolerance, coverage, minimumLengthRatio))
                    { fits = false; break; }
                if (fits) { match = group; break; }
            }
            if (match == null) { match = new List<int>(); groups.Add(match); }
            match.Add(i);
        }
        return groups;
    }

    // A separate visual grouping: nearby markers share a region bubble without
    // claiming that their activities have the same route geometry.
    public static List<List<int>> Regions(IReadOnlyList<Point> points, double radius)
    {
        var groups = new List<List<int>>();
        double squared = radius * radius;
        for (int i = 0; i < points.Count; i++)
        {
            List<int> match = null;
            foreach (var group in groups)
            {
                bool fits = true;
                foreach (int j in group) if (DistanceSquared(points[i], points[j]) > squared) { fits = false; break; }
                if (fits) { match = group; break; }
            }
            if (match == null) { match = new List<int>(); groups.Add(match); }
            match.Add(i);
        }
        return groups;
    }

    // Directions: 0 up, 1 right, 2 down, 3 left. Clip each segment to its 90-degree
    // wedge, then find the closest point INSIDE it (not just the closest vertex).
    public static double InDirection(Point[] points, int direction, double minimumDistance,
        out int segment, out double t)
    {
        segment = -1; t = 0;
        double best = double.PositiveInfinity;
        for (int i = 1; i < points.Length; i++)
        {
            Point a = Rotate(points[i - 1], direction), b = Rotate(points[i], direction);
            double lo = 0, hi = 1;
            if (!Clip(a.y - a.x, b.y - b.x, ref lo, ref hi) ||
                !Clip(a.y + a.x, b.y + b.x, ref lo, ref hi) ||
                !Clip(a.y - minimumDistance, b.y - minimumDistance, ref lo, ref hi)) continue;
            double dx = b.x - a.x, dy = b.y - a.y, denom = dx * dx + dy * dy;
            double u = denom > 1e-15 ? -(a.x * dx + a.y * dy) / denom : lo;
            u = Math.Max(lo, Math.Min(hi, u));
            double d = DistanceSquared(new Point(0, 0), Point.Lerp(a, b, u));
            if (d < best) { best = d; segment = i - 1; t = u; }
        }
        return best;
    }

    static Point Rotate(Point p, int direction)
    {
        switch (direction)
        {
            case 1: return new Point(-p.y, p.x);
            case 2: return new Point(-p.x, -p.y);
            case 3: return new Point(p.y, -p.x);
            default: return p;
        }
    }

    static bool Clip(double a, double b, ref double lo, ref double hi)
    {
        if (a < 0 && b < 0) return false;
        if (a < 0) lo = Math.Max(lo, a / (a - b));
        else if (b < 0) hi = Math.Min(hi, a / (a - b));
        return lo <= hi;
    }
}
