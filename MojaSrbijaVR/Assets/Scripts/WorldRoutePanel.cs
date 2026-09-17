using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Small world-space menu. Pointer hit testing is shared by both controllers and
// does not depend on an XRI UI action asset or on generated scene wiring.
public sealed class WorldRoutePanel
{
    public readonly GameObject root;
    readonly RectTransform rect;
    readonly List<(RectTransform rect, Image bg, Action action)> buttons = new List<(RectTransform, Image, Action)>();
    readonly List<GameObject> contents = new List<GameObject>();
    readonly Font font;
    readonly PanelFX fx;
    float cursor;
    int hoverIndex = -1;
    RawImage firstPhoto, secondPhoto;

    static readonly Color ButtonBase = new Color(0.10f, 0.20f, 0.25f, 1);
    static readonly Color ButtonHover = new Color(0.88f, 0.40f, 0.10f, 0.95f);

    public WorldRoutePanel(string name, float width = 520, float height = 920)
    {
        root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image));
        rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.055f, 0.97f);
        root.transform.localScale = Vector3.one * 0.001f;
        root.AddComponent<CanvasGroup>();
        fx = root.AddComponent<PanelFX>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Clear();
    }

    public void Clear()
    {
        foreach (var go in contents) UnityEngine.Object.Destroy(go);
        contents.Clear(); buttons.Clear(); cursor = 20;
        hoverIndex = -1;
        firstPhoto = secondPhoto = null;
    }

    RectTransform Row(string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        contents.Add(go);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(rect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(rect.rect.width - 32, height);
        rt.anchoredPosition = new Vector2(0, -cursor);
        cursor += height + 8;
        return rt;
    }

    public Text AddText(string value, float height = 65, int size = 24, bool heading = false)
    {
        var rt = Row("Tekst", height);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size;
        text.fontStyle = heading ? FontStyle.Bold : FontStyle.Normal;
        text.color = heading ? new Color(1, 0.65f, 0.3f) : new Color(0.86f, 0.92f, 0.95f);
        text.alignment = TextAnchor.UpperLeft;
        text.text = value;
        return text;
    }

    public void AddButton(string value, Action action, float height = 55)
    {
        var rt = Row("Dugme", height);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = ButtonBase;
        var label = new GameObject("Natpis", typeof(RectTransform), typeof(Text));
        var child = label.GetComponent<RectTransform>();
        child.SetParent(rt, false);
        child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one;
        child.offsetMin = new Vector2(12, 3); child.offsetMax = new Vector2(-12, -3);
        var text = label.GetComponent<Text>();
        text.font = font; text.fontSize = 23; text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft; text.text = value;
        buttons.Add((rt, bg, action));
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
        var image = MakePhoto(row, 0, 1f);
        var frame = row.gameObject.AddComponent<Image>();
        frame.color = new Color(0, 0, 0, 0.45f);
        return image;
    }

    static RawImage MakePhoto(RectTransform row, float start, float end)
    {
        var slot = new GameObject("OkvirSlike", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(row, false);
        slot.anchorMin = new Vector2(start, 0); slot.anchorMax = new Vector2(end, 1);
        slot.offsetMin = new Vector2(4, 0); slot.offsetMax = new Vector2(-4, 0);
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
        // Fit instead of stretching the photograph into a fixed aspect ratio.
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
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].bg == null) continue;
            buttons[i].bg.color = i == index ? ButtonHover : ButtonBase;
            if (buttons[i].rect != null && i != index) buttons[i].rect.localScale = Vector3.one;
        }
    }

    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
}
