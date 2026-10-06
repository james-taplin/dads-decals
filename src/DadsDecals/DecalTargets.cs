using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>A renderer decals can be drawn onto: an exterior mesh of a car, at any detail level.</summary>
    internal sealed class DecalTarget
    {
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        /// <summary>Mesh-space bounds of the triangles actually drawn (see <see cref="DecalTargets.UsedBounds"/>).</summary>
        public readonly Bounds LocalBounds;

        public readonly Transform Anchor;       // car root, or the bogie this renderer belongs to

        public DecalTarget(MeshRenderer renderer, Mesh mesh, Transform anchor)
        {
            Renderer = renderer;
            Mesh = mesh;
            LocalBounds = DecalTargets.UsedBounds(mesh);
            Anchor = anchor;
        }
    }

    internal static class DecalTargets
    {
        private static readonly Dictionary<Mesh, Bounds> usedBounds = new Dictionary<Mesh, Bounds>();

        /// <summary>
        /// Bounds of the vertices the mesh's triangles actually use. Usually the same as mesh.bounds,
        /// but Locomotive Mesh Splitter's pieces keep the whole body's vertices and only some of its
        /// triangles, so their mesh.bounds covers the whole loco. Computed once per mesh; meshes
        /// that aren't CPU-readable fall back to mesh.bounds.
        /// </summary>
        public static Bounds UsedBounds(Mesh mesh)
        {
            if (usedBounds.TryGetValue(mesh, out var b)) return b;
            b = mesh.bounds;
            if (mesh.isReadable)
            {
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                if (verts.Length > 0 && tris.Length > 0)
                {
                    Vector3 min = verts[tris[0]], max = min;
                    foreach (var i in tris)
                    {
                        min = Vector3.Min(min, verts[i]);
                        max = Vector3.Max(max, verts[i]);
                    }
                    b.SetMinMax(min, max);
                }
            }
            usedBounds[mesh] = b;
            return b;
        }

        public static List<DecalTarget> Find(TrainCar car)
        {
            var root = car.transform;
            var bogies = car.Bogies.Where(b => b != null).Select(b => b.transform).ToArray();
            var list = new List<DecalTarget>();
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                if (IsExcluded(r, Diagnostics.Path(root, r.transform))) continue;
                var anchor = bogies.FirstOrDefault(b => r.transform.IsChildOf(b)) ?? root;
                list.Add(new DecalTarget(r, f.sharedMesh, anchor));
            }
            return list;
        }

        /// <summary>The anchor a decal should use when placed on this collider: its bogie, else the car body.</summary>
        public static string AnchorFor(TrainCar car, Transform hitTransform)
        {
            foreach (var b in car.Bogies)
                if (b != null && hitTransform.IsChildOf(b.transform))
                    return Diagnostics.Path(car.transform, b.transform);
            return "";
        }

        /// <summary>Resolves a decal's anchor path; missing anchors (e.g. layout copied from another loco type) fall back to the body.</summary>
        public static Transform ResolveAnchor(TrainCar car, string anchor)
        {
            if (string.IsNullOrEmpty(anchor)) return car.transform;
            var t = car.transform.Find(anchor);
            return t != null ? t : car.transform;
        }

        /// <summary>Anchors the player can pick from: the body and each bogie.</summary>
        public static List<(string path, string label)> AnchorChoices(TrainCar car)
        {
            var list = new List<(string, string)> { ("", "Body") };
            var bogies = car.Bogies;
            for (var i = 0; i < bogies.Length; i++)
            {
                if (bogies[i] == null) continue;
                var label = bogies.Length == 2 ? (i == 0 ? "Rear bogie" : "Front bogie") : $"Bogie {i + 1}";
                list.Add((Diagnostics.Path(car.transform, bogies[i].transform), label));
            }
            return list;
        }

        public static bool IsExcluded(MeshRenderer r, string path)
        {
            var p = path.ToLowerInvariant();
            if (p.Contains("[interior") || p.Contains("broken") || p.Contains("[car plate") || p.Contains("textmeshpro")) return true;
            // Every detail level (LOD) is a target: the projection works on any mesh, and only the
            // level being shown is visible, so decals stay on when a car switches to its low-detail
            // model in the distance instead of popping off.
            // Particle-ish / glass-ish materials make poor decal targets.
            var shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "";
            if (shader.StartsWith("Particles/") || shader.StartsWith("Unlit/") || shader.Contains("Window") || shader.Contains("Smoke") || shader == "TransparencyWithFog") return true;
            return false;
        }
    }
}
