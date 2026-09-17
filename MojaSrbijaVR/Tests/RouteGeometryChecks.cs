using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

public static class RouteGeometryChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    static RouteGeometry.Point P(double x, double y) => new RouteGeometry.Point(x, y);
    static RouteGeometry.Path Path(string sport, params RouteGeometry.Point[] p) => new RouteGeometry.Path(p, sport);

    public static int Main(string[] args)
    {
        var a = Path("Ride", P(0, 0), P(1000, 0), P(1000, 1000));
        var reverse = Path("Ride", P(1010, 1000), P(1010, 500), P(1010, 0), P(500, 0), P(0, 0));
        Check(RouteGeometry.Similar(a, reverse), "Reversed direction and sampling density must not split a route.");
        Check(!RouteGeometry.Similar(a, Path("Run", a.points)), "Different sports must stay separate.");
        Check(!RouteGeometry.Similar(a, Path("Ride", P(0, 0), P(1000, 0))), "A shared partial route is not a whole-route match.");
        Check(!RouteGeometry.Similar(a, Path("Ride", P(0, 0), P(1000, 0), P(1000, -1000))), "Different branches must stay separate.");
        Check(!RouteGeometry.Similar(a, Path("Ride", P(0, 0), P(1000, 0), P(1000, 1000), P(1000, 0), P(0, 0))),
            "Different lap counts must stay separate by length.");
        Check(!RouteGeometry.Similar(Path("Ride", P(0, 0), P(0, 0)), a), "Degenerate data must not merge.");
        var chain = new[] { Path("Ride", P(0, 0), P(0, 1000)), Path("Ride", P(100, 0), P(100, 1000)),
            Path("Ride", P(200, 0), P(200, 1000)) };
        var chainGroups = RouteGeometry.Group(chain);
        Check(chainGroups.Count == 2 && chainGroups[0].Count == 2, "A bridge route must not chain unrelated groups.");

        var square = Path("Ride", P(0, 0), P(1000, 0), P(1000, 1000), P(0, 1000), P(0, 0));
        var shiftedStart = Path("Ride", P(1000, 1000), P(0, 1000), P(0, 0), P(1000, 0), P(1000, 1000));
        Check(RouteGeometry.Similar(square, shiftedStart), "A loop can start at a different location.");

        var axes = new[] {
            new[] { P(-1, 2), P(1, 2) }, new[] { P(2, -1), P(2, 1) },
            new[] { P(-1, -2), P(1, -2) }, new[] { P(-2, -1), P(-2, 1) }
        };
        for (int direction = 0; direction < 4; direction++)
        {
            double dist = RouteGeometry.InDirection(axes[direction], direction, 0.003, out int segment, out double t);
            Check(segment == 0 && Math.Abs(t - 0.5) < 1e-9 && Math.Abs(dist - 4) < 1e-9, "Incorrect cardinal direction.");
            Check(double.IsPositiveInfinity(RouteGeometry.InDirection(axes[direction], (direction + 2) % 4, 0.003, out _, out _)),
                "An empty direction must not wrap to a route behind it.");
        }
        double crossing = RouteGeometry.InDirection(new[] { P(-10, 2), P(10, 2) }, 0, 0.003, out _, out double crossT);
        Check(Math.Abs(crossing - 4) < 1e-9 && Math.Abs(crossT - 0.5) < 1e-9,
            "Segments crossing a wedge must be found even when both endpoints are outside.");
        double nearest = RouteGeometry.Nearest(new[] { P(-10, 2), P(10, 2) }, P(0, 0), out _, out double nearestT);
        Check(Math.Abs(nearest - 4) < 1e-9 && Math.Abs(nearestT - 0.5) < 1e-9, "Hover must use segments, not vertices.");
        Check(double.IsPositiveInfinity(RouteGeometry.InDirection(new RouteGeometry.Point[0], 0, 0.003, out _, out _)),
            "Empty routes must not be selectable.");
        double clipped = RouteGeometry.InDirection(new[] { P(5, 0), P(0, 5) }, 0, 0.003, out _, out double clipT);
        Check(Math.Abs(clipped - 12.5) < 1e-9 && Math.Abs(clipT - 0.5) < 1e-9, "Sector edge clipping is incorrect.");

        var regions = RouteGeometry.Regions(new[] { P(0, 0), P(0.02, 0.01), P(0.8, 0.8) }, 0.14);
        Check(regions.Count == 2 && regions[0].Count == 2, "Nearby route markers must share an overview region.");
        Check(regions.SelectMany(g => g).Distinct().Count() == 3, "The overview must preserve access to every route.");
        Check(RouteGeometry.Regions(new RouteGeometry.Point[0], 0.14).Count == 0, "Empty overview should be supported.");
        var distances = RouteReplayMath.Distances(new[] { P(0, 0), P(0, 0), P(30, 40), P(60, 40) });
        Check(Math.Abs(distances[3] - 80) < 1e-9, "Replay distance must follow polyline segments.");
        Check(RouteReplayMath.Sample(distances, 25, out int rs, out double rt) && rs == 1 && Math.Abs(rt - 0.5) < 1e-9,
            "Replay interpolation must skip duplicate starting points.");
        Check(RouteReplayMath.Sample(distances, 80, out rs, out rt) && rs == 2 && rt == 1,
            "Replay must reach the final point exactly.");
        Check(RouteReplayMath.Sample(distances, -10, out rs, out rt) && rs == 1 && rt == 0,
            "Negative progress must clamp to the start.");
        Check(RouteReplayMath.Advance(79, 10, 1, 80) == 80, "Replay must stop at the end, not wrap or overshoot.");
        Check(RouteReplayMath.Advance(25, 0, 2, 80) == 25, "Paused replay must not move.");
        Check(!RouteReplayMath.Sample(new double[] { 0, 0 }, 0, out _, out _), "A zero-length route cannot be replayed.");

        if (args.Length > 0)
        {
            var paths = new List<RouteGeometry.Path>();
            var inv = CultureInfo.InvariantCulture;
            var borderPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]), "border.txt");
            double centerLatitude = 44;
            if (File.Exists(borderPath))
            {
                var latitudes = File.ReadAllText(borderPath).Trim().Split(';')
                    .Select(p => double.Parse(p.Split(',')[1], inv)).ToArray();
                centerLatitude = (latitudes.Min() + latitudes.Max()) / 2;
            }
            foreach (string line in File.ReadLines(args[0]))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                var points = parts[1].Split(';').Select(p =>
                {
                    var c = p.Split(',');
                    return P((double.Parse(c[0], inv) - 21) * 111320 * Math.Cos(centerLatitude * Math.PI / 180),
                        (double.Parse(c[1], inv) - 44) * 111320);
                }).ToArray();
                paths.Add(Path(parts[0], points));
            }
            var timer = Stopwatch.StartNew();
            var groups = RouteGeometry.Group(paths);
            timer.Stop();
            Check(groups.SelectMany(g => g).Distinct().Count() == paths.Count, "Grouping lost activities.");
            Check(groups.Sum(g => g.Count) == paths.Count, "An activity occurs more than once.");
            foreach (var group in groups)
                for (int i = 0; i < group.Count; i++)
                    for (int j = 0; j < i; j++)
                        Check(RouteGeometry.Similar(paths[group[i]], paths[group[j]]), "A real-data group contains incompatible routes.");
            Console.WriteLine("Actual data: {0} activities -> {1} groups ({2} repeated groups), {3} ms.",
                paths.Count, groups.Count, groups.Count(g => g.Count > 1), timer.ElapsedMilliseconds);
            Console.WriteLine("Group sizes: " + string.Join(", ", groups.Select(g => g.Count).OrderByDescending(n => n)));
        }
        Console.WriteLine("PASS: " + checks + " geometry/grouping checks.");
        return 0;
    }
}
