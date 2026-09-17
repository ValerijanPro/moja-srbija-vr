// Mini avatar koji se stalno vozi po selektovanoj ruti.
// Ako postoje sprite kadrovi u StreamingAssets/avatar/frame_*.png koristi njih,
// inace svetleca kapsula.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MiniRunner : MonoBehaviour
{
    public static MiniRunner I;
    public float worldSpeed = 0.05f;     // m/s u svetu, nezavisno od zuma
    public float worldSize = 0.035f;

    Transform vis;
    Renderer visRend;
    readonly List<Texture2D> bikeFrames = new List<Texture2D>();   // frame_*.png i ostalo
    readonly List<Texture2D> runFrames = new List<Texture2D>();    // run*.png
    List<Texture2D> frames = new List<Texture2D>();
    float frameT; int frameIdx;
    LineRenderer path;
    float[] cum; float total; float d;

    void Awake()
    {
        I = this;
    }

    System.Collections.IEnumerator Start()
    {
        yield return RuntimeData.Prepare();
        var dir = System.IO.Path.Combine(RuntimeData.Root, "avatar");
        if (System.IO.Directory.Exists(dir))
        {
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.png").OrderBy(s => s))
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(System.IO.File.ReadAllBytes(f));
                if (System.IO.Path.GetFileName(f).StartsWith("run")) runFrames.Add(t);
                else bikeFrames.Add(t);
            }
        }
        frames = bikeFrames.Count > 0 ? bikeFrames : runFrames;
        Debug.Log($"[MojaSrbija] Avatar kadrovi: bicikl {bikeFrames.Count}, trcanje {runFrames.Count}");
        if (frames.Count > 0)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = "MiniJa";
            var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = frames[0] };
            q.GetComponent<Renderer>().material = m;
            vis = q.transform;
            visRend = q.GetComponent<Renderer>();
        }
        else
        {
            // placeholder: mala svetleca tacka sa repom
            worldSize = 0.014f;
            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(c.GetComponent<Collider>());
            c.name = "MiniJa";
            var sh = Shader.Find("MojaSrbija/Additive");
            var mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"))
            { color = new Color(1f, 0.55f, 0.15f) };
            c.GetComponent<Renderer>().material = mat;
            var tr = c.AddComponent<TrailRenderer>();
            tr.time = 1.4f;
            tr.minVertexDistance = 0.001f;
            tr.material = mat;
            tr.startColor = new Color(1f, 0.45f, 0.1f, 0.8f);
            tr.endColor = new Color(1f, 0.45f, 0.1f, 0f);
            trail = tr;
            vis = c.transform;
        }
        vis.gameObject.SetActive(false);
    }

    TrailRenderer trail;

    public void Follow(LineRenderer lr, string sport = null)
    {
        // izbor seta kadrova po sportu (trkac za Run, biciklista za ostalo)
        if (sport != null && visRend != null)
        {
            var wanted = sport == "Run" && runFrames.Count > 0 ? runFrames
                       : bikeFrames.Count > 0 ? bikeFrames : runFrames;
            if (wanted.Count > 0 && frames != wanted)
            {
                frames = wanted;
                frameIdx = 0;
                visRend.material.mainTexture = frames[0];
            }
        }
        path = lr;
        int n = lr.positionCount;
        cum = new float[n];
        for (int i = 1; i < n; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(lr.GetPosition(i), lr.GetPosition(i - 1));
        total = cum[n - 1];
        d = 0;
        if (vis != null) vis.gameObject.SetActive(total > 0.0001f);
        if (trail != null) trail.Clear();
    }

    public void Stop()
    {
        path = null;
        if (vis != null) vis.gameObject.SetActive(false);
    }

    Vector3 PointAt(float dist)
    {
        int i = 1;
        while (i < cum.Length - 1 && cum[i] < dist) i++;
        float seg = Mathf.Max(cum[i] - cum[i - 1], 1e-6f);
        float t = (dist - cum[i - 1]) / seg;
        return Vector3.Lerp(path.GetPosition(i - 1), path.GetPosition(i), t);
    }

    float restT;
    bool flipState;
    [Tooltip("Ukljuci ako biciklista gleda suprotno od smera kretanja")]
    public bool invertFacing;

    void Update()
    {
        if (path == null || vis == null || total <= 0.0001f) return;
        float s = Mathf.Max(path.transform.lossyScale.x, 1e-5f);
        if (restT > 0) restT -= Time.deltaTime;
        else
        {
            d += worldSpeed / s * Time.deltaTime;
            if (d >= total) { d = 0; restT = 0.7f; }   // kratka pauza na startu petlje
        }

        var wp = path.transform.TransformPoint(PointAt(d));
        vis.position = wp + Vector3.up * worldSize * 0.45f;
        vis.localScale = Vector3.one * worldSize;
        if (trail != null)
        {
            trail.startWidth = worldSize * 0.5f;
            trail.endWidth = 0f;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            vis.rotation = Quaternion.LookRotation(vis.position - cam.transform.position);
            // sprite gleda u smeru kretanja; histereza sprecava treperenje
            // kad se krece pravo ka/od posmatraca
            if (visRend != null)
            {
                var ahead = path.transform.TransformPoint(PointAt(Mathf.Min(d + total * 0.01f, total)));
                var moveDir = (ahead - wp).normalized;
                float side = Vector3.Dot(moveDir, cam.transform.right);
                if (Mathf.Abs(side) > 0.25f) flipState = side > 0;
                bool flip = flipState ^ invertFacing;
                visRend.material.mainTextureScale = new Vector2(flip ? -1 : 1, 1);
                visRend.material.mainTextureOffset = new Vector2(flip ? 1 : 0, 0);
            }
        }

        if (frames.Count > 1 && visRend != null)
        {
            frameT += Time.deltaTime;
            if (frameT > 0.12f)
            {
                frameT = 0;
                frameIdx = (frameIdx + 1) % frames.Count;
                visRend.material.mainTexture = frames[frameIdx];
            }
        }
    }
}
