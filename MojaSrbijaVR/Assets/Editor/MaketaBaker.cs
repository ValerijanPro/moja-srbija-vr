// "Pekara" makete v2: osnovna tekstura z11 (cela Srbija) + high-res patch z14
// oko regiona sa rutama; paralelno skidanje; ispravna import podesavanja.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public static class MaketaBaker
{
    const float SIZE_NS = 1.25f;
    const float EXAG = 5f;
    const float BASE_Y = -0.045f;
    const int GRID_X = 400, GRID_Y = 440;
    const int HZOOM = 9, SATZOOM = 11;
    const int PATCH_HZOOM = 12, PATCH_SATZOOM = 13;

    static readonly HttpClient http = new HttpClient();
    static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    static double minLon, maxLon, minLat, maxLat, cLon, cLat;
    static float F;

    // kes osnovne teksture za poravnanje boje patcha
    static Color32[] basePix; static int baseW, baseH;
    static double bLo0, bLo1, bLa0, bLa1;

    static float LocalX(double lon) => (float)((lon - cLon) * 111320.0 * Math.Cos(cLat * Math.PI / 180) * F);
    static float LocalZ(double lat) => (float)((lat - cLat) * 111320.0 * F);

    [MenuItem("MojaSrbija/8 - Ispeci maketu (bez Cesiuma)")]
    public static void Bake()
    {
        try
        {
            var border = LoadBorder();
            minLon = 999; maxLon = -999; minLat = 999; maxLat = -999;
            foreach (var p in border)
            {
                minLon = Math.Min(minLon, p.x); maxLon = Math.Max(maxLon, p.x);
                minLat = Math.Min(minLat, p.y); maxLat = Math.Max(maxLat, p.y);
            }
            minLon -= 0.02; maxLon += 0.02; minLat -= 0.02; maxLat += 0.02;
            cLon = (minLon + maxLon) / 2; cLat = (minLat + maxLat) / 2;
            F = SIZE_NS / (float)((maxLat - minLat) * 111320.0);

            System.IO.Directory.CreateDirectory("Assets/Maketa");

            var H = DownloadHeights(minLon, maxLon, minLat, maxLat, HZOOM, "Visine (Srbija)");
            string satPath = BakeSatTexture(minLon, maxLon, minLat, maxLat, SATZOOM,
                "Assets/Maketa/SrbijaSat.jpg", "Satelit (Srbija, z11)", false);
            var mesh = BuildMesh(border, H, GRID_X, GRID_Y,
                minLon, maxLon, minLat, maxLat, true);
            AssetDatabase.CreateAsset(mesh, "Assets/Maketa/SrbijaMesh.asset");

            // high-res patch oko regiona sa rutama
            var (pLon0, pLon1, pLat0, pLat1) = PatchBBox();
            var H2 = DownloadHeights(pLon0, pLon1, pLat0, pLat1, PATCH_HZOOM, "Visine (patch)");
            string patchSatPath = BakeSatTexture(pLon0, pLon1, pLat0, pLat1, PATCH_SATZOOM,
                "Assets/Maketa/PatchSat.png", "Satelit (patch, z13)", true);
            var patchMesh = BuildMesh(border, H2, 320, 280, pLon0, pLon1, pLat0, pLat1, false);
            AssetDatabase.CreateAsset(patchMesh, "Assets/Maketa/PatchMesh.asset");

            var satTex = AssetDatabase.LoadAssetAtPath<Texture2D>(satPath);
            var patchTex = AssetDatabase.LoadAssetAtPath<Texture2D>(patchSatPath);
            BuildSceneObjects(mesh, satTex, patchMesh, patchTex, H, border, false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("<color=lime>[MojaSrbija] Maketa v2 ispecena (z11 + z14 patch). Play!</color>");
        }
        catch (Exception e) { Debug.LogError("[MojaSrbija] Bake pukao: " + e); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    public static bool RestoreFromExistingAssets()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Maketa/SrbijaMesh.asset");
        var patchMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Maketa/PatchMesh.asset");
        var sat = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Maketa/SrbijaSat.jpg");
        var patch = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Maketa/PatchSat.png");
        if (mesh == null || patchMesh == null || sat == null || patch == null)
        {
            Debug.LogError("[MojaSrbija] Nedostaju Assets/Maketa podaci; pokreni korak 8.");
            return false;
        }
        var border = LoadBorder();
        minLon = 999; maxLon = -999; minLat = 999; maxLat = -999;
        foreach (var p in border)
        {
            minLon = Math.Min(minLon, p.x); maxLon = Math.Max(maxLon, p.x);
            minLat = Math.Min(minLat, p.y); maxLat = Math.Max(maxLat, p.y);
        }
        minLon -= 0.02; maxLon += 0.02; minLat -= 0.02; maxLat += 0.02;
        cLon = (minLon + maxLon) / 2; cLat = (minLat + maxLat) / 2;
        F = SIZE_NS / (float)((maxLat - minLat) * 111320.0);
        BuildSceneObjects(mesh, sat, patchMesh, patch, null, border, true);
        foreach (var name in new[] { "Sto", "Kocka_1", "Kocka_2", "Kocka_3" })
        {
            var demo = GameObject.Find(name);
            if (demo != null) UnityEngine.Object.DestroyImmediate(demo);
        }
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=lime>[MojaSrbija] Maketa vracena iz postojecih lokalnih asseta.</color>");
        return true;
    }

    /* ---------------- pomocno ---------------- */

    static List<Vector2> LoadBorder()
    {
        var txt = System.IO.File.ReadAllText("Assets/StreamingAssets/border.txt").Trim();
        return txt.Split(';').Select(p =>
        {
            var c = p.Split(',');
            return new Vector2(float.Parse(c[0], INV), float.Parse(c[1], INV));
        }).ToList();
    }

    static (double, double, double, double) PatchBBox()
    {
        var lons = new List<double>(); var lats = new List<double>();
        foreach (var raw in System.IO.File.ReadAllText("Assets/StreamingAssets/routes.txt").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            foreach (var p in line.Split('|')[1].Split(';'))
            {
                var c = p.Split(',');
                lons.Add(double.Parse(c[0], INV));
                lats.Add(double.Parse(c[1], INV));
            }
        }
        lons.Sort(); lats.Sort();
        double q(List<double> l, double t) => l[(int)(t * (l.Count - 1))];
        double lo0 = q(lons, 0.05) - 0.05, lo1 = q(lons, 0.95) + 0.05;
        double la0 = q(lats, 0.05) - 0.04, la1 = q(lats, 0.95) + 0.04;
        // ogranici velicinu patcha
        double mLon = (lo0 + lo1) / 2, mLat = (la0 + la1) / 2;
        if (lo1 - lo0 > 0.9) { lo0 = mLon - 0.45; lo1 = mLon + 0.45; }
        if (la1 - la0 > 0.7) { la0 = mLat - 0.35; la1 = mLat + 0.35; }
        lo0 = Math.Max(lo0, minLon); lo1 = Math.Min(lo1, maxLon);
        la0 = Math.Max(la0, minLat); la1 = Math.Min(la1, maxLat);
        Debug.Log($"[MojaSrbija] Patch bbox: {lo0:F2},{la0:F2} - {lo1:F2},{la1:F2}");
        return (lo0, lo1, la0, la1);
    }

    static double TileX(double lon, int n) => (lon + 180) / 360 * n;
    static double TileY(double lat, int n)
    {
        double r = lat * Math.PI / 180;
        return (1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * n;
    }

    static Dictionary<(int, int), byte[]> DownloadBatch(Func<int, int, string> url,
        int x0, int x1, int y0, int y1, string label)
    {
        var keys = new List<(int, int)>();
        for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++) keys.Add((tx, ty));
        var result = new Dictionary<(int, int), byte[]>();
        int done = 0;
        foreach (var batch in keys.Select((k, i) => (k, i)).GroupBy(t => t.i / 12))
        {
            var tasks = batch.Select(t => (t.k, task: http.GetByteArrayAsync(url(t.k.Item1, t.k.Item2)))).ToList();
            Task.WaitAll(tasks.Select(t => (Task)t.task).ToArray());
            foreach (var t in tasks) result[t.k] = t.task.Result;
            done += tasks.Count;
            EditorUtility.DisplayProgressBar("Maketa", $"{label}: {done}/{keys.Count}", (float)done / keys.Count);
        }
        return result;
    }

    class HeightGrid
    {
        public float[] h; public int w, hgt, x0, y0, n;
        public float Sample(double lon, double lat)
        {
            double px = TileX(lon, n) * 256 - x0 * 256;
            double py = TileY(lat, n) * 256 - y0 * 256;
            int x = Mathf.Clamp((int)px, 0, w - 2), y = Mathf.Clamp((int)py, 0, hgt - 2);
            float tx = (float)(px - x), ty = (float)(py - y);
            float a = h[y * w + x], b = h[y * w + x + 1], c = h[(y + 1) * w + x], d = h[(y + 1) * w + x + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }

    static HeightGrid DownloadHeights(double lo0, double lo1, double la0, double la1, int zoom, string label)
    {
        int n = 1 << zoom;
        int x0 = (int)TileX(lo0, n), x1 = (int)TileX(lo1, n);
        int y0 = (int)TileY(la1, n), y1 = (int)TileY(la0, n);
        var tiles = DownloadBatch((tx, ty) =>
            $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{zoom}/{tx}/{ty}.png",
            x0, x1, y0, y1, label);
        int tw = x1 - x0 + 1, th = y1 - y0 + 1;
        var grid = new HeightGrid { w = tw * 256, hgt = th * 256, x0 = x0, y0 = y0, n = n };
        grid.h = new float[grid.w * grid.hgt];
        var tex = new Texture2D(2, 2);
        foreach (var kv in tiles)
        {
            tex.LoadImage(kv.Value);
            var px = tex.GetPixels32();
            int ox = (kv.Key.Item1 - x0) * 256, oy = (kv.Key.Item2 - y0) * 256;
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    var c = px[(255 - y) * 256 + x];
                    grid.h[(oy + y) * grid.w + ox + x] = c.r * 256f + c.g + c.b / 256f - 32768f;
                }
        }
        return grid;
    }

    static string BakeSatTexture(double lo0, double lo1, double la0, double la1,
        int zoom, string path, string label, bool isPatch)
    {
        int n = 1 << zoom;
        int x0 = (int)TileX(lo0, n), x1 = (int)TileX(lo1, n);
        int y0 = (int)TileY(la1, n), y1 = (int)TileY(la0, n);
        var tiles = DownloadBatch((tx, ty) =>
            $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{zoom}/{ty}/{tx}",
            x0, x1, y0, y1, label);
        int tw = x1 - x0 + 1, th = y1 - y0 + 1;
        int W = tw * 256, Hm = th * 256;
        var merc = new Color32[(long)W * Hm];
        var tex = new Texture2D(2, 2);
        foreach (var kv in tiles)
        {
            tex.LoadImage(kv.Value);
            var px = tex.GetPixels32();
            int ox = (kv.Key.Item1 - x0) * 256, oy = (kv.Key.Item2 - y0) * 256;
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    merc[(long)(oy + y) * W + ox + x] = px[(255 - y) * 256 + x];
        }
        EditorUtility.DisplayProgressBar("Maketa", label + ": preslikavanje...", 0.97f);
        double aspect = ((la1 - la0) * 111.32) / ((lo1 - lo0) * 111.32 * Math.Cos((la0 + la1) / 2 * Math.PI / 180));
        int Ho = Mathf.Min(8192, (int)(W * aspect));
        var outPix = new Color32[(long)W * Ho];
        for (int y = 0; y < Ho; y++)
        {
            double lat = la1 - (la1 - la0) * y / (Ho - 1);
            int sy = Mathf.Clamp((int)(TileY(lat, n) * 256 - y0 * 256), 0, Hm - 1);
            Array.Copy(merc, (long)sy * W, outPix, (long)(Ho - 1 - y) * W, W); // flip odmah
        }
        if (!isPatch)
        {
            basePix = outPix; baseW = W; baseH = Ho;
            bLo0 = lo0; bLo1 = lo1; bLa0 = la0; bLa1 = la1;
        }
        else if (basePix != null)
        {
            // poravnanje boje sa osnovnom teksturom (prosek po kanalu)
            long pr = 0, pg = 0, pb = 0, pn = 0;
            for (long i = 0; i < outPix.Length; i += 7)
            { pr += outPix[i].r; pg += outPix[i].g; pb += outPix[i].b; pn++; }
            long br = 0, bg = 0, bb = 0, bn = 0;
            int cx0 = (int)((lo0 - bLo0) / (bLo1 - bLo0) * baseW), cx1 = (int)((lo1 - bLo0) / (bLo1 - bLo0) * baseW);
            int cy0 = (int)((la0 - bLa0) / (bLa1 - bLa0) * baseH), cy1 = (int)((la1 - bLa0) / (bLa1 - bLa0) * baseH);
            for (int y = Mathf.Max(cy0, 0); y < Mathf.Min(cy1, baseH); y += 4)
                for (int x = Mathf.Max(cx0, 0); x < Mathf.Min(cx1, baseW); x += 4)
                { var c = basePix[(long)y * baseW + x]; br += c.r; bg += c.g; bb += c.b; bn++; }
            if (bn > 0 && pn > 0)
            {
                float gr = Mathf.Clamp((float)br / bn / ((float)pr / pn), 0.75f, 1.35f);
                float gg = Mathf.Clamp((float)bg / bn / ((float)pg / pn), 0.75f, 1.35f);
                float gb = Mathf.Clamp((float)bb / bn / ((float)pb / pn), 0.75f, 1.35f);
                for (long i = 0; i < outPix.Length; i++)
                {
                    outPix[i].r = (byte)Mathf.Min(255, outPix[i].r * gr);
                    outPix[i].g = (byte)Mathf.Min(255, outPix[i].g * gg);
                    outPix[i].b = (byte)Mathf.Min(255, outPix[i].b * gb);
                }
            }
            // prozirne ivice: patch se utapa u osnovu
            int fw = W / 18, fh = Ho / 18;
            for (int y = 0; y < Ho; y++)
                for (int x = 0; x < W; x++)
                {
                    float a = Mathf.Min(
                        Mathf.Min(x / (float)fw, (W - 1 - x) / (float)fw),
                        Mathf.Min(y / (float)fh, (Ho - 1 - y) / (float)fh));
                    outPix[(long)y * W + x].a = (byte)(Mathf.Clamp01(a) * 255);
                }
        }

        var outTex = new Texture2D(W, Ho, TextureFormat.RGBA32, false);
        outTex.SetPixels32(outPix);
        outTex.Apply();
        System.IO.File.WriteAllBytes(path,
            isPatch ? outTex.EncodeToPNG() : outTex.EncodeToJPG(92));
        UnityEngine.Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.maxTextureSize = 8192;                   // BEZ default 2048 smanjenja!
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 16;                         // ostro i pod kosim uglom
        imp.alphaIsTransparency = isPatch;
        imp.SaveAndReimport();
        return path;
    }

    static bool Inside(List<Vector2> poly, double lon, double lat)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > lat) != (poly[j].y > lat) &&
                lon < (poly[j].x - poly[i].x) * (lat - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    static Mesh BuildMesh(List<Vector2> border, HeightGrid H, int NX, int NY,
        double lo0, double lo1, double la0, double la1, bool withSkirt)
    {
        EditorUtility.DisplayProgressBar("Maketa", "Gradim mesh...", 0.98f);
        int NV = (NX + 1) * (NY + 1);
        var verts = new Vector3[NV * 2];
        var uv = new Vector2[NV * 2];
        var inside = new bool[NV];
        for (int j = 0; j <= NY; j++)
            for (int i = 0; i <= NX; i++)
            {
                double lon = lo0 + (lo1 - lo0) * i / NX;
                double lat = la0 + (la1 - la0) * j / NY;
                int vi = j * (NX + 1) + i;
                float y = (H.Sample(lon, lat) * EXAG + 40f) * F;
                verts[vi] = new Vector3(LocalX(lon), y, LocalZ(lat));
                verts[NV + vi] = new Vector3(verts[vi].x, BASE_Y, verts[vi].z);
                uv[vi] = new Vector2((float)((lon - lo0) / (lo1 - lo0)), (float)((lat - la0) / (la1 - la0)));
                uv[NV + vi] = uv[vi];
                inside[vi] = Inside(border, lon, lat);
            }
        var top = new List<int>();
        var dark = new List<int>();
        bool QuadIn(int i, int j) =>
            i >= 0 && j >= 0 && i < NX && j < NY &&
            inside[j * (NX + 1) + i] && inside[j * (NX + 1) + i + 1] &&
            inside[(j + 1) * (NX + 1) + i] && inside[(j + 1) * (NX + 1) + i + 1];
        for (int j = 0; j < NY; j++)
            for (int i = 0; i < NX; i++)
            {
                if (!QuadIn(i, j)) continue;
                int a = j * (NX + 1) + i, b = a + 1, c = a + (NX + 1), d = c + 1;
                top.AddRange(new[] { a, c, b, b, c, d });
                if (!withSkirt) continue;
                dark.AddRange(new[] { NV + a, NV + b, NV + c, NV + b, NV + d, NV + c });
                if (!QuadIn(i - 1, j)) dark.AddRange(new[] { a, NV + a, c, c, NV + a, NV + c });
                if (!QuadIn(i + 1, j)) dark.AddRange(new[] { b, d, NV + b, d, NV + d, NV + b });
                if (!QuadIn(i, j - 1)) dark.AddRange(new[] { a, b, NV + a, b, NV + b, NV + a });
                if (!QuadIn(i, j + 1)) dark.AddRange(new[] { c, NV + c, d, d, NV + c, NV + d });
            }
        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.subMeshCount = withSkirt ? 2 : 1;
        mesh.SetTriangles(top, 0);
        if (withSkirt) mesh.SetTriangles(dark, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void BuildSceneObjects(Mesh mesh, Texture2D satTex, Mesh patchMesh, Texture2D patchTex,
        HeightGrid H, List<Vector2> border, bool reuseAssets)
    {
        foreach (var name in new[] { "MaketaRoot", "MaketaSrbije", "MaketaV2" })
        {
            var old = GameObject.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
        }
        var root = new GameObject("MaketaV2");
        root.transform.position = new Vector3(0, 0.95f, 0.9f);

        var litSh = Shader.Find("Universal Render Pipeline/Lit");
        Material MkLit(Texture2D t, Color c, string file)
        {
            if (reuseAssets)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Maketa/" + file);
                if (existing != null) return existing;
            }
            var m = new Material(litSh);
            if (t != null) m.mainTexture = t; else m.color = c;
            m.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(m, "Assets/Maketa/" + file);
            return m;
        }
        var mTop = MkLit(satTex, Color.white, "TerenMat.mat");
        var mDark = MkLit(null, new Color(0.09f, 0.1f, 0.12f), "StranaMat.mat");
        var mPatch = MkLit(patchTex, Color.white, "PatchMat.mat");

        var terrain = new GameObject("Teren");
        terrain.transform.SetParent(root.transform, false);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterials = new[] { mTop, mDark };
        var terrainCollider = terrain.AddComponent<MeshCollider>();
        terrainCollider.sharedMesh = mesh;

        // patch je providan po ivicama -> URP Lit transparent
        mPatch.SetFloat("_Surface", 1f);
        mPatch.SetOverrideTag("RenderType", "Transparent");
        mPatch.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mPatch.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mPatch.SetInt("_ZWrite", 1);
        mPatch.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mPatch.renderQueue = 2800;

        var patch = new GameObject("TerenPatch");
        patch.transform.SetParent(root.transform, false);
        patch.transform.localPosition = new Vector3(0, 0.0007f, 0);
        patch.AddComponent<MeshFilter>().sharedMesh = patchMesh;
        patch.AddComponent<MeshRenderer>().sharedMaterial = mPatch;

        var addSh = Shader.Find("MojaSrbija/Additive");
        Material LineMat(Color c, string file)
        {
            if (reuseAssets)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Maketa/" + file);
                if (existing != null) return existing;
            }
            var m = new Material(addSh != null ? addSh : Shader.Find("Sprites/Default")) { color = c };
            AssetDatabase.CreateAsset(m, "Assets/Maketa/" + file);
            return m;
        }
        var runM = LineMat(new Color(1f, 0.22f, 0.05f), "RunMat.mat");
        var rideM = LineMat(new Color(1f, 0.55f, 0.10f), "RideMat.mat");
        var othM = LineMat(new Color(1f, 0.4f, 0.08f), "OstaloMat.mat");
        var borM = LineMat(new Color(1f, 0.32f, 0.05f), "GranicaMat.mat");

        var rute = new GameObject("Rute");
        rute.transform.SetParent(root.transform, false);
        var txt = System.IO.File.ReadAllText("Assets/StreamingAssets/routes.txt");
        int rn = 0;
        foreach (var raw in txt.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split('|');
            var go = new GameObject("ruta_" + rn++);
            go.transform.SetParent(rute.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.0022f;
            lr.numCapVertices = 2; lr.numCornerVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.sharedMaterial = parts[0] == "Run" ? runM : (parts[0] == "Ride" ? rideM : othM);
            var pts = parts[1].Split(';');
            var arr = new Vector3[pts.Length];
            for (int k = 0; k < pts.Length; k++)
            {
                var c = pts[k].Split(',');
                double lon = double.Parse(c[0], INV), lat = double.Parse(c[1], INV);
                float x = LocalX(lon), z = LocalZ(lat);
                float y = 0.0032f;
                if (H != null) y += (H.Sample(lon, lat) * EXAG + 40f) * F;
                else if (terrainCollider.Raycast(new Ray(root.transform.TransformPoint(x, 1, z), -root.transform.up), out var hit, 2f))
                    y += root.transform.InverseTransformPoint(hit.point).y;
                arr[k] = new Vector3(x, y, z);
            }
            lr.positionCount = arr.Length;
            lr.SetPositions(arr);
        }

        var gGo = new GameObject("Granica");
        gGo.transform.SetParent(root.transform, false);
        var glr = gGo.AddComponent<LineRenderer>();
        glr.useWorldSpace = false; glr.loop = true;
        glr.widthMultiplier = 0.0045f;
        glr.numCornerVertices = 2;
        glr.shadowCastingMode = ShadowCastingMode.Off;
        glr.sharedMaterial = borM;
        var bArr = new Vector3[border.Count];
        for (int k = 0; k < border.Count; k++)
        {
            float x = LocalX(border[k].x), z = LocalZ(border[k].y), y = 0.003f;
            if (H != null) y += (H.Sample(border[k].x, border[k].y) * EXAG + 90f) * F;
            else if (terrainCollider.Raycast(new Ray(root.transform.TransformPoint(x, 1, z), -root.transform.up), out var hit, 2f))
                y += root.transform.InverseTransformPoint(hit.point).y;
            bArr[k] = new Vector3(x, y, z);
        }
        glr.positionCount = bArr.Length;
        glr.SetPositions(bArr);

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, -0.02f, 0);
        col.size = new Vector3(1.15f, 0.12f, 1.35f);
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Multiple;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.trackScale = true;
        root.AddComponent<XRGeneralGrabTransformer>().allowTwoHandedScaling = true;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.055f);
        }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.72f);
        var pod = GameObject.Find("Pod");
        if (pod != null) pod.GetComponent<Renderer>().sharedMaterial.color = new Color(0.07f, 0.085f, 0.105f);
    }
}
