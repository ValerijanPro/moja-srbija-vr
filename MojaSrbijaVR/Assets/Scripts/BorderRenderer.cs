// Svetleca granica Srbije na maketi.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

public class BorderRenderer : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public float heightMeters = 500f;
    public float lineWidth = 0.004f;

    IEnumerator Start()
    {
        if (georeference == null) georeference = GetComponentInParent<CesiumGeoreference>();
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "border.txt");
        string text;
        if (path.Contains("://"))
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                text = req.downloadHandler.text;
            }
        }
        else text = System.IO.File.ReadAllText(path);

        var inv = CultureInfo.InvariantCulture;
        var pts = text.Trim().Split(';');
        var go = new GameObject("granica");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;   // lokalno: granica prati maketu
        lr.loop = true;
        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.color = new Color(1f, 0.47f, 0.1f);
        lr.material = m;

        var arr = new List<Vector3>(pts.Length);
        foreach (var p in pts)
        {
            var c = p.Split(',');
            double lon = double.Parse(c[0], inv);
            double lat = double.Parse(c[1], inv);
            double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(lon, lat, heightMeters));
            double3 u = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            var world = new Vector3((float)u.x, (float)u.y, (float)u.z);
            arr.Add(go.transform.InverseTransformPoint(world));
        }
        lr.positionCount = arr.Count;
        lr.SetPositions(arr.ToArray());
        Debug.Log("[MojaSrbija] Granica nacrtana.");
    }
}
