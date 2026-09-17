// Ista projekcija kao u MaketaBaker-u (mora ostati sinhronizovano!):
// lon/lat -> lokalne koordinate makete.
using System.Globalization;
using UnityEngine;

public static class ProjSrbija
{
    const float SIZE_NS = 1.25f;
    static bool init;
    static double minLon, maxLon, minLat, maxLat, cLon, cLat;
    static float F;

    public static float UnitsPerMeter { get { Init(); return F; } }

    static void Init()
    {
        if (init) return;
        var txt = System.IO.File.ReadAllText(
            System.IO.Path.Combine(RuntimeData.Root, "border.txt")).Trim();
        minLon = 999; maxLon = -999; minLat = 999; maxLat = -999;
        var inv = CultureInfo.InvariantCulture;
        foreach (var p in txt.Split(';'))
        {
            var c = p.Split(',');
            double lon = double.Parse(c[0], inv), lat = double.Parse(c[1], inv);
            minLon = System.Math.Min(minLon, lon); maxLon = System.Math.Max(maxLon, lon);
            minLat = System.Math.Min(minLat, lat); maxLat = System.Math.Max(maxLat, lat);
        }
        minLon -= 0.02; maxLon += 0.02; minLat -= 0.02; maxLat += 0.02;
        cLon = (minLon + maxLon) / 2; cLat = (minLat + maxLat) / 2;
        F = SIZE_NS / (float)((maxLat - minLat) * 111320.0);
        init = true;
    }

    public static float LocalX(double lon)
    {
        Init();
        return (float)((lon - cLon) * 111320.0 * System.Math.Cos(cLat * System.Math.PI / 180) * F);
    }

    public static float LocalZ(double lat)
    {
        Init();
        return (float)((lat - cLat) * 111320.0 * F);
    }
}
