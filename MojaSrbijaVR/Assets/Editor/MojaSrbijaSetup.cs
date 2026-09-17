// Automatski setup projekta MojaSrbijaVR — pokrece se iz menija "MojaSrbija"
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class MojaSrbijaSetup
{
    [MenuItem("MojaSrbija/1 - Podesi XR i Android")]
    public static void SetupXR()
    {
        try
        {
            EnableOpenXRLoader(BuildTargetGroup.Standalone);
            EnableOpenXRLoader(BuildTargetGroup.Android);
            EnableFeatures(BuildTargetGroup.Standalone);
            EnableFeatures(BuildTargetGroup.Android);
            SetupAndroidPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("<color=lime>[MojaSrbija] XR + Android podeseni. Sledece: meni MojaSrbija -> 2 - Napravi scenu</color>");
        }
        catch (Exception e)
        {
            Debug.LogError("[MojaSrbija] Setup pukao: " + e);
        }
    }

    static void EnableOpenXRLoader(BuildTargetGroup group)
    {
        XRGeneralSettingsPerBuildTarget all;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out all) || all == null)
        {
            all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
            AssetDatabase.CreateAsset(all, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, all, true);
        }
        var settings = all.SettingsForBuildTarget(group);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            settings.Manager = manager;
            AssetDatabase.AddObjectToAsset(settings, all);
            AssetDatabase.AddObjectToAsset(manager, all);
            all.SetSettingsForBuildTarget(group, settings);
        }
        bool ok = XRPackageMetadataStore.AssignLoader(settings.Manager,
            "UnityEngine.XR.OpenXR.OpenXRLoader", group);
        Debug.Log($"[MojaSrbija] OpenXR loader za {group}: {(ok ? "ukljucen" : "VEC ukljucen ili greska")}");
    }

    static void EnableFeatures(BuildTargetGroup group)
    {
        FeatureHelpers.RefreshFeatures(group);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) { Debug.LogWarning($"[MojaSrbija] Nema OpenXR settings za {group}"); return; }

        // kontroleri (Quest Touch) + hand tracking + Meta Quest podrska
        string[] want = {
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.handtracking",
            "com.unity.openxr.feature.input.metaquesttouchpro", // ne skodi
            "com.unity.openxr.feature.metaquest"                // Android: Meta Quest Support
        };
        foreach (var f in settings.GetFeatures())
        {
            var attr = f.GetType().GetCustomAttributes(typeof(OpenXRFeatureAttribute), true)
                        .Cast<OpenXRFeatureAttribute>().FirstOrDefault();
            string id = attr != null ? attr.FeatureId : "";
            if (want.Contains(id))
            {
                f.enabled = true;
                EditorUtility.SetDirty(f);
                Debug.Log($"[MojaSrbija] {group}: feature '{id}' ukljucen");
            }
        }
        EditorUtility.SetDirty(settings);
    }

    static void SetupAndroidPlayer()
    {
        var android = NamedBuildTarget.Android;
        // OpenXR blocks Android/OpenGLES builds in Gamma space. Quest projects
        // use Linear lighting, so make menu option 10 self-contained instead of
        // requiring a manual Project Validation fix before every build.
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetApplicationIdentifier(android, "com.valerijan.mojasrbija");
        PlayerSettings.productName = "Moja Srbija VR";
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
        // Meta's current binary validator accepts API 34 or lower for this
        // release flow; Unity Auto selected API 36 from the installed SDK.
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        Debug.Log("[MojaSrbija] Android player podesen (Landscape, SDK 34, install auto, Linear, IL2CPP, ARM64, ASTC)");
    }

    [MenuItem("MojaSrbija/2 - Napravi prvu scenu")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // pod
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Pod";
        floor.transform.localScale = new Vector3(4, 1, 4);
        floor.GetComponent<Renderer>().sharedMaterial = Mat("PodMat", new Color(0.16f, 0.19f, 0.23f));

        // sto
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Sto";
        table.transform.position = new Vector3(0, 0.4f, 0.7f);
        table.transform.localScale = new Vector3(1.4f, 0.8f, 0.8f);
        table.GetComponent<Renderer>().sharedMaterial = Mat("StoMat", new Color(0.35f, 0.27f, 0.2f));

        // kocke za hvatanje (Strava narandzasta paleta)
        Color[] cols = { new Color(1f, 0.37f, 0f), new Color(1f, 0.69f, 0.23f), new Color(0f, 0.76f, 1f) };
        for (int i = 0; i < 3; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Kocka_" + (i + 1);
            cube.transform.position = new Vector3(-0.35f + i * 0.35f, 0.95f, 0.7f);
            cube.transform.localScale = Vector3.one * 0.12f;
            cube.GetComponent<Renderer>().sharedMaterial = Mat("KockaMat" + i, cols[i]);
            cube.AddComponent<XRGrabInteractable>(); // sam dodaje Rigidbody
        }

        // XR rig (kamera + kontroleri/ruke) preko zvanicnog XRI menija
        var cam = GameObject.Find("Main Camera");
        if (cam != null) UnityEngine.Object.DestroyImmediate(cam);
        if (!EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)"))
            Debug.LogWarning("[MojaSrbija] Nisam nasao meni 'XR Origin (VR)' - dodaj rucno: GameObject > XR > XR Origin (VR)");

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Vece1.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Vece1.unity", true) };
        Debug.Log("<color=lime>[MojaSrbija] Scena 'Vece1' napravljena i snimljena. Pritisni Play (uz Virtual Desktop) ili buildaj APK.</color>");
    }

    [MenuItem("MojaSrbija/3 - Ukljuci URP (popravi roze)")]
    public static void SetupURP()
    {
        System.IO.Directory.CreateDirectory("Assets/Settings");
        var rendererData = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");
        var pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP_Asset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        Debug.Log("<color=lime>[MojaSrbija] URP aktiviran - roze materijali postaju normalni.</color>");
    }

    [MenuItem("MojaSrbija/4 - Ubaci pravi XR rig (posle importa Starter Assets)")]
    public static void SwapRig()
    {
        var guids = AssetDatabase.FindAssets("XR Origin t:prefab", new[] { "Assets/Samples" });
        string path = guids.Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.Contains("XR Origin (XR Rig)"));
        if (path == null)
        {
            Debug.LogError("[MojaSrbija] Nema Starter Assets prefaba! Prvo: Window > Package Manager > " +
                "In Project > XR Interaction Toolkit > tab Samples > Import 'Starter Assets', pa opet ovo.");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var name in new[] { "XR Origin (VR)", "XR Origin (XR Rig)" })
        {
            var old = GameObject.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
        }
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.position = new Vector3(0, 0, -0.4f);
        var origin = rig.GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=lime>[MojaSrbija] Pravi rig ubacen ({path}) - kontroleri i ruke sada imaju modele.</color>");
    }

    const string CESIUM_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJub25jZSI6InJubXZ2MnJxTVhRYk8xTEsiLCJqdGkiOiIzOWExNmIyOC1hMTlmLTQ0N2QtOTAyYS1hNDE1OGUyY2U2OTMiLCJpZCI6NDg0NzEyLCJpc3MiOiJodHRwczovL2FwaS5jZXNpdW0uY29tIiwiYXVkIjoidW5kZWZpbmVkX2RlZmF1bHQiLCJpYXQiOjE3ODg5NDcwODZ9.4QPrQVrDHy9kVcli0T_76Xye0NhbWVxEJygbkYP5JMs";

    [MenuItem("MojaSrbija/5 - Teren Srbije (Cesium maketa)")]
    public static void SetupCesiumScene()
    {
        // 1) upisi ion token
        var server = CesiumForUnity.CesiumIonServer.defaultServer;
        server.defaultIonAccessToken = CESIUM_TOKEN;
        EditorUtility.SetDirty(server);
        AssetDatabase.SaveAssets();
        Debug.Log("[MojaSrbija] Cesium ion token upisan.");

        // 2) skloni sto i kocke - maketa dolazi na njihovo mesto
        foreach (var name in new[] { "Sto", "Kocka_1", "Kocka_2", "Kocka_3" })
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // 3) georeferenca = maketa (Srbija ~480 km -> ~1.25 m na visini stola)
        var old = GameObject.Find("MaketaSrbije");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var geoGO = new GameObject("MaketaSrbije");
        geoGO.transform.position = new Vector3(0, 0.85f, 0.9f);
        var geo = geoGO.AddComponent<CesiumForUnity.CesiumGeoreference>();
        geo.latitude = 44.02; geo.longitude = 20.91; geo.height = 0;
        geo.scale = 2.6e-6;   // Cesium-ova razmera: Srbija ~1.25 m (obicna Unity skala se ignorise!)

        // 4) svetski teren + satelitski snimci sa Cesium ion-a
        var terrGO = new GameObject("CesiumWorldTerrain");
        terrGO.transform.SetParent(geoGO.transform, false);
        var tileset = terrGO.AddComponent<CesiumForUnity.Cesium3DTileset>();
        tileset.ionAssetID = 1;                    // Cesium World Terrain
        tileset.maximumScreenSpaceError = 8;       // vise detalja na maketi
        var overlay = terrGO.AddComponent<CesiumForUnity.CesiumIonRasterOverlay>();
        overlay.ionAssetID = 2;                    // Bing Maps Aerial

        // 5) rute preko terena
        var rr = geoGO.AddComponent<RoutesRenderer>();
        rr.georeference = geo;

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=lime>[MojaSrbija] Maketa Srbije postavljena. Pritisni Play (kroz Virtual Desktop).</color>");
    }

    [MenuItem("MojaSrbija/6 - Iseci teren na Srbiju + granica")]
    public static void ClipToSerbia()
    {
        var geoGO = GameObject.Find("MaketaSrbije");
        if (geoGO == null) { Debug.LogError("[MojaSrbija] Prvo pokreni '5 - Teren Srbije'."); return; }
        var geo = geoGO.GetComponent<CesiumForUnity.CesiumGeoreference>();
        var terrGO = GameObject.Find("CesiumWorldTerrain");

        // 1) poligon granice za isecanje (spline u lokalnim koordinatama)
        var oldPoly = GameObject.Find("GranicaClip");
        if (oldPoly != null) UnityEngine.Object.DestroyImmediate(oldPoly);
        var polyGO = new GameObject("GranicaClip");
        polyGO.transform.SetParent(geoGO.transform, false);
        var poly = polyGO.AddComponent<CesiumForUnity.CesiumCartographicPolygon>();

        string txt = System.IO.File.ReadAllText("Assets/StreamingAssets/border.txt").Trim();
        var pts = txt.Split(';');
        var spline = polyGO.GetComponent<UnityEngine.Splines.SplineContainer>().Spline;
        spline.Clear();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        // redji poligon za clipping (svaka 2. tacka je dovoljna)
        for (int i = 0; i < pts.Length; i += 2)
        {
            var c = pts[i].Split(',');
            double lon = double.Parse(c[0], inv), lat = double.Parse(c[1], inv);
            var ecef = CesiumForUnity.CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new Unity.Mathematics.double3(lon, lat, 0));
            var u = geo.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            var world = new Vector3((float)u.x, (float)u.y, (float)u.z);
            var local = polyGO.transform.InverseTransformPoint(world);
            spline.Add(new UnityEngine.Splines.BezierKnot((Unity.Mathematics.float3)local),
                       UnityEngine.Splines.TangentMode.Linear);
        }
        spline.Closed = true;

        // 2) overlay koji sece sve VAN poligona
        var oldClip = terrGO.GetComponent<CesiumForUnity.CesiumPolygonRasterOverlay>();
        if (oldClip != null) UnityEngine.Object.DestroyImmediate(oldClip);
        var clip = terrGO.AddComponent<CesiumForUnity.CesiumPolygonRasterOverlay>();
        clip.polygons = new System.Collections.Generic.List<CesiumForUnity.CesiumCartographicPolygon> { poly };
        clip.invertSelection = true;

        // 3) svetleca granica
        if (geoGO.GetComponent<BorderRenderer>() == null)
        {
            var br = geoGO.AddComponent<BorderRenderer>();
            br.georeference = geo;
        }

        // 4) ostriji tile-ovi na maketi
        var ts = terrGO.GetComponent<CesiumForUnity.Cesium3DTileset>();
        if (ts != null) ts.maximumScreenSpaceError = 4;

        // 5) tamna "void" pozadina umesto neba i horizonta
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.055f, 0.075f);
        }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=lime>[MojaSrbija] Teren isecen na Srbiju, granica svetli, pozadina void. Play!</color>");
    }

    [MenuItem("MojaSrbija/7 - Maketa u ruke + doterivanje")]
    public static void GrabbableMaketa()
    {
        var geoGO = GameObject.Find("MaketaSrbije");
        if (geoGO == null) { Debug.LogError("[MojaSrbija] Prvo koraci 5 i 6."); return; }

        // rupe: ne prikazuj krupni tile dok se sitniji ne ucita; malo blaza greska = stabilnije
        var terrGO = GameObject.Find("CesiumWorldTerrain");
        var ts = terrGO != null ? terrGO.GetComponent<CesiumForUnity.Cesium3DTileset>() : null;
        if (ts != null) { ts.forbidHoles = true; ts.maximumScreenSpaceError = 8; }

        // tamniji ambijent
        var pod = GameObject.Find("Pod");
        if (pod != null) pod.GetComponent<Renderer>().sharedMaterial.color = new Color(0.07f, 0.085f, 0.105f);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.055f);
        }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);

        // grab koren: hvatanje jednom rukom (pomeranje/rotacija), dve ruke = zum
        var root = GameObject.Find("MaketaRoot");
        if (root == null)
        {
            root = new GameObject("MaketaRoot");
            root.transform.position = geoGO.transform.position;
            geoGO.transform.SetParent(root.transform, true);
        }
        var col = root.GetComponent<BoxCollider>();
        if (col == null) col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, -0.03f, 0);
        col.size = new Vector3(1.55f, 0.12f, 1.25f);
        var rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var grab = root.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = root.AddComponent<XRGrabInteractable>();
        grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.trackScale = true;
        var xf = root.GetComponent<UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer>();
        if (xf == null) xf = root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer>();
        xf.allowTwoHandedScaling = true;

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=lime>[MojaSrbija] Maketa se sada hvata (grip); dve ruke = zum. Play!</color>");
    }

    [MenuItem("MojaSrbija/9 - Selekcija rute + panel")]
    public static void SetupPicker()
    {
        var root = GameObject.Find("MaketaV2");
        if (root == null) { Debug.LogError("[MojaSrbija] Prvo korak 8 (ispeci maketu)."); return; }
        var rute = root.transform.Find("Rute");
        if (rute == null) { Debug.LogError("[MojaSrbija] Nema 'Rute' pod maketom."); return; }

        // gadjamo direktno teren (mesh collider prati reljef) - nema vise lebdecih kutija
        var terenGO2 = root.transform.Find("Teren");
        if (terenGO2 == null) { Debug.LogError("[MojaSrbija] Nema 'Teren' pod maketom."); return; }
        var bc = terenGO2.GetComponent<MeshCollider>();
        if (bc == null) bc = terenGO2.gameObject.AddComponent<MeshCollider>();
        var oldBoard = root.transform.Find("RouteBoard");
        if (oldBoard != null) UnityEngine.Object.DestroyImmediate(oldBoard.gameObject);
        var rootBox = root.GetComponent<BoxCollider>();
        if (rootBox != null) UnityEngine.Object.DestroyImmediate(rootBox);

        // world-space panel
        var panelGO = root.transform.Find("RutaPanel")?.gameObject;
        if (panelGO != null) UnityEngine.Object.DestroyImmediate(panelGO);
        panelGO = new GameObject("RutaPanel");
        panelGO.transform.SetParent(root.transform, false);
        panelGO.transform.localPosition = new Vector3(0.75f, 0.35f, 0.2f);
        var canvas = panelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420, 360);
        panelGO.transform.localScale = Vector3.one * 0.001f;

        var bg = new GameObject("BG").AddComponent<UnityEngine.UI.Image>();
        bg.transform.SetParent(panelGO.transform, false);
        bg.color = new Color(0.06f, 0.08f, 0.1f, 0.92f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        UnityEngine.UI.Text MakeText(string n, float top, float height, int size, FontStyle style, Color col)
        {
            var go = new GameObject(n);
            go.transform.SetParent(panelGO.transform, false);
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = col;
            t.alignment = TextAnchor.UpperLeft;
            var r = t.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -top);
            r.sizeDelta = new Vector2(-40, height);
            return t;
        }
        var title = MakeText("Naslov", 12, 78, 32, FontStyle.Bold, new Color(1f, 0.55f, 0.15f));
        var stats = MakeText("Stat", 92, 100, 24, FontStyle.Normal, new Color(0.9f, 0.93f, 0.96f));
        title.text = "";
        stats.text = "";

        UnityEngine.UI.RawImage MakeImg(string n, float x)
        {
            var go = new GameObject(n);
            go.transform.SetParent(panelGO.transform, false);
            var ri = go.AddComponent<UnityEngine.UI.RawImage>();
            var r = ri.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 0);
            r.pivot = new Vector2(0, 0);
            r.anchoredPosition = new Vector2(x, 16);
            r.sizeDelta = new Vector2(186, 140);
            go.SetActive(false);
            return ri;
        }
        var i1 = MakeImg("Slika1", 16);
        var i2 = MakeImg("Slika2", 218);

        var picker = root.GetComponent<RoutePicker>();
        if (picker == null) picker = root.AddComponent<RoutePicker>();
        picker.ruteParent = rute;
        picker.board = bc;
        picker.panel = panelGO;
        picker.titleText = title;
        picker.statsText = stats;
        picker.img1 = i1;
        picker.img2 = i2;

        // zvuk na maketi
        if (root.GetComponent<SfxManager>() == null) root.AddComponent<SfxManager>();

        // kretanje rukama: grip u prazno = vuces svet
        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig != null)
        {
            var drag = rig.GetComponent<SpaceDragLocomotion>();
            if (drag == null) drag = rig.AddComponent<SpaceDragLocomotion>();
            drag.rig = rig.transform;
        }

        // auto-fokus na klaster ruta pri ulasku
        var focus = root.GetComponent<MaketaStartFocus>();
        if (focus == null) focus = root.AddComponent<MaketaStartFocus>();
        focus.ruteParent = rute;

        // ostrija slika: veci render scale (VD dobija cistiji ulaz)
        var urp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(
            "Assets/Settings/URP_Asset.asset");
        if (urp != null) urp.renderScale = 1.25f;

        // foto-pinovi, mini avatar, dan-noc, poster mod
        if (root.GetComponent<PhotoPins>() == null) root.AddComponent<PhotoPins>();
        if (root.GetComponent<MiniRunner>() == null) root.AddComponent<MiniRunner>();
        var dn = root.GetComponent<DayNight>();
        if (dn == null) dn = root.AddComponent<DayNight>();
        var sunGO = GameObject.Find("Directional Light");
        if (sunGO != null) dn.sun = sunGO.GetComponent<Light>();
        if (root.GetComponent<PosterMode>() == null) root.AddComponent<PosterMode>();

        // collider terena: pinovi i buduce interakcije znaju visinu reljefa
        var teren = root.transform.Find("Teren");
        if (teren != null && teren.GetComponent<MeshCollider>() == null)
            teren.gameObject.AddComponent<MeshCollider>();

        // produzeni zrak kontrolera (default 10 m je prekratak za veliku maketu)
        foreach (var handName in new[] { "Left Controller", "Right Controller" })
        {
            var h = GameObject.Find(handName);
            if (h == null) { Debug.LogWarning("[MojaSrbija] Nema " + handName); continue; }
            var nf = h.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            if (nf != null && nf.farInteractionCaster is
                UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster cc)
            {
                cc.castDistance = 40f;
                Debug.Log($"[MojaSrbija] {handName}: domasaj zraka = 40 m");
            }
            else Debug.LogWarning($"[MojaSrbija] {handName}: nisam nasao NearFar caster!");
            foreach (var lv in h.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(true))
            {
                lv.overrideInteractorLineLength = true;
                lv.lineLength = 40f;
            }
            // gasimo XRI zrake - nas laser je jedini vidljivi
            foreach (var lrX in h.GetComponentsInChildren<LineRenderer>(true))
                lrX.enabled = false;
            Debug.Log($"[MojaSrbija] {handName}: XRI zraci ugaseni, ostaje nas laser");
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=lime>[MojaSrbija] Selekcija spremna: uperi desni kontroler u rutu i povuci trigger.</color>");
    }

    [MenuItem("MojaSrbija/10 - Build APK")]
    public static void BuildAPK()
    {
        if (EditorApplication.isPlaying)
        { Debug.LogError("[MojaSrbija] Zaustavi Play pre pravljenja APK-a."); return; }
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        { Debug.LogError("[MojaSrbija] U Unity Hub-u dodaj Android Build Support, SDK/NDK i OpenJDK."); return; }
        if (!EditorSceneManager.SaveOpenScenes()) return;

        // Always prepare the exact scene file passed to BuildPipeline. Previously
        // the map was restored in another open scene, after which BuildPipeline
        // silently reopened the old Vece1 file containing only the demo cubes.
        const string sourceScene = "Assets/Scenes/Vece1.unity";
        const string androidScene = "Assets/Scenes/MojaSrbijaVR_Android.unity";
        if (!System.IO.File.Exists(sourceScene))
        { Debug.LogError("[MojaSrbija] Nedostaje " + sourceScene); return; }
        EditorSceneManager.OpenScene(sourceScene, OpenSceneMode.Single);
        if (GameObject.Find("MaketaV2") == null)
        {
            Debug.LogWarning("[MojaSrbija] Build scena nema mapu; vracam je iz Assets/Maketa...");
            if (!MaketaBaker.RestoreFromExistingAssets()) return;
            SwapRig();
            SetupPicker();
            if (GameObject.Find("MaketaV2")?.GetComponent<RoutePicker>() == null)
            { Debug.LogError("[MojaSrbija] Mapa nije pravilno povezana; APK nije napravljen."); return; }
            EditorSceneManager.SaveOpenScenes();
        }
        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), androidScene, true))
        { Debug.LogError("[MojaSrbija] Android scena nije sacuvana; build je prekinut."); return; }
        AssetDatabase.ImportAsset(androidScene, ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene(androidScene, OpenSceneMode.Single);
        if (GameObject.Find("MaketaV2")?.GetComponent<RoutePicker>() == null ||
            GameObject.Find("Kocka_1") != null)
        {
            Debug.LogError("[MojaSrbija] Provera Android scene nije prosla: mapa nedostaje ili su demo kocke ostale.");
            return;
        }
        Debug.Log("<color=lime>[MojaSrbija] Android scena proverena: MaketaV2 + RoutePicker, bez demo kocki.</color>");
        SetupAndroidPlayer();
        EnableOpenXRLoader(BuildTargetGroup.Android);
        EnableFeatures(BuildTargetGroup.Android);
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();
        EnsureKeystore();
        PlayerSettings.Android.bundleVersionCode += 1;
        var scenes = new[] { androidScene };
        System.IO.Directory.CreateDirectory("Builds");
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/MojaSrbijaVR.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var rep = UnityEditor.BuildPipeline.BuildPlayer(opts);
        if (rep.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"<color=lime>[MojaSrbija] APK gotov: Builds/MojaSrbijaVR.apk " +
                      $"({rep.summary.totalSize / 1024 / 1024} MB, versionCode {PlayerSettings.Android.bundleVersionCode})</color>");
        else
            Debug.LogError("[MojaSrbija] Build nije uspeo: " + rep.summary.result);
    }

    static void EnsureKeystore()
    {
        string ks = System.IO.Path.GetFullPath("Builds/moja.keystore");
        System.IO.Directory.CreateDirectory("Builds");
        if (!System.IO.File.Exists(ks))
        {
            string keytool = System.IO.Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = keytool,
                Arguments = $"-genkeypair -v -keystore \"{ks}\" -alias moja -keyalg RSA -keysize 2048 " +
                            "-validity 10000 -storepass mojasrbija1 -keypass mojasrbija1 " +
                            "-dname \"CN=Valerijan, OU=VR, O=MojaSrbija, L=Beograd, C=RS\"",
                UseShellExecute = false, CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi)) p.WaitForExit();
            Debug.Log("[MojaSrbija] Keystore napravljen: " + ks);
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ks;
        PlayerSettings.Android.keystorePass = "mojasrbija1";
        PlayerSettings.Android.keyaliasName = "moja";
        PlayerSettings.Android.keyaliasPass = "mojasrbija1";
    }

    static Material Mat(string name, Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader) { color = c };
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(m, $"Assets/Materials/{name}.mat");
        return m;
    }
}
