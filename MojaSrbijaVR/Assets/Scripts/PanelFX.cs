// Animacije panela: fade-in pri otvaranju, puls dugmeta pri kliku.
using System.Collections;
using UnityEngine;

public class PanelFX : MonoBehaviour
{
    CanvasGroup group;

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
            t += Time.unscaledDeltaTime / 0.22f;
            group.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            yield return null;
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
            target.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }
}
