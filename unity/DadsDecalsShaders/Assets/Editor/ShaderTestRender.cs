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
        tex.LoadImage(File.ReadAllBytes("../../examples/Decals/Logos/DVRT herald.png"));
        tex.wrapMode = TextureWrapMode.Clamp;

        var root = new GameObject("TestRoot");
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        light.transform.parent = root.transform;
        light.shadows = LightShadows.Soft;
        QualitySettings.shadowDistance = 60;

        // A bar between the sun and the decal, so its shadow falls across the decal (and the boiler).
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.transform.SetParent(root.transform);
        // Aim at the decal centre's surface point (boiler front at z = -0.8), horizontal along X.
        bar.transform.position = new Vector3(0.5f, 1.6f, -0.8f) - light.transform.forward * 1.0f;
        bar.transform.rotation = Quaternion.identity;
        bar.transform.localScale = new Vector3(0.25f, 6f, 0.25f);   // vertical post: stripe through the decal
        bar.transform.position += new Vector3(-0.35f, 0, 0);
        bar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

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
        // DADSDECALS_TEST_MIRROR=1: mirror the boiler (scale -1 on X, about the decal centre), like
        // Locomotive Mesh Splitter's S060 pieces. Same shape, flipped winding.
        if (System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_MIRROR") == "1")
        {
            var mirror = new GameObject("Mirror").transform;
            mirror.SetParent(root.transform);
            mirror.position = new Vector3(0.5f, 0, 0);
            boiler.transform.SetParent(mirror, true);
            mirror.localScale = new Vector3(-1, 1, 1);
        }
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

        // DADSDECALS_TEST_LAYERS="qa,qb": a second, overlapping decal (hazard stripe) for layering.
        // The herald draws at queue qa, the stripe at qb, and the stripe is submitted FIRST, so the
        // result shows queue order winning over submission order.
        Material stripeMat = null;
        MaterialPropertyBlock stripeMpb = null;
        var layers = System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_LAYERS");
        if (!string.IsNullOrEmpty(layers))
        {
            var q = layers.Split(',');
            decalMat.renderQueue = int.Parse(q[0]);
            var stripeTex = new Texture2D(2, 2);
            stripeTex.LoadImage(File.ReadAllBytes("../../examples/Decals/Warning Stripes/Stripes yellow-black.png"));
            stripeTex.wrapMode = TextureWrapMode.Clamp;
            stripeMat = new Material(shader) { mainTexture = stripeTex, renderQueue = int.Parse(q[1]) };
            var stripeToWorld = Matrix4x4.TRS(new Vector3(0.5f, 1.6f, -0.8f), Quaternion.identity, new Vector3(1.6f, 0.4f, 0.6f));
            stripeMpb = new MaterialPropertyBlock();
            stripeMpb.SetMatrix("_WorldToDecal", stripeToWorld.inverse);
            stripeMpb.SetVector("_DecalForward", stripeToWorld.MultiplyVector(Vector3.forward).normalized);
            stripeMpb.SetFloat("_WrapCos", Mathf.Cos(75 * Mathf.Deg2Rad));
            stripeMpb.SetVector("_DecalSize", new Vector4(1.6f, 0.4f, 0, 0));
        }

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform);
        cam.transform.position = new Vector3(1.5f, 2.2f, -7f);
        cam.transform.LookAt(new Vector3(1.5f, 1.5f, 0));
        if (System.Environment.GetEnvironmentVariable("DADSDECALS_TEST_CLOSE") == "1")
        {
            cam.transform.position = new Vector3(0.5f, 1.65f, -2.4f);
            cam.transform.LookAt(new Vector3(0.5f, 1.6f, -0.8f));
        }
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
            // Same approach as the mod: Graphics.DrawMesh of each target's mesh with the decal
            // material, into this camera only (queued draws are used by its next render).
            if (stripeMat != null)
                Graphics.DrawMesh(boiler.GetComponent<MeshFilter>().sharedMesh, boiler.transform.localToWorldMatrix,
                    stripeMat, 0, cam, 0, stripeMpb, UnityEngine.Rendering.ShadowCastingMode.Off, true);
            foreach (var target in new[] { boiler, cab })
                Graphics.DrawMesh(target.GetComponent<MeshFilter>().sharedMesh, target.transform.localToWorldMatrix,
                    decalMat, 0, cam, 0, mpb, UnityEngine.Rendering.ShadowCastingMode.Off, true);
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
