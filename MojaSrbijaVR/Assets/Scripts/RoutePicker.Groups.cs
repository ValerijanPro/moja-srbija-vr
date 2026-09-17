using System.Collections.Generic;
using UnityEngine;

public partial class RoutePicker
{
    [Header("Grupisanje slicnih ruta")]
    public bool aggregateSimilarRoutes = true;
    [Tooltip("GPS odstupanje u metrima, nezavisno od zuma makete. Primenjuje se pri Play-u.")]
    [Range(25, 400)] public float similarityMeters = 150f;
    [Range(0.8f, 1f)] public float requiredCoverage = 0.9f;
    [Range(0.5f, 1f)] public float minimumLengthRatio = 0.8f;

    readonly List<List<int>> routeGroups = new List<List<int>>();
    LineRenderer[] routeLines;
    Vector3[][] routePoints;
    RouteGeometry.Point[][] flatPoints;
    int[] groupForRoute, displayedMember;
    bool[] originalVisibility;
    int selectedIndex = -1;
    Material selectionMaterial;
    Material rideRouteMaterial, runRouteMaterial, otherRouteMaterial;
    readonly List<int> recommendedGroups = new List<int>();

    void BuildGroups()
    {
        int count = ruteParent.childCount;
        routeLines = new LineRenderer[count];
        routePoints = new Vector3[count][];
        flatPoints = new RouteGeometry.Point[count][];
        groupForRoute = new int[count];
        originalVisibility = new bool[count];
        var paths = new List<RouteGeometry.Path>();
        var indices = new List<int>();
        var sports = new string[0];
        float unitsPerMeter = 1;
        bool canAggregate = aggregateSimilarRoutes;
        var routeShader = Shader.Find("MojaSrbija/RouteUnlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        rideRouteMaterial = new Material(routeShader) { color = new Color(0.02f, 0.92f, 1f, 1f) };
        runRouteMaterial = new Material(routeShader) { color = new Color(1f, 0.08f, 0.42f, 1f) };
        otherRouteMaterial = new Material(routeShader) { color = new Color(0.65f, 1f, 0.05f, 1f) };
        if (canAggregate)
        {
            try
            {
                unitsPerMeter = ProjSrbija.UnitsPerMeter;
                sports = System.IO.File.ReadAllLines(System.IO.Path.Combine(RuntimeData.Root, "routes.txt"));
            }
            catch (System.Exception e)
            {
                canAggregate = false;
                Debug.LogWarning("[MojaSrbija] Grupisanje nije dostupno, ostaju pojedinacne rute: " + e.Message);
            }
        }
        for (int i = 0; i < count; i++)
        {
            groupForRoute[i] = -1;
            var lr = ruteParent.GetChild(i).GetComponent<LineRenderer>();
            routeLines[i] = lr;
            if (lr == null) continue;
            originalVisibility[i] = lr.enabled;
            var points = new Vector3[lr.positionCount];
            lr.GetPositions(points);
            var flat = new RouteGeometry.Point[points.Length];
            var metres = new RouteGeometry.Point[points.Length];
            for (int k = 0; k < points.Length; k++)
            {
                var world = lr.useWorldSpace ? points[k] : lr.transform.TransformPoint(points[k]);
                points[k] = ruteParent.InverseTransformPoint(world);
                flat[k] = new RouteGeometry.Point(points[k].x, points[k].z);
                metres[k] = new RouteGeometry.Point(points[k].x / unitsPerMeter, points[k].z / unitsPerMeter);
            }
            routePoints[i] = points;
            flatPoints[i] = flat;
            // The baked lines were designed for a tiny tabletop and became almost
            // transparent after the new vertical-map layout. Keep them opaque,
            // slightly wider and above the imagery in the render order.
            lr.widthMultiplier = Mathf.Max(lr.widthMultiplier, 0.010f);
            lr.sortingOrder = 20;
            // okrugle kapice prave "perle" kad je sirina veca od duzine segmenta
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.alignment = LineAlignment.View;
            string routeSport = i < sports.Length && sports[i].Contains("|")
                ? sports[i].Substring(0, sports[i].IndexOf('|')) : "Ride";
            lr.sharedMaterial = routeSport == "Run" ? runRouteMaterial :
                routeSport == "Ride" ? rideRouteMaterial : otherRouteMaterial;
            // Separate overlapping paths by a tiny depth offset to stop z-fighting.
            float depthOffset = 0.010f + (i % 17) * 0.000025f;
            for (int k = 0; k < points.Length; k++) points[k].y += depthOffset;
            lr.SetPositions(points);
            if (!lr.enabled || !lr.gameObject.activeInHierarchy || points.Length < 2) continue;
            int dataIndex = DataIndex(i);
            // Unknown sport must not merge with another unknown activity.
            string sport = dataIndex < sports.Length && sports[dataIndex].Contains("|")
                ? sports[dataIndex].Substring(0, sports[dataIndex].IndexOf('|')) : "unknown_" + i;
            paths.Add(new RouteGeometry.Path(metres, sport));
            indices.Add(i);
        }
        if (canAggregate)
        {
            foreach (var group in RouteGeometry.Group(paths, similarityMeters, requiredCoverage, minimumLengthRatio))
            {
                var members = new List<int>();
                foreach (int p in group) members.Add(indices[p]);
                routeGroups.Add(members);
            }
        }
        else foreach (int i in indices) routeGroups.Add(new List<int> { i });

        displayedMember = new int[routeGroups.Count];
        for (int g = 0; g < routeGroups.Count; g++)
        {
            displayedMember[g] = routeGroups[g][0];
            foreach (int i in routeGroups[g])
            {
                groupForRoute[i] = g;
                routeLines[i].enabled = i == displayedMember[g];
            }
        }
        BuildRecommendations();
        Debug.Log($"[MojaSrbija] {indices.Count} aktivnosti -> {routeGroups.Count} grupa ruta. " +
            "Trigger + desna palica: smer; A/B: aktivnosti iz grupe.");
    }


    void BuildRecommendations()
    {
        recommendedGroups.Clear();
        var scored = new List<(int group, float score)>();
        for (int g = 0; g < routeGroups.Count; g++)
        {
            int r = routeGroups[g][0]; var m = Metadata(r);
            if (m.Length < 6 || !float.TryParse(m[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float km)) continue;
            string name = m[1].ToLowerInvariant();
            float score = routeGroups[g].Count * 12f - Mathf.Abs(km - 22f) * 0.25f;
            if (name.Contains("ada")) score += 35;
            if (name.Contains("avala")) score += 25;
            if (name.Contains("fru")) score += 18;
            if (name.Contains("run") || name.Contains("walk")) score -= 100;
            scored.Add((g, score));
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));
        for (int i = 0; i < Mathf.Min(6, scored.Count); i++) recommendedGroups.Add(scored[i].group);
    }

    public IReadOnlyList<int> RecommendedGroups => recommendedGroups;

    public (int count, float km, float elev) Totals()
    {
        int n = 0; float km = 0, elev = 0;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < RouteCount; i++)
        {
            var m = Metadata(i);
            if (m.Length > 4 && float.TryParse(m[3], System.Globalization.NumberStyles.Float, inv, out float k))
            {
                n++; km += k;
                if (float.TryParse(m[4], System.Globalization.NumberStyles.Float, inv, out float e)) elev += e;
            }
        }
        return (n, km, elev);
    }
    public string RecommendationReason(int route)
    {
        var m = Metadata(route); int count = GroupSize(route);
        string familiar = count > 1 ? $"Slicnu putanju si vozio {count} puta." : "Odgovara duzini tvojih cestih voznji.";
        string name = m.Length > 1 ? m[1].ToLowerInvariant() : "";
        if (name.Contains("ada")) return familiar + " Ada je medju tvojim ponavljanim destinacijama.";
        if (name.Contains("avala")) return familiar + " Izdvaja se kao voznja sa usponom.";
        return familiar;
    }

    int DataIndex(int route)
    {
        string n = routeLines[route].name;
        return n.StartsWith("ruta_") && int.TryParse(n.Substring(5), out int index) && index >= 0 ? index : route;
    }

    string[] Metadata(int route)
    {
        int i = DataIndex(route);
        return i < meta.Length ? meta[i] : new string[0];
    }

    string RouteTitle(int route)
    {
        var m = Metadata(route);
        string name = m.Length > 1 ? m[1] : "Ruta " + (DataIndex(route) + 1);
        return name;
    }

    bool IsPickable(int i) => routeLines[i] != null && routeLines[i].gameObject.activeInHierarchy &&
        groupForRoute[i] >= 0 && displayedMember[groupForRoute[i]] == i;

    public int RouteCount => routeLines == null ? 0 : routeLines.Length;
    public int GroupCount => routeGroups.Count;
    public int SelectedIndex => selectedIndex;
    public Vector3[] Points(int i) => routePoints[i];
    public int Representative(int group) => displayedMember[group];
    public int GroupSize(int route) => routeGroups[groupForRoute[route]].Count;
    public string Title(int route) => RouteTitle(route);
    public string ListTitle(int route)
    {
        string title = RouteTitle(route);
        var m = Metadata(route);
        if (title.Length > 40) title = title.Substring(0, 37) + "...";
        return (m.Length > 2 ? m[2] + " | " : "") + title;
    }
    public float SelectedSpeedMetersPerSecond
    {
        get
        {
            var m = Metadata(selectedIndex);
            return m.Length > 5 && float.TryParse(m[5], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float speed) ? Mathf.Clamp(speed / 3.6f, 1, 20) : 5;
        }
    }
    public void SelectFromMenu(int i) { CancelNavigation(); Select(i); hoverTimer = 1; }
    public void BrowseActivity(int step) => CycleActivity(step);
    public bool TryAimOnMap(out Vector3 point) => RayToBoard(out point, out _);

    bool ridingVisibility;
    readonly Dictionary<Renderer, bool> rideHidden = new Dictionary<Renderer, bool>();
    public void SetRideVisibility(bool riding)
    {
        ridingVisibility = riding;
        if (riding)
        {
            var border = transform.Find("Granica");
            if (border != null)
                foreach (var renderer in border.GetComponentsInChildren<Renderer>())
                { rideHidden[renderer] = renderer.enabled; renderer.enabled = false; }
        }
        else
        {
            foreach (var pair in rideHidden) if (pair.Key != null) pair.Key.enabled = pair.Value;
            rideHidden.Clear();
            if (selected != null && MiniRunner.I != null) MiniRunner.I.Follow(selected);
        }
        RefreshVisibleRoutes();
    }

    void RefreshVisibleRoutes()
    {
        for (int i = 0; i < routeLines.Length; i++)
            if (routeLines[i] != null) routeLines[i].enabled = ridingVisibility
                ? routeLines[i] == selected
                : IsPickable(i);
    }

    void ShowMember(int i)
    {
        int g = groupForRoute[i];
        if (g < 0) return;
        routeLines[displayedMember[g]].enabled = false;
        displayedMember[g] = i;
        routeLines[i].enabled = true;
    }

    void RestoreRepresentative()
    {
        if (selectedIndex < 0) return;
        int g = groupForRoute[selectedIndex];
        if (g >= 0) ShowMember(routeGroups[g][0]);
        selectedIndex = -1;
    }

    bool CycleActivity(int step)
    {
        if (selectedIndex < 0) return false;
        var members = routeGroups[groupForRoute[selectedIndex]];
        if (members.Count <= 1) return false;
        int next = (members.IndexOf(selectedIndex) + step + members.Count) % members.Count;
        Select(members[next]);
        hoverTimer = 1;
        navigationTimer = 0;
        if (SfxManager.I != null) SfxManager.I.Click();
        return true;
    }

    void RestoreRouteVisibility()
    {
        if (routeLines == null) return;
        for (int i = 0; i < routeLines.Length; i++)
            if (routeLines[i] != null) routeLines[i].enabled = originalVisibility[i];
    }
}
