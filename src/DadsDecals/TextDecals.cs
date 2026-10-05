using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>
    /// Turns text decals into textures: legacy TextMesh with any Windows font, drawn once by an
    /// off-screen orthographic camera into a RenderTexture using our own TextRender shader, then
    /// copied to a mipmapped Texture2D. Results are cached by their settings and resolved text.
    /// </summary>
    internal static class TextDecals
    {
        private const int Layer = 31;
        private const int FontSize = 128;
        private const int PixelsPerLine = 256;
        private const int MaxPixels = 2048;
        private static readonly Vector3 StudioPosition = new Vector3(0, -5000, 0);

        private sealed class Entry
        {
            public Texture2D Texture = null!;
            public Material? Material;
            public float Aspect;
            public float LastUsed;
        }

        private const int MaxCached = 48;
        private static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        // Requests from the GUI: rendering with a camera mid-OnGUI is unsafe, so they wait for Update.
        private static readonly Dictionary<string, (TextSpec spec, string text)> pending = new Dictionary<string, (TextSpec, string)>();
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
        private static string[]? fontNames;
        private static Material? textMaterial;

        public static string[] FontNames =>
            fontNames ??= Font.GetOSInstalledFontNames().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray();

        // ---- tokens --------------------------------------------------------------------------

        private static readonly Regex Digits = new Regex(@"(\d+)(?!.*\d)", RegexOptions.Compiled);

        /// <summary>Replaces {id}, {num}, {n} and {type} with this car's values.</summary>
        public static string Resolve(string text, TrainCar? car)
        {
            if (text.IndexOf('{') < 0) return text;
            var id = car != null && car.logicCar != null ? car.ID : "L-000";
            var m = Digits.Match(id);
            var num = m.Success ? m.Groups[1].Value : id;
            var n = num.TrimStart('0');
            if (n.Length == 0) n = "0";
            var type = car != null && car.carLivery != null ? car.carLivery.id : "";
            return text.Replace("{id}", id).Replace("{num}", num).Replace("{n}", n).Replace("{type}", type);
        }

        public static bool HasTokens(string text) => text.IndexOf('{') >= 0;

        // ---- materials -----------------------------------------------------------------------

        /// <summary>
        /// Material for this text decal on this car, or null if not available (yet).
        /// With <paramref name="allowRender"/> false (GUI code) a missing texture is queued for <see cref="RenderPending"/>.
        /// </summary>
        public static Material? GetMaterial(TextSpec spec, TrainCar? car, out float aspect, bool allowRender = true)
        {
            aspect = 1;
            var e = Get(spec, car, allowRender);
            if (e == null) return null;
            aspect = e.Aspect;
            if (e.Material == null && Assets.DecalShader != null)
                e.Material = new Material(Assets.DecalShader) { name = "DadsDecal text", mainTexture = e.Texture };
            return e.Material;
        }

        /// <summary>For GUI previews: cached texture, or null after queueing it to render.</summary>
        public static Texture2D? GetTexture(TextSpec spec, TrainCar? car) => Get(spec, car, allowRender: false)?.Texture;

        /// <summary>Renders queued GUI requests. Called every frame from Interaction.Update.</summary>
        public static void RenderPending()
        {
            if (pending.Count == 0) return;
            var first = pending.First();
            pending.Remove(first.Key);
            if (!cache.ContainsKey(first.Key)) Store(first.Key, first.Value.text, first.Value.spec);
        }

        private static Entry? Get(TextSpec spec, TrainCar? car, bool allowRender)
        {
            var text = Resolve(spec.Text, car);
            if (string.IsNullOrWhiteSpace(text)) return null;
            var key = string.Join("|", text, spec.Font, spec.Bold, spec.Italic, Hex(spec.Color), Hex(spec.OutlineColor), spec.Outline.ToString("0.###"), spec.Align);
            if (cache.TryGetValue(key, out var e))
            {
                e.LastUsed = Time.unscaledTime;
                return e;
            }
            if (!allowRender)
            {
                pending[key] = (spec.Clone(), text);
                return null;
            }
            return Store(key, text, spec);
        }

        private static Entry? Store(string key, string text, TextSpec spec)
        {
            Entry? e;
            try
            {
                e = Render(text, spec);
            }
            catch (Exception ex)
            {
                Main.Log.LogException($"Rendering text \"{text}\"", ex);
                e = null;
            }
            if (e == null) return null;
            e.LastUsed = Time.unscaledTime;
            cache[key] = e;
            // Typing makes a texture per keystroke; drop the least recently used beyond the cap.
            while (cache.Count > MaxCached)
            {
                var oldest = cache.OrderBy(kv => kv.Value.LastUsed).First();
                if (oldest.Value.Material != null) UnityEngine.Object.Destroy(oldest.Value.Material);
                if (oldest.Value.Texture != null) UnityEngine.Object.Destroy(oldest.Value.Texture);
                cache.Remove(oldest.Key);
            }
            return e;
        }

        private static string Hex(float[] c) => ColorUtility.ToHtmlStringRGBA(new Color(c[0], c[1], c[2], c[3]));

        public static void ClearCache()
        {
            foreach (var e in cache.Values)
            {
                if (e.Material != null) UnityEngine.Object.Destroy(e.Material);
                if (e.Texture != null) UnityEngine.Object.Destroy(e.Texture);
            }
            cache.Clear();
            pending.Clear();
        }

        // ---- rendering -----------------------------------------------------------------------

        private static Font? GetFont(string name)
        {
            if (fonts.TryGetValue(name, out var f) && f != null) return f;
            f = Font.CreateDynamicFontFromOSFont(name, FontSize);
            if (f != null) fonts[name] = f;
            return f;
        }

        private static Entry? Render(string text, TextSpec spec)
        {
            var font = GetFont(spec.Font) ?? GetFont("Arial");
            if (font == null || Assets.TextShader == null) return null;
            textMaterial ??= new Material(Assets.TextShader);

            var style = spec.Bold && spec.Italic ? FontStyle.BoldAndItalic : spec.Bold ? FontStyle.Bold : spec.Italic ? FontStyle.Italic : FontStyle.Normal;
            font.RequestCharactersInTexture(text, FontSize, style);
            textMaterial.mainTexture = font.material.mainTexture;

            var root = new GameObject("DadsDecals_TextStudio");
            try
            {
                root.transform.position = StudioPosition;
                var main = MakeText(root.transform, font, text, spec, style, ToColor(spec.Color), Vector3.zero);
                var bounds = main.GetComponent<MeshRenderer>().bounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) return null;

                var lineHeight = bounds.size.y / Mathf.Max(1, text.Split('\n').Length);
                var outline = Mathf.Clamp(spec.Outline, 0, 0.15f) * lineHeight;
                if (outline > 0)
                {
                    // 8 copies around the text in the outline colour, behind the main text.
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * Mathf.PI / 4;
                        MakeText(root.transform, font, text, spec, style, ToColor(spec.OutlineColor),
                            new Vector3(Mathf.Cos(a) * outline, Mathf.Sin(a) * outline, 0.1f));
                    }
                }

                var pad = outline + lineHeight * 0.05f;
                var worldW = bounds.size.x + pad * 2;
                var worldH = bounds.size.y + pad * 2;
                var scale = Mathf.Min(PixelsPerLine / lineHeight, MaxPixels / worldW, MaxPixels / worldH);
                var w = Mathf.Max(8, Mathf.RoundToInt(worldW * scale));
                var h = Mathf.Max(8, Mathf.RoundToInt(worldH * scale));

                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var camGo = new GameObject("cam");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.position = bounds.center - Vector3.forward * 10;
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = worldH / 2;
                cam.aspect = (float)w / h;
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.renderingPath = RenderingPath.Forward;
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, recalculateMipMaps: false);
                RenderTexture.active = prev;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);

                // The blend leaves colour premultiplied by coverage; decals expect straight alpha.
                var px = tex.GetPixels32();
                for (var i = 0; i < px.Length; i++)
                {
                    var a = px[i].a;
                    if (a == 0 || a == 255) continue;
                    px[i].r = (byte)Mathf.Min(255, px[i].r * 255 / a);
                    px[i].g = (byte)Mathf.Min(255, px[i].g * 255 / a);
                    px[i].b = (byte)Mathf.Min(255, px[i].b * 255 / a);
                }
                tex.SetPixels32(px);
                tex.name = "DadsDecal text " + text;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 4;
                tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                return new Entry { Texture = tex, Aspect = (float)w / h };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject MakeText(Transform parent, Font font, string text, TextSpec spec, FontStyle style, Color color, Vector3 offset)
        {
            var go = new GameObject("text") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = FontSize;
            tm.fontStyle = style;
            tm.characterSize = 0.05f;
            tm.richText = false;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = spec.Align == "left" ? TextAlignment.Left : spec.Align == "right" ? TextAlignment.Right : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            return go;
        }

        private static Color ToColor(float[] c) => new Color(c[0], c[1], c[2], c[3]);
    }
}
