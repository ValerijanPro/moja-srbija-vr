// Poster mod: taster P na tastaturi snima 4K render makete iz lepog ugla
// na Desktop (za izvestaj / zid u stanu).
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PosterMode : MonoBehaviour
{
    public int width = 3840, height = 2160;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();

        // sakrij UI elemente
        var panel = transform.Find("RutaPanel");
        var nisan = GameObject.Find("Nisan");
        bool panelWas = panel != null && panel.gameObject.activeSelf;
        if (panel != null) panel.gameObject.SetActive(false);
        if (nisan != null) nisan.SetActive(false);

        // kamera: 3/4 pogled sa strane korisnika
        var bounds = GetComponent<BoxCollider>();
        Vector3 center = transform.TransformPoint(bounds != null ? bounds.center : Vector3.zero);
        var head = Camera.main != null ? Camera.main.transform.position : center + new Vector3(0, 0.6f, -1f);
        Vector3 dir = (head - center).normalized;
        dir.y = 0.55f; dir.Normalize();
        float dist = 1.1f * Mathf.Max(transform.lossyScale.x, 1f) * 0.9f + 0.6f;

        var go = new GameObject("PosterCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = center + dir * dist;
        cam.transform.LookAt(center);
        cam.fieldOfView = 42;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Camera.main != null ? Camera.main.backgroundColor : Color.black;

        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Destroy(rt); Destroy(go);

        string path = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
            $"MojaSrbijaPoster_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log("<color=lime>[MojaSrbija] Poster snimljen: " + path + "</color>");
        if (SfxManager.I != null) SfxManager.I.Click();

        if (panel != null) panel.gameObject.SetActive(panelWas);
        if (nisan != null) nisan.SetActive(true);
    }
}
