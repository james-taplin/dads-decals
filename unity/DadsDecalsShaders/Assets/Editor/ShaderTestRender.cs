using System.IO;
using UnityEditor;
using UnityEngine;

// Offline check of the projector shader under the game's camera setup (deferred + HDR).
//   Unity.exe -quit -projectPath <this project> -executeMethod ShaderTestRender.Run -logFile <log>
// Writes TestRenders/deferred.png and TestRenders/forward.png next to the project.
public static class ShaderTestRender
{
    public static void Run()
    {
        var shaderPath = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_SHADER") ?? "Assets/Shaders/DadsDecal.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes("../../examples/Decals/Test/Number 42.png"));
        tex.wrapMode = TextureWrapMode.Clamp;

        var root = new GameObject("TestRoot");
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        light.transform.parent = root.transform;

        // "Loco" parts: a long cylinder (boiler) and a box (cab), with grey Standard material.
        // Livery stand-in: vertical grime streaks on a mid tone, with mipmaps like a real livery.
        var livery = new Texture2D(256, 256, TextureFormat.RGBA32, true);
        for (var y = 0; y < 256; y++)
        for (var x = 0; x < 256; x++)
        {
            var streak = 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.15f, y * 0.01f);
            livery.SetPixel(x, y, new Color(0.8f, 0.7f, 0.6f) * streak);
        }
        livery.Apply(true);
        var grey = new Material(Shader.Find("Standard")) { mainTexture = livery };
        var boiler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        boiler.transform.SetParent(root.transform);
        boiler.transform.localPosition = new Vector3(0, 1.5f, 0);
        boiler.transform.localRotation = Quaternion.Euler(0, 0, 90);
        boiler.transform.localScale = new Vector3(1.6f, 3f, 1.6f);
        boiler.GetComponent<Renderer>().sharedMaterial = grey;
        var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cab.transform.SetParent(root.transform);
        cab.transform.localPosition = new Vector3(4f, 1.8f, 0);
        cab.transform.localScale = new Vector3(2f, 2.4f, 2.2f);
        cab.GetComponent<Renderer>().sharedMaterial = grey;

        // Decal: 1m square on the boiler side facing the camera (-Z side), projecting +Z.
        var decalMat = new Material(shader) { mainTexture = tex };
        var toWorld = Matrix4x4.TRS(new Vector3(0.5f, 1.6f, -0.8f), Quaternion.identity, new Vector3(1.2f, 1.2f, 0.6f));
        var mpb = new MaterialPropertyBlock();
        mpb.SetMatrix("_WorldToDecal", toWorld.inverse);
        mpb.SetVector("_DecalForward", toWorld.MultiplyVector(Vector3.forward).normalized);
        mpb.SetFloat("_WrapCos", Mathf.Cos(75 * Mathf.Deg2Rad));
        mpb.SetVector("_DecalSize", new Vector4(1.2f, 1.2f, 0, 0));
        mpb.SetTexture("_BaseTex", livery);
        // Extra properties for a test, e.g. DADSDECALS_TEST_PROPS="_Grime=1;_Chipping=0.5"
        foreach (var kv in (System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_PROPS") ?? "").Split(';'))
        {
            var parts = kv.Split('=');
            if (parts.Length != 2) continue;
            var nums = System.Array.ConvertAll(parts[1].Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            if (nums.Length == 1) mpb.SetFloat(parts[0].Trim(), nums[0]);
            else mpb.SetVector(parts[0].Trim(), new Vector4(nums[0], nums[1], nums[2], nums.Length > 3 ? nums[3] : 1));
        }

        // Same approach as the mod: draw each target's mesh again with the decal material.
        foreach (var target in new[] { boiler, cab })
        {
            var copy = new GameObject(target.name + "_decal");
            copy.transform.SetParent(target.transform, false);
            copy.AddComponent<MeshFilter>().sharedMesh = target.GetComponent<MeshFilter>().sharedMesh;
            var r = copy.AddComponent<MeshRenderer>();
            r.sharedMaterial = decalMat;
            r.SetPropertyBlock(mpb);
        }

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform);
        cam.transform.position = new Vector3(1.5f, 2.2f, -7f);
        cam.transform.LookAt(new Vector3(1.5f, 1.5f, 0));
        cam.allowHDR = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.6f, 0.7f);

        Directory.CreateDirectory("TestRenders");
        var tag = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_TAG") ?? "";
        foreach (var path in new[] { RenderingPath.DeferredShading, RenderingPath.Forward })
        {
            cam.renderingPath = path;
            var rt = new RenderTexture(800, 450, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var outTex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            outTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            outTex.Apply();
            File.WriteAllBytes($"TestRenders/{tag}{(path == RenderingPath.DeferredShading ? "deferred" : "forward")}.png", outTex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }

        Object.DestroyImmediate(root);
        Debug.Log("DADSDECALS_TEST_OK");
    }
}
