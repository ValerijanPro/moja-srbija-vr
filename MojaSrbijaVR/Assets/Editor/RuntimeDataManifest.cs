using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class RuntimeDataManifest : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        const string root = "Assets/StreamingAssets";
        var required = new[] { "border.txt", "routes.txt", "routes_meta.txt", "photos.txt" };
        foreach (var file in required)
            if (!File.Exists(Path.Combine(root, file))) throw new BuildFailedException("Nedostaje " + file);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => p.Substring(root.Length + 1).Replace('\\', '/'))
            .Where(p => !p.EndsWith(".meta") && p != "runtime_files.txt" &&
                (p.EndsWith(".txt") || p.StartsWith("avatar/") || p.StartsWith("music/")))
            .OrderBy(p => p).ToArray();
        File.WriteAllLines(Path.Combine(root, "runtime_files.txt"), files);
        AssetDatabase.ImportAsset(root + "/runtime_files.txt");
    }
}
