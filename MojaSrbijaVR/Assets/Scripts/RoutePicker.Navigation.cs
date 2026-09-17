using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public partial class RoutePicker
{
    bool navigationActive, stickArmed, awaitingNeutral;
    Vector3 navigationAnchor;
    InputAction navigationStick;
    float navigationTimer;
    readonly int[] directionRoutes = { -1, -1, -1, -1 };
    readonly Vector3[] directionPoints = new Vector3[4];
    readonly List<Behaviour> turnProviders = new List<Behaviour>();
    readonly HashSet<Behaviour> pausedTurns = new HashSet<Behaviour>();
    readonly TextMesh[] directionLabels = new TextMesh[4];
    readonly LineRenderer[] directionThreads = new LineRenderer[4];
    readonly string[] directionSymbols = { "\u2191", "\u2192", "\u2193", "\u2190" };
    GameObject navigationVisuals;
    Material navigationMaterial;

    // Also stays true after trigger release until the stick returns to neutral,
    // preventing an accidental snap turn / time change on that release frame.
    public bool RightStickReserved => navigationActive || awaitingNeutral;
    public bool NavigationReserved => RightStickReserved;
    Vector2 ReadNavigationStick() => (activeLeft ? leftNavigationStick : navigationStick).ReadValue<Vector2>();

    public void CancelNavigation()
    {
        navigationActive = false;
        if (navigationVisuals != null) navigationVisuals.SetActive(false);
    }

    void InitializeNavigation()
    {
        navigationStick = new InputAction(type: InputActionType.Value,
            binding: "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        navigationStick.Enable();
        turnProviders.AddRange(FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        turnProviders.AddRange(FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    void BeginNavigation(Vector3 worldPoint)
    {
        navigationAnchor = ruteParent.InverseTransformPoint(worldPoint);
        navigationActive = true;
        stickArmed = ReadNavigationStick().sqrMagnitude < 0.09f;
        navigationTimer = 0;
    }

    void UpdateNavigation()
    {
        Vector2 stick = ReadNavigationStick();
        bool neutral = stick.sqrMagnitude < 0.09f;
        if (neutral) { stickArmed = true; awaitingNeutral = false; }
        if (navigationActive && !neutral) awaitingNeutral = true;
        SetTurnCapture(RightStickReserved);

        if (navigationActive && Camera.main != null)
        {
            navigationTimer -= Time.deltaTime;
            if (navigationTimer <= 0)
            {
                RefreshDirections();
                navigationTimer = 0.1f;
            }
            if (stickArmed && Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y)) >= 0.65f)
            {
                stickArmed = false;
                int direction = Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                    ? (stick.x > 0 ? 1 : 3) : (stick.y > 0 ? 0 : 2);
                // Refresh on input as well: the map or the user's head may have moved.
                RefreshDirections();
                int route = directionRoutes[direction];
                if (route >= 0)
                {
                    Vector3 point = directionPoints[direction];
                    Select(route);
                    navigationAnchor = point;
                    if (SfxManager.I != null) SfxManager.I.Click();
                    RefreshDirections();
                    hoverTimer = 1;
                }
            }
            DrawDirections();
        }
        else if (navigationVisuals != null) navigationVisuals.SetActive(false);
    }

    void SetTurnCapture(bool capture)
    {
        if (capture)
        {
            foreach (var provider in turnProviders)
                if (provider != null && provider.enabled)
                {
                    pausedTurns.Add(provider);
                    provider.enabled = false;
                }
        }
        else
        {
            foreach (var provider in pausedTurns)
                if (provider != null) provider.enabled = true;
            pausedTurns.Clear();
        }
    }

    RouteGeometry.Point[][] viewPoints;
    void RefreshDirections()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 origin = ruteParent.TransformPoint(navigationAnchor);
        Vector3 right = cam.transform.right, up = cam.transform.up;
        var best = new double[] { double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity };
        for (int d = 0; d < 4; d++) directionRoutes[d] = -1;
        if (viewPoints == null) viewPoints = new RouteGeometry.Point[routeLines.Length][];
        int excludedGroup = selectedIndex >= 0 ? groupForRoute[selectedIndex] : -1;
        for (int g = 0; g < routeGroups.Count; g++)
        {
            if (g == excludedGroup) continue;
            int i = displayedMember[g];
            if (!IsPickable(i)) continue;
            var points = routePoints[i];
            if (viewPoints[i] == null) viewPoints[i] = new RouteGeometry.Point[points.Length];
            for (int k = 0; k < points.Length; k++)
            {
                Vector3 delta = ruteParent.TransformPoint(points[k]) - origin;
                viewPoints[i][k] = new RouteGeometry.Point(Vector3.Dot(delta, right), Vector3.Dot(delta, up));
            }
            // A tiny view-space dead zone keeps a crossing at the anchor from winning
            // every direction. It is measured in real world metres, not map scale.
            for (int d = 0; d < 4; d++)
            {
                double distance = RouteGeometry.InDirection(viewPoints[i], d, 0.003, out int segment, out double t);
                if (segment < 0 || distance >= best[d]) continue;
                Vector3 point = Vector3.Lerp(points[segment], points[segment + 1], (float)t);
                Vector3 world = ruteParent.TransformPoint(point);
                if (Vector3.Dot(world - cam.transform.position, cam.transform.forward) <= 0) continue;
                best[d] = distance;
                directionRoutes[d] = i;
                directionPoints[d] = point;
            }
        }
    }

    void EnsureDirectionVisuals()
    {
        if (navigationVisuals != null) return;
        navigationVisuals = new GameObject("SmeroviRuta");
        var shader = Shader.Find("MojaSrbija/Additive") ?? Shader.Find("Sprites/Default");
        navigationMaterial = new Material(shader) { color = new Color(0.15f, 0.6f, 0.8f, 0.45f) };
        for (int d = 0; d < 4; d++)
        {
            var go = new GameObject("Smer_" + d);
            go.transform.SetParent(navigationVisuals.transform, false);
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 48;
            text.characterSize = 0.004f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            directionLabels[d] = text;
            var thread = new GameObject("Nit_" + d).AddComponent<LineRenderer>();
            thread.transform.SetParent(navigationVisuals.transform, false);
            thread.useWorldSpace = true;
            thread.positionCount = 2;
            thread.startWidth = thread.endWidth = 0.0008f;
            thread.sharedMaterial = navigationMaterial;
            thread.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            directionThreads[d] = thread;
        }
    }

    void DrawDirections()
    {
        EnsureDirectionVisuals();
        navigationVisuals.SetActive(true);
        var cam = Camera.main;
        Vector3 center = ruteParent.TransformPoint(navigationAnchor);
        float distance = Vector3.Distance(cam.transform.position, center);
        float radius = Mathf.Clamp(distance * 0.09f, 0.065f, 0.5f);
        Vector3 right = cam.transform.right, up = cam.transform.up;
        for (int d = 0; d < 4; d++)
        {
            int route = directionRoutes[d];
            string name = route < 0 ? "Nema rute" : RouteTitle(route);
            if (name.Length > 25) name = name.Substring(0, 22) + "...";
            var text = directionLabels[d];
            text.text = directionSymbols[d];
            text.color = route < 0 ? new Color(0.5f, 0.53f, 0.55f) : new Color(0.4f, 0.95f, 1f);
            Vector3 offset = d == 0 ? up : d == 1 ? right : d == 2 ? -up : -right;
            text.transform.position = center + offset * radius - cam.transform.forward * 0.015f;
            text.transform.rotation = cam.transform.rotation;
            text.transform.localScale = Vector3.one * Mathf.Clamp(distance, 0.65f, 5f);
            var thread = directionThreads[d];
            thread.enabled = false;
        }
    }

    void OnEnable()
    {
        trigger?.Enable(); btnA?.Enable(); btnB?.Enable(); navigationStick?.Enable();
        leftTrigger?.Enable(); leftNavigationStick?.Enable();
        if (Experience != null) Experience.enabled = true;
        if (displayedMember != null)
            for (int g = 0; g < routeGroups.Count; g++)
                foreach (int i in routeGroups[g]) routeLines[i].enabled = i == displayedMember[g];
    }

    void OnDisable()
    {
        navigationActive = awaitingNeutral = wasPressed = false;
        leftWasPressed = false;
        if (Experience != null) Experience.enabled = false;
        SetTurnCapture(false);
        trigger?.Disable(); btnA?.Disable(); btnB?.Disable(); navigationStick?.Disable();
        leftTrigger?.Disable(); leftNavigationStick?.Disable();
        if (keeper != null) Deselect();
        RestoreRouteVisibility();
        if (navigationVisuals != null) navigationVisuals.SetActive(false);
        if (reticle != null) reticle.gameObject.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
        if (laser != null) laser.enabled = false;
        if (leftLaser != null) leftLaser.enabled = false;
        if (tether != null) tether.enabled = false;
    }

    void OnDestroy()
    {
        trigger?.Dispose(); btnA?.Dispose(); btnB?.Dispose(); navigationStick?.Dispose();
        leftTrigger?.Dispose(); leftNavigationStick?.Dispose();
        Destroy(navigationVisuals);
        Destroy(navigationMaterial);
        if (reticle != null) { Destroy(reticle.GetComponent<Renderer>().sharedMaterial); Destroy(reticle.gameObject); }
        if (laser != null) { Destroy(laser.sharedMaterial); Destroy(laser.gameObject); }
        if (leftLaser != null) { Destroy(leftLaser.sharedMaterial); Destroy(leftLaser.gameObject); }
        if (tether != null) { Destroy(tether.sharedMaterial); Destroy(tether.gameObject); }
        if (label != null) Destroy(label.gameObject);
    }
}
