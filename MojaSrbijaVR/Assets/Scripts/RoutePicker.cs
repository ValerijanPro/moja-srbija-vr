// Trigger bira rutu; drzan trigger + desna palica bira susednu u smeru pogleda.
// Slicne putanje su jedna grupa; A/B lista aktivnosti iz izabrane grupe.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public partial class RoutePicker : MonoBehaviour
{
    public Transform ruteParent;
    public Collider board;               // teren (mesh collider)
    public Text titleText;
    public Text statsText;
    public GameObject panel;
    public RawImage img1;
    public RawImage img2;
    public float snapWorldRadius = 0.10f;    // "laso" radijus hvatanja rute (m sveta)

    Transform rayOrigin;
    InputAction trigger;
    Transform leftAim, rightAim;
    InputAction leftTrigger, leftNavigationStick;
    LineRenderer leftLaser;
    bool activeLeft, leftWasPressed, ready;
    public MapExperience Experience { get; private set; }
    string[][] meta;
    readonly Dictionary<string, string[]> photos = new Dictionary<string, string[]>();
    LineRenderer selected;
    Material selectedMat;
    RouteWidthKeeper keeper;
    Transform reticle;
    LineRenderer laser;
    bool wasPressed;
    float hoverTimer;
    Coroutine photoCo;
    readonly List<Texture2D> loadedPhotos = new List<Texture2D>();

    // hover stanje
    LineRenderer hoverLR;
    Vector3 hoverPointLocal;
    readonly List<(int idx, float dWorld, Vector3 pt)> hoverCands = new List<(int, float, Vector3)>();

    IEnumerator Start()
    {
        if (ruteParent == null || board == null)
        {
            Debug.LogError("[MojaSrbija] RoutePicker zahteva Rute i collider terena.");
            enabled = false;
            yield break;
        }
        yield return RuntimeData.Prepare();
        if (!RuntimeData.Ready) { enabled = false; yield break; }
        meta = LoadTsv("routes_meta.txt");
        BuildGroups();
        foreach (var row in LoadTsv("photos.txt"))
        {
            if (row.Length < 2) continue;
            var urls = new string[row.Length - 1];
            System.Array.Copy(row, 1, urls, 0, urls.Length);
            photos[row[0]] = urls;
        }
        trigger = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/trigger");
        trigger.Enable();
        leftTrigger = new InputAction(type: InputActionType.Value, binding: "<XRController>{LeftHand}/trigger");
        leftNavigationStick = new InputAction(type: InputActionType.Value, binding: "<XRController>{LeftHand}/thumbstick", expectedControlType: "Vector2");
        leftTrigger.Enable(); leftNavigationStick.Enable();
        btnA = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/primaryButton");
        btnB = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/secondaryButton");
        btnA.Enable(); btnB.Enable();
        InitializeNavigation();
        if (panel != null) panel.SetActive(false);

        keeper = GetComponent<RouteWidthKeeper>();
        if (keeper == null) keeper = gameObject.AddComponent<RouteWidthKeeper>();
        var granica = transform.Find("Granica");
        keeper.InitFrom(ruteParent, granica != null ? granica.GetComponent<LineRenderer>() : null);

        var sh = Shader.Find("MojaSrbija/Additive");
        var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        r.name = "Nisan";
        Object.Destroy(r.GetComponent<Collider>());
        r.GetComponent<Renderer>().material = new Material(sh != null ? sh : Shader.Find("Sprites/Default"))
        { color = new Color(0.4f, 0.9f, 1f) };
        reticle = r.transform;
        reticle.gameObject.SetActive(false);

        var lgo = new GameObject("Laser");
        laser = lgo.AddComponent<LineRenderer>();
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.startWidth = 0.0025f;
        laser.endWidth = 0.001f;
        laser.material = new Material(sh != null ? sh : Shader.Find("Sprites/Default"))
        { color = new Color(0.25f, 0.7f, 0.9f) };
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.enabled = false;
        leftLaser = Instantiate(laser);
        leftLaser.name = "LaserLevi";
        leftLaser.sharedMaterial = new Material(laser.sharedMaterial) { color = new Color(0.4f, 0.85f, 1) };

        // "laso" nit: od kursora do rute koju bi klik uzeo
        var tgo = new GameObject("Laso");
        tether = tgo.AddComponent<LineRenderer>();
        tether.useWorldSpace = true;
        tether.positionCount = 2;
        tether.startWidth = 0.0015f;
        tether.endWidth = 0.003f;
        tether.material = new Material(sh != null ? sh : Shader.Find("Sprites/Default"))
        { color = new Color(0.3f, 0.95f, 1f) };
        tether.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tether.enabled = false;
        ready = true;
        Experience = GetComponent<MapExperience>() ?? gameObject.AddComponent<MapExperience>();
        Experience.Initialize(this);
    }

    LineRenderer tether;
    readonly List<Transform> handCache = new List<Transform>();
    InputAction btnA, btnB;
    int hoverSel;
    TextMesh label;
    bool behavioursKilled;

    void EnsureLabel()
    {
        if (label != null) return;
        var lgo2 = new GameObject("KandidatLabela");
        label = lgo2.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().material = label.font.material;
        label.fontSize = 48;
        label.characterSize = 0.01f;
        label.anchor = TextAnchor.LowerCenter;
        label.color = new Color(0.75f, 0.95f, 1f);
        lgo2.SetActive(false);
    }

    string[][] LoadTsv(string file)
    {
        var path = System.IO.Path.Combine(RuntimeData.Root, file);
        if (!System.IO.File.Exists(path)) return new string[0][];
        var lines = System.IO.File.ReadAllText(path).Trim().Split('\n');
        var res = new string[lines.Length][];
        for (int i = 0; i < lines.Length; i++) res[i] = lines[i].Trim().Split('\t');
        return res;
    }

    static Transform FindDeep(Transform t, string needle)
    {
        if (t.name.Contains(needle)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindDeep(t.GetChild(i), needle);
            if (f != null) return f;
        }
        return null;
    }

    void SuppressXRIBeams()
    {
        if (handCache.Count < 2)
        {
            foreach (var handName in new[] { "Left Controller", "Right Controller" })
            {
                var h = GameObject.Find(handName);
                if (h != null && !handCache.Contains(h.transform)) handCache.Add(h.transform);
            }
        }
        foreach (var h in handCache)
        {
            if (!behavioursKilled)
                foreach (var mb in h.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name.Contains("Visual"))
                        mb.enabled = false;   // CurveVisualController i slicni ne smeju vise da crtaju
            foreach (var lrX in h.GetComponentsInChildren<LineRenderer>(true))
                if (lrX.enabled) lrX.enabled = false;
        }
        if (handCache.Count == 2) behavioursKilled = true;
    }

    void LateUpdate()
    {
        SuppressXRIBeams();   // POSLE XRI-jevog crtanja, da ne prezive frejm
        if (ready)
        {
            RefreshVisibleRoutes();
            UpdateSelectedPulse();
        }
    }

    void UpdateSelectedPulse()
    {
        if (selectionMaterial == null || selected == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.5f);
        Color colour = Color.Lerp(
            new Color(1f, 0.46f, 0.015f, 1f),
            new Color(1f, 1f, 0.18f, 1f), pulse);
        selectionMaterial.color = colour;
        if (selectionMaterial.HasProperty("_BaseColor"))
            selectionMaterial.SetColor("_BaseColor", colour);
    }

    void Update()
    {
        if (!ready) return;
        if (leftAim == null) leftAim = ControllerAim("Left Controller");
        if (rightAim == null) rightAim = ControllerAim("Right Controller");
        bool rightPressed = trigger.ReadValue<float>() > 0.7f;
        bool leftPressed = leftTrigger.ReadValue<float>() > 0.7f;
        bool rightClick = rightPressed && !wasPressed;
        bool leftClick = leftPressed && !leftWasPressed;
        wasPressed = rightPressed; leftWasPressed = leftPressed;
        if (Experience != null && Experience.BlocksMapInput)
        {
            CancelNavigation();
            laser.enabled = leftLaser.enabled = false;
            reticle.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
            return;
        }
        if (leftClick) activeLeft = true;
        if (rightClick) activeLeft = false;
        rayOrigin = activeLeft ? leftAim : rightAim;
        if (rayOrigin == null) rayOrigin = rightAim != null ? rightAim : leftAim;
        if (rayOrigin == null) return;

        bool aiming = RayToBoard(out Vector3 hitPoint, out float hitDist);
        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
        bool menuHit = Experience != null && Experience.HitMenu(ray, out _);
        bool click = activeLeft ? leftClick : rightClick;
        if (click)
        {
            if (menuHit) { CancelNavigation(); Experience.HitMenu(ray, out _, true); }
            else if (aiming) { UpdateHover(hitPoint); Pick(hitPoint); BeginNavigation(hitPoint); }
        }
        if (!(activeLeft ? leftPressed : rightPressed)) navigationActive = false;
        UpdateNavigation();
        if (navigationActive)
        {
            hitPoint = ruteParent.TransformPoint(navigationAnchor);
            hitDist = Vector3.Distance(rayOrigin.position, hitPoint);
            aiming = true;
        }

        RenderPointer(rightAim, laser, !activeLeft);
        RenderPointer(leftAim, leftLaser, activeLeft);
        int step = btnA.WasPressedThisFrame() ? 1 : btnB.WasPressedThisFrame() ? -1 : 0;
        if (step != 0) CycleActivity(step);
        hoverTimer += Time.deltaTime;
        if (aiming && !menuHit && hoverTimer > 0.1f)
        { hoverTimer = 0; UpdateHover(hitPoint); }
        else if (!aiming || menuHit) { hoverLR = null; keeper.hovered = null; }
        reticle.gameObject.SetActive(aiming && !menuHit);
        if (aiming)
        {
            reticle.position = hitPoint;
            reticle.localScale = Vector3.one * Mathf.Clamp(hitDist * 0.007f, 0.003f, 0.015f);
        }
        // Region markers and the side panel carry the labels. No long lasso or
        // floating activity names compete with them on the map.
        tether.enabled = false;
        if (label != null) label.gameObject.SetActive(false);
    }

    static Transform ControllerAim(string name)
    {
        var controller = GameObject.Find(name);
        if (controller == null) return null;
        return FindDeep(controller.transform, "Stabilized")
            ?? FindDeep(controller.transform, "Ray Origin")
            ?? FindDeep(controller.transform, "Aim")
            ?? controller.transform;
    }

    void RenderPointer(Transform hand, LineRenderer line, bool controlsSelection)
    {
        if (hand == null) { line.enabled = false; return; }
        var ray = new Ray(hand.position, hand.forward);
        Vector3 end = ray.GetPoint(2f);
        if (controlsSelection && navigationActive) end = ruteParent.TransformPoint(navigationAnchor);
        else if (Experience != null && Experience.HitMenu(ray, out Vector3 uiPoint)) end = uiPoint;
        else if (board.Raycast(ray, out var hit, 300)) end = hit.point;
        line.enabled = true;
        line.SetPosition(0, ray.GetPoint(0.04f));
        line.SetPosition(1, end);
    }

    bool RayToBoard(out Vector3 point, out float dist)
    {
        point = default; dist = float.MaxValue;
        if (rayOrigin == null || board == null) return false;

        // 1) DODIR: vrh kontrolera blizu povrsine -> kursor je projekcija vrha
        if (board != null)
        {
            var tip = rayOrigin.position + rayOrigin.forward * 0.04f;
            var mapUp = board.transform.up;
            if (board.Raycast(new Ray(tip + mapUp * 0.3f, -mapUp), out var hitN, 1.2f))
            {
                float tipDist = Vector3.Distance(tip, hitN.point);
                if (tipDist < 0.25f)
                {
                    point = hitN.point;
                    dist = tipDist;
                    return true;
                }
            }
        }

        // 2) ZRAK: klasicno na daljinu
        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (board.Raycast(ray, out var hit, 300f))
        {
            point = hit.point;
            dist = hit.distance;
            return true;
        }
        return false;
    }

    void UpdateHover(Vector3 hitWorld)
    {
        hoverCands.Clear();
        hoverLR = null;
        Vector3 local = ruteParent.InverseTransformPoint(hitWorld);

        for (int i = 0; i < ruteParent.childCount; i++)
        {
            if (!IsPickable(i)) continue;
            RouteGeometry.Nearest(flatPoints[i], new RouteGeometry.Point(local.x, local.z), out int segment, out double t);
            if (segment < 0) continue;
            Vector3 bestPt = Vector3.Lerp(routePoints[i][segment], routePoints[i][segment + 1], (float)t);
            Vector3 surfaceDelta = new Vector3(bestPt.x - local.x, 0, bestPt.z - local.z);
            float dWorld = ruteParent.TransformVector(surfaceDelta).magnitude;
            if (dWorld < snapWorldRadius * 1.6f)
                hoverCands.Add((i, dWorld, bestPt));
        }
        hoverCands.Sort((a, b) => a.dWorld.CompareTo(b.dWorld));
        if (hoverCands.Count > 0 && hoverCands[0].dWorld > snapWorldRadius) hoverCands.Clear();
        hoverSel = Mathf.Clamp(hoverSel, 0, Mathf.Max(hoverCands.Count - 1, 0));
        ApplyHoverSel();
    }

    void ApplyHoverSel()
    {
        if (hoverCands.Count > 0)
        {
            var c = hoverCands[Mathf.Clamp(hoverSel, 0, hoverCands.Count - 1)];
            hoverLR = ruteParent.GetChild(c.idx).GetComponent<LineRenderer>();
            hoverPointLocal = c.pt;
        }
        else hoverLR = null;
        keeper.hovered = hoverLR != selected ? hoverLR : null;
    }

    void Pick(Vector3 hitWorld)
    {
        if (hoverCands.Count == 0) { Deselect(); return; }
        var chosen = hoverCands[Mathf.Clamp(hoverSel, 0, hoverCands.Count - 1)];
        if (SfxManager.I != null) SfxManager.I.Click();
        Select(chosen.idx);
    }

    void Select(int i)
    {
        Deselect();
        ShowMember(i);
        selectedIndex = i;
        var lr = routeLines[i];
        selected = lr;
        selectedMat = lr.sharedMaterial;
        selectionMaterial = new Material(selectedMat) { color = new Color(1f, 0.92f, 0.05f, 1f) };
        lr.sharedMaterial = selectionMaterial;
        keeper.highlighted = lr;
        if (MiniRunner.I != null) MiniRunner.I.Follow(lr);

        var m = Metadata(i);
        if (m.Length >= 6 && panel != null)
        {
            titleText.text = RouteTitle(i);
            string hr = m.Length > 6 && m[6] != "0" ? $" · {m[6]} bpm" : "";
            var members = routeGroups[groupForRoute[i]];
            string groupInfo = members.Count > 1 ? $"Aktivnost {members.IndexOf(i) + 1}/{members.Count} (A/B)\n" : "";
            statsText.text = groupInfo + $"{m[2]} · {m[3]} km · {m[4]} m uspona\n{m[5]} km/h prosek{hr}";
            panel.SetActive(true);

            img1.gameObject.SetActive(false);
            img2.gameObject.SetActive(false);
            if (photoCo != null) StopCoroutine(photoCo);
            if (photos.TryGetValue(m[0], out var urls))
                photoCo = StartCoroutine(LoadPhotos(urls));
        }
        Experience?.SelectionChanged();
    }

    // photos.txt: id \t P|url \t V|videoUrl|posterUrl ...
    public string[] MediaEntries(int route)
    {
        var m = Metadata(route);
        return m.Length > 0 && photos.TryGetValue(m[0], out var e) ? e : new string[0];
    }

    static string PosterOf(string entry)
    {
        var c = entry.Split('|');
        if (c.Length >= 3 && c[0] == "V") return c[2];
        if (c.Length >= 2 && c[0] == "P") return c[1];
        return c[c.Length - 1];
    }

    IEnumerator LoadPhotos(string[] entries)
    {
        var urls = new List<string>();
        foreach (var e in entries)
        {
            var u = PosterOf(e);
            if (!string.IsNullOrEmpty(u)) urls.Add(u);
            if (urls.Count == 2) break;
        }
        var targets = new[] { img1, img2 };
        for (int k = 0; k < urls.Count && k < 2; k++)
        {
            using (var req = UnityWebRequestTexture.GetTexture(urls[k]))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(req);
                    loadedPhotos.Add(texture);
                    targets[k].texture = texture;
                    targets[k].gameObject.SetActive(true);
                }
            }
        }
    }

    void Deselect()
    {
        if (selected != null)
        {
            selected.sharedMaterial = selectedMat;
            selected = null;
        }
        if (selectionMaterial != null) Destroy(selectionMaterial);
        selectionMaterial = null;
        RestoreRepresentative();
        keeper.highlighted = null;
        if (MiniRunner.I != null) MiniRunner.I.Stop();
        if (photoCo != null) { StopCoroutine(photoCo); photoCo = null; }
        foreach (var texture in loadedPhotos) if (texture != null) Destroy(texture);
        loadedPhotos.Clear();
        if (img1 != null) { img1.texture = null; img1.gameObject.SetActive(false); }
        if (img2 != null) { img2.texture = null; img2.gameObject.SetActive(false); }
        if (panel != null) panel.SetActive(false);
    }
}
