// Fotografije sa aktivnosti lebde kao pinovi iznad mesta gde su snimljene.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

public class PhotoPins : MonoBehaviour
{
    public float photoWorldSize = 0.04f;   // konstantna velicina bez obzira na zum
    public int maxPins = 5;

    readonly List<Transform> quads = new List<Transform>();
    Transform holder;

    IEnumerator Start()
    {
        yield return RuntimeData.Prepare();
        if (!enabled) yield break;
        var path = System.IO.Path.Combine(RuntimeData.Root, "photopins.txt");
        if (!System.IO.File.Exists(path)) yield break;
        var inv = CultureInfo.InvariantCulture;
        holder = new GameObject("FotoPinovi").transform;
        holder.SetParent(transform, false);

        var shAdd = Shader.Find("MojaSrbija/Additive");
        var stalkMat = new Material(shAdd != null ? shAdd : Shader.Find("Sprites/Default"))
        { color = new Color(1f, 0.6f, 0.25f, 0.8f) };

        int made = 0;
        var seenActivity = new HashSet<string>();
        var placed = new List<Vector2>();
        foreach (var raw in System.IO.File.ReadAllText(path).Trim().Split('\n'))
        {
            if (made >= maxPins) break;
            var c = raw.Trim().Split('\t');
            if (c.Length < 4) continue;
            if (c.Length >= 5 && c[4] == "0") continue;  // samo slike sa pravim GPS-om
            if (!seenActivity.Add(c[0])) continue;   // jedna slika po aktivnosti
            double lat = double.Parse(c[1], inv), lon = double.Parse(c[2], inv);
            string url = c[3];

            float x = ProjSrbija.LocalX(lon), z = ProjSrbija.LocalZ(lat);
            // razmakni preklopljene pinove (spirala)
            int bump = 0;
            while (bump < 30 && placed.Exists(p => Vector2.Distance(p, new Vector2(x, z)) < 0.045f))
            {
                bump++;
                float ang = bump * 2.4f, r = 0.02f * Mathf.Sqrt(bump);
                x = ProjSrbija.LocalX(lon) + Mathf.Cos(ang) * r;
                z = ProjSrbija.LocalZ(lat) + Mathf.Sin(ang) * r;
            }
            placed.Add(new Vector2(x, z));
            // spusti pin na teren - iskljucivo na collider naseg terena, najblizi pogodak
            float baseY = 0.012f;
            var terenT = transform.Find("Teren");
            var terenCol = terenT != null ? terenT.GetComponent<MeshCollider>() : null;
            if (terenCol != null)
            {
                var from = transform.TransformPoint(new Vector3(x, 0.6f, z));
                float best = float.MaxValue;
                foreach (var hg in Physics.RaycastAll(from, -transform.up, 3f))
                    if (hg.collider == terenCol && hg.distance < best)
                    {
                        best = hg.distance;
                        baseY = transform.InverseTransformPoint(hg.point).y + 0.0015f;
                    }
            }
            var pin = new GameObject("pin_" + made);
            pin.transform.SetParent(holder, false);
            pin.transform.localPosition = new Vector3(x, baseY, z);

            float stalkH = 0.045f + (made % 3) * 0.014f;
            var stalk = pin.AddComponent<LineRenderer>();
            stalk.useWorldSpace = false;
            stalk.widthMultiplier = 0.0012f;
            stalk.positionCount = 2;
            stalk.SetPositions(new[] { Vector3.zero, new Vector3(0, stalkH, 0) });
            stalk.sharedMaterial = stalkMat;
            stalk.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "foto";
            quad.transform.SetParent(pin.transform, false);
            quad.transform.localPosition = new Vector3(0, stalkH + 0.008f, 0);
            var mat = new Material(Shader.Find("Sprites/Default"));
            quad.GetComponent<Renderer>().material = mat;
            quad.SetActive(false);   // pokazi tek kad slika stigne
            quads.Add(quad.transform);
            StartCoroutine(LoadPhoto(url, mat, quad.transform));
            made++;
            if (made % 4 == 0) yield return null;
        }
    }

    IEnumerator LoadPhoto(string url, Material mat, Transform quad)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                mat.mainTexture = tex;
                float asp = tex.width / (float)tex.height;
                quad.localScale = new Vector3(asp, 1f, 1f);   // bazni odnos, ukupno skalira LateUpdate
                quad.gameObject.SetActive(true);
            }
        }
    }

    void LateUpdate()
    {
        float s = Mathf.Max(transform.lossyScale.x, 1e-5f);
        float k = photoWorldSize / s;
        var cam = Camera.main;
        foreach (var q in quads)
        {
            if (q == null) continue;
            float asp = q.localScale.x / Mathf.Max(q.localScale.y, 1e-5f);
            q.localScale = new Vector3(asp * k, k, 1f);
            if (cam != null)
            {
                var dir = q.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f) q.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
