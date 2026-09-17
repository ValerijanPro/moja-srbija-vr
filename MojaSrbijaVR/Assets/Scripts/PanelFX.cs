// Animacije panela: fade-in, ulaz redova sa kaskadom, glatki hover, puls na klik.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelFX : MonoBehaviour
{
    CanvasGroup group;
    public readonly List<RectTransform> tracked = new List<RectTransform>();
    public RectTransform hovered;

    void Awake() { group = GetComponent<CanvasGroup>(); }

    void OnEnable()
    {
        if (group == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        group.alpha = 0;
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.2f;
            group.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            yield return null;
        }
    }

    void Update()
    {
        // glatki hover: dugmad "disu" ka ciljanoj velicini
        for (int i = 0; i < tracked.Count; i++)
        {
            var rt = tracked[i];
            if (rt == null) continue;
            float target = rt == hovered ? 1.045f : 1f;
            float s = Mathf.Lerp(rt.localScale.x, target, Time.unscaledDeltaTime * 14f);
            rt.localScale = new Vector3(s, s, 1);
        }
    }

    public void Pulse(RectTransform target)
    {
        if (target != null && isActiveAndEnabled) StartCoroutine(DoPulse(target));
    }

    IEnumerator DoPulse(RectTransform target)
    {
        float t = 0;
        while (t < 1f && target != null)
        {
            t += Time.unscaledDeltaTime / 0.16f;
            float s = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            target.localScale = new Vector3(s, s, 1);
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }

    public void Stagger(List<CanvasGroup> rows)
    {
        if (isActiveAndEnabled) StartCoroutine(DoStagger(rows));
    }

    IEnumerator DoStagger(List<CanvasGroup> rows)
    {
        var starts = new List<Vector2>();
        foreach (var row in rows)
        {
            if (row == null) { starts.Add(Vector2.zero); continue; }
            var rt = (RectTransform)row.transform;
            starts.Add(rt.anchoredPosition);
            row.alpha = 0;
            rt.anchoredPosition = starts[starts.Count - 1] + new Vector2(-26, 0);
        }
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.42f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null) continue;
                float local = Mathf.Clamp01(t * 2.2f - i * 0.09f);
                float e = Mathf.SmoothStep(0, 1, local);
                rows[i].alpha = e;
                ((RectTransform)rows[i].transform).anchoredPosition =
                    starts[i] + new Vector2(-26 * (1 - e), 0);
            }
            yield return null;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;
            rows[i].alpha = 1;
            ((RectTransform)rows[i].transform).anchoredPosition = starts[i];
        }
    }
}
