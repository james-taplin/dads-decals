using System.Collections.Generic;
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

            // Renderers of the car itself (outside, every detail level; the cab interior is listed separately).
            var targets = car.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => car.interior == null || !r.transform.IsChildOf(car.interior))
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

            // Cab interior: a separate object, loaded when you're near.
            var interior = car.interior;
            var interiorTargets = interior != null
                ? DecalTargets.Find(car).Where(t => t.Anchor == interior).Select(t => (t.Renderer, t.Mesh, path: Path(interior, t.Renderer.transform))).ToList()
                : new List<(MeshRenderer Renderer, Mesh Mesh, string path)>();
            var interiorRenderers = interior != null ? interior.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            var interiorColliders = interior != null ? interior.GetComponentsInChildren<Collider>(true).Count(c => !c.isTrigger) : 0;
            sb.AppendLine($"Cab interior: {(interior == null ? "none" : car.IsInteriorLoaded ? "loaded" : "not loaded")}" +
                          (interior != null ? $", targets {interiorTargets.Count} of {interiorRenderers} renderers (the rest are moving parts or excluded), solid colliders {interiorColliders}" : ""));

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
            sb.AppendLine();
            sb.AppendLine("Cab interior targets:");
            foreach (var (r, mesh, path) in interiorTargets)
                sb.AppendLine($"  {(mesh.isReadable ? "R" : "-")} verts={mesh.vertexCount,6}  interior/{path}  mesh={mesh.name}  shader={r.sharedMaterial?.shader?.name}");

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
