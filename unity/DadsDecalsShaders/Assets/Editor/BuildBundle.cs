using System.IO;
using UnityEditor;
using UnityEngine;

// Run from the command line:
//   Unity.exe -quit -projectPath <this project> -executeMethod BuildBundle.Build -logFile <log>
// Output: src/DadsDecals/Bundles/dadsdecals (copied into the mod folder on deploy).
public static class BuildBundle
{
    private const string OutDir = "../../src/DadsDecals/Bundles";

    [MenuItem("Dad's Decals/Build bundle")]
    public static void Build()
    {
        Directory.CreateDirectory(OutDir);
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "dadsdecals",
                assetNames = new[] { "Assets/Shaders/DadsDecal.shader", "Assets/Shaders/DadsTextRender.shader" },
            },
        };
        var manifest = BuildPipeline.BuildAssetBundles(OutDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null)
        {
            Debug.LogError("DADSDECALS_BUILD_FAILED");
            EditorApplication.Exit(1);
            return;
        }
        // Unity also writes a folder-named manifest bundle we don't need.
        foreach (var junk in new[] { "Bundles", "Bundles.manifest" })
        {
            var p = Path.Combine(OutDir, junk);
            if (File.Exists(p)) File.Delete(p);
        }
        Debug.Log("DADSDECALS_BUILD_OK");
    }
}
