// Drzi konstantnu SVETSKU debljinu linija bez obzira na zum makete,
// i zadebljava hover/selektovanu rutu.
using System.Collections.Generic;
using UnityEngine;

public class RouteWidthKeeper : MonoBehaviour
{
    public LineRenderer highlighted;
    public LineRenderer hovered;

    readonly Dictionary<LineRenderer, float> baseW = new Dictionary<LineRenderer, float>();
    float lastScale = -1;
    LineRenderer lastHi, lastHov;

    public void Register(LineRenderer lr)
    {
        if (lr != null && !baseW.ContainsKey(lr)) baseW[lr] = lr.widthMultiplier;
    }

    public void InitFrom(Transform ruteParent, LineRenderer border)
    {
        for (int i = 0; i < ruteParent.childCount; i++)
            Register(ruteParent.GetChild(i).GetComponent<LineRenderer>());
        Register(border);
        lastScale = -1;
    }

    void LateUpdate()
    {
        float s = Mathf.Max(transform.lossyScale.x, 1e-5f);
        if (Mathf.Abs(s - lastScale) < 1e-6f && highlighted == lastHi && hovered == lastHov) return;
        lastScale = s; lastHi = highlighted; lastHov = hovered;
        float k = 1f / s;
        foreach (var kv in baseW)
        {
            if (kv.Key == null) continue;
            float f = kv.Key == highlighted ? 2.4f : (kv.Key == hovered ? 1.7f : 1.25f);
            kv.Key.widthMultiplier = kv.Value * f * k;
        }
    }
}
