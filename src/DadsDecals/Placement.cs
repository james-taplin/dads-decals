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

        /// <summary>The car under the mouse while placing (loco, tender, wagon...). Kept after placing stops.</summary>
        public TrainCar? Car { get; private set; }

        // Settings for the next decal, edited from the panel.
        public float Width = 0.6f;
        public float Height = 0.6f;
        public bool LockAspect = true;
        public float Depth = 1.0f;
        public float Angle;
        public float Opacity = 1f;
        public float WrapAngle = 75f;
        public bool MirrorX;
        public Color Tint = Color.white;

        public string? Image;

        /// <summary>What the mouse is aiming at, for the panel's readout.</summary>
        public string AimInfo { get; private set; } = "";
        public string MouseInfo { get; private set; } = "";

        // Collision shapes can sit off the visible body, so the box starts this far outside the
        // point we hit and runs inward from there.
        private const float SurfaceMargin = 0.25f;
        private bool hasHit;
        private readonly DecalPlacement ghost = new DecalPlacement();
        private ModToolbarWindow? panelWindow;

        public static void Create()
        {
            var go = new GameObject("DadsDecals_Placement");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Placement>();
        }

        public void Arm(string image)
        {
            Disarm();
            Image = image;
            Armed = true;
            var tex = Main.Library.TryGet(image, out var img) ? img.Texture : null;
            if (tex != null && LockAspect) Height = Width * tex.height / tex.width;
        }

        public void Disarm()
        {
            ClearGhost();
            Armed = false;
            hasHit = false;
            AimInfo = "";
        }

        /// <summary>Forget the targeted car, e.g. when it is deleted.</summary>
        public void ForgetCar(TrainCar car)
        {
            if (Car != car) return;
            ClearGhost();
            Car = null;
            hasHit = false;
        }

        private void ClearGhost()
        {
            if (Car == null) return;
            var r = Car.GetComponent<DecalRenderer>();
            if (r != null) r.Ghost = null;
        }

        private void Update()
        {
            if (!Armed || Image == null) return;

            var cam = PlayerManager.PlayerCamera;
            var overUi = MouseOverUi();
            MouseInfo = $"mouse {Input.mousePosition.x:0},{Input.mousePosition.y:0} of {Screen.width}x{Screen.height}, " +
                        $"cursor visible={Cursor.visible} lock={Cursor.lockState}, over UI={overUi}, camera={(cam != null ? cam.name : "none")}";
            if (cam == null || !Cursor.visible || overUi)
            {
                AimInfo = cam == null ? "waiting: no player camera" : !Cursor.visible ? "waiting: cursor hidden" : "waiting: mouse is over the panel/toolbar";
                // Keep showing the ghost where it was, so slider changes preview live.
                if (Car != null) DecalRenderer.Ensure(Car).Ghost = hasHit ? Fill(ghost) : null;
                return;
            }

            hasHit = RaycastAnyCar(cam.ScreenPointToRay(Input.mousePosition), out var hit, out var hitCar);
            AimInfo = hasHit
                ? $"{hitCar!.ID} / {Diagnostics.Path(hitCar.transform, hit.collider.transform)} ({hit.collider.GetType().Name}), {hit.distance:0.0} m"
                : "not over a car";
            if (hasHit && hitCar != Car)
            {
                ClearGhost();
                Car = hitCar;
            }
            if (Car == null) return;
            if (hasHit) Pose(hit);
            var renderer = DecalRenderer.Ensure(Car);
            renderer.Ghost = hasHit ? Fill(ghost) : null;

            if (Input.GetMouseButtonDown(1)) { Disarm(); return; }
            if (hasHit && Input.GetMouseButtonDown(0))
                Main.Layouts.GetOrCreate(Car).Decals.Add(Copy(Fill(ghost)));
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
            var margin = Mathf.Min(SurfaceMargin, Depth * 0.5f);
            localPos = root.InverseTransformPoint(hit.point + forward * (Depth * 0.5f - margin));
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

        /// <summary>Nearest hit on any train car along the ray (skips the player and scenery).</summary>
        private static bool RaycastAnyCar(Ray ray, out RaycastHit result, out TrainCar? car)
        {
            result = default;
            car = null;
            var hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.distance >= best) continue;
                var c = h.collider.GetComponentInParent<TrainCar>();
                if (c == null || c.logicCar == null) continue;
                best = h.distance;
                result = h;
                car = c;
            }
            return car != null;
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
