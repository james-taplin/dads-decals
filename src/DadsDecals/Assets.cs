using System.IO;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>Loads the projector shader from Bundles/dadsdecals (built by build-shaders.ps1).</summary>
    internal static class Assets
    {
        private const string ShaderName = "DadsDecals/Projected";

        private static AssetBundle? bundle;
        public static Shader? DecalShader { get; private set; }

        public static void Load()
        {
            var path = Path.Combine(Path.Combine(Main.Mod.Path, "Bundles"), "dadsdecals");
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Main.Log.Error("Could not load " + path);
                return;
            }
            DecalShader = bundle.LoadAsset<Shader>("Assets/Shaders/DadsDecal.shader");
            if (DecalShader == null || !DecalShader.isSupported)
                Main.Log.Error($"{ShaderName} missing or unsupported on this GPU");
        }

        public static void Unload()
        {
            if (bundle != null) bundle.Unload(unloadAllLoadedObjects: true);
            bundle = null;
            DecalShader = null;
        }
    }
}
