using System.IO;
using UnityEditor;
using UnityEngine;

// Offline check of the text-decal pipeline (mirrors TextDecals.Render in the mod):
// OS font -> TextMesh (+8 outline copies) -> off-screen camera with DadsDecals/TextRender
// -> RenderTexture -> Texture2D, un-premultiplied. Writes TestRenders/text.png.
//   Unity.exe -quit -projectPath <this project> -executeMethod TextTestRender.Run -logFile <log>
public static class TextTestRender
{
    private const int Layer = 31;

    public static void Run()
    {
        var text = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_TEXT") ?? "4025";
        var fontName = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_FONT") ?? "Arial";
        var px = int.Parse(System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_PX") ?? "256");
        var font = Font.CreateDynamicFontFromOSFont(fontName, 128);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DadsTextRender.shader");
        var mat = new Material(shader);
        font.RequestCharactersInTexture(text, px, FontStyle.Bold);
        mat.mainTexture = font.material.mainTexture;

        var root = new GameObject("studio");
        root.transform.position = new Vector3(0, -5000, 0);
        var main = Make(root.transform, font, text, mat, px, new Color(0.95f, 0.9f, 0.75f), Vector3.zero);
        var bounds = main.GetComponent<MeshRenderer>().bounds;
        var outline = 0.08f * bounds.size.y;
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.PI / 4;
            Make(root.transform, font, text, mat, px, new Color(0.1f, 0.1f, 0.1f), new Vector3(Mathf.Cos(a) * outline, Mathf.Sin(a) * outline, 0.1f));
        }

        var pad = outline + bounds.size.y * 0.05f;
        var worldW = bounds.size.x + pad * 2;
        var worldH = bounds.size.y + pad * 2;
        var scale = px / bounds.size.y;
        int w = Mathf.RoundToInt(worldW * scale), h = Mathf.RoundToInt(worldH * scale);

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.transform.position = bounds.center - Vector3.forward * 10;
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = worldH / 2;
        cam.aspect = (float)w / h;
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.renderingPath = RenderingPath.Forward;
        cam.allowHDR = false;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = null;
        var pixels = tex.GetPixels32();
        for (var i = 0; i < pixels.Length; i++)
        {
            var al = pixels[i].a;
            if (al == 0 || al == 255) continue;
            pixels[i].r = (byte)Mathf.Min(255, pixels[i].r * 255 / al);
            pixels[i].g = (byte)Mathf.Min(255, pixels[i].g * 255 / al);
            pixels[i].b = (byte)Mathf.Min(255, pixels[i].b * 255 / al);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        Directory.CreateDirectory("TestRenders");
        File.WriteAllBytes($"TestRenders/text{px}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(root);
        RenderTexture.ReleaseTemporary(rt);
        Debug.Log($"DADSDECALS_TEST_OK text {w}x{h}");
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
        tm.color = color;
        tm.text = text;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
