using System;
using System.Collections;
using System.Linq;
using DerailValleyModToolbar;
using UnityEngine;

namespace DadsDecals
{
    internal enum ToolMode { None, Place, Edit }

    /// <summary>
    /// All mouse work in the world: placing (ghost follows the mouse, click places), editing
    /// (click selects, drag moves, wheel rotates/resizes, Delete removes) and the colour eyedropper.
    /// Aims through PlayerManager.ActiveCamera so the exterior camera works.
    /// </summary>
    internal sealed class Interaction : MonoBehaviour
    {
        public static Interaction? Instance { get; private set; }

        public ToolMode Mode = ToolMode.None;

        /// <summary>Settings for the next decal placed (image or text, size, colour, finish...).</summary>
        public readonly DecalPlacement Template = new DecalPlacement();

        public bool KeepLevel = true;
        public bool SnapRotation;
        public bool MirrorPlace;
        public bool LockAspect = true;

        /// <summary>The car under the mouse most recently (loco, tender, wagon...).</summary>
        public TrainCar? Car { get; private set; }
        public DecalPlacement? Selected { get; private set; }
        public TrainCar? SelectedCar { get; private set; }

        public string AimInfo { get; private set; } = "";
        public string MouseInfo { get; private set; } = "";
        public bool Picking => pickCallback != null;

        // Collision shapes can sit off the visible body, so the box starts this far outside the
        // point we hit and runs inward from there.
        private const float SurfaceMargin = 0.25f;

        private readonly DecalPlacement ghost = new DecalPlacement();
        private readonly DecalPlacement ghostTwin = new DecalPlacement();
        private bool ghostValid;
        private ModToolbarWindow? panelWindow;
        private Action<Color>? pickCallback;

        private bool mouseDownOnSelected;
        private bool dragging;
        private Vector3 mouseDownPos;
        private float lastWheelUndo;

        public static void Create()
        {
            var go = new GameObject("DadsDecals_Interaction");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Interaction>();
        }

        // ---- mode control (from the panel) ---------------------------------------------------

        public void StartPlacing()
        {
            Deselect();
            Mode = ToolMode.Place;
        }

        public void StopPlacing()
        {
            if (Mode == ToolMode.Place) Mode = ToolMode.None;
            ClearGhost();
        }

        public void StartEditing()
        {
            ClearGhost();
            Mode = ToolMode.Edit;
        }

        public void Select(TrainCar car, DecalPlacement d)
        {
            SelectedCar = car;
            Selected = d;
            Car = car;
        }

        public void Deselect()
        {
            Selected = null;
            SelectedCar = null;
            dragging = false;
            mouseDownOnSelected = false;
        }

        public void ForgetCar(TrainCar car)
        {
            if (Car == car) { ClearGhost(); Car = null; }
            if (SelectedCar == car) Deselect();
        }

        public void PickColour(Action<Color> callback) => pickCallback = callback;
        public void CancelPick() => pickCallback = null;

        private void ClearGhost()
        {
            ghostValid = false;
            if (Car == null) return;
            var r = Car.GetComponent<DecalRenderer>();
            if (r != null) { r.Ghost = null; r.GhostTwin = null; }
        }

        // ---- per frame -----------------------------------------------------------------------

        private void Update()
        {
            TextDecals.RenderPending();
            var cam = PlayerManager.ActiveCamera != null ? PlayerManager.ActiveCamera : PlayerManager.PlayerCamera;
            var overUi = MouseOverUi();
            MouseInfo = $"mouse {Input.mousePosition.x:0},{Input.mousePosition.y:0} of {Screen.width}x{Screen.height}, " +
                        $"cursor visible={Cursor.visible} lock={Cursor.lockState}, over UI={overUi}, camera={(cam != null ? cam.name : "none")}";

            if (Mode == ToolMode.None && pickCallback == null) return;
            if (cam == null || !Cursor.visible)
            {
                AimInfo = cam == null ? "waiting: no player camera" : "waiting: cursor hidden";
                return;
            }

            if (pickCallback != null)
            {
                AimInfo = "colour picker: left-click anywhere to sample, right-click to cancel";
                if (overUi) return;
                if (Input.GetMouseButtonDown(1)) { pickCallback = null; return; }
                if (Input.GetMouseButtonDown(0))
                {
                    var cb = pickCallback;
                    pickCallback = null;
                    StartCoroutine(SampleScreen(Input.mousePosition, cb));
                }
                return;
            }

            if (overUi)
            {
                AimInfo = "waiting: mouse is over the panel/toolbar";
                // Keep showing the ghost where it was, so slider changes preview live.
                if (Mode == ToolMode.Place && Car != null && ghostValid) ShowGhost(Car);
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Mode == ToolMode.Place) UpdatePlacing(ray);
            else if (Mode == ToolMode.Edit) UpdateEditing(ray);
        }

        private void UpdatePlacing(Ray ray)
        {
            ApplyWheel(Template, null);

            var hasHit = RaycastAnyCar(ray, null, out var hit, out var hitCar);
            AimInfo = hasHit ? Describe(hit, hitCar!) : "not over a car";
            if (hasHit && hitCar != Car)
            {
                ClearGhost();
                Car = hitCar;
            }
            if (Car == null) return;
            if (hasHit)
            {
                Pose(Car, hit, ghost, Template);
                ghostValid = true;
            }
            if (ghostValid) ShowGhost(Car); else ClearGhost();

            if (Input.GetMouseButtonDown(1)) { StopPlacing(); return; }
            if (hasHit && Input.GetMouseButtonDown(0))
            {
                var layout = Main.Layouts.GetOrCreate(Car);
                Undo.Record(layout);
                var placed = ghost.Clone();
                placed.ChipSeed = UnityEngine.Random.Range(0f, 100f);
                placed.GrimeSeed = UnityEngine.Random.Range(0f, 100f);
                if (MirrorPlace)
                {
                    placed.PairId = Guid.NewGuid().ToString("N");
                    var twin = placed.Mirrored();
                    layout.Decals.Add(placed);
                    layout.Decals.Add(twin);
                }
                else layout.Decals.Add(placed);
                DecalRenderer.Ensure(Car);
            }
        }

        private void ShowGhost(TrainCar car)
        {
            var r = DecalRenderer.Ensure(car);
            // Style may have changed in the panel since the ghost was posed.
            ghost.CopyStyleFrom(Template);
            ghost.Angle = Template.Angle;
            r.Ghost = ghost;
            if (MirrorPlace)
            {
                var m = ghost.Mirrored();
                ghostTwin.CopyStyleFrom(m);
                ghostTwin.Anchor = m.Anchor;
                ghostTwin.Position = m.Position;
                ghostTwin.Rotation = m.Rotation;
                ghostTwin.Angle = m.Angle;
                r.GhostTwin = ghostTwin;
            }
            else r.GhostTwin = null;
        }

        private void UpdateEditing(Ray ray)
        {
            // Dragging sticks to the selected decal's car.
            var hasHit = RaycastAnyCar(ray, dragging ? SelectedCar : null, out var hit, out var hitCar);
            AimInfo = hasHit ? Describe(hit, hitCar!) : "not over a car";
            if (hasHit) Car = hitCar;

            if (Selected != null && SelectedCar != null)
            {
                var layout = Main.Layouts.Get(SelectedCar.CarGUID);
                if (layout == null || !layout.Decals.Contains(Selected)) { Deselect(); return; }

                ApplyWheel(Selected, layout);

                if (Input.GetKeyDown(KeyCode.Delete) && GUIUtility.keyboardControl == 0)
                {
                    Undo.Record(layout);
                    DeleteWithTwin(layout, Selected);
                    Deselect();
                    return;
                }

                if (mouseDownOnSelected && Input.GetMouseButton(0))
                {
                    if (!dragging && (Input.mousePosition - mouseDownPos).sqrMagnitude > 16)
                    {
                        dragging = true;
                        Undo.Record(layout);
                    }
                    if (dragging && hasHit && hitCar == SelectedCar)
                    {
                        Pose(SelectedCar, hit, Selected, Selected);
                        SyncTwin(layout, Selected);
                    }
                }
                if (Input.GetMouseButtonUp(0)) { dragging = false; mouseDownOnSelected = false; }
            }

            if (Input.GetMouseButtonDown(1)) { Deselect(); return; }
            if (Input.GetMouseButtonDown(0))
            {
                mouseDownPos = Input.mousePosition;
                var picked = hasHit ? PickDecal(hitCar!, hit.point) : null;
                if (picked != null)
                {
                    Select(hitCar!, picked);
                    mouseDownOnSelected = true;
                }
                else Deselect();
            }
        }

        // ---- decal maths ---------------------------------------------------------------------

        /// <summary>
        /// Places decal <paramref name="d"/> where the ray hit, keeping its size and roll.
        /// Keep level: project along the nearest car axis with the image upright; otherwise along the surface normal.
        /// </summary>
        private void Pose(TrainCar car, RaycastHit hit, DecalPlacement d, DecalPlacement sizeFrom)
        {
            var anchorPath = DecalTargets.AnchorFor(car, hit.collider.transform);
            var anchor = DecalTargets.ResolveAnchor(car, anchorPath);
            Vector3 forward, up;
            if (KeepLevel)
            {
                var n = anchor.InverseTransformDirection(hit.normal);
                var a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                Vector3 f, u;
                if (a.x >= a.y && a.x >= a.z) { f = new Vector3(-Mathf.Sign(n.x), 0, 0); u = Vector3.up; }
                else if (a.y >= a.z) { f = new Vector3(0, -Mathf.Sign(n.y), 0); u = Vector3.forward; }
                else { f = new Vector3(0, 0, -Mathf.Sign(n.z)); u = Vector3.up; }
                forward = anchor.TransformDirection(f);
                up = anchor.TransformDirection(u);
            }
            else
            {
                forward = -hit.normal;
                up = Vector3.ProjectOnPlane(anchor.up, forward);
                if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(anchor.forward, forward);
            }
            var depth = sizeFrom.Size[2];
            var margin = Mathf.Min(SurfaceMargin, depth * 0.5f);
            var centre = hit.point + forward * (depth * 0.5f - margin);
            var rot = Quaternion.Inverse(anchor.rotation) * Quaternion.LookRotation(forward, up.normalized);
            var local = anchor.InverseTransformPoint(centre);
            d.Anchor = anchorPath;
            d.Position = new[] { local.x, local.y, local.z };
            d.Rotation = new[] { rot.x, rot.y, rot.z, rot.w };
        }

        /// <summary>The smallest decal on this car whose box contains the point.</summary>
        private static DecalPlacement? PickDecal(TrainCar car, Vector3 worldPoint)
        {
            var layout = Main.Layouts.Get(car.CarGUID);
            var renderer = car.GetComponent<DecalRenderer>();
            if (layout == null || renderer == null) return null;
            DecalPlacement? best = null;
            var bestArea = float.MaxValue;
            foreach (var d in layout.Decals)
            {
                var p = renderer.DecalToWorld(d).inverse.MultiplyPoint3x4(worldPoint);
                var zSlack = 0.3f / Mathf.Max(0.05f, d.Size[2]);   // collider may sit off the paint
                if (Mathf.Abs(p.x) > 0.55f || Mathf.Abs(p.y) > 0.55f || Mathf.Abs(p.z) > 0.5f + zSlack) continue;
                var area = d.Size[0] * d.Size[1];
                if (area < bestArea) { best = d; bestArea = area; }
            }
            return best;
        }

        /// <summary>Keeps a linked mirror twin in step with <paramref name="d"/>.</summary>
        public static void SyncTwin(LocoLayout layout, DecalPlacement d)
        {
            if (d.PairId.Length == 0) return;
            var twin = layout.Decals.FirstOrDefault(o => o != d && o.PairId == d.PairId);
            if (twin == null) return;
            var m = d.Mirrored();
            twin.CopyStyleFrom(m);
            twin.Anchor = m.Anchor;
            twin.Position = m.Position;
            twin.Rotation = m.Rotation;
            twin.Angle = m.Angle;
        }

        public static void DeleteWithTwin(LocoLayout layout, DecalPlacement d)
        {
            layout.Decals.Remove(d);
            if (d.PairId.Length > 0) layout.Decals.RemoveAll(o => o.PairId == d.PairId);
        }

        /// <summary>Ctrl+wheel rotates, Shift+wheel resizes (the next decal while placing, the selected one while editing).</summary>
        private void ApplyWheel(DecalPlacement d, LocoLayout? layout)
        {
            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) return;
            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl && !shift) return;
            if (layout != null && Time.unscaledTime - lastWheelUndo > 1f)
            {
                Undo.Record(layout);
                lastWheelUndo = Time.unscaledTime;
            }
            if (ctrl)
            {
                var step = SnapRotation ? 15f : 5f;
                d.Angle = Mathf.Repeat(d.Angle + Mathf.Sign(wheel) * step + 180f, 360f) - 180f;
                if (SnapRotation) d.Angle = Mathf.Round(d.Angle / 15f) * 15f;
            }
            else
            {
                var f = Mathf.Pow(1.1f, Mathf.Sign(wheel));
                d.Size[0] = Mathf.Clamp(d.Size[0] * f, 0.02f, 10f);
                d.Size[1] = Mathf.Clamp(d.Size[1] * f, 0.02f, 10f);
            }
            if (layout != null) SyncTwin(layout, d);
        }

        // ---- input helpers -------------------------------------------------------------------

        private static string Describe(RaycastHit hit, TrainCar car) =>
            $"{car.ID} / {Diagnostics.Path(car.transform, hit.collider.transform)} ({hit.collider.GetType().Name}), {hit.distance:0.0} m";

        /// <summary>Nearest hit on a train car along the ray (only <paramref name="only"/> if given).</summary>
        private static bool RaycastAnyCar(Ray ray, TrainCar? only, out RaycastHit result, out TrainCar? car)
        {
            result = default;
            car = null;
            var hits = Physics.RaycastAll(ray, 80f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.distance >= best) continue;
                var c = h.collider.GetComponentInParent<TrainCar>();
                if (c == null || c.logicCar == null) continue;
                if (only != null && c != only) continue;
                best = h.distance;
                result = h;
                car = c;
            }
            return car != null;
        }

        private static IEnumerator SampleScreen(Vector3 pos, Action<Color> callback)
        {
            yield return new WaitForEndOfFrame();
            var t = new Texture2D(1, 1, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(Mathf.Clamp(pos.x, 0, Screen.width - 1), Mathf.Clamp(pos.y, 0, Screen.height - 1), 1, 1), 0, 0);
            t.Apply();
            var c = t.GetPixel(0, 0);
            Destroy(t);
            c.a = 1;
            callback(c);
        }

        /// <summary>True when the mouse is over our panel or the toolbar strip, so clicks there don't act in the world.</summary>
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
