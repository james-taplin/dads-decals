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
        private const int DefaultFontSize = 128;
        private const int MaxPixels = 4096;
        // Resolution steps (pixels per line of text). The font is rasterised at the same size,
        // so a big decal gets genuinely sharper glyphs, not an upscaled small render.
        private static readonly int[] Steps = { 128, 256, 512, 768 };
        private const float PixelsPerMetre = 900f;   // per metre of line height on the car
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
        private static readonly Dictionary<string, (TextSpec spec, string text, int px)> pending = new Dictionary<string, (TextSpec, string, int)>();
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
        public static Material? GetMaterial(TextSpec spec, TrainCar? car, out float aspect, bool allowRender = true, float decalHeight = 0.5f)
        {
            aspect = 1;
            var e = Get(spec, car, allowRender, decalHeight);
            if (e == null) return null;
            aspect = e.Aspect;
            if (e.Material == null && Assets.DecalShader != null)
                e.Material = new Material(Assets.DecalShader) { name = "DadsDecal text", mainTexture = e.Texture };
            return e.Material;
        }

        /// <summary>For GUI previews: cached texture, or null after queueing it to render.</summary>
        public static Texture2D? GetTexture(TextSpec spec, TrainCar? car) => Get(spec, car, allowRender: false, decalHeight: 0)?.Texture;

        /// <summary>Pixels per line for text on a decal this tall (metres): the smallest step that's sharp enough.</summary>
        private static int ResolutionFor(float decalHeight, int lines)
        {
            var wanted = decalHeight / Mathf.Max(1, lines) * PixelsPerMetre;
            foreach (var s in Steps)
                if (s >= wanted) return s;
            return Steps[Steps.Length - 1];
        }

        /// <summary>Renders queued GUI requests. Called every frame from Interaction.Update.</summary>
        public static void RenderPending()
        {
            if (pending.Count == 0) return;
            var first = pending.First();
            pending.Remove(first.Key);
            if (!cache.ContainsKey(first.Key)) Store(first.Key, first.Value.text, first.Value.spec, first.Value.px);
        }

        private static Entry? Get(TextSpec spec, TrainCar? car, bool allowRender, float decalHeight)
        {
            var text = Resolve(spec.Text, car);
            if (string.IsNullOrWhiteSpace(text)) return null;
            var px = ResolutionFor(decalHeight, text.Split('\n').Length);
            var key = string.Join("|", text, spec.Font, spec.Bold, spec.Italic, Hex(spec.Color), Hex(spec.OutlineColor), spec.Outline.ToString("0.###"), spec.Align, px);
            if (cache.TryGetValue(key, out var e))
            {
                e.LastUsed = Time.unscaledTime;
                return e;
            }
            if (!allowRender)
            {
                pending[key] = (spec.Clone(), text, px);
                return null;
            }
            return Store(key, text, spec, px);
        }

        private static Entry? Store(string key, string text, TextSpec spec, int px)
        {
            Entry? e;
            try
            {
                e = Render(text, spec, px);
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
            f = Font.CreateDynamicFontFromOSFont(name, DefaultFontSize);
            if (f != null) fonts[name] = f;
            return f;
        }

        private static Entry? Render(string text, TextSpec spec, int pxPerLine)
        {
            var font = GetFont(spec.Font) ?? GetFont("Arial");
            if (font == null || Assets.TextShader == null) return null;
            textMaterial ??= new Material(Assets.TextShader);

            var style = spec.Bold && spec.Italic ? FontStyle.BoldAndItalic : spec.Bold ? FontStyle.Bold : spec.Italic ? FontStyle.Italic : FontStyle.Normal;
            font.RequestCharactersInTexture(text, pxPerLine, style);
            textMaterial.mainTexture = font.material.mainTexture;

            var root = new GameObject("DadsDecals_TextStudio");
            try
            {
                root.transform.position = StudioPosition;
                var main = MakeText(root.transform, font, text, spec, style, pxPerLine, ToColor(spec.Color), Vector3.zero);
                var bounds = main.GetComponent<MeshRenderer>().bounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) return null;

                var lineHeight = bounds.size.y / Mathf.Max(1, text.Split('\n').Length);
                var outline = Mathf.Clamp(spec.Outline, 0, 0.15f) * lineHeight;
                if (outline > 0)
                {
                    // 16 copies around the text in the outline colour, behind the main text
                    // (8 left visible bumps on thick outlines).
                    for (var i = 0; i < 16; i++)
                    {
                        var a = i * Mathf.PI / 8;
                        MakeText(root.transform, font, text, spec, style, pxPerLine, ToColor(spec.OutlineColor),
                            new Vector3(Mathf.Cos(a) * outline, Mathf.Sin(a) * outline, 0.1f));
                    }
                }

                // The renderer's bounds don't match where the glyphs are actually drawn: they sit too
                // high and miss swashes and overhangs, which clipped descenders and script fonts. So
                // render a generous canvas around them and crop to the real ink. If the ink still
                // touches the canvas edge, try once more with twice the margin.
                Color32[]? canvas = null;
                int w = 0, h = 0;
                var scale = 0f;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var marginX = outline + lineHeight * (attempt == 0 ? 0.5f : 1.0f);
                    var marginY = outline + lineHeight * (attempt == 0 ? 0.75f : 1.5f);
                    var worldW = bounds.size.x + marginX * 2;
                    var worldH = bounds.size.y + marginY * 2;
                    scale = Mathf.Min(pxPerLine / lineHeight, MaxPixels / worldW, MaxPixels / worldH);
                    w = Mathf.Max(8, Mathf.RoundToInt(worldW * scale));
                    h = Mathf.Max(8, Mathf.RoundToInt(worldH * scale));
                    canvas = RenderCanvas(root.transform, bounds.center, worldH, w, h);
                    if (!InkTouchesEdge(canvas, w, h)) break;
                }

                if (!FindInk(canvas!, w, h, out var x0, out var y0, out var x1, out var y1)) return null;
                var pad = Mathf.Max(2, Mathf.RoundToInt(lineHeight * scale * 0.04f));
                x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad);
                x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
                var cw = x1 - x0 + 1;
                var ch = y1 - y0 + 1;

                // Crop, and un-premultiply: the blend leaves colour premultiplied by coverage, and
                // decals expect straight alpha.
                var px = new Color32[cw * ch];
                for (var y = 0; y < ch; y++)
                for (var x = 0; x < cw; x++)
                {
                    var c = canvas![(y0 + y) * w + x0 + x];
                    var a = c.a;
                    if (a != 0 && a != 255)
                    {
                        c.r = (byte)Mathf.Min(255, c.r * 255 / a);
                        c.g = (byte)Mathf.Min(255, c.g * 255 / a);
                        c.b = (byte)Mathf.Min(255, c.b * 255 / a);
                    }
                    px[y * cw + x] = c;
                }
                var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, mipChain: true);
                tex.SetPixels32(px);
                tex.name = "DadsDecal text " + text;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 4;
                tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                return new Entry { Texture = tex, Aspect = (float)cw / ch };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Renders the studio (text on layer 31) to a w x h canvas centred on <paramref name="centre"/>.</summary>
        private static Color32[] RenderCanvas(Transform root, Vector3 centre, float worldH, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var camGo = new GameObject("cam");
            try
            {
                camGo.transform.SetParent(root, false);
                camGo.transform.position = centre - Vector3.forward * 10;
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
                cam.targetTexture = null;

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var read = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, recalculateMipMaps: false);
                RenderTexture.active = prev;
                var px = read.GetPixels32();
                UnityEngine.Object.DestroyImmediate(read);
                return px;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private const byte InkAlpha = 4;   // faint antialiasing below this is ignored when cropping

        private static bool FindInk(Color32[] px, int w, int h, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = w; y0 = h; x1 = -1; y1 = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (px[y * w + x].a < InkAlpha) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            return x1 >= 0;
        }

        private static bool InkTouchesEdge(Color32[] px, int w, int h)
        {
            for (var x = 0; x < w; x++)
                if (px[x].a >= InkAlpha || px[(h - 1) * w + x].a >= InkAlpha) return true;
            for (var y = 0; y < h; y++)
                if (px[y * w].a >= InkAlpha || px[y * w + w - 1].a >= InkAlpha) return true;
            return false;
        }

        private static GameObject MakeText(Transform parent, Font font, string text, TextSpec spec, FontStyle style, int fontSize, Color color, Vector3 offset)
        {
            var go = new GameObject("text") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = fontSize;
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
