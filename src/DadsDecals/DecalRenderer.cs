using System.Collections.Generic;
using DV.Customization.Paint;
using UnityEngine;
using UnityEngine.Rendering;

namespace DadsDecals
{
    /// <summary>
    /// Lives on a TrainCar. Every frame, for each decal in the car's layout (plus the placement ghost),
    /// redraws the overlapped full-detail exterior meshes with the projector material.
    /// A decal is only drawn on renderers under its own anchor (body or bogie), so it doesn't slide
    /// when a bogie swings. Nothing is allocated per frame.
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
        private MaterialPropertyBlock mpb = null!;
        private readonly Vector3[] corners = new Vector3[8];
        private TrainCarPaint? paint;

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

        public void RefreshTargets() => targets = DecalTargets.Find(car);

        private void OnThemeChanged(TrainCarPaint p)
        {
            RefreshTargets();   // reskins can swap materials
            var layout = Main.Layouts.Get(car.CarGUID);
            if (layout != null) PaintLink.OnThemeChanged(layout, PaintLink.CurrentTheme(car));
        }

        private void LateUpdate()
        {
            if (Assets.DecalShader == null || car.logicCar == null) return;
            var layout = Main.Layouts.Get(car.CarGUID);
            var selected = Interaction.Instance != null ? Interaction.Instance.Selected : null;
            if (layout != null)
                foreach (var d in layout.Decals)
                    Draw(d, d == selected || (selected != null && d.PairId.Length > 0 && d.PairId == selected.PairId));
            GhostDraws = Ghost != null ? Draw(Ghost, false) : 0;
            if (GhostTwin != null) Draw(GhostTwin, false);
        }

        /// <summary>World matrix of the decal box (unit cube -> world), including roll and mirroring.</summary>
        public static Matrix4x4 DecalToWorld(Transform anchor, DecalPlacement d)
        {
            var size = new Vector3(d.Size[0] * (d.MirrorX ? -1 : 1), d.Size[1] * (d.MirrorY ? -1 : 1), d.Size[2]);
            var rot = new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]) * Quaternion.AngleAxis(d.Angle, Vector3.forward);
            var local = Matrix4x4.TRS(new Vector3(d.Position[0], d.Position[1], d.Position[2]), rot, size);
            return anchor.localToWorldMatrix * local;
        }

        public Matrix4x4 DecalToWorld(DecalPlacement d) => DecalToWorld(DecalTargets.ResolveAnchor(car, d.Anchor), d);

        private int Draw(DecalPlacement d, bool highlight)
        {
            var material = Assets.MaterialFor(d, car, out _);
            if (material == null) return 0;

            var anchor = DecalTargets.ResolveAnchor(car, d.Anchor);
            var toWorld = DecalToWorld(anchor, d);
            var bounds = WorldBounds(toWorld);

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
            foreach (var t in targets)
            {
                if (t.Anchor != anchor) continue;
                var r = t.Renderer;
                if (r == null || !r.enabled || !r.isVisible || !r.gameObject.activeInHierarchy) continue;
                if (!r.bounds.Intersects(bounds)) continue;
                var m = r.localToWorldMatrix;
                for (var sub = 0; sub < t.Mesh.subMeshCount; sub++)
                    Graphics.DrawMesh(t.Mesh, m, material, r.gameObject.layer, null, sub, mpb, ShadowCastingMode.Off, true);
                draws++;
            }
            return draws;
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
