using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class MapExperience
{
    WorldRoutePanel menu;
    GameObject markerRoot;
    readonly List<TextMesh> markers = new List<TextMesh>();
    readonly List<List<int>> regions = new List<List<int>>();
    readonly List<Vector3> regionLocations = new List<Vector3>();
    List<int> listedGroups;
    int page;
    float clusterTimer;
    bool details;
    bool recommendations;
    bool menuVisible;
    int lastSelection = -1;
    string notice;

    public void OpenOverview()
    {
        menuVisible = true;
        details = false; recommendations = false; page = 0; listedGroups = new List<int>();
        for (int g = 0; g < picker.GroupCount; g++) listedGroups.Add(g);
        RebuildMenu();
    }

    public void OpenRecommendations()
    {
        menuVisible = true;
        details = false; recommendations = true; page = 0;
        listedGroups = new List<int>(picker.RecommendedGroups);
        RebuildMenu();
    }

    // preporuke kao pinovi na mapi: uokviri ih pogledom, meni sklonjen
    public void FocusRecommended()
    {
        var reps = new List<int>();
        foreach (int g in picker.RecommendedGroups) reps.Add(picker.Representative(g));
        if (reps.Count > 0) FitRoutes(reps);
        menuVisible = false;
        RebuildMenu();
    }

    void Experience_ShowWelcome()
    {
        StopVideo();
        gallery = false; details = false;
        menuVisible = false;
        WelcomeOpen = true;
        var welcome = gameObject.GetComponent<WelcomeScreen>();
        if (welcome == null) welcome = gameObject.AddComponent<WelcomeScreen>();
        welcome.Begin(this, picker);
    }

    public void SelectionChanged()
    {
        if (picker == null) return;
        lastSelection = picker.SelectedIndex;
        details = lastSelection >= 0;
        menuVisible = details;
        gallery = false;
        StopVideo();
        if (details) PlacePanel();
        notice = null;
        RebuildMenu();
    }

    /* ---------- galerija (slike + video) ---------- */

    bool gallery;
    int galleryIdx;
    RawImage galleryImage;
    UnityEngine.Video.VideoPlayer videoPlayer;
    RenderTexture videoRT;
    readonly Dictionary<string, Texture2D> galleryCache = new Dictionary<string, Texture2D>();
    Coroutine galleryCo;

    void OpenGallery()
    {
        gallery = true; galleryIdx = 0;
        menuVisible = true;
        PlacePanel();
        RebuildMenu();
    }

    void StopVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
    }

    public void GalleryCleanup()
    {
        StopVideo();
        if (videoRT != null) { videoRT.Release(); videoRT = null; }
        foreach (var t in galleryCache.Values) if (t != null) Destroy(t);
        galleryCache.Clear();
    }

    void BuildGallery()
    {
        var items = picker.MediaEntries(picker.SelectedIndex);
        if (items.Length == 0) { gallery = false; RebuildMenu(); return; }
        galleryIdx = (galleryIdx % items.Length + items.Length) % items.Length;
        var e = items[galleryIdx].Split('|');
        bool isVideo = e[0] == "V";

        menu.AddText($"Galerija  {galleryIdx + 1}/{items.Length}" + (isVideo ? "  ·  VIDEO" : ""), 45, 27, true);
        galleryImage = menu.AddBigPhoto(310);
        string poster = isVideo ? (e.Length > 2 ? e[2] : "") : e[1];
        if (galleryCo != null) StopCoroutine(galleryCo);
        if (!string.IsNullOrEmpty(poster)) galleryCo = StartCoroutine(ShowGalleryPhoto(poster, galleryIdx));

        if (isVideo)
            menu.AddButton("▶  Pusti video (sa zvukom)", () => PlayVideo(e[1]));
        if (items.Length > 1)
        {
            menu.AddButton("Sledeća  ▸", () => { StopVideo(); galleryIdx++; RebuildMenu(); });
            menu.AddButton("◂  Prethodna", () => { StopVideo(); galleryIdx--; RebuildMenu(); });
        }
        menu.AddButton("Nazad na rutu", () => { gallery = false; StopVideo(); RebuildMenu(); });
    }

    IEnumerator ShowGalleryPhoto(string url, int forIdx)
    {
        if (galleryCache.TryGetValue(url, out var cached) && cached != null)
        {
            if (gallery && galleryIdx == forIdx) WorldRoutePanel.SetPhoto(galleryImage, cached);
            yield break;
        }
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
            var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            galleryCache[url] = tex;
            if (gallery && galleryIdx == forIdx) WorldRoutePanel.SetPhoto(galleryImage, tex);
        }
    }

    void PlayVideo(string url)
    {
        // lokalni video iz APK podataka (videos/xxx.mp4) ili veb URL
        if (!url.StartsWith("http"))
            url = "file:///" + System.IO.Path.Combine(RuntimeData.Root, url).Replace('\\', '/');
        if (videoPlayer == null)
        {
            var go = new GameObject("GalerijaVideo");
            go.transform.SetParent(transform, false);
            videoPlayer = go.AddComponent<UnityEngine.Video.VideoPlayer>();
            var audio = go.AddComponent<AudioSource>();
            videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audio);
            videoRT = new RenderTexture(720, 405, 0);
            videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRT;
            videoPlayer.isLooping = true;
            videoPlayer.errorReceived += (_, message) =>
            {
                Debug.LogWarning("[MojaSrbija] Video greska: " + message);
                notice = "Video ne može da se pusti na ovom uređaju.";
                RebuildMenu();
            };
        }
        videoPlayer.url = url;
        videoPlayer.Play();
        if (galleryImage != null)
        {
            WorldRoutePanel.SetPhoto(galleryImage, videoRT);
        }
        if (SfxManager.I != null) SfxManager.I.Click();
    }

    void RebuildMenu()
    {
        if (menu == null) return;
        if (picker.SelectedIndex < 0) { details = false; gallery = false; }
        menu.Clear();
        if (gallery && picker.SelectedIndex >= 0) { BuildGallery(); menu.PlayEntrance(); return; }
        menu.AddText(details ? picker.Title(picker.SelectedIndex) : recommendations ? "Preporučeno za tebe" : "Moje rute", 75, 30, true);
        if (!string.IsNullOrEmpty(notice)) menu.AddText(notice, 65, 22);
        if (details && picker.SelectedIndex >= 0)
        {
            menu.AddText(picker.statsText != null ? picker.statsText.text : "", 100, 23);
            menu.AddPhotos();
            int mediaCount = picker.MediaEntries(picker.SelectedIndex).Length;
            if (mediaCount > 0)
                menu.AddButton($"Galerija  ({mediaCount} slika/videa)", OpenGallery);
            menu.AddButton("Pokreni vožnju  (X)", StartSelectedRide);
            menu.AddButton("Uvećaj izabranu rutu", () => FitRoutes(new[] { picker.SelectedIndex }));
            if (picker.GroupSize(picker.SelectedIndex) > 1)
            {
                menu.AddButton("Prethodna aktivnost  (B)", () => picker.BrowseActivity(-1));
                menu.AddButton("Sledeća aktivnost  (A)", () => picker.BrowseActivity(1));
            }
            menu.AddButton("Nazad na spisak ruta", () => { details = false; RebuildMenu(); });
        }
        else
        {
            menu.AddText(recommendations
                ? "Predlozi su iz tvoje arhive: učestalost, dužina i prepoznate destinacije. Podaci ne napuštaju uređaj."
                : "Izaberi rutu sa spiska ili uperi laser direktno u liniju.", 70, 22);
            if (listedGroups == null) listedGroups = new List<int>();
            int start = page * 6;
            for (int j = start; j < Mathf.Min(start + 6, listedGroups.Count); j++)
            {
                int route = picker.Representative(listedGroups[j]);
                string label = picker.ListTitle(route);
                if (recommendations) label += "\n" + picker.RecommendationReason(route);
                menu.AddButton(label, () => picker.SelectFromMenu(route), recommendations ? 78 : 55);
            }
            if (listedGroups.Count > 6)
            {
                int pages = (listedGroups.Count + 5) / 6;
                menu.AddButton($"Sledeća strana  ({page + 1}/{pages})", () => { page = (page + 1) % pages; RebuildMenu(); });
                menu.AddButton("Prethodna strana", () => { page = (page + pages - 1) % pages; RebuildMenu(); });
            }
            if (recommendations) menu.AddButton("Nazad na moje rute", OpenOverview);
            menu.AddButton("Glavna oblast ruta  (Y)", () => { FitDominantArea(); OpenOverview(); });
            menu.AddButton("Početni ekran", () => Experience_ShowWelcome());
        }
        menu.AddText("Oba triggera: izbor\nGrip: pomeri mapu | dve ruke: zum\nLeva palica: zum", 90, 20);
        menu.PlayEntrance();
    }

    void UpdateOverview()
    {
        if (lastSelection != picker.SelectedIndex) SelectionChanged();
        menu.root.SetActive(menuVisible);
        if (details && !gallery) menu.SetPhotos(picker.img1 != null ? picker.img1.texture : null, picker.img2 != null ? picker.img2.texture : null);
        clusterTimer -= Time.deltaTime;
        if (clusterTimer <= 0) { RebuildRegions(); clusterTimer = 0.2f; }
    }

    void RebuildRegions()
    {
        var positions = new List<Vector3>();
        var projected = new List<RouteGeometry.Point>();
        var groupIds = new List<int>();
        foreach (int g in picker.RecommendedGroups)
        {
            var points = picker.Points(picker.Representative(g));
            if (points == null || points.Length == 0) continue;
            // Use the point nearest the route's mean, so a marker remains on its route.
            Vector3 center = Vector3.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            Vector3 nearest = points[0]; float best = float.MaxValue;
            foreach (var p in points) if ((p - center).sqrMagnitude < best) { best = (p - center).sqrMagnitude; nearest = p; }
            Vector3 world = picker.ruteParent.TransformPoint(nearest);
            Vector3 view = head.WorldToViewportPoint(world);
            if (view.z <= 0 || view.x < -0.05f || view.x > 1.05f || view.y < -0.05f || view.y > 1.05f) continue;
            positions.Add(world); projected.Add(new RouteGeometry.Point(view.x * head.aspect, view.y)); groupIds.Add(g);
        }
        double radius = 0.06;
        var clusters = RouteGeometry.Regions(projected, radius);
        regions.Clear(); regionLocations.Clear();
        if (markerRoot == null) markerRoot = new GameObject("RegioniRuta");
        foreach (var cluster in clusters)
        {
            var groups = new List<int>(); Vector3 location = Vector3.zero;
            foreach (int i in cluster) { groups.Add(groupIds[i]); location += positions[i]; }
            regions.Add(groups); regionLocations.Add(location / cluster.Count - mapForward * 0.02f);
        }
        while (markers.Count < regions.Count)
        {
            var container = new GameObject("Region");
            container.transform.SetParent(markerRoot.transform, false);
            markers.Add(MakeMarkerText(container.transform, 84, new Color(1f, 0.5f, 0.08f), Vector3.zero, FontStyle.Bold));
            regionLabels.Add(MakeMarkerText(container.transform, 32, new Color(1f, 0.88f, 0.72f), new Vector3(0, -0.052f, 0), FontStyle.Normal));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(container.transform, false);
            quad.transform.localPosition = new Vector3(0, 0.09f, 0);
            quad.transform.localScale = new Vector3(0.115f, 0.075f, 1);
            quad.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));
            quad.SetActive(false);
            regionPhotos.Add(quad);
        }
        for (int i = 0; i < markers.Count; i++)
        {
            bool active = i < regions.Count;
            markers[i].transform.parent.gameObject.SetActive(active);
            if (!active) continue;
            markers[i].text = "\u25cf";
            var groups = regions[i];
            string title = groups.Count == 1 ? picker.Title(picker.Representative(groups[0])) : groups.Count + " predložene rute";
            if (title.Length > 26) title = title.Substring(0, 24) + "...";
            regionLabels[i].text = title;
            markers[i].transform.parent.SetPositionAndRotation(regionLocations[i], Quaternion.LookRotation(mapForward));
            regionPhotos[i].SetActive(false);
            if (groups.Count == 1)
            {
                var entries = picker.MediaEntries(picker.Representative(groups[0]));
                if (entries.Length > 0) StartCoroutine(LoadMarkerPhoto(PosterUrl(entries[0]), regionPhotos[i]));
            }
        }
        SetMarkersVisible(true);
    }

    readonly List<TextMesh> regionLabels = new List<TextMesh>();
    readonly List<GameObject> regionPhotos = new List<GameObject>();

    static TextMesh MakeMarkerText(Transform parent, int size, Color color, Vector3 offset, FontStyle style)
    {
        var go = new GameObject("Tekst", typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var text = go.GetComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.fontSize = size; text.characterSize = 0.009f;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.fontStyle = style;
        text.color = color;
        return text;
    }

    static string PosterUrl(string entry)
    {
        var c = entry.Split('|');
        if (c.Length >= 3 && c[0] == "V") return c[2];
        return c.Length >= 2 ? c[1] : "";
    }

    IEnumerator LoadMarkerPhoto(string url, GameObject quad)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("http")) yield break;
        if (!galleryCache.TryGetValue(url, out var tex) || tex == null)
        {
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
                tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                galleryCache[url] = tex;
            }
        }
        if (quad == null) yield break;
        var mat = quad.GetComponent<MeshRenderer>().material;
        mat.mainTexture = tex;
        float aspect = (float)tex.width / tex.height;
        quad.transform.localScale = new Vector3(0.075f * aspect, 0.075f, 1);
        quad.SetActive(true);
    }

    void SetMarkersVisible(bool visible) { if (markerRoot != null) markerRoot.SetActive(visible); }

    public bool HitMenu(Ray ray, out Vector3 point, bool press = false)
    {
        point = default;
        if (!initialized || BlocksMapInput) return false;
        if (menu.Hit(ray, out point, press)) return true;
        int target = -1; float nearest = float.MaxValue;
        for (int i = 0; i < regionLocations.Count; i++)
        {
            float along = Vector3.Dot(regionLocations[i] - ray.origin, ray.direction);
            if (along <= 0 || along >= nearest) continue;
            if (Vector3.Distance(ray.GetPoint(along), regionLocations[i]) > 0.06f) continue;
            nearest = along; target = i;
        }
        if (target < 0) return false;
        point = ray.GetPoint(nearest);
        if (press)
        {
            listedGroups = new List<int>(regions[target]); page = 0; details = false;
            if (listedGroups.Count == 1) picker.SelectFromMenu(picker.Representative(listedGroups[0]));
            else { menuVisible = true; PlacePanel(); RebuildMenu(); }
            if (SfxManager.I != null) SfxManager.I.Click();
        }
        return true;
    }
}
