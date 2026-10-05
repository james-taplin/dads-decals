using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DadsDecals
{
    public sealed class DecalImage
    {
        /// <summary>Path relative to the Decals folder, with forward slashes. This is what layouts store.</summary>
        public string Key = "";
        public string Category = "";
        public string Name = "";
        public Texture2D Texture = null!;

        private Material? material;

        /// <summary>Projector material for this image, shared by every decal that uses it.</summary>
        public Material? Material
        {
            get
            {
                if (material == null && Assets.DecalShader != null)
                    material = new Material(Assets.DecalShader) { name = "DadsDecal " + Key, mainTexture = Texture };
                return material;
            }
        }

        public void Destroy()
        {
            if (material != null) UnityEngine.Object.Destroy(material);
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
        }
    }

    /// <summary>Loads PNGs from the user's Decals folder. Textures are shared between all decals using them.</summary>
    public sealed class DecalLibrary : IDisposable
    {
        private const int MaxSize = 2048;

        private readonly string root;
        private readonly Dictionary<string, DecalImage> images = new Dictionary<string, DecalImage>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<DecalImage> Images => images.Values;

        public DecalLibrary(string root)
        {
            this.root = root;
        }

        public bool TryGet(string key, out DecalImage image) => images.TryGetValue(key, out image);

        public void Reload()
        {
            Dispose();
            foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var key = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                    if (!tex.LoadImage(File.ReadAllBytes(file)))
                    {
                        UnityEngine.Object.Destroy(tex);
                        Main.Log.Warning($"Could not read {key}");
                        continue;
                    }
                    if (tex.width > MaxSize || tex.height > MaxSize)
                        Main.Log.Warning($"{key} is {tex.width}x{tex.height}; images over {MaxSize}px will be downscaled in a later version");
                    tex.name = key;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.anisoLevel = 4;
                    tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);

                    var slash = key.LastIndexOf('/');
                    images[key] = new DecalImage
                    {
                        Key = key,
                        Category = slash < 0 ? "" : key.Substring(0, slash),
                        Name = Path.GetFileNameWithoutExtension(key),
                        Texture = tex,
                    };
                }
                catch (Exception e)
                {
                    Main.Log.LogException($"Loading {file}", e);
                }
            }
            Main.Log.Log($"Loaded {images.Count} decal image(s) from {root}");
        }

        public void Dispose()
        {
            foreach (var img in images.Values) img.Destroy();
            images.Clear();
        }
    }
}
