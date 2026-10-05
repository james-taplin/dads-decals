using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DadsDecals
{
    /// <summary>
    /// Lives on a TrainCar. Every frame, for each decal in the car's layout (plus the placement ghost),
    /// redraws the overlapped full-detail exterior meshes with the projector material.
    /// Nothing is allocated per frame.
    /// </summary>
    internal sealed class DecalRenderer : MonoBehaviour
    {
        private static readonly int WorldToDecalId = Shader.PropertyToID("_WorldToDecal");
        private static readonly int DecalForwardId = Shader.PropertyToID("_DecalForward");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int WrapCosId = Shader.PropertyToID("_WrapCos");

        private TrainCar car = null!;
        private List<DecalTarget> targets = new List<DecalTarget>();
        private MaterialPropertyBlock mpb = null!;
        private readonly Vector3[] corners = new Vector3[8];

        /// <summary>Set by Placement while the player is positioning a new decal on this car.</summary>
        public DecalPlacement? Ghost;

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
        }

        public void RefreshTargets() => targets = DecalTargets.Find(car);

        private void LateUpdate()
        {
            if (Assets.DecalShader == null || car.logicCar == null) return;
            var layout = Main.Layouts.Get(car.CarGUID);
            if (layout != null)
                foreach (var d in layout.Decals) Draw(d);
            GhostDraws = Ghost != null ? Draw(Ghost) : 0;
        }

        /// <summary>World matrix of the decal box (unit cube -> world).</summary>
        public static Matrix4x4 DecalToWorld(Transform carRoot, DecalPlacement d)
        {
            var size = new Vector3(d.Size[0] * (d.MirrorX ? -1 : 1), d.Size[1] * (d.MirrorY ? -1 : 1), d.Size[2]);
            var local = Matrix4x4.TRS(
                new Vector3(d.Position[0], d.Position[1], d.Position[2]),
                new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]),
                size);
            return carRoot.localToWorldMatrix * local;
        }

        private int Draw(DecalPlacement d)
        {
            if (!Main.Library.TryGet(d.Image, out var image)) return 0;
            var material = image.Material;
            if (material == null) return 0;
            var draws = 0;

            var toWorld = DecalToWorld(transform, d);
            var bounds = WorldBounds(toWorld);

            mpb.Clear();
            mpb.SetMatrix(WorldToDecalId, toWorld.inverse);
            mpb.SetVector(DecalForwardId, toWorld.MultiplyVector(Vector3.forward).normalized);
            mpb.SetColor(ColorId, new Color(d.Tint[0], d.Tint[1], d.Tint[2], d.Tint[3]));
            mpb.SetFloat(OpacityId, d.Opacity);
            mpb.SetFloat(WrapCosId, Mathf.Cos(d.WrapAngle * Mathf.Deg2Rad));

            foreach (var t in targets)
            {
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
