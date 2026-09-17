// Crta Strava rute kao svetlece linije preko Cesium terena (maketa Srbije).
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

public class RoutesRenderer : MonoBehaviour
{
    public CesiumGeoreference georeference;
    [Tooltip("Visina linija iznad elipsoida (m) - na maketi je razlika milimetarska")]
    public float heightMeters = 420f;
    [Tooltip("Debljina linije u metrima scene")]
    public float lineWidth = 0.0018f;

    IEnumerator Start()
    {
        if (georeference == null) georeference = GetComponentInParent<CesiumGeoreference>();
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "routes.txt");
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

        var runMat = MakeMat(new Color(1f, 0.30f, 0.11f));
        var rideMat = MakeMat(new Color(1f, 0.69f, 0.23f));
        var otherMat = MakeMat(new Color(1f, 0.54f, 0.16f));
        var inv = CultureInfo.InvariantCulture;
        int n = 0;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split('|');
            var sport = parts[0];
            var pts = parts[1].Split(';');

            var go = new GameObject("ruta_" + n);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;   // lokalno: rute prate maketu kad se hvata/skalira
            lr.widthMultiplier = lineWidth;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = sport == "Run" ? runMat : (sport == "Ride" ? rideMat : otherMat);

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

            n++;
            if (n % 12 == 0) yield return null; // ne gusi frame
        }
        Debug.Log($"[MojaSrbija] Nacrtano {n} ruta.");
    }

    Material MakeMat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.color = c;
        return m;
    }
}
