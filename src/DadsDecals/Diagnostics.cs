using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>
    /// Answers the open technical questions from BRIEF.md with evidence from the running game:
    /// are loco meshes CPU-readable (needed for mesh-clipping decals), which shaders can we find
    /// at runtime (needed if we avoid shipping our own), and which rendering path the camera uses.
    /// Full report goes to Mods/DadsDecals/Diagnostics/&lt;carId&gt;_&lt;livery&gt;.txt; a summary goes to the log.
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
            "TransparencyWithFog",
        };

        public static string Run(TrainCar car)
        {
            var root = car.transform;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Dad's Decals diagnostics: {car.ID} ({car.carLivery?.id}) guid={car.CarGUID}");

            var cam = PlayerManager.PlayerCamera;
            if (cam != null)
                sb.AppendLine($"Camera: renderingPath={cam.renderingPath} actual={cam.actualRenderingPath} hdr={cam.allowHDR}");

            // Only renderers that draw the outside of the loco at full detail are decal targets.
            var targets = car.GetComponentsInChildren<MeshRenderer>(true)
                .Select(r => (r, f: r.GetComponent<MeshFilter>()))
                .Where(x => x.f != null && x.f.sharedMesh != null)
                .Select(x => (x.r, mesh: x.f.sharedMesh, path: Path(root, x.r.transform)))
                .Where(x => !DecalTargets.IsExcluded(x.r, x.path))
                .OrderByDescending(x => x.mesh.vertexCount)
                .ToList();

            var readable = targets.Where(t => t.mesh.isReadable).ToList();
            var totalVerts = targets.Sum(t => t.mesh.vertexCount);
            var readableVerts = readable.Sum(t => t.mesh.vertexCount);
            sb.AppendLine($"Exterior targets (all detail levels): {targets.Count}, readable: {readable.Count}  " +
                          $"(vertices {readableVerts}/{totalVerts} = {(totalVerts == 0 ? 0 : 100 * readableVerts / totalVerts)}% readable)");
            sb.AppendLine($"Skinned renderers: {car.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}");

            sb.AppendLine("Largest targets:");
            foreach (var t in targets.Take(12))
                sb.AppendLine($"  {(t.mesh.isReadable ? "R" : "-")} verts={t.mesh.vertexCount,6}  {t.path}  mesh={t.mesh.name}");

            var shadersInUse = car.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.shader != null)
                .Select(m => m.shader.name)
                .Distinct();
            sb.AppendLine("Shaders used by this car: " + string.Join(", ", shadersInUse));
            foreach (var name in CandidateShaders)
                sb.AppendLine($"Shader.Find(\"{name}\"): {(Shader.Find(name) != null ? "found" : "MISSING")}");

            var summary = sb.ToString();

            sb.AppendLine();
            sb.AppendLine("All exterior targets:");
            foreach (var t in targets)
                sb.AppendLine($"  {(t.mesh.isReadable ? "R" : "-")} verts={t.mesh.vertexCount,6}  {t.path}  mesh={t.mesh.name}  shader={t.r.sharedMaterial?.shader?.name}");

            var dir = System.IO.Path.Combine(Main.Mod.Path, "Diagnostics");
            Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, $"{car.ID}_{car.carLivery?.id}.txt");
            File.WriteAllText(file, sb.ToString());

            Main.Log.Log(summary + "Full report: " + file);
            return summary;
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
