using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// World-space meni: zaobljene kartice, senke, akcentna traka, hover i kaskadni ulaz.
// Pointer hit testing radi za oba kontrolera, bez XRI UI sistema.
public sealed class WorldRoutePanel
{
    public readonly GameObject root;
    readonly RectTransform rect;
    readonly List<(RectTransform rect, Image bg, Action action)> buttons = new List<(RectTransform, Image, Action)>();
    readonly List<GameObject> contents = new List<GameObject>();
    readonly List<CanvasGroup> rows = new List<CanvasGroup>();
    readonly Font font;
    readonly PanelFX fx;
    float cursor;
    int hoverIndex = -1;
    RawImage firstPhoto, secondPhoto;

    static readonly Color PanelBg = new Color(0.045f, 0.06f, 0.08f, 0.96f);
    static readonly Color ButtonBase = new Color(1f, 1f, 1f, 0.055f);
    static readonly Color ButtonHover = new Color(0.95f, 0.42f, 0.09f, 0.32f);
    static readonly Color Accent = new Color(1f, 0.47f, 0.1f);
    static Sprite rounded;

    public WorldRoutePanel(string name, float width = 520, float height = 920)
    {
        root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image));
        rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var bg = root.GetComponent<Image>();
        bg.sprite = Rounded(); bg.type = Image.Type.Sliced;
        bg.color = PanelBg;
        var shadow = root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.55f);
        shadow.effectDistance = new Vector2(6, -8);
        root.transform.localScale = Vector3.one * 0.001f;
        root.AddComponent<CanvasGroup>();
        fx = root.AddComponent<PanelFX>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Clear();
    }

    static Sprite Rounded()
    {
        if (rounded != null) return rounded;
        const int S = 64, R = 17;
        var tex = new Texture2D(S, S, TextureFormat.ARGB32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x, R, S - 1 - R);
                float cy = Mathf.Clamp(y, R, S - 1 - R);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(R - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        rounded = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(R + 4, R + 4, R + 4, R + 4));
        return rounded;
    }

    public void Clear()
    {
        foreach (var go in contents) UnityEngine.Object.Destroy(go);
        contents.Clear(); buttons.Clear(); rows.Clear();
        fx.tracked.Clear(); fx.hovered = null;
        cursor = 22;
        hoverIndex = -1;
        firstPhoto = secondPhoto = null;
    }

    public void PlayEntrance() => fx.Stagger(new List<CanvasGroup>(rows));

    RectTransform Row(string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        contents.Add(go);
        rows.Add(go.GetComponent<CanvasGroup>());
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(rect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(rect.rect.width - 36, height);
        rt.anchoredPosition = new Vector2(0, -cursor);
        cursor += height + 9;
        return rt;
    }

    public Text AddText(string value, float height = 65, int size = 24, bool heading = false)
    {
        var rt = Row("Tekst", height);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size;
        text.fontStyle = heading ? FontStyle.Bold : FontStyle.Normal;
        text.color = heading ? Accent : new Color(0.86f, 0.92f, 0.95f);
        text.alignment = TextAnchor.UpperLeft;
        text.text = value;
        if (heading)
        {
            var underline = new GameObject("Linija", typeof(RectTransform), typeof(Image));
            var ur = underline.GetComponent<RectTransform>();
            ur.SetParent(rt, false);
            ur.anchorMin = new Vector2(0, 0); ur.anchorMax = new Vector2(0.42f, 0);
            ur.pivot = new Vector2(0, 0);
            ur.anchoredPosition = new Vector2(0, 2);
            ur.sizeDelta = new Vector2(0, 3);
            underline.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.65f);
        }
        return text;
    }

    public void AddButton(string value, Action action, float height = 55)
    {
        var rt = Row("Dugme", height);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = Rounded(); bg.type = Image.Type.Sliced;
        bg.color = ButtonBase;
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.35f);
        shadow.effectDistance = new Vector2(2, -3);

        // akcentna traka levo
        var barGo = new GameObject("Akcent", typeof(RectTransform), typeof(Image));
        var bar = barGo.GetComponent<RectTransform>();
        bar.SetParent(rt, false);
        bar.anchorMin = new Vector2(0, 0.18f); bar.anchorMax = new Vector2(0, 0.82f);
        bar.pivot = new Vector2(0, 0.5f);
        bar.anchoredPosition = new Vector2(10, 0);
        bar.sizeDelta = new Vector2(4, 0);
        barGo.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);

        var label = new GameObject("Natpis", typeof(RectTransform), typeof(Text));
        var child = label.GetComponent<RectTransform>();
        child.SetParent(rt, false);
        child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one;
        child.offsetMin = new Vector2(26, 3); child.offsetMax = new Vector2(-12, -3);
        var text = label.GetComponent<Text>();
        text.font = font; text.fontSize = 23; text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft; text.text = value;

        buttons.Add((rt, bg, action));
        fx.tracked.Add(rt);
    }

    public void AddPhotos()
    {
        var row = Row("Fotografije", 155);
        firstPhoto = MakePhoto(row, 0, 0.5f);
        secondPhoto = MakePhoto(row, 0.5f, 1f);
    }

    public RawImage AddBigPhoto(float height = 300)
    {
        var row = Row("VelikaSlika", height);
        var frame = row.gameObject.AddComponent<Image>();
        frame.sprite = Rounded(); frame.type = Image.Type.Sliced;
        frame.color = new Color(0, 0, 0, 0.5f);
        return MakePhoto(row, 0, 1f);
    }

    static RawImage MakePhoto(RectTransform row, float start, float end)
    {
        var slot = new GameObject("OkvirSlike", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(row, false);
        slot.anchorMin = new Vector2(start, 0); slot.anchorMax = new Vector2(end, 1);
        slot.offsetMin = new Vector2(5, 5); slot.offsetMax = new Vector2(-5, -5);
        var go = new GameObject("Slika", typeof(RectTransform), typeof(RawImage));
        go.GetComponent<RectTransform>().SetParent(slot, false);
        var image = go.GetComponent<RawImage>();
        image.enabled = false;
        return image;
    }

    public void SetPhotos(Texture a, Texture b)
    {
        SetPhoto(firstPhoto, a); SetPhoto(secondPhoto, b);
    }

    public static void SetPhoto(RawImage image, Texture texture)
    {
        if (image == null) return;
        image.texture = texture;
        image.enabled = texture != null;
        if (texture == null) return;
        float aspect = (float)texture.width / texture.height;
        image.uvRect = new Rect(0, 0, 1, 1);
        var fitter = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = aspect;
    }

    public bool Hit(Ray ray, out Vector3 point, bool press = false)
    {
        point = default;
        if (!root.activeInHierarchy) return false;
        if (!new Plane(root.transform.forward, root.transform.position).Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        if (!rect.rect.Contains((Vector2)rect.InverseTransformPoint(point))) { SetHover(-1); return false; }
        int hit = -1;
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].rect != null &&
                buttons[i].rect.rect.Contains((Vector2)buttons[i].rect.InverseTransformPoint(point)))
            { hit = i; break; }
        SetHover(hit);
        if (press && hit >= 0)
        {
            fx.Pulse(buttons[hit].rect);
            buttons[hit].action?.Invoke();
        }
        return true;
    }

    void SetHover(int index)
    {
        if (index == hoverIndex) return;
        hoverIndex = index;
        fx.hovered = index >= 0 ? buttons[index].rect : null;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].bg == null) continue;
            buttons[i].bg.color = i == index ? ButtonHover : ButtonBase;
        }
    }

    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
}
