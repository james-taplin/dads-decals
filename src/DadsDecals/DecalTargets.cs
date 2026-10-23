using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        /// <summary>Per submesh: drawn solid. Cutout and transparent submeshes (ladders, grilles, glass) get no decal.</summary>
        public readonly bool[] Opaque;

        /// <summary>
        /// A lower detail level (LOD1, LOD2...). Decals are drawn on it, but it's left out of the
        /// first-surface picture: simplified shells sit in front of the real surface in places
        /// (a flat panel level with the ladder on a tender's rear), which hid the decal.
        /// </summary>
        public readonly bool LowerDetail;

        public DecalTarget(MeshRenderer renderer, Mesh mesh, Transform anchor, bool lowerDetail)
        {
            Renderer = renderer;
            Mesh = mesh;
            LocalBounds = DecalTargets.UsedBounds(mesh);
            Anchor = anchor;
            LowerDetail = lowerDetail;
            var materials = renderer.sharedMaterials;
            Opaque = new bool[mesh.subMeshCount];
            for (var i = 0; i < Opaque.Length; i++)
                Opaque[i] = materials.Length == 0 || DecalTargets.IsOpaque(materials[Mathf.Min(i, materials.Length - 1)]);
        }

        /// <summary>
        /// Is the part actually being drawn solid right now? Switched-off objects (e.g. the low-detail
        /// cab while the full interior is loaded) are left out of the first-surface picture.
        /// </summary>
        public bool ShownNow => Renderer != null && Renderer.enabled && !Renderer.forceRenderingOff && Renderer.gameObject.activeInHierarchy;
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
                list.Add(new DecalTarget(r, f.sharedMesh, anchor, IsLowerDetail(r, Diagnostics.Path(root, r.transform))));
            }

            // The cab interior: loaded when you're near (plus the outside handles and doors), so
            // DecalRenderer refreshes targets when it loads or unloads. Moving parts are left out.
            if (interior != null)
            {
                foreach (var r in interior.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    var path = Diagnostics.Path(interior, r.transform);
                    if (IsExcluded(r, path) || IsMovingPart(r.transform, interior)) continue;
                    list.Add(new DecalTarget(r, f.sharedMesh, interior, IsLowerDetail(r, path)));
                }
            }
            return list;
        }

        private static readonly Regex LodName = new Regex(@"lod[1-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Not the highest detail level: a LODGroup that lists it only below LOD0, or (for models
        /// switched by name, like DV's LOD0/LOD1 children) "LOD1", "LOD2"... in its path.
        /// </summary>
        private static bool IsLowerDetail(MeshRenderer r, string path)
        {
            for (var t = r.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<LODGroup>();
                if (group == null) continue;
                var lods = group.GetLODs();
                if (lods.Length > 1 && System.Array.IndexOf(lods[0].renderers, r) < 0 && lods.Any(l => System.Array.IndexOf(l.renderers, r) >= 0))
                    return true;
                break;
            }
            return LodName.IsMatch(path);
        }

        /// <summary>Drawn solid: not alpha-tested (cutout) or blended. A decal can't follow a cutout's holes.</summary>
        public static bool IsOpaque(Material? m)
        {
            if (m == null) return true;
            if (m.renderQueue >= 2450) return false;   // AlphaTest and later
            if (m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_ALPHABLEND_ON") || m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) return false;
            var type = m.GetTag("RenderType", false);
            return type != "TransparentCutout" && type != "Transparent";
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
            // Shadow-only meshes are invisible; a decal on one floated in mid-air.
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return true;
            // Every detail level (LOD) is a target: the projection works on any mesh, and only the
            // level being shown is visible, so decals stay on when a car switches to its low-detail
            // model in the distance instead of popping off.
            // Headlight beams (Volumetric Light Beam cones): switched on with the lights, they reach
            // out in front of the lamps, and a decal near a lamp got drawn on the beam too, floating
            // in mid-air. Their renderer can be inactive when targets are gathered, hence the walk.
            if (r.GetComponent<VLB.BeamGeometry>() != null) return true;
            for (var t = r.transform; t != null; t = t.parent)
                if (t.GetComponent<VLB.VolumetricLightBeam>() != null) return true;
            // Particle-ish / glass-ish / effect materials make poor decal targets.
            var shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "";
            if (shader.StartsWith("Particles/") || shader.StartsWith("Unlit/") || shader.StartsWith("Hidden/") || shader.Contains("Window") || shader.Contains("Smoke") ||
                shader.Contains("VolumetricLightBeam") || shader.Contains("SpriteLight") || shader == "TransparencyWithFog") return true;
            return false;
        }
    }
}
