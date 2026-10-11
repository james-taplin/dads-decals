using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>A renderer decals can be drawn onto: a mesh of a car (outside or cab interior), at any detail level.</summary>
    internal sealed class DecalTarget
    {
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        /// <summary>Mesh-space bounds of the triangles actually drawn (see <see cref="DecalTargets.UsedBounds"/>).</summary>
        public readonly Bounds LocalBounds;

        /// <summary>
        /// The frame this renderer moves with: the car root, the bogie it belongs to, or the cab
        /// interior (<see cref="TrainCar.interior"/>). The interior is a separate object that DV keeps
        /// lined up with the car body, so body decals draw on interior parts too, using the
        /// interior's own transform (which can trail the body by a frame).
        /// </summary>
        public readonly Transform Anchor;

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
            var interior = car.interior;
            var bogies = car.Bogies.Where(b => b != null).Select(b => b.transform).ToArray();
            var list = new List<DecalTarget>();
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Cars without an interior prefab keep [interior] under the root; it's handled below.
                if (interior != null && r.transform.IsChildOf(interior)) continue;
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                if (IsExcluded(r, Diagnostics.Path(root, r.transform))) continue;
                var anchor = bogies.FirstOrDefault(b => r.transform.IsChildOf(b)) ?? root;
                list.Add(new DecalTarget(r, f.sharedMesh, anchor));
            }

            // The cab interior: loaded when you're near (plus the outside handles and doors), so
            // DecalRenderer refreshes targets when it loads or unloads. Moving parts are left out.
            if (interior != null)
            {
                foreach (var r in interior.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    if (IsExcluded(r, Diagnostics.Path(interior, r.transform)) || IsMovingPart(r.transform, interior)) continue;
                    list.Add(new DecalTarget(r, f.sharedMesh, interior));
                }
            }
            return list;
        }

        /// <summary>
        /// Levers, valves, switches, doors (cab controls) and gauges (their needles turn): a decal
        /// drawn on one would stay put while the part moves through it.
        /// </summary>
        private static bool IsMovingPart(Transform t, Transform stopAt)
        {
            for (var x = t; x != null && x != stopAt; x = x.parent)
                if (x.GetComponent<DV.CabControls.ControlImplBase>() != null || x.GetComponent<Indicator>() != null)
                    return true;
            return false;
        }

        /// <summary>
        /// The transform a hit's position should be measured against: the cab interior for interior
        /// colliders (it can trail the body by a frame at speed), else the decal's anchor. Decals on
        /// the interior still use the body anchor, since the interior is lined up with the body.
        /// </summary>
        public static Transform FrameFor(TrainCar car, Transform hitTransform, Transform anchor) =>
            anchor == car.transform && car.interior != null && hitTransform.IsChildOf(car.interior) ? car.interior : anchor;

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
            // [interior LOD] (the low-detail cab shown while the interior isn't loaded) is a target, so
            // cab decals stay visible through the windows from outside.
            if (p.Contains("broken") || p.Contains("[car plate") || p.Contains("textmeshpro")) return true;
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
