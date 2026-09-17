using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Android StreamingAssets live inside the APK, not in a normal directory.
// Extract the build manifest once per launch before any synchronous data reader runs.
public static class RuntimeData
{
    public static string Root => Application.platform == RuntimePlatform.Android
        ? Path.Combine(Application.persistentDataPath, "RouteData") : Application.streamingAssetsPath;
    public static bool Ready { get; private set; }
    public static string Error { get; private set; }
    static bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Ready = loading = false; Error = null; }

    public static IEnumerator Prepare()
    {
        if (Ready || Error != null) yield break;
        if (loading) { while (loading) yield return null; yield break; }
        loading = true;
        try
        {
            if (Application.platform != RuntimePlatform.Android) { Ready = true; yield break; }
            string source = Application.streamingAssetsPath.TrimEnd('/');
            string manifest;
            using (var request = UnityWebRequest.Get(source + "/runtime_files.txt"))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { Error = "Nedostaje spisak podataka u APK-u: " + request.error; yield break; }
                manifest = request.downloadHandler.text;
            }
            if (!MakeDirectory(Root)) yield break;
            foreach (string raw in manifest.Split('\n'))
            {
                string relative = raw.Trim();
                if (relative.Length == 0) continue;
                if (relative.Contains("..") || Path.IsPathRooted(relative))
                { Error = "Neispravna putanja podataka."; yield break; }
                using (var request = UnityWebRequest.Get(source + "/" + relative))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { Error = "Podaci nisu ucitani: " + relative + " (" + request.error + ")"; yield break; }
                    string destination = Path.Combine(Root, relative);
                    if (!MakeDirectory(Path.GetDirectoryName(destination))) yield break;
                    try { File.WriteAllBytes(destination, request.downloadHandler.data); }
                    catch (Exception exception) { Error = exception.Message; }
                    if (Error != null) yield break;
                }
            }
            Ready = true;
        }
        finally
        {
            loading = false;
            if (Error != null) Debug.LogError("[MojaSrbija] " + Error);
        }
    }

    static bool MakeDirectory(string path)
    {
        try { Directory.CreateDirectory(path); return true; }
        catch (Exception exception) { Error = exception.Message; return false; }
    }
}
