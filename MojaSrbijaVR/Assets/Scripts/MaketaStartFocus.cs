// Pri ulasku: kratko prikaze celu Srbiju pa glatko zumira na region tvojih ruta.
using System.Collections;
using UnityEngine;

public class MaketaStartFocus : MonoBehaviour
{
    public Transform ruteParent;
    public float targetWidth = 0.95f;   // koliko siroko (m) da klaster stane pred korisnika
    public Vector3 focusPoint = new Vector3(0, 1.02f, 0.72f);

    IEnumerator Start()
    {
        if (GetComponent<RoutePicker>() != null) yield break;
        yield return null;
        if (ruteParent == null || ruteParent.childCount == 0) yield break;

        var min = new Vector3(1e9f, 0, 1e9f);
        var max = new Vector3(-1e9f, 0, -1e9f);
        for (int i = 0; i < ruteParent.childCount; i++)
        {
            var lr = ruteParent.GetChild(i).GetComponent<LineRenderer>();
            if (lr == null) continue;
            for (int k = 0; k < lr.positionCount; k += 3)
            {
                var p = lr.GetPosition(k);
                min.x = Mathf.Min(min.x, p.x); min.z = Mathf.Min(min.z, p.z);
                max.x = Mathf.Max(max.x, p.x); max.z = Mathf.Max(max.z, p.z);
            }
        }
        // odseci ekstreme: fokus na centralnih ~90% (klaster, ne outlieri)
        var centerLocal = (min + max) * 0.5f;
        float width = Mathf.Max(max.x - min.x, max.z - min.z) * 0.75f;
        if (width < 0.01f) yield break;
        float k2 = Mathf.Min(targetWidth / width, 40f);

        yield return new WaitForSeconds(0.8f);   // kratak pogled na celu Srbiju

        var s0 = transform.localScale;
        var s1 = s0 * k2;
        var r0 = transform.rotation;
        var r1 = Quaternion.Euler(-32f, 0, 0) * r0;   // nagni ka korisniku kao crtaca tabla
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 2.0f;
            float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            transform.localScale = Vector3.Lerp(s0, s1, e);
            transform.rotation = Quaternion.Slerp(r0, r1, e);
            var cw = transform.TransformPoint(centerLocal);
            transform.position += focusPoint - cw;
            yield return null;
        }
    }
}
