using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>A renderer decals can be drawn onto: full-detail exterior mesh of a car.</summary>
    internal sealed class DecalTarget
    {
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;

        public readonly Transform Anchor;       // car root, or the bogie this renderer belongs to

        public DecalTarget(MeshRenderer renderer, Mesh mesh, Transform anchor)
        {
            Renderer = renderer;
            Mesh = mesh;

            Anchor = anchor;
        }
    }

    internal static class DecalTargets
    {
        private static readonly Regex LowerLod = new Regex(@"lod[1-9]", RegexOptions.Compiled);

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
            if (LowerLod.IsMatch(p)) return true;
            var group = r.GetComponentInParent<LODGroup>();
            if (group != null)
            {
                var lods = group.GetLODs();
                if (lods.Length > 0 && !lods[0].renderers.Contains(r) && lods.Skip(1).Any(l => l.renderers.Contains(r))) return true;
            }
            // Particle-ish / glass-ish materials make poor decal targets.
            var shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "";
            if (shader.StartsWith("Particles/") || shader.StartsWith("Unlit/") || shader.Contains("Window") || shader.Contains("Smoke") || shader == "TransparencyWithFog") return true;
            return false;
        }
    }
}
