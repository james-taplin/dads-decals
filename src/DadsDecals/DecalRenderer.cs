using System.Collections.Generic;
using DV.Customization.Paint;
using UnityEngine;
using UnityEngine.Rendering;

namespace DadsDecals
{
    /// <summary>
    /// Lives on a TrainCar. Every frame, for each decal in the car's layout (plus the placement ghost),
    /// redraws the overlapped exterior meshes with the projector material.
    /// A decal is only drawn on renderers under its own anchor (body or bogie), so it doesn't slide
    /// when a bogie swings. Which parts a decal overlaps is worked out once and cached until the
    /// decal (or the car's parts) change; decals too small on screen or too far away are skipped.
    /// Nothing is allocated per frame.
    /// </summary>
    internal sealed class DecalRenderer : MonoBehaviour
    {
        private static readonly int WorldToDecalId = Shader.PropertyToID("_WorldToDecal");
        private static readonly int DecalForwardId = Shader.PropertyToID("_DecalForward");
        private static readonly int DecalSizeId = Shader.PropertyToID("_DecalSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int WrapCosId = Shader.PropertyToID("_WrapCos");
        private static readonly int GlossId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int GrimeId = Shader.PropertyToID("_Grime");
        private static readonly int GrimeSeedId = Shader.PropertyToID("_GrimeSeed");
        private static readonly int GrimeColorId = Shader.PropertyToID("_GrimeColor");
        private static readonly int ChippingId = Shader.PropertyToID("_Chipping");
        private static readonly int ChipSeedId = Shader.PropertyToID("_ChipSeed");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int HighlightId = Shader.PropertyToID("_Highlight");


        private TrainCar car = null!;
        private List<DecalTarget> targets = new List<DecalTarget>();
        private readonly Dictionary<DecalPlacement, int> queues = new Dictionary<DecalPlacement, int>();

        /// <summary>The parts a decal overlaps, valid while its anchor-space box and the car's parts are unchanged.</summary>
        private sealed class Overlap
        {
            public Matrix4x4 Local;
            public Transform? Anchor;
            public int TargetsVersion = -1;
            public readonly List<DecalTarget> Parts = new List<DecalTarget>();
        }
        private readonly Dictionary<DecalPlacement, Overlap> overlaps = new Dictionary<DecalPlacement, Overlap>();
        private readonly Overlap ghostOverlap = new Overlap();
        private readonly Overlap ghostTwinOverlap = new Overlap();
        private int targetsVersion;
        // Parts can be animated relative to the body (rods, doors); a little slack keeps the cache valid.
        private const float OverlapMargin = 0.25f;
        private static readonly float[] DetailMinPixels = { 0f, 2f, 6f };   // High, Medium, Low
        private MaterialPropertyBlock mpb = null!;
        private readonly Vector3[] corners = new Vector3[8];
        private TrainCarPaint? paint;
        private Camera? viewCamera;

        /// <summary>Set by Interaction while the player is positioning a new decal on this car.</summary>
        public DecalPlacement? Ghost;
        public DecalPlacement? GhostTwin;

        /// <summary>How many renderer draws the ghost made last frame (panel readout).</summary>
        public int GhostDraws { get; private set; }

        public static DecalRenderer Ensure(TrainCar car)
        {
            var r = car.GetComponent<DecalRenderer>();
            return r != null ? r : car.gameObject.AddComponent<DecalRenderer>();
        }

        private void Awake()
        {
            car = GetComponent<TrainCar>();
            mpb = new MaterialPropertyBlock();
            RefreshTargets();
            paint = car.PaintExterior;
            if (paint != null) paint.OnThemeChanged += OnThemeChanged;
        }

        private void OnDestroy()
        {
            if (paint != null) paint.OnThemeChanged -= OnThemeChanged;
        }

        public void RefreshTargets()
        {
            targets = DecalTargets.Find(car);
            targetsVersion++;
        }

        private void OnThemeChanged(TrainCarPaint p)
        {
            RefreshTargets();   // reskins can swap materials
            // In multiplayer only the host swaps paint-linked decals and sends the result, so the
            // machines can't drift apart; guests get the new layout from the host.
            if (Sync.Role == SyncRole.Client) return;
            var layout = Main.Layouts.Get(car.CarGUID);
            if (layout != null) PaintLink.OnThemeChanged(layout, PaintLink.CurrentTheme(car));
            Sync.MarkDirty(car.CarGUID);
        }

        private void LateUpdate()
        {
            if (Assets.DecalShader == null || car.logicCar == null) return;
            // Only the camera the player looks through. camera: null in DrawMesh means *every* camera,
            // which put decals into DV's other cameras (UI/render-texture/probe cameras) - seen as a
            // stray white dot drawn over the screen and even the menus.
            viewCamera = PlayerManager.ActiveCamera != null ? PlayerManager.ActiveCamera : PlayerManager.PlayerCamera;
            if (viewCamera == null) return;
            var layout = Main.Layouts.Get(car.CarGUID);
            if ((layout == null || layout.Decals.Count == 0) && Ghost == null) return;
            RenderStats.BeginCar();
            // No selection pulse while the panel is closed or the mouse isn't free.
            var selected = Interaction.Instance != null && Interaction.Instance.Active ? Interaction.Instance.Selected : null;
            if (layout != null)
            {
                Layers.Assign(layout.Decals, queues);
                foreach (var d in layout.Decals)
                {
                    var isSelected = d == selected || (selected != null && d.PairId.Length > 0 && d.PairId == selected.PairId);
                    if (!overlaps.TryGetValue(d, out var o)) overlaps[d] = o = new Overlap();
                    Draw(d, isSelected, queues[d], o, cullable: !isSelected);
                }
                if (overlaps.Count > layout.Decals.Count + 8) PruneOverlaps(layout);
            }
            GhostDraws = Ghost != null ? Draw(Ghost, false, Layers.GhostQueue, ghostOverlap, cullable: false) : 0;
            if (GhostTwin != null) Draw(GhostTwin, false, Layers.GhostQueue, ghostTwinOverlap, cullable: false);
            RenderStats.EndCar();
        }

        private void PruneOverlaps(LocoLayout layout)
        {
            foreach (var d in new List<DecalPlacement>(overlaps.Keys))
                if (!layout.Decals.Contains(d)) overlaps.Remove(d);
        }

        /// <summary>World matrix of the decal box (unit cube -> world), including roll and mirroring.</summary>
        public static Matrix4x4 DecalToWorld(Transform anchor, DecalPlacement d) => anchor.localToWorldMatrix * DecalToAnchor(d);

        /// <summary>The decal box in its anchor's space (unit cube -> anchor).</summary>
        private static Matrix4x4 DecalToAnchor(DecalPlacement d)
        {
            var size = new Vector3(d.Size[0] * (d.MirrorX ? -1 : 1), d.Size[1] * (d.MirrorY ? -1 : 1), d.Size[2]);
            var rot = new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]) * Quaternion.AngleAxis(d.Angle, Vector3.forward);
            return Matrix4x4.TRS(new Vector3(d.Position[0], d.Position[1], d.Position[2]), rot, size);
        }

        public Matrix4x4 DecalToWorld(DecalPlacement d) => DecalToWorld(DecalTargets.ResolveAnchor(car, d.Anchor), d);

        private int Draw(DecalPlacement d, bool highlight, int queue, Overlap overlap, bool cullable)
        {
            var anchor = DecalTargets.ResolveAnchor(car, d.Anchor);
            var local = DecalToAnchor(d);
            var toWorld = anchor.localToWorldMatrix * local;
            if (cullable && TooSmallOrFar(toWorld)) { RenderStats.Culled(); return 0; }

            var baseMaterial = Assets.MaterialFor(d, car, out _);
            if (baseMaterial == null) return 0;
            var material = Layers.AtQueue(baseMaterial, queue);

            if (overlap.TargetsVersion != targetsVersion || overlap.Anchor != anchor || overlap.Local != local)
                FindOverlaps(overlap, anchor, local, toWorld);

            mpb.Clear();
            mpb.SetMatrix(WorldToDecalId, toWorld.inverse);
            mpb.SetVector(DecalForwardId, toWorld.MultiplyVector(Vector3.forward).normalized);
            mpb.SetVector(DecalSizeId, new Vector4(d.Size[0], d.Size[1], 0, 0));
            mpb.SetColor(ColorId, new Color(d.Tint[0], d.Tint[1], d.Tint[2], d.Tint[3]));
            mpb.SetFloat(OpacityId, d.Opacity);
            mpb.SetFloat(WrapCosId, Mathf.Cos(d.WrapAngle * Mathf.Deg2Rad));
            mpb.SetFloat(GlossId, d.Smoothness);
            mpb.SetFloat(MetallicId, d.Metallic);
            mpb.SetFloat(GrimeId, d.Grime);
            mpb.SetFloat(GrimeSeedId, d.GrimeSeed);
            mpb.SetColor(GrimeColorId, new Color(d.GrimeColor[0], d.GrimeColor[1], d.GrimeColor[2], d.GrimeColor[3]));
            mpb.SetFloat(ChippingId, d.Chipping);
            mpb.SetFloat(ChipSeedId, d.ChipSeed);
            mpb.SetFloat(GlowId, d.Glow);
            mpb.SetFloat(HighlightId, highlight ? 1f : 0f);

            var draws = 0;
            foreach (var t in overlap.Parts)
            {
                var r = t.Renderer;
                // isVisible is false for detail levels (LODs) not currently shown, and for parts off screen.
                if (r == null || !r.enabled || !r.isVisible || !r.gameObject.activeInHierarchy) continue;
                var m = r.localToWorldMatrix;
                for (var sub = 0; sub < t.Mesh.subMeshCount; sub++)
                    Graphics.DrawMesh(t.Mesh, m, material, r.gameObject.layer, viewCamera, sub, mpb, ShadowCastingMode.Off, true);
                draws++;
            }
            RenderStats.Decal(draws);
            return draws;
        }

        /// <summary>Skips decals smaller on screen than the Decal detail setting allows, or beyond the max distance.</summary>
        private bool TooSmallOrFar(Matrix4x4 toWorld)
        {
            var settings = Main.Settings;
            var minPx = DetailMinPixels[Mathf.Clamp(settings.DecalDetail, 0, DetailMinPixels.Length - 1)];
            if (minPx <= 0 && settings.MaxDistance <= 0) return false;
            var cam = viewCamera!;
            var dist = Vector3.Distance(toWorld.GetColumn(3), cam.transform.position);
            if (settings.MaxDistance > 0 && dist > settings.MaxDistance) return true;
            if (minPx <= 0 || cam.orthographic || dist < 1f) return false;
            var size = Mathf.Max(((Vector3)toWorld.GetColumn(0)).magnitude, ((Vector3)toWorld.GetColumn(1)).magnitude);
            var pixels = size / (2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * cam.pixelHeight;
            return pixels < minPx;
        }

        /// <summary>Works out which parts (under the same anchor) the decal box overlaps, using each part's real triangle bounds.</summary>
        private void FindOverlaps(Overlap overlap, Transform anchor, Matrix4x4 local, Matrix4x4 toWorld)
        {
            overlap.Parts.Clear();
            overlap.Local = local;
            overlap.Anchor = anchor;
            overlap.TargetsVersion = targetsVersion;
            var box = WorldBounds(toWorld);
            box.Expand(OverlapMargin * 2);
            foreach (var t in targets)
            {
                if (t.Anchor != anchor || t.Renderer == null) continue;
                if (TransformBounds(t.Renderer.localToWorldMatrix, t.LocalBounds).Intersects(box)) overlap.Parts.Add(t);
            }
        }

        private Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            var i = 0;
            var min = b.min;
            var max = b.max;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
                corners[i++] = m.MultiplyPoint3x4(new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z));
            var result = new Bounds(corners[0], Vector3.zero);
            for (i = 1; i < 8; i++) result.Encapsulate(corners[i]);
            return result;
        }

        private Bounds WorldBounds(Matrix4x4 toWorld)
        {
            var i = 0;
            for (var x = -0.5f; x <= 0.5f; x += 1f)
            for (var y = -0.5f; y <= 0.5f; y += 1f)
            for (var z = -0.5f; z <= 0.5f; z += 1f)
                corners[i++] = toWorld.MultiplyPoint3x4(new Vector3(x, y, z));
            var b = new Bounds(corners[0], Vector3.zero);
            for (i = 1; i < 8; i++) b.Encapsulate(corners[i]);
            return b;
        }
    }
}
