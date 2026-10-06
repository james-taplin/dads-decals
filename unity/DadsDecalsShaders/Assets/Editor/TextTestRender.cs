using System.IO;
using UnityEditor;
using UnityEngine;

// Offline check of the text-decal pipeline (mirrors TextDecals.Render in the mod):
// OS font -> TextMesh (+16 outline copies) -> off-screen camera with DadsDecals/TextRender on a
// generous canvas -> crop to the ink -> un-premultiply. Writes TestRenders/text<px>.png (cropped)
// and TestRenders/text<px>-canvas.png (the uncropped canvas, to see where the ink fell).
//   Unity.exe -quit -projectPath <this project> -executeMethod TextTestRender.Run -logFile <log>
// Env: DADSDECALS_TEST_TEXT, DADSDECALS_TEST_FONT, DADSDECALS_TEST_PX, DADSDECALS_TEST_OUTLINE (0-0.15).
public static class TextTestRender
{
    private const int Layer = 31;
    private const int MaxPixels = 4096;
    private const byte InkAlpha = 4;

    public static void Run()
    {
        var text = (System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_TEXT") ?? "4025").Replace("\\n", "\n");
        var fontName = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_FONT") ?? "Arial";
        var px = int.Parse(System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_PX") ?? "256");
        var outlineFrac = float.Parse(System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_OUTLINE") ?? "0.08", System.Globalization.CultureInfo.InvariantCulture);
        var font = Font.CreateDynamicFontFromOSFont(fontName, 128);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DadsTextRender.shader");
        var mat = new Material(shader);
        font.RequestCharactersInTexture(text, px, FontStyle.Bold);
        mat.mainTexture = font.material.mainTexture;

        var root = new GameObject("studio");
        root.transform.position = new Vector3(0, -5000, 0);
        var main = Make(root.transform, font, text, mat, px, new Color(0.95f, 0.9f, 0.75f), Vector3.zero);
        var bounds = main.GetComponent<MeshRenderer>().bounds;
        var lineHeight = bounds.size.y / Mathf.Max(1, text.Split('\n').Length);
        var outline = outlineFrac * lineHeight;
        if (outline > 0)
            for (var i = 0; i < 16; i++)
            {
                var a = i * Mathf.PI / 8;
                Make(root.transform, font, text, mat, px, new Color(0.1f, 0.1f, 0.1f), new Vector3(Mathf.Cos(a) * outline, Mathf.Sin(a) * outline, 0.1f));
            }

        Color32[] canvas = null;
        int w = 0, h = 0;
        var scale = 0f;
        var attempts = 0;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            attempts++;
            var marginX = outline + lineHeight * (attempt == 0 ? 0.5f : 1.0f);
            var marginY = outline + lineHeight * (attempt == 0 ? 0.75f : 1.5f);
            var worldW = bounds.size.x + marginX * 2;
            var worldH = bounds.size.y + marginY * 2;
            scale = Mathf.Min(px / lineHeight, MaxPixels / worldW, MaxPixels / worldH);
            w = Mathf.Max(8, Mathf.RoundToInt(worldW * scale));
            h = Mathf.Max(8, Mathf.RoundToInt(worldH * scale));
            canvas = RenderCanvas(root.transform, bounds.center, worldH, w, h);
            if (!TouchesEdge(canvas, w, h)) break;
        }

        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            if (canvas[y * w + x].a < InkAlpha) continue;
            x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
        }
        var pad = Mathf.Max(2, Mathf.RoundToInt(lineHeight * scale * 0.04f));
        x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad); x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        var crop = new Color32[cw * ch];
        for (var y = 0; y < ch; y++)
        for (var x = 0; x < cw; x++)
        {
            var c = canvas[(y0 + y) * w + x0 + x];
            if (c.a != 0 && c.a != 255)
            {
                c.r = (byte)Mathf.Min(255, c.r * 255 / c.a);
                c.g = (byte)Mathf.Min(255, c.g * 255 / c.a);
                c.b = (byte)Mathf.Min(255, c.b * 255 / c.a);
            }
            crop[y * cw + x] = c;
        }

        Directory.CreateDirectory("TestRenders");
        var tag = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_TAG") ?? "";
        Save(canvas, w, h, $"TestRenders/{tag}text{px}-canvas.png");
        Save(crop, cw, ch, $"TestRenders/{tag}text{px}.png");
        Object.DestroyImmediate(root);
        Debug.Log($"DADSDECALS_TEST_OK text canvas {w}x{h} ({attempts} attempt(s)) cropped {cw}x{ch}, reported bounds {bounds.size.x * scale:0}x{bounds.size.y * scale:0}px");
    }

    private static Color32[] RenderCanvas(Transform root, Vector3 centre, float worldH, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.transform.SetParent(root, false);
        cam.transform.position = centre - Vector3.forward * 10;
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = worldH / 2;
        cam.aspect = (float)w / h;
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.renderingPath = RenderingPath.Forward;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = null;
        var px = read.GetPixels32();
        Object.DestroyImmediate(read);
        Object.DestroyImmediate(cam.gameObject);
        RenderTexture.ReleaseTemporary(rt);
        return px;
    }

    private static bool TouchesEdge(Color32[] px, int w, int h)
    {
        for (var x = 0; x < w; x++)
            if (px[x].a >= InkAlpha || px[(h - 1) * w + x].a >= InkAlpha) return true;
        for (var y = 0; y < h; y++)
            if (px[y * w].a >= InkAlpha || px[y * w + w - 1].a >= InkAlpha) return true;
        return false;
    }

    private static void Save(Color32[] px, int w, int h, string path)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }

    private static GameObject Make(Transform parent, Font font, string text, Material mat, int px, Color color, Vector3 offset)
    {
        var go = new GameObject("t") { layer = Layer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = px;
        tm.fontStyle = FontStyle.Bold;
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = text;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
