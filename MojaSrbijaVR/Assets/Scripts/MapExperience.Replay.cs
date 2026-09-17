using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class MapExperience
{
    Vector3[] ridePoints;
    double[] rideDistances;
    double rideDistance;
    float rideSpeed = 5, speedMultiplier = 1;
    bool paused;
    Vector3 savedMapPosition, savedMapScale, savedRigPosition, rideOrigin;
    Quaternion savedMapRotation, savedRigRotation;
    bool snapshot;
    float originalFarClip, headingOffset;
    GameObject floor;
    bool floorWasActive;
    GameObject fadeRoot, rideHudRoot;
    Image fadeImage;
    Text rideStatus;
    float hudTimer;
    Transform terrainPatch;
    Vector3 savedPatchPosition;
    bool patchAdjusted;
    MeshCollider addedPatchCollider;
    Color savedBackground;

    void InitializeReplayUI()
    {
        fadeRoot = new GameObject("PrelazRezim", typeof(RectTransform), typeof(Canvas));
        fadeRoot.transform.SetParent(head.transform, false);
        fadeRoot.transform.localPosition = new Vector3(0, 0, Mathf.Max(head.nearClipPlane + 0.02f, 0.12f));
        fadeRoot.transform.localScale = Vector3.one * 0.001f;
        fadeRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(3000, 3000);
        fadeRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        fadeRoot.GetComponent<Canvas>().sortingOrder = 100;
        fadeImage = fadeRoot.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeRoot.SetActive(false);
        rideHudRoot = new GameObject("VoznjaStatus", typeof(RectTransform), typeof(Canvas));
        rideHudRoot.transform.SetParent(head.transform, false);
        rideHudRoot.transform.localPosition = new Vector3(0, -0.25f, 0.8f);
        rideHudRoot.transform.localScale = Vector3.one * 0.001f;
        rideHudRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(650, 130);
        rideHudRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        rideStatus = rideHudRoot.AddComponent<Text>();
        rideStatus.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rideStatus.fontSize = 22; rideStatus.alignment = TextAnchor.MiddleCenter; rideStatus.color = Color.white;
        rideHudRoot.SetActive(false);
    }

    void StartSelectedRide()
    {
        if (picker.SelectedIndex < 0 || Transitioning || IsRiding) return;
        if (rig == null) { notice = "XR rig nije dostupan."; RebuildMenu(); return; }
        StartCoroutine(EnterRide());
    }

    IEnumerator Fade(float from, float to)
    {
        fadeRoot.SetActive(true);
        float elapsed = 0;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / 0.25f)));
            yield return null;
        }
        if (to == 0) fadeRoot.SetActive(false);
    }

    IEnumerator EnterRide()
    {
        Transitioning = true;
        picker.CancelNavigation();
        StopVideo();
        yield return Fade(0, 1);
        Vector3[] source = picker.Points(picker.SelectedIndex);
        float units = ProjSrbija.UnitsPerMeter;
        var metres = new RouteGeometry.Point[source.Length];
        var local = new Vector3[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            local[i] = transform.InverseTransformPoint(picker.ruteParent.TransformPoint(source[i]));
            metres[i] = new RouteGeometry.Point(local[i].x / units, local[i].z / units);
        }
        var originalDistances = RouteReplayMath.Distances(metres);
        double total = originalDistances.Length > 0 ? originalDistances[originalDistances.Length - 1] : 0;
        if (total < 1)
        {
            notice = "Ova ruta nema dovoljno tačaka za vožnju.";
            yield return Fade(1, 0); Transitioning = false; RebuildMenu(); yield break;
        }
        // Sample the actual mesh, removing both the exaggerated relief and the
        // decorative altitude offset baked into route LineRenderers.
        terrainPatch = transform.Find("TerenPatch");
        MeshCollider patchCollider = null;
        if (terrainPatch != null && terrainPatch.TryGetComponent<MeshFilter>(out var patchMesh))
        {
            savedPatchPosition = terrainPatch.localPosition;
            terrainPatch.localPosition = new Vector3(savedPatchPosition.x, 0, savedPatchPosition.z);
            patchAdjusted = true;
            patchCollider = terrainPatch.GetComponent<MeshCollider>();
            if (patchCollider == null)
            {
                addedPatchCollider = terrainPatch.gameObject.AddComponent<MeshCollider>();
                addedPatchCollider.sharedMesh = patchMesh.sharedMesh;
                patchCollider = addedPatchCollider;
            }
        }
        int samples = Mathf.Clamp(Mathf.CeilToInt((float)total / 20) + 1, 2, 10000);
        ridePoints = new Vector3[samples]; rideDistances = new double[samples];
        Physics.SyncTransforms();
        int missing = 0;
        for (int i = 0; i < samples; i++)
        {
            double distance = total * i / (samples - 1);
            RouteReplayMath.Sample(originalDistances, distance, out int segment, out double t);
            Vector3 p = Vector3.Lerp(local[segment], local[segment + 1], (float)t);
            var ray = new Ray(transform.TransformPoint(new Vector3(p.x, 0.5f, p.z)), -transform.up);
            float rayLength = transform.TransformVector(Vector3.up).magnitude;
            bool found = picker.board.Raycast(ray, out var hit, rayLength);
            if (patchCollider != null && patchCollider.Raycast(ray, out var patchHit, rayLength) &&
                (!found || patchHit.distance < hit.distance)) { hit = patchHit; found = true; }
            if (found) p.y = transform.InverseTransformPoint(hit.point).y;
            else missing++;
            ridePoints[i] = p; rideDistances[i] = distance;
            if (i % 128 == 0) yield return null;
        }
        if (missing > 0)
        {
            RestorePatch();
            notice = "Deo rute je van dostupnog terena. Izaberi drugu rutu.";
            yield return Fade(1, 0); Transitioning = false; RebuildMenu(); yield break;
        }
        savedMapPosition = transform.position; savedMapRotation = transform.rotation; savedMapScale = transform.localScale;
        savedRigPosition = rig.position; savedRigRotation = rig.rotation;
        originalFarClip = head.farClipPlane;
        savedBackground = head.backgroundColor;
        snapshot = true;
        floor = GameObject.Find("Pod"); floorWasActive = floor != null && floor.activeSelf;
        if (floor != null) floor.SetActive(false);
        Vector3 flatView = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        if (flatView.sqrMagnitude < 0.1f) flatView = Vector3.forward;
        // Frame this route at roughly the same useful magnification as the map
        // detail view. The old fixed 1:500 scale enlarged a regional raster by
        // 20x or more for city routes, which produced the giant blurred pixels.
        Vector3 routeMin = ridePoints[0], routeMax = ridePoints[0];
        foreach (var point in ridePoints)
        {
            routeMin = Vector3.Min(routeMin, point);
            routeMax = Vector3.Max(routeMax, point);
        }
        float routeSpan = Mathf.Max(routeMax.x - routeMin.x, routeMax.z - routeMin.z, 0.001f);
        float replayZoom = Mathf.Clamp(0.95f / routeSpan, 2f, 180f);
        rideOrigin = head.transform.position + flatView * 1.05f - Vector3.up * 0.72f;
        headingOffset = Mathf.DeltaAngle(head.transform.eulerAngles.y, rig.eulerAngles.y);
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(replayZoom, Mathf.Min(replayZoom, 3f), replayZoom);
        head.farClipPlane = 20000;
        head.backgroundColor = new Color(0.38f, 0.52f, 0.65f);
        rideDistance = 0; paused = true; speedMultiplier = 1;
        rideSpeed = picker.SelectedSpeedMetersPerSecond;
        IsRiding = true;
        picker.SetRideVisibility(true);
        if (MiniRunner.I != null) MiniRunner.I.Stop();
        if (SfxManager.I != null) SfxManager.I.SetRiding(true);
        UpdateRidePose(true);
        rideHudRoot.SetActive(true);
        UpdateRideStatus();
        yield return Fade(1, 0);
        Transitioning = false;
    }

    void UpdateRide()
    {
        if (buttonB.WasPressedThisFrame() || buttonY.WasPressedThisFrame()) { StartCoroutine(ExitRide()); return; }
        if (buttonA.WasPressedThisFrame() || buttonX.WasPressedThisFrame())
        {
            if (rideDistance >= rideDistances[rideDistances.Length - 1]) rideDistance = 0;
            paused = !paused;
        }
        Vector2 stick = leftStick.ReadValue<Vector2>();
        if (Mathf.Abs(stick.y) > 0.5f) speedMultiplier = Mathf.Clamp(speedMultiplier + stick.y * Time.deltaTime, 0.25f, 4);
        double total = rideDistances[rideDistances.Length - 1];
        if (!paused) rideDistance = RouteReplayMath.Advance(rideDistance, rideSpeed * speedMultiplier, Time.deltaTime, total);
        if (rideDistance >= total) paused = true;
        UpdateRidePose(false);
        hudTimer -= Time.deltaTime;
        if (hudTimer <= 0) { UpdateRideStatus(); hudTimer = 0.2f; }
    }

    Vector3 RidePoint(double distance)
    {
        RouteReplayMath.Sample(rideDistances, distance, out int segment, out double t);
        return Vector3.Lerp(ridePoints[segment], ridePoints[segment + 1], (float)t);
    }

    void UpdateRidePose(bool instant)
    {
        Vector3 p = RidePoint(rideDistance);
        double total = rideDistances[rideDistances.Length - 1];
        Vector3 next = RidePoint(System.Math.Min(total, rideDistance + 20));
        Vector3 before = RidePoint(System.Math.Max(0, rideDistance - 2));
        Vector3 direction = next - before;
        if (direction.x * direction.x + direction.z * direction.z > 1e-14f)
        {
            float desired = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + headingOffset;
            float yaw = instant ? desired : Mathf.MoveTowardsAngle(rig.eulerAngles.y, desired, 30f * Time.deltaTime);
            rig.RotateAround(head.transform.position, Vector3.up, Mathf.DeltaAngle(rig.eulerAngles.y, yaw));
        }
        // Floating origin: keep the tracked viewer near the Unity origin while
        // translating the real-scale terrain underneath the sampled route position.
        transform.position = rideOrigin - transform.TransformVector(p);
    }

    void UpdateRideStatus()
    {
        rideStatus.text = $"{(paused ? "PAUZA" : "PREGLED TRASE")}  |  {rideDistance / 1000:0.00} / {rideDistances[rideDistances.Length - 1] / 1000:0.00} km  |  {speedMultiplier:0.0}x  |  mapa rute\n" +
            "A / X: vozi ili pauziraj   B / Y: nazad na mapu\nLeva palica: brzina";
    }

    IEnumerator ExitRide()
    {
        Transitioning = true;
        yield return Fade(0, 1);
        RestoreAfterRide();
        yield return Fade(1, 0);
        Transitioning = false;
    }

    void RestoreAfterRide()
    {
        RestorePatch();
        if (!snapshot) { if (fadeRoot != null) fadeRoot.SetActive(false); return; }
        transform.SetPositionAndRotation(savedMapPosition, savedMapRotation); transform.localScale = savedMapScale;
        if (rig != null) rig.SetPositionAndRotation(savedRigPosition, savedRigRotation);
        if (head != null) { head.farClipPlane = originalFarClip; head.backgroundColor = savedBackground; }
        if (floor != null) floor.SetActive(floorWasActive);
        IsRiding = false; snapshot = false;
        picker.SetRideVisibility(false);
        if (SfxManager.I != null) SfxManager.I.SetRiding(false);
        if (rideHudRoot != null) rideHudRoot.SetActive(false);
        if (fadeRoot != null) fadeRoot.SetActive(false);
        clusterTimer = 0;
        RebuildMenu();
    }

    void RestorePatch()
    {
        if (patchAdjusted && terrainPatch != null) terrainPatch.localPosition = savedPatchPosition;
        patchAdjusted = false;
        if (addedPatchCollider != null) Destroy(addedPatchCollider);
        addedPatchCollider = null;
    }

    void DisposeReplayUI()
    {
        if (fadeRoot != null) Destroy(fadeRoot);
        if (rideHudRoot != null) Destroy(rideHudRoot);
    }
}
