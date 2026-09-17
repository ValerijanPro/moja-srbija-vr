// Ekran dobrodoslice: stakleni panel u prostoru sa statistikom i opcijama.
// Sopstveni pointer (laser + trigger), radi nezavisno od mape.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WelcomeScreen : MonoBehaviour
{
    MapExperience experience;
    RoutePicker picker;
    GameObject root;
    RectTransform rect;
    readonly List<(RectTransform rect, Image bg, Action action)> buttons = new List<(RectTransform, Image, Action)>();
    readonly List<Graphic> graphics = new List<Graphic>();
    InputAction trigger;
    LineRenderer laser;
    Transform aim;
    Font font;
    Text aboutText;
    bool aboutShown, closing, pressed;
    int hoverIndex = -1;

    static readonly Color Accent = new Color(1f, 0.42f, 0.08f);
    static readonly Color TxtMain = new Color(0.93f, 0.95f, 0.97f);
    static readonly Color TxtDim = new Color(0.62f, 0.68f, 0.74f);
    static readonly Color BtnBg = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color BtnHover = new Color(1f, 0.42f, 0.08f, 0.28f);

    public void Begin(MapExperience exp, RoutePicker source)
    {
        experience = exp;
        picker = source;
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        while (Camera.main == null) yield return null;
        var head = Camera.main.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        root = new GameObject("Dobrodoslica", typeof(RectTransform), typeof(Canvas));
        rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(660, 860);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<Canvas>().sortingOrder = 60;
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.1f) fwd = Vector3.forward;
        root.transform.SetPositionAndRotation(
            head.position + fwd * 1.15f - Vector3.up * 0.05f, Quaternion.LookRotation(fwd));
        root.transform.localScale = Vector3.one * 0.00085f;

        // staklena pozadina + akcentna linija
        var bg = MakeImage(root.transform, new Color(0.05f, 0.065f, 0.085f, 0.94f));
        Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);
        var strip = MakeImage(root.transform, Accent);
        strip.rectTransform.anchorMin = new Vector2(0, 1); strip.rectTransform.anchorMax = new Vector2(1, 1);
        strip.rectTransform.pivot = new Vector2(0.5f, 1);
        strip.rectTransform.anchoredPosition = Vector2.zero;
        strip.rectTransform.sizeDelta = new Vector2(0, 7);

        MakeText("MOJA SRBIJA", 66, FontStyle.Bold, Accent, 34, 84);
        MakeText("Tvoje Strava rute u virtuelnoj Srbiji", 27, FontStyle.Normal, TxtDim, 112, 40);

        var totals = picker.Totals();
        MakeText($"{totals.count} aktivnosti   ·   {totals.km:0} km   ·   {totals.elev:0} m uspona",
            27, FontStyle.Bold, TxtMain, 168, 44);
        var divider = MakeImage(root.transform, new Color(1, 1, 1, 0.10f));
        divider.rectTransform.anchorMin = new Vector2(0, 1); divider.rectTransform.anchorMax = new Vector2(1, 1);
        divider.rectTransform.pivot = new Vector2(0.5f, 1);
        divider.rectTransform.anchoredPosition = new Vector2(0, -238);
        divider.rectTransform.sizeDelta = new Vector2(-72, 2);

        float y = 272;
        AddButton("Istrazi mapu", "slobodno razgledanje, laser bira rute", ref y,
            () => Close(() => experience.SetMenuVisible(false)));
        AddButton("Moje rute", "spisak svih grupa ruta", ref y,
            () => Close(() => experience.OpenOverview()));
        AddButton("Preporuceno za tebe", "predlozi iz tvoje arhive", ref y,
            () => Close(() => experience.OpenRecommendations()));
        AddButton("O projektu", "", ref y, ToggleAbout);

        aboutText = MakeText(
            "VR sistem za istrazivanje licnih Strava aktivnosti na 3D terenu Srbije.\n" +
            "Projekat: Virtuelna stvarnost, doktorske studije.\n" +
            "Podaci ne napustaju uredjaj.",
            21, FontStyle.Normal, TxtDim, y + 6, 120);
        aboutText.gameObject.SetActive(false);

        MakeText("Uperi laser i povuci trigger", 21, FontStyle.Italic, TxtDim, 800, 40);

        // sopstveni pointer
        trigger = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/trigger");
        trigger.Enable();
        var lgo = new GameObject("WelcomeLaser");
        laser = lgo.AddComponent<LineRenderer>();
        laser.useWorldSpace = true; laser.positionCount = 2;
        laser.startWidth = 0.0022f; laser.endWidth = 0.001f;
        var sh = Shader.Find("MojaSrbija/Additive");
        laser.material = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { color = Accent };
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ulazna animacija: sve providno pa postepeno
        foreach (var g in graphics) SetAlpha(g, 0);
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.7f;
            for (int i = 0; i < graphics.Count; i++)
            {
                float local = Mathf.Clamp01(t * 1.6f - i * 0.045f);
                SetAlpha(graphics[i], Mathf.SmoothStep(0, 1, local));
            }
            yield return null;
        }
    }

    void ToggleAbout()
    {
        aboutShown = !aboutShown;
        if (aboutText != null) aboutText.gameObject.SetActive(aboutShown);
    }

    void Close(Action after)
    {
        if (closing) return;
        closing = true;
        StartCoroutine(FadeOut(after));
    }

    IEnumerator FadeOut(Action after)
    {
        if (SfxManager.I != null) SfxManager.I.Click();
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.3f;
            foreach (var g in graphics) if (g != null) SetAlpha(g, 1 - t);
            yield return null;
        }
        laser.enabled = false;
        experience.WelcomeOpen = false;
        after?.Invoke();
        Destroy(root);
        Destroy(laser.gameObject);
        Destroy(this);
    }

    void Update()
    {
        if (root == null || closing) return;
        if (aim == null)
        {
            var rc = GameObject.Find("Right Controller");
            if (rc == null) return;
            aim = rc.transform;
            foreach (var needle in new[] { "Stabilized", "Ray Origin", "Aim" })
            {
                var f = FindDeep(rc.transform, needle);
                if (f != null) { aim = f; break; }
            }
        }

        var ray = new Ray(aim.position, aim.forward);
        var plane = new Plane(-root.transform.forward, root.transform.position);
        int hit = -1;
        Vector3 end = ray.GetPoint(1.4f);
        if (plane.Raycast(ray, out float dist) && dist < 6f)
        {
            Vector3 world = ray.GetPoint(dist);
            Vector3 local = root.transform.InverseTransformPoint(world);
            end = world;
            for (int i = 0; i < buttons.Count; i++)
            {
                var r = buttons[i].rect;
                // dugme: pivot gore-centar, anchored na gornju ivicu panela
                Vector2 topCenter = new Vector2(r.anchoredPosition.x,
                    rect.sizeDelta.y * 0.5f + r.anchoredPosition.y);
                Vector2 rel = new Vector2(local.x - topCenter.x, topCenter.y - local.y);
                if (Mathf.Abs(rel.x) < r.sizeDelta.x * 0.5f && rel.y > 0 && rel.y < r.sizeDelta.y)
                { hit = i; break; }
            }
        }
        laser.enabled = true;
        laser.SetPosition(0, ray.GetPoint(0.04f));
        laser.SetPosition(1, end);

        if (hit != hoverIndex)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].bg.color = i == hit ? BtnHover : BtnBg;
                buttons[i].rect.localScale = Vector3.one * (i == hit ? 1.035f : 1f);
            }
            hoverIndex = hit;
        }
        bool now = trigger.ReadValue<float>() > 0.7f;
        if (now && !pressed && hit >= 0) buttons[hit].action?.Invoke();
        pressed = now;
    }

    /* ---------- UI helpers ---------- */

    Image MakeImage(Transform parent, Color c)
    {
        var go = new GameObject("img", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        graphics.Add(img);
        return img;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = min; r.offsetMax = -max;
    }

    Text MakeText(string value, int size, FontStyle style, Color c, float top, float height)
    {
        var go = new GameObject("txt", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var t = go.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.fontStyle = style; t.color = c;
        t.text = value;
        t.alignment = TextAnchor.MiddleCenter;
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.anchoredPosition = new Vector2(0, -top);
        r.sizeDelta = new Vector2(-64, height);
        graphics.Add(t);
        return t;
    }

    void AddButton(string title, string subtitle, ref float y, Action action)
    {
        var go = new GameObject("btn_" + title, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 1); r.anchorMax = new Vector2(0.5f, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.anchoredPosition = new Vector2(0, -y);
        r.sizeDelta = new Vector2(560, subtitle.Length > 0 ? 96 : 74);
        var bg = go.AddComponent<Image>();
        bg.color = BtnBg;
        graphics.Add(bg);

        var titleText = new GameObject("t", typeof(RectTransform)).AddComponent<Text>();
        titleText.transform.SetParent(go.transform, false);
        titleText.font = font; titleText.fontSize = 30; titleText.fontStyle = FontStyle.Bold;
        titleText.color = TxtMain; titleText.text = title;
        titleText.alignment = subtitle.Length > 0 ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
        Stretch(titleText.rectTransform, new Vector2(14, 6), new Vector2(14, 10));
        graphics.Add(titleText);
        if (subtitle.Length > 0)
        {
            var sub = new GameObject("s", typeof(RectTransform)).AddComponent<Text>();
            sub.transform.SetParent(go.transform, false);
            sub.font = font; sub.fontSize = 20; sub.color = TxtDim; sub.text = subtitle;
            sub.alignment = TextAnchor.LowerCenter;
            Stretch(sub.rectTransform, new Vector2(14, 12), new Vector2(14, 46));
            graphics.Add(sub);
        }
        buttons.Add((r, bg, action));
        y += (subtitle.Length > 0 ? 96 : 74) + 16;
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color;
        g.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a) * BaseAlpha(g));
    }

    static readonly Dictionary<Graphic, float> baseAlphas = new Dictionary<Graphic, float>();
    static float BaseAlpha(Graphic g)
    {
        if (!baseAlphas.TryGetValue(g, out float a))
        {
            a = g.color.a <= 0 ? 1f : g.color.a;
            baseAlphas[g] = a;
        }
        return a;
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

    void OnDestroy()
    {
        trigger?.Dispose();
        baseAlphas.Clear();
    }
}
