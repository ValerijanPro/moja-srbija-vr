using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

[DefaultExecutionOrder(-9500)]
public partial class MapExperience : MonoBehaviour
{
    RoutePicker picker;
    Camera head;
    Transform rig;
    readonly Dictionary<Behaviour, bool> suspended = new Dictionary<Behaviour, bool>();
    InputAction leftStick, leftGrip, rightGrip, buttonX, buttonY, buttonA, buttonB;
    Vector3 mapCenter, mapRight, mapForward, focus;
    Quaternion mapRotation;
    float zoom = 1;
    Transform leftHand, rightHand;
    bool dragging, twoHands;
    Vector3 previousHand;
    Transform previousDraggingHand;
    float previousSpan;
    bool initialized;
    public bool IsRiding { get; private set; }
    public bool Transitioning { get; private set; }
    public bool WelcomeOpen;
    public bool BlocksMapInput => IsRiding || Transitioning || WelcomeOpen;

    public void Initialize(RoutePicker source)
    {
        picker = source;
        head = Camera.main;
        if (head == null) { Debug.LogError("[MojaSrbija] Nema glavne XR kamere."); return; }
        var origin = head.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        rig = origin != null ? origin.transform : head.transform.parent;
        foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Suspend(provider);
        foreach (var drag in FindObjectsByType<SpaceDragLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Suspend(drag);
        Suspend(GetComponent<XRGrabInteractable>());
        var start = GetComponent<MaketaStartFocus>();
        if (start != null) start.StopAllCoroutines();
        Suspend(start);
        Suspend(GetComponent<DayNight>());
        RemoveTerrainGlitter();
        // PhotoPins owns a small, curated set of recent photos and remains active.

        leftStick = Action("<XRController>{LeftHand}/thumbstick", "Vector2");
        leftGrip = Action("<XRController>{LeftHand}/grip");
        rightGrip = Action("<XRController>{RightHand}/grip");
        buttonX = Action("<XRController>{LeftHand}/primaryButton");
        buttonY = Action("<XRController>{LeftHand}/secondaryButton");
        buttonA = Action("<XRController>{RightHand}/primaryButton");
        buttonB = Action("<XRController>{RightHand}/secondaryButton");
        menu = new WorldRoutePanel("PregledRuta", 540, 1040);
        // Keep the panel in the room. A head-locked panel runs away as the user
        // turns to read it and is uncomfortable in a headset.
        menu.root.transform.SetParent(null, true);
        menu.root.transform.localScale = Vector3.one * 0.00068f;
        InitializeReplayUI();
        initialized = true;
        Recenter();
        FitDominantArea();
        OpenOverview();
        menuVisible = false;

        // dobrodoslica pri ulasku
        WelcomeOpen = true;
        var welcome = gameObject.GetComponent<WelcomeScreen>();
        if (welcome == null) welcome = gameObject.AddComponent<WelcomeScreen>();
        welcome.Begin(this, picker);
    }

    public void SetMenuVisible(bool visible) { menuVisible = visible; if (visible) { PlacePanel(); RebuildMenu(); } }

    void RemoveTerrainGlitter()
    {
        // The highly tessellated relief combined with URP/Lit specular lighting
        // produces the white dotted/triangular pattern visible at map distance.
        // Keep the satellite colour, but render it as a matte cartographic surface.
        var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        foreach (var childName in new[] { "Teren", "TerenPatch" })
        {
            var child = transform.Find(childName);
            if (child == null || !child.TryGetComponent<Renderer>(out var renderer)) continue;
            if (childName == "TerenPatch")
                child.localPosition = new Vector3(child.localPosition.x, 0.006f, child.localPosition.z);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            foreach (var material in renderer.materials)
            {
                // The main terrain has no transparent edge, so Unlit removes the
                // lighting artefact completely and keeps the source pixels stable.
                if (childName == "Teren" && unlit != null)
                {
                    Texture texture = material.mainTexture;
                    Color tint = material.color;
                    material.shader = unlit;
                    material.mainTexture = texture;
                    material.color = tint;
                }
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
                if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
                ApplySatelliteCrop(material, childName == "TerenPatch");
            }
        }
    }

    void ApplySatelliteCrop(Material material, bool patch)
    {
        // Downloaded images contain complete Web-Mercator tiles, while mesh UVs
        // describe the exact geographic bbox. Sampling the complete tile range
        // stretched/shifted imagery horizontally relative to GPS routes.
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var lons = new List<double>();
        var lats = new List<double>();
        string file = System.IO.Path.Combine(RuntimeData.Root, patch ? "routes.txt" : "border.txt");
        if (!System.IO.File.Exists(file)) return;
        string raw = System.IO.File.ReadAllText(file).Trim();
        if (patch)
        {
            foreach (string row in raw.Split('\n'))
            {
                string line = row.Trim();
                if (line.Length == 0) continue;
                string[] columns = line.Split('|');
                if (columns.Length < 2) continue;
                foreach (string point in columns[1].Split(';'))
                {
                    string[] c = point.Split(',');
                    if (c.Length >= 2) { lons.Add(double.Parse(c[0], inv)); lats.Add(double.Parse(c[1], inv)); }
                }
            }
        }
        else foreach (string point in raw.Split(';'))
        {
            string[] c = point.Split(',');
            if (c.Length >= 2) { lons.Add(double.Parse(c[0], inv)); lats.Add(double.Parse(c[1], inv)); }
        }
        if (lons.Count == 0) return;
        lons.Sort(); lats.Sort();
        double lo0, lo1;
        int zoom;
        if (patch)
        {
            double Q(List<double> values, double t) => values[(int)(t * (values.Count - 1))];
            lo0 = Q(lons, 0.05) - 0.05; lo1 = Q(lons, 0.95) + 0.05;
            double mid = (lo0 + lo1) * 0.5;
            if (lo1 - lo0 > 0.9) { lo0 = mid - 0.45; lo1 = mid + 0.45; }
            zoom = 13;
        }
        else { lo0 = lons[0] - 0.02; lo1 = lons[lons.Count - 1] + 0.02; zoom = 11; }
        double n = 1 << zoom;
        double tx0 = (lo0 + 180.0) / 360.0 * n;
        double tx1 = (lo1 + 180.0) / 360.0 * n;
        int tile0 = (int)tx0, tile1 = (int)tx1;
        float tileCount = tile1 - tile0 + 1;
        material.mainTextureScale = new Vector2((float)((tx1 - tx0) / tileCount), 1f);
        material.mainTextureOffset = new Vector2((float)((tx0 - tile0) / tileCount), 0f);
    }

    static InputAction Action(string binding, string expected = "Axis")
    {
        var action = new InputAction(type: InputActionType.Value, binding: binding, expectedControlType: expected);
        action.Enable(); return action;
    }

    void Suspend(Behaviour component)
    {
        if (component == null || suspended.ContainsKey(component)) return;
        suspended[component] = component.enabled;
        component.enabled = false;
    }

    void Recenter()
    {
        mapForward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        if (mapForward.sqrMagnitude < 0.1f) mapForward = Vector3.forward;
        mapRight = Vector3.Cross(Vector3.up, mapForward);
        mapCenter = head.transform.position + mapForward * 1.4f;
        mapRotation = Quaternion.LookRotation(Vector3.up, -mapForward);
        ApplyMapPose();
        PlacePanel();
    }

    void PlacePanel()
    {
        if (menu == null || head == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        // Slightly to the right, but closer than the map, so terrain cannot hide it.
        menu.root.transform.SetPositionAndRotation(
            head.transform.position + forward * 0.82f + right * 0.30f - Vector3.up * 0.08f,
            Quaternion.LookRotation(forward));
        menu.root.transform.localScale = Vector3.one * 0.00062f;
    }

    void FitDominantArea()
    {
        // Find the densest cluster of route starting points, then frame only the
        // nearby geometry. One remote trip can no longer drag the opening view
        // across the whole country.
        var starts = new List<Vector3>();
        for (int g = 0; g < picker.GroupCount; g++)
        {
            var p = picker.Points(picker.Representative(g));
            if (p != null && p.Length > 0) starts.Add(p[0]);
        }
        if (starts.Count == 0) { FitRoutes(null); return; }
        float clusterRadius = ProjSrbija.UnitsPerMeter * 30000f;
        int bestCount = -1; Vector3 anchor = starts[0];
        foreach (var candidate in starts)
        {
            int count = 0;
            foreach (var other in starts)
                if (new Vector2(candidate.x - other.x, candidate.z - other.z).sqrMagnitude <= clusterRadius * clusterRadius) count++;
            if (count > bestCount) { bestCount = count; anchor = candidate; }
        }
        float viewRadius = ProjSrbija.UnitsPerMeter * 35000f;
        var min = new Vector3(float.MaxValue, 0, float.MaxValue);
        var max = new Vector3(float.MinValue, 0, float.MinValue);
        for (int i = 0; i < picker.RouteCount; i++)
        {
            var points = picker.Points(i); if (points == null) continue;
            foreach (var p in points)
                if (new Vector2(p.x - anchor.x, p.z - anchor.z).sqrMagnitude <= viewRadius * viewRadius)
                { min = Vector3.Min(min, new Vector3(p.x, 0, p.z)); max = Vector3.Max(max, new Vector3(p.x, 0, p.z)); }
        }
        if (min.x == float.MaxValue) { FitRoutes(null); return; }
        focus = (min + max) * 0.5f;
        zoom = Mathf.Clamp(0.9f / Mathf.Max(max.x - min.x, max.z - min.z, 0.001f), 0.5f, 600);
        ApplyMapPose();
    }

    void ApplyMapPose()
    {
        transform.rotation = mapRotation;
        // Keep terrain depth shallow even at high zoom, so relief cannot reach the viewer.
        transform.localScale = new Vector3(zoom, Mathf.Min(zoom, 3f), zoom);
        transform.position += mapCenter - picker.ruteParent.TransformPoint(focus);
        Physics.SyncTransforms();
        clusterTimer = 0;
    }

    void FitRoutes(IReadOnlyList<int> routes)
    {
        var min = new Vector3(float.MaxValue, 0, float.MaxValue);
        var max = new Vector3(float.MinValue, 0, float.MinValue);
        int count = routes == null ? picker.RouteCount : routes.Count;
        for (int j = 0; j < count; j++)
        {
            var points = picker.Points(routes == null ? j : routes[j]);
            if (points == null) continue;
            foreach (var p in points) { min = Vector3.Min(min, new Vector3(p.x, 0, p.z)); max = Vector3.Max(max, new Vector3(p.x, 0, p.z)); }
        }
        if (min.x == float.MaxValue) return;
        focus = (min + max) * 0.5f;
        zoom = Mathf.Clamp(1.0f / Mathf.Max(max.x - min.x, max.z - min.z, 0.001f), 0.5f, 600);
        ApplyMapPose();
    }

    void Update()
    {
        if (!initialized || Transitioning || WelcomeOpen) return;
        if (IsRiding) { UpdateRide(); return; }
        if (buttonX.WasPressedThisFrame()) StartSelectedRide();
        if (buttonY.WasPressedThisFrame()) { Recenter(); FitDominantArea(); OpenOverview(); }
        if (BlocksMapInput) return;
        if (leftHand == null) leftHand = GameObject.Find("Left Controller")?.transform;
        if (rightHand == null) rightHand = GameObject.Find("Right Controller")?.transform;
        bool l = leftHand != null && leftGrip.ReadValue<float>() > 0.6f;
        bool r = rightHand != null && rightGrip.ReadValue<float>() > 0.6f;
        if (picker.NavigationReserved) { dragging = twoHands = false; return; }
        Vector2 stick = leftStick.ReadValue<Vector2>();
        if (Mathf.Abs(stick.y) > 0.2f)
        {
            if (picker.TryAimOnMap(out var aim)) focus = picker.ruteParent.InverseTransformPoint(aim);
            // Hold the pointed location fixed on the map while changing magnification.
            Vector3 anchor = picker.ruteParent.TransformPoint(focus);
            zoom = Mathf.Clamp(zoom * Mathf.Exp(stick.y * Time.deltaTime), 0.5f, 600);
            transform.localScale = new Vector3(zoom, Mathf.Min(zoom, 3f), zoom);
            transform.position += anchor - picker.ruteParent.TransformPoint(focus);
            mapCenter = picker.ruteParent.TransformPoint(focus);
            Physics.SyncTransforms();
            clusterTimer = 0;
        }
        if (l && r)
        {
            float span = Vector3.Distance(leftHand.position, rightHand.position);
            if (twoHands && previousSpan > 0.03f) { zoom = Mathf.Clamp(zoom * span / previousSpan, 0.5f, 600); ApplyMapPose(); }
            previousSpan = span; twoHands = true; dragging = false;
        }
        else if (l || r)
        {
            Transform handTransform = l ? leftHand : rightHand;
            Vector3 hand = handTransform.position;
            if (dragging && !twoHands && previousDraggingHand == handTransform)
            {
                Vector3 delta = Vector3.ProjectOnPlane(hand - previousHand, mapForward);
                mapCenter += delta;
                ApplyMapPose();
            }
            previousHand = hand; previousDraggingHand = handTransform; dragging = true; twoHands = false;
        }
        else dragging = twoHands = false;
    }

    void LateUpdate()
    {
        if (!initialized) return;
        // XRI action managers must not re-enable free locomotion or free map rotation.
        foreach (var entry in suspended) if (entry.Key != null) entry.Key.enabled = false;
        if (picker.panel != null) picker.panel.SetActive(false);
        if (!IsRiding && !Transitioning && !WelcomeOpen) UpdateOverview();
        else { SetMarkersVisible(false); menu.root.SetActive(false); }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        RestoreAfterRide();
        Transitioning = false;
        foreach (var entry in suspended) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var action in new[] { leftStick, leftGrip, rightGrip, buttonX, buttonY, buttonA, buttonB }) action?.Disable();
        if (menu != null) menu.root.SetActive(false);
        SetMarkersVisible(false);
    }

    void OnEnable()
    {
        if (!initialized) return;
        foreach (var entry in suspended) if (entry.Key != null) entry.Key.enabled = false;
        foreach (var action in new[] { leftStick, leftGrip, rightGrip, buttonX, buttonY, buttonA, buttonB }) action?.Enable();
        clusterTimer = 0;
    }

    void OnDestroy()
    {
        GalleryCleanup();
        foreach (var action in new[] { leftStick, leftGrip, rightGrip, buttonX, buttonY, buttonA, buttonB }) action?.Dispose();
        menu?.Dispose();
        if (markerRoot != null) Destroy(markerRoot);
        DisposeReplayUI();
    }
}
