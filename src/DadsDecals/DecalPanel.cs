using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>The Toolbar panel: palette, placement settings, placed-decal list, orphaned layouts.</summary>
    internal sealed class DecalPanel
    {
        public const string Title = "Dad's Decals";
        private const float ThumbSize = 64f;

        private string lastReport = "";
        private bool showDebug;
        private bool confirmClear;

        /// <summary>The car the panel edits: the one last pointed at while placing, else the one you're on, else your last loco.</summary>
        private static TrainCar? CurrentCar
        {
            get
            {
                var targeted = Placement.Instance.Car;
                if (targeted != null && targeted.logicCar != null) return targeted;
                if (PlayerManager.Car != null) return PlayerManager.Car;
                return PlayerManager.LastLoco;
            }
        }

        public void Draw(Rect rect)
        {
            var car = CurrentCar;
            var placement = Placement.Instance;

            if (Assets.DecalShader == null)
                GUILayout.Label("Decal shader failed to load - see the mod log.");

            LocoLayout? layout = null;
            if (car == null || car.logicCar == null)
            {
                car = null;
                GUILayout.Label("Pick a decal, then point at any loco, tender or wagon.");
            }
            else
            {
                layout = Main.Layouts.Get(car.CarGUID);
                GUILayout.Label($"Editing: {car.ID}  ({car.carLivery?.id})  - {layout?.Decals.Count ?? 0} decal(s)");
            }

            if (placement.Armed) DrawPlacementControls(placement);
            DrawPalette(rect, placement);
            DrawPlaced(layout);
            DrawOrphans(car);
            if (car != null) DrawDebug(car);
        }

        private void DrawPlacementControls(Placement p)
        {
            GUILayout.Space(6);
            GUILayout.Label($"Placing: {p.Image}");
            GUILayout.Label("Left-click any loco, tender or wagon to place. Right-click to stop.");
            var ghostDraws = p.Car != null && p.Car.GetComponent<DecalRenderer>() is DecalRenderer dr ? dr.GhostDraws : 0;
            GUILayout.Label($"Aiming at: {p.AimInfo}\nPreview drawn on {ghostDraws} part(s)\n{p.MouseInfo}");

            var oldWidth = p.Width;
            p.Width = Slider("Width (m)", p.Width, 0.05f, 5f);
            p.LockAspect = GUILayout.Toggle(p.LockAspect, "Keep image proportions");
            if (p.LockAspect)
            {
                if (oldWidth > 0.0001f) p.Height *= p.Width / oldWidth;
            }
            else p.Height = Slider("Height (m)", p.Height, 0.05f, 5f);

            p.Angle = Slider("Rotation (deg)", p.Angle, -180f, 180f);
            p.Depth = Slider("Projection depth (m)", p.Depth, 0.05f, 3f);
            p.WrapAngle = Slider("Wrap angle (deg)", p.WrapAngle, 5f, 89f);
            p.Opacity = Slider("Opacity", p.Opacity, 0f, 1f);
            p.MirrorX = GUILayout.Toggle(p.MirrorX, "Mirror");
            p.Tint.r = Slider("Tint R", p.Tint.r, 0f, 1f);
            p.Tint.g = Slider("Tint G", p.Tint.g, 0f, 1f);
            p.Tint.b = Slider("Tint B", p.Tint.b, 0f, 1f);

            if (GUILayout.Button("Stop placing")) p.Disarm();
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.##}", GUILayout.Width(170));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        private void DrawPalette(Rect rect, Placement placement)
        {
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Decals (click one to place it)");
            if (GUILayout.Button("Reload folder", GUILayout.Width(110)))
            {
                placement.Disarm();
                Main.Library.Reload();
            }
            GUILayout.EndHorizontal();

            if (Main.Library.Images.Count == 0)
                GUILayout.Label($"No PNGs yet. Put some in:\n{Main.DecalsDir}");

            var perRow = Mathf.Max(1, (int)((rect.width - 40f) / (ThumbSize + 6f)));
            foreach (var group in Main.Library.Images.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                if (group.Key.Length > 0) GUILayout.Label(group.Key);
                var items = group.OrderBy(i => i.Name).ToList();
                for (var i = 0; i < items.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    foreach (var img in items.Skip(i).Take(perRow))
                    {
                        var selected = placement.Armed && placement.Image == img.Key;
                        var content = new GUIContent(img.Texture, img.Name);
                        if (GUILayout.Toggle(selected, content, "Button", GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize)) != selected)
                        {
                            if (selected) placement.Disarm();
                            else placement.Arm(img.Key);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawPlaced(LocoLayout? layout)
        {
            if (layout == null || layout.Decals.Count == 0) return;
            GUILayout.Space(8);
            GUILayout.Label($"On {CurrentCar?.ID}");
            for (var i = 0; i < layout.Decals.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}. {layout.Decals[i].Image}");
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    layout.Decals.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo last")) layout.Decals.RemoveAt(layout.Decals.Count - 1);
            if (!confirmClear && GUILayout.Button("Clear all")) confirmClear = true;
            if (confirmClear)
            {
                if (GUILayout.Button("Really clear?")) { layout.Decals.Clear(); confirmClear = false; }
                if (GUILayout.Button("Cancel")) confirmClear = false;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawOrphans(TrainCar? car)
        {
            var orphans = Main.Layouts.Orphans;
            if (orphans.Count == 0) return;
            GUILayout.Space(8);
            GUILayout.Label("Orphaned layouts (from locos the game deleted)");
            foreach (var o in orphans.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{o.CarId} ({o.LiveryId}) - {o.Decals.Count} decal(s)");
                if (car != null && GUILayout.Button("Apply here", GUILayout.Width(80)))
                {
                    Main.Layouts.ApplyOrphan(o, car);
                    DecalRenderer.Ensure(car);
                }
                if (GUILayout.Button("Forget", GUILayout.Width(60))) Main.Layouts.ForgetOrphan(o);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawDebug(TrainCar car)
        {
            GUILayout.Space(8);
            showDebug = GUILayout.Toggle(showDebug, "Debug");
            if (!showDebug) return;
            if (GUILayout.Button("Run diagnostics"))
                lastReport = Diagnostics.Run(car);
            if (lastReport.Length > 0) GUILayout.Label(lastReport);
        }
    }
}
