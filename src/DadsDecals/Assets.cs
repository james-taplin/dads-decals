using System.IO;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>Loads our shaders from Bundles/dadsdecals (built by build-shaders.ps1).</summary>
    internal static class Assets
    {
        private static AssetBundle? bundle;
        public static Shader? DecalShader { get; private set; }
        public static Shader? TextShader { get; private set; }

        public static void Load()
        {
            var path = Path.Combine(Path.Combine(Main.Mod.Path, "Bundles"), "dadsdecals");
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Main.Log.Error("Could not load " + path);
                return;
            }
            DecalShader = LoadShader("Assets/Shaders/DadsDecal.shader");
            TextShader = LoadShader("Assets/Shaders/DadsTextRender.shader");
        }

        private static Shader? LoadShader(string asset)
        {
            var s = bundle!.LoadAsset<Shader>(asset);
            if (s == null || !s.isSupported)
            {
                Main.Log.Error($"{asset} missing or unsupported on this GPU");
                return null;
            }
            return s;
        }

        public static void Unload()
        {
            if (bundle != null) bundle.Unload(unloadAllLoadedObjects: true);
            bundle = null;
            DecalShader = null;
            TextShader = null;
        }

        /// <summary>Material and image aspect (width / height) for a decal, image or text.</summary>
        public static Material? MaterialFor(DecalPlacement d, TrainCar? car, out float aspect, bool allowRender = true)
        {
            aspect = 1;
            if (d.Kind == DecalKind.Text)
                return d.Text != null ? TextDecals.GetMaterial(d.Text, car, out aspect, allowRender, d.Size[1]) : null;
            if (!Main.Library.TryGet(d.Image, out var image)) return null;
            aspect = image.Texture != null && image.Texture.height > 0 ? (float)image.Texture.width / image.Texture.height : 1;
            return image.Material;
        }
    }
}
