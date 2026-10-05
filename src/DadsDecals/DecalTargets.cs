using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>A renderer decals can be drawn onto: full-detail exterior mesh of a car.</summary>
    internal readonly struct DecalTarget
    {
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;

        public DecalTarget(MeshRenderer renderer, Mesh mesh)
        {
            Renderer = renderer;
            Mesh = mesh;
        }
    }

    internal static class DecalTargets
    {
        private static readonly Regex LowerLod = new Regex(@"lod[1-9]", RegexOptions.Compiled);

        public static List<DecalTarget> Find(TrainCar car)
        {
            var root = car.transform;
            var list = new List<DecalTarget>();
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                if (IsExcluded(r, Diagnostics.Path(root, r.transform))) continue;
                list.Add(new DecalTarget(r, f.sharedMesh));
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
