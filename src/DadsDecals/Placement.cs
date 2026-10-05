using System.Linq;
using DerailValleyModToolbar;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>
    /// Placement mode: while armed, the selected decal follows the mouse over the loco as a ghost.
    /// Left click places a copy (and stays armed for more), right click cancels.
    /// </summary>
    internal sealed class Placement : MonoBehaviour
    {
        public static Placement Instance { get; private set; } = null!;

        public bool Armed { get; private set; }
        public TrainCar? Car { get; private set; }

        // Settings for the next decal, edited from the panel.
        public float Width = 0.6f;
        public float Height = 0.6f;
        public bool LockAspect = true;
        public float Depth = 0.3f;
        public float Angle;
        public float Opacity = 1f;
        public float WrapAngle = 75f;
        public bool MirrorX;
        public Color Tint = Color.white;

        public string? Image;
        private bool hasHit;
        private readonly DecalPlacement ghost = new DecalPlacement();
        private ModToolbarWindow? panelWindow;

        public static void Create()
        {
            var go = new GameObject("DadsDecals_Placement");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Placement>();
        }

        public void Arm(TrainCar car, string image)
        {
            Disarm();
            Car = car;
            Image = image;
            Armed = true;
            var tex = Main.Library.TryGet(image, out var img) ? img.Texture : null;
            if (tex != null && LockAspect) Height = Width * tex.height / tex.width;
        }

        public void Disarm()
        {
            if (Car != null)
            {
                var r = Car.GetComponent<DecalRenderer>();
                if (r != null) r.Ghost = null;
            }
            Armed = false;
            Car = null;
            hasHit = false;
        }

        private void Update()
        {
            if (!Armed || Car == null || Image == null) { if (Armed) Disarm(); return; }
            var renderer = DecalRenderer.Ensure(Car);

            var cam = PlayerManager.PlayerCamera;
            var mouseFree = Cursor.visible;
            if (cam == null || !mouseFree || MouseOverUi())
            {
                renderer.Ghost = hasHit ? Fill(ghost) : null;
                return;
            }

            hasHit = RaycastCar(cam.ScreenPointToRay(Input.mousePosition), out var hit);
            if (hasHit) Pose(hit);
            renderer.Ghost = hasHit ? Fill(ghost) : null;

            if (Input.GetMouseButtonDown(1)) { Disarm(); return; }
            if (hasHit && Input.GetMouseButtonDown(0))
            {
                var layout = Main.Layouts.GetOrCreate(Car);
                layout.Decals.Add(Copy(Fill(ghost)));
            }
        }

        private Vector3 localPos;
        private Quaternion localRot;

        private void Pose(RaycastHit hit)
        {
            var root = Car!.transform;
            var forward = -hit.normal;   // project into the surface
            var up = Vector3.ProjectOnPlane(root.up, forward);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(root.forward, forward);
            var worldRot = Quaternion.LookRotation(forward, up.normalized) * Quaternion.AngleAxis(Angle, Vector3.forward);
            localPos = root.InverseTransformPoint(hit.point);
            localRot = Quaternion.Inverse(root.rotation) * worldRot;
        }

        private DecalPlacement Fill(DecalPlacement d)
        {
            d.Image = Image ?? "";
            d.Position = new[] { localPos.x, localPos.y, localPos.z };
            d.Rotation = new[] { localRot.x, localRot.y, localRot.z, localRot.w };
            d.Size = new[] { Width, Height, Depth };
            d.Tint = new[] { Tint.r, Tint.g, Tint.b, Tint.a };
            d.Opacity = Opacity;
            d.WrapAngle = WrapAngle;
            d.MirrorX = MirrorX;
            return d;
        }

        private static DecalPlacement Copy(DecalPlacement d) => new DecalPlacement
        {
            Image = d.Image,
            TargetPath = d.TargetPath,
            Position = (float[])d.Position.Clone(),
            Rotation = (float[])d.Rotation.Clone(),
            Size = (float[])d.Size.Clone(),
            Tint = (float[])d.Tint.Clone(),
            Opacity = d.Opacity,
            WrapAngle = d.WrapAngle,
            MirrorX = d.MirrorX,
            MirrorY = d.MirrorY,
        };

        private bool RaycastCar(Ray ray, out RaycastHit result)
        {
            result = default;
            var hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.distance >= best) continue;
                if (h.collider.GetComponentInParent<TrainCar>() != Car) continue;
                best = h.distance;
                result = h;
            }
            return best < float.MaxValue;
        }

        /// <summary>True when the mouse is over our panel or the toolbar strip, so clicks there don't place decals.</summary>
        private bool MouseOverUi()
        {
            if (GUIUtility.hotControl != 0) return true;
            var scale = DerailValleyModToolbar.Main.settings != null ? DerailValleyModToolbar.Main.settings.Scale : 1f;
            var gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
            if (gui.y < Toolbar.Margin * 2 + Toolbar.ButtonSize) return true;
            if (panelWindow == null)
                panelWindow = FindObjectsOfType<ModToolbarWindow>().FirstOrDefault(w => w.Title == DecalPanel.Title);
            return panelWindow != null && panelWindow.Visible && panelWindow.WindowRect.HasValue && panelWindow.WindowRect.Value.Contains(gui);
        }
    }
}
