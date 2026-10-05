using System.Linq;
using System.Text;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>
    /// Answers the open technical questions from BRIEF.md with evidence from the running game:
    /// are loco meshes CPU-readable (needed for mesh-clipping decals), which shaders can we find
    /// at runtime (needed if we avoid shipping our own), and which rendering path the camera uses.
    /// </summary>
    internal static class Diagnostics
    {
        private static readonly string[] CandidateShaders =
        {
            "Standard",
            "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Bumped Diffuse",
            "Unlit/Transparent",
            "Unlit/Transparent Cutout",
            "Sprites/Default",
        };

        public static string Run(TrainCar car)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Dad's Decals diagnostics: {car.ID} ({car.carLivery?.id}) guid={car.CarGUID}");

            var cam = PlayerManager.PlayerCamera;
            if (cam != null)
                sb.AppendLine($"Camera: renderingPath={cam.renderingPath} actual={cam.actualRenderingPath} hdr={cam.allowHDR}");

            var filters = car.GetComponentsInChildren<MeshFilter>(true);
            var withMesh = filters.Where(f => f.sharedMesh != null).ToArray();
            var readable = withMesh.Count(f => f.sharedMesh.isReadable);
            var skinned = car.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            sb.AppendLine($"MeshFilters: {withMesh.Length}, readable: {readable}, skinned renderers: {skinned}");
            foreach (var f in withMesh.Take(40))
                sb.AppendLine($"  {(f.sharedMesh.isReadable ? "R" : "-")} {Path(car.transform, f.transform)}  mesh={f.sharedMesh.name} verts={f.sharedMesh.vertexCount}");

            var shadersInUse = car.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.shader != null)
                .Select(m => m.shader.name)
                .Distinct();
            sb.AppendLine("Shaders used by this car: " + string.Join(", ", shadersInUse));

            foreach (var name in CandidateShaders)
                sb.AppendLine($"Shader.Find(\"{name}\"): {(Shader.Find(name) != null ? "found" : "MISSING")}");

            var report = sb.ToString();
            Main.Log.Log(report);
            return report;
        }

        public static string Path(Transform root, Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var cur = t; cur != null && cur != root; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
