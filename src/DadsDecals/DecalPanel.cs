using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>The Toolbar panel: Place | Edit | Layouts | Debug tabs.</summary>
    internal sealed class DecalPanel
    {
        public const string Title = "Dad's Decals";
        private const float ThumbSize = 64f;
        private static readonly string[] Tabs = { "Place", "Edit", "Layouts", "Debug" };

        private int tab;
        private string lastReport = "";
        private bool confirmClear;
        private string templateName = "";
        private string exportName = "";
        private string lastExportPath = "";
        private string? confirmDeleteTemplate;
        private bool showOtherTemplates;
        private bool showFontList;
        private string fontFilter = "";
        private readonly Dictionary<string, bool> colourOpen = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> hexEdits = new Dictionary<string, string>();
        private GUIStyle? swatchStyle;
        private GUIStyle? selectedStyle;
        private GUIStyle? sectionStyle;

        private static Interaction I => Interaction.Instance!;

        /// <summary>The car the panel works on: selected, else last pointed at, else the one you're on, else your last loco.</summary>
        private static TrainCar? CurrentCar
        {
            get
            {
                TrainCar? c = I.SelectedCar;
                if (c == null || c.logicCar == null) c = I.Car;
                if (c == null || c.logicCar == null) c = PlayerManager.Car;
                if (c == null || c.logicCar == null) c = PlayerManager.LastLoco;
                return c != null && c.logicCar != null ? c : null;
            }
        }

        public void Draw(Rect rect)
        {
            swatchStyle ??= new GUIStyle(GUI.skin.box) { normal = { background = Texture2D.whiteTexture } };
            selectedStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            sectionStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            // One undo step per slider drag / text edit on the selected decal.
            var e = Event.current;
            var car = CurrentCar;
            var layout = car != null ? Main.Layouts.Get(car.CarGUID) : null;
            if (layout != null && I.Selected != null && (e.type == EventType.MouseDown || e.type == EventType.KeyDown))
                Undo.BeginGesture(layout);
            if (e.type == EventType.MouseUp) Undo.EndGesture();

            if (Assets.DecalShader == null)
                GUILayout.Label("Decal shader failed to load - see the mod log.");

            var newTab = GUILayout.Toolbar(tab, Tabs);
            if (newTab != tab) SwitchTab(newTab);

            GUILayout.Label(car != null
                ? $"Car: {car.ID}  ({car.carLivery?.id})  - {layout?.Decals.Count ?? 0} decal(s)"
                : "Point at a loco, tender or wagon (or get on one).");

            switch (tab)
            {
                case 0: DrawPlaceTab(rect, car); break;
                case 1: DrawEditTab(car, layout); break;
                case 2: DrawLayoutsTab(car, layout); break;
                case 3: DrawDebugTab(car); break;
            }
        }

        /// <summary>A fold-out header. Returns whether the section is open; folded sections are remembered.</summary>
        private bool Section(string id, string title)
        {
            var collapsed = Main.Settings.Collapsed;
            var open = !collapsed.Contains(id);
            if (GUILayout.Button((open ? "▼  " : "►  ") + title, sectionStyle))
            {
                if (open) collapsed.Add(id); else collapsed.Remove(id);
                Main.Settings.Save(Main.Mod);
                open = !open;
            }
            return open;
        }

        private void SwitchTab(int newTab)
        {
            tab = newTab;
            I.StopPlacing();
            I.CancelPick();
            if (tab == 1) I.StartEditing();
            else
            {
                I.Deselect();
                I.Mode = ToolMode.None;
            }
        }

        // ---- Place -------------------------------------------------------------------------

        private void DrawPlaceTab(Rect rect, TrainCar? car)
        {
            var t = I.Template;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(t.Kind == DecalKind.Image, "Image", "Button")) t.Kind = DecalKind.Image;
            if (GUILayout.Toggle(t.Kind == DecalKind.Text, "Text", "Button"))
            {
                if (t.Kind != DecalKind.Text && I.Mode == ToolMode.Place) I.StopPlacing();
                t.Kind = DecalKind.Text;
            }
            GUILayout.EndHorizontal();

            if (I.Mode == ToolMode.Place)
            {
                GUILayout.Label("Left-click a loco, tender or wagon to place. Right-click or the button stops.");
                GUILayout.Label("Ctrl+wheel rotates, Shift+wheel resizes.");
                var dr = I.Car != null ? I.Car.GetComponent<DecalRenderer>() : null;
                GUILayout.Label($"Aiming at: {I.AimInfo}  |  preview on {(dr != null ? dr.GhostDraws : 0)} part(s)");
                if (GUILayout.Button("Stop placing")) I.StopPlacing();
            }

            GUILayout.BeginHorizontal();
            I.KeepLevel = GUILayout.Toggle(I.KeepLevel, "Keep level");
            I.SnapRotation = GUILayout.Toggle(I.SnapRotation, "Snap 15°");
            I.MirrorPlace = GUILayout.Toggle(I.MirrorPlace, "Mirror to other side");
            GUILayout.EndHorizontal();

            if (t.Kind == DecalKind.Image) DrawPalette(rect);
            else
            {
                t.Text ??= new TextSpec { Font = Main.Settings.LastFont };
                if (Section("place.text", "Text")) DrawTextEditor(t.Text, car, "place");
                if (I.Mode != ToolMode.Place && GUILayout.Button("Place this text")) I.StartPlacing();
            }

            GUILayout.Space(6);
            GUILayout.Label("Settings for the next decal");
            DrawStyle(t, car, "place", null);
        }

        private void DrawPalette(Rect rect)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Click an image to place it");
            if (GUILayout.Button("Reload folder", GUILayout.Width(110)))
            {
                I.StopPlacing();
                Main.Library.Reload();
            }
            GUILayout.EndHorizontal();

            if (Main.Library.Images.Count == 0)
                GUILayout.Label($"No PNGs yet. Put some in:\n{Main.DecalsDir}");

            var perRow = Mathf.Max(1, (int)((rect.width - 40f) / (ThumbSize + 6f)));
            foreach (var group in Main.Library.Images.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                var items = group.OrderBy(i => i.Name).ToList();
                if (!Section("cat:" + group.Key, $"{(group.Key.Length > 0 ? group.Key : "General")}  ({items.Count})")) continue;
                for (var i = 0; i < items.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    foreach (var img in items.Skip(i).Take(perRow))
                    {
                        var selected = I.Mode == ToolMode.Place && I.Template.Kind == DecalKind.Image && I.Template.Image == img.Key;
                        if (GUILayout.Toggle(selected, new GUIContent(img.Texture, img.Name), "Button", GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize)) != selected)
                        {
                            if (selected) I.StopPlacing();
                            else
                            {
                                I.Template.Kind = DecalKind.Image;
                                I.Template.Image = img.Key;
                                I.StartPlacing();
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        // ---- Text --------------------------------------------------------------------------

        private void DrawTextEditor(TextSpec s, TrainCar? car, string id)
        {
            GUILayout.Label("Text  (tokens: {n} = 42, {num} = 042, {id} = L-042, {type} = livery)");
            s.Text = GUILayout.TextArea(s.Text, GUILayout.MinHeight(40));
            if (TextDecals.HasTokens(s.Text))
                GUILayout.Label($"On {car?.ID ?? "this car"}: \"{TextDecals.Resolve(s.Text, car)}\"");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Font: {s.Font}");
            if (GUILayout.Button(showFontList ? "Close" : "Choose font", GUILayout.Width(100))) showFontList = !showFontList;
            GUILayout.EndHorizontal();
            if (showFontList)
            {
                fontFilter = GUILayout.TextField(fontFilter);
                foreach (var name in TextDecals.FontNames.Where(n => fontFilter.Length == 0 || n.IndexOf(fontFilter, StringComparison.OrdinalIgnoreCase) >= 0).Take(40))
                {
                    if (GUILayout.Button(name, name == s.Font ? selectedStyle : GUI.skin.button))
                    {
                        s.Font = name;
                        Main.Settings.LastFont = name;
                        Main.Settings.Save(Main.Mod);
                        showFontList = false;
                    }
                }
            }

            GUILayout.BeginHorizontal();
            s.Bold = GUILayout.Toggle(s.Bold, "Bold");
            s.Italic = GUILayout.Toggle(s.Italic, "Italic");
            var align = s.Align == "left" ? 0 : s.Align == "right" ? 2 : 1;
            align = GUILayout.Toolbar(align, new[] { "Left", "Centre", "Right" });
            s.Align = align == 0 ? "left" : align == 2 ? "right" : "center";
            GUILayout.EndHorizontal();

            ColourField("Text colour", id + ".text", s.Color);
            s.Outline = Slider("Outline", s.Outline, 0f, 0.15f);
            if (s.Outline > 0) ColourField("Outline colour", id + ".outline", s.OutlineColor);

            var preview = TextDecals.GetTexture(s, car);
            if (preview != null)
            {
                var h = 48f;
                var w = Mathf.Min(320f, h * preview.width / Mathf.Max(1, preview.height));
                var r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
                GUI.Box(r, GUIContent.none);
                GUI.DrawTexture(r, preview, ScaleMode.ScaleToFit, true);
            }
        }

        // ---- Shared settings block ---------------------------------------------------------

        /// <summary>Size, rotation, colour, finish and weathering for a decal (the next one, or the selected one).</summary>
        private void DrawStyle(DecalPlacement d, TrainCar? car, string id, LocoLayout? layout)
        {
            var before = GUI.changed;
            GUI.changed = false;

            Assets.MaterialFor(d, car, out var aspect, allowRender: false);
            if (Section("style.size", "Size and placement"))
            {
                d.Size[0] = Slider("Width (m)", d.Size[0], 0.02f, 5f);
                I.LockAspect = GUILayout.Toggle(I.LockAspect, "Keep image proportions");
                if (!I.LockAspect) d.Size[1] = Slider("Height (m)", d.Size[1], 0.02f, 5f);

                d.Angle = Slider("Rotation (deg)", d.Angle, -180f, 180f);
                if (I.SnapRotation) d.Angle = Mathf.Round(d.Angle / 15f) * 15f;
                d.Size[2] = Slider("Projection depth (m)", d.Size[2], 0.05f, 3f);
                d.WrapAngle = Slider("Wrap angle (deg)", d.WrapAngle, 5f, 89f);
                d.Opacity = Slider("Opacity", d.Opacity, 0f, 1f);
                GUILayout.BeginHorizontal();
                d.MirrorX = GUILayout.Toggle(d.MirrorX, "Flip horizontally");
                d.MirrorY = GUILayout.Toggle(d.MirrorY, "Flip vertically");
                GUILayout.EndHorizontal();
            }
            // Proportions apply even when the section is folded away.
            if (I.LockAspect && aspect > 0.001f) d.Size[1] = d.Size[0] / aspect;

            if (Section("style.colour", "Colour"))
                ColourField(d.Kind == DecalKind.Text ? "Tint (multiplies text colour)" : "Tint", id + ".tint", d.Tint);

            if (Section("style.finish", "Finish"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Matte paint")) { d.Smoothness = 0.15f; d.Metallic = 0f; GUI.changed = true; }
                if (GUILayout.Button("Gloss vinyl")) { d.Smoothness = 0.75f; d.Metallic = 0f; GUI.changed = true; }
                if (GUILayout.Button("Metal plate")) { d.Smoothness = 0.55f; d.Metallic = 0.9f; GUI.changed = true; }
                GUILayout.EndHorizontal();
                d.Smoothness = Slider("Smoothness", d.Smoothness, 0f, 1f);
                d.Metallic = Slider("Metallic", d.Metallic, 0f, 1f);
                d.Glow = Slider("Glow", d.Glow, 0f, 3f);
            }

            if (Section("style.weathering", "Weathering"))
            {
                GUILayout.BeginHorizontal();
                d.Grime = Slider("Grime", d.Grime, 0f, 1f);
                if (GUILayout.Button("New grime", GUILayout.Width(80))) { d.GrimeSeed = UnityEngine.Random.Range(0f, 100f); GUI.changed = true; }
                GUILayout.EndHorizontal();
                ColourField("Grime colour", id + ".grime", d.GrimeColor);
                GUILayout.BeginHorizontal();
                d.Chipping = Slider("Chipping", d.Chipping, 0f, 1f);
                if (GUILayout.Button("New chips", GUILayout.Width(80))) { d.ChipSeed = UnityEngine.Random.Range(0f, 100f); GUI.changed = true; }
                GUILayout.EndHorizontal();
            }

            if (GUI.changed && layout != null)
            {
                Undo.Changed(layout);
                Interaction.SyncTwin(layout, d);
            }
            GUI.changed |= before;
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.##}", GUILayout.Width(170));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        // ---- Colour picker -----------------------------------------------------------------

        private static readonly (string name, Color c)[] Presets =
        {
            ("White", Color.white), ("Black", new Color(0.05f, 0.05f, 0.05f)), ("Cream", new Color(0.93f, 0.89f, 0.76f)),
            ("Red", new Color(0.7f, 0.08f, 0.06f)), ("Yellow", new Color(0.98f, 0.8f, 0.1f)), ("Gold", new Color(0.83f, 0.66f, 0.25f)),
            ("Silver", new Color(0.75f, 0.75f, 0.75f)), ("Green", new Color(0.12f, 0.3f, 0.2f)),
        };

        /// <summary>A colour row (swatch + hex) that expands to RGB sliders, swatches and an eyedropper.</summary>
        private void ColourField(string label, string id, float[] c)
        {
            var colour = new Color(c[0], c[1], c[2], c[3]);
            colourOpen.TryGetValue(id, out var open);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170));
            Swatch(colour, 28);
            var hex = ColorUtility.ToHtmlStringRGB(colour);
            if (!hexEdits.TryGetValue(id, out var edit) || GUIUtility.keyboardControl == 0) edit = hex;
            var newEdit = GUILayout.TextField(edit, 7, GUILayout.Width(70));
            hexEdits[id] = newEdit;
            if (newEdit != hex && newEdit.Length == 6 && ColorUtility.TryParseHtmlString("#" + newEdit, out var parsed))
            {
                Set(c, parsed);
                GUI.changed = true;
            }
            if (GUILayout.Button(open ? "Close" : "Edit", GUILayout.Width(50))) colourOpen[id] = open = !open;
            GUILayout.EndHorizontal();
            if (!open) return;

            c[0] = Slider("  Red", c[0], 0f, 1f);
            c[1] = Slider("  Green", c[1], 0f, 1f);
            c[2] = Slider("  Blue", c[2], 0f, 1f);

            GUILayout.BeginHorizontal();
            foreach (var (name, pc) in Presets)
                if (SwatchButton(pc, name)) { Set(c, pc); GUI.changed = true; }
            GUILayout.EndHorizontal();

            var saved = Main.Settings.SavedColours;
            if (saved.Count > 0)
            {
                GUILayout.BeginHorizontal();
                foreach (var h in saved.ToList())
                {
                    if (!ColorUtility.TryParseHtmlString("#" + h, out var sc)) continue;
                    if (SwatchButton(sc, "#" + h + " (right-click removes)"))
                    {
                        if (Event.current.button == 1) { saved.Remove(h); Main.Settings.Save(Main.Mod); }
                        else { Set(c, sc); GUI.changed = true; }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save colour"))
            {
                var h = ColorUtility.ToHtmlStringRGB(new Color(c[0], c[1], c[2]));
                if (!saved.Contains(h)) { saved.Add(h); Main.Settings.Save(Main.Mod); }
            }
            if (GUILayout.Button(I.Picking ? "Click in the world..." : "Pick from screen"))
            {
                var target = c;
                var layout = I.SelectedCar != null ? Main.Layouts.Get(I.SelectedCar.CarGUID) : null;
                var selected = I.Selected;
                I.PickColour(col =>
                {
                    if (layout != null && selected != null) Undo.Record(layout);
                    Set(target, col);
                    if (layout != null && selected != null) Interaction.SyncTwin(layout, selected);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("  Picked colours include the game's lighting, so treat them as a starting point.");
        }

        private static void Set(float[] c, Color col)
        {
            c[0] = col.r;
            c[1] = col.g;
            c[2] = col.b;
        }

        private void Swatch(Color c, float size)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUILayout.Box(GUIContent.none, swatchStyle, GUILayout.Width(size), GUILayout.Height(18));
            GUI.backgroundColor = old;
        }

        private bool SwatchButton(Color c, string tooltip)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            var clicked = GUILayout.Button(new GUIContent("", tooltip), swatchStyle, GUILayout.Width(24), GUILayout.Height(18));
            GUI.backgroundColor = old;
            return clicked;
        }

        // ---- Edit --------------------------------------------------------------------------

        private void DrawEditTab(TrainCar? car, LocoLayout? layout)
        {
            if (I.Mode != ToolMode.Edit) I.StartEditing();
            GUILayout.Label("Click a decal on the car to select it. Drag to move. Ctrl+wheel rotates, Shift+wheel resizes, Delete removes, right-click deselects.");
            GUILayout.BeginHorizontal();
            I.KeepLevel = GUILayout.Toggle(I.KeepLevel, "Keep level (when moving)");
            I.SnapRotation = GUILayout.Toggle(I.SnapRotation, "Snap 15°");
            GUILayout.EndHorizontal();

            if (car == null || layout == null || layout.Decals.Count == 0)
            {
                GUILayout.Label("No decals on this car yet.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = Undo.CanUndo(layout);
            if (GUILayout.Button("Undo")) { I.Deselect(); Undo.UndoStep(layout); }
            GUI.enabled = Undo.CanRedo(layout);
            if (GUILayout.Button("Redo")) { I.Deselect(); Undo.RedoStep(layout); }
            GUI.enabled = true;
            if (!confirmClear && GUILayout.Button("Clear all")) confirmClear = true;
            if (confirmClear)
            {
                if (GUILayout.Button("Really clear?")) { Undo.Record(layout); layout.Decals.Clear(); I.Deselect(); confirmClear = false; }
                if (GUILayout.Button("Cancel")) confirmClear = false;
            }
            GUILayout.EndHorizontal();

            if (Section("edit.list", $"Decals on this car ({layout.Decals.Count})"))
            {
                for (var i = 0; i < layout.Decals.Count; i++)
                {
                    var d = layout.Decals[i];
                    var label = $"{i + 1}. {d.DisplayName}{(d.PairId.Length > 0 ? "  (mirrored pair)" : "")}";
                    if (GUILayout.Button(label, d == I.Selected ? selectedStyle : GUI.skin.button)) I.Select(car, d);
                }
            }

            var sel = I.Selected;
            if (sel == null || I.SelectedCar != car) return;

            GUILayout.Space(6);
            GUILayout.Label($"Selected: {sel.DisplayName}");

            // Attach to body / bogie, keeping it where it is in the world.
            var choices = DecalTargets.AnchorChoices(car);
            var current = Math.Max(0, choices.FindIndex(c => c.path == sel.Anchor));
            var picked = GUILayout.Toolbar(current, choices.Select(c => c.label).ToArray());
            if (picked != current)
            {
                Undo.Record(layout);
                Reanchor(car, sel, choices[picked].path);
                Interaction.SyncTwin(layout, sel);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate"))
            {
                Undo.Record(layout);
                var copy = sel.Clone();
                copy.PairId = "";
                copy.Position[1] -= sel.Size[1] * 1.1f;   // just below, so it's visible; drag it into place
                layout.Decals.Add(copy);
                I.Select(car, copy);
            }
            if (sel.PairId.Length == 0)
            {
                if (GUILayout.Button("Mirror to other side"))
                {
                    Undo.Record(layout);
                    sel.PairId = Guid.NewGuid().ToString("N");
                    layout.Decals.Add(sel.Mirrored());
                }
            }
            else if (GUILayout.Button("Unlink mirror"))
            {
                Undo.Record(layout);
                foreach (var d in layout.Decals.Where(o => o.PairId == sel.PairId).ToList()) d.PairId = "";
            }
            if (GUILayout.Button("Delete"))
            {
                Undo.Record(layout);
                Interaction.DeleteWithTwin(layout, sel);
                I.Deselect();
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.EndHorizontal();

            if (sel.Kind == DecalKind.Text && sel.Text != null)
            {
                var before = GUI.changed;
                GUI.changed = false;
                if (Section("edit.text", "Text")) DrawTextEditor(sel.Text, car, "edit");
                if (GUI.changed) { Undo.Changed(layout); Interaction.SyncTwin(layout, sel); }
                GUI.changed |= before;
            }
            DrawStyle(sel, car, "edit", layout);
        }

        private static void Reanchor(TrainCar car, DecalPlacement d, string newPath)
        {
            var oldA = DecalTargets.ResolveAnchor(car, d.Anchor);
            var newA = DecalTargets.ResolveAnchor(car, newPath);
            var worldPos = oldA.TransformPoint(new Vector3(d.Position[0], d.Position[1], d.Position[2]));
            var worldRot = oldA.rotation * new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]);
            var p = newA.InverseTransformPoint(worldPos);
            var r = Quaternion.Inverse(newA.rotation) * worldRot;
            d.Anchor = newPath;
            d.Position = new[] { p.x, p.y, p.z };
            d.Rotation = new[] { r.x, r.y, r.z, r.w };
        }

        // ---- Layouts -----------------------------------------------------------------------

        private void DrawLayoutsTab(TrainCar? car, LocoLayout? layout)
        {
            if (car == null)
            {
                GUILayout.Label("Point at or get on a car first.");
                DrawOrphans(null);
                return;
            }

            // Layouts per livery
            var theme = PaintLink.CurrentTheme(car);
            var linked = layout != null && layout.LinkToPaint;
            var newLinked = GUILayout.Toggle(linked, $"Link layout to paint (current paint: {(theme.Length > 0 ? theme : "unknown")})");
            if (newLinked != linked)
            {
                layout ??= Main.Layouts.GetOrCreate(car);
                PaintLink.SetLinked(layout, car, newLinked);
                DecalRenderer.Ensure(car);
            }
            if (layout != null && layout.LinkToPaint && layout.ByTheme.Count > 0)
                GUILayout.Label("Other paints: " + string.Join(", ", layout.ByTheme.Select(kv => $"{kv.Key} ({kv.Value.Count})")));

            // Templates
            GUILayout.Space(6);
            GUILayout.Label("Templates (shared across saves)");
            GUILayout.BeginHorizontal();
            templateName = GUILayout.TextField(templateName);
            GUI.enabled = layout != null && layout.Decals.Count > 0 && templateName.Trim().Length > 0;
            if (GUILayout.Button("Save as template", GUILayout.Width(130))) { LayoutFiles.SaveTemplate(templateName, layout!); templateName = ""; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var templates = LayoutFiles.ListTemplates();
            var livery = car.carLivery != null ? car.carLivery.id : "";
            var mine = templates.Where(t => t.LiveryId == LayoutFiles.SafeName(livery)).ToList();
            var others = templates.Except(mine).ToList();
            if (mine.Count == 0) GUILayout.Label($"No templates for {livery} yet.");
            foreach (var t in mine) DrawTemplateRow(t, car);
            if (others.Count > 0)
            {
                showOtherTemplates = GUILayout.Toggle(showOtherTemplates, $"Templates for other cars ({others.Count})");
                if (showOtherTemplates) foreach (var t in others) DrawTemplateRow(t, car);
            }

            // Copy from another car
            var sources = Main.Layouts.All.Where(l => l.CarGuid != car.CarGUID && l.Decals.Count > 0).ToList();
            if (sources.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("Copy from another car in this save");
                foreach (var src in sources)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{src.CarId} ({src.LiveryId}) - {src.Decals.Count} decal(s)");
                    if (GUILayout.Button("Copy here", GUILayout.Width(90))) AddDecals(car, src.Decals, replace: false);
                    GUILayout.EndHorizontal();
                }
            }
            DrawOrphans(car);

            // Export / import
            GUILayout.Space(6);
            GUILayout.Label("Share: export this car's layout (with its images) / import one");
            GUILayout.BeginHorizontal();
            exportName = GUILayout.TextField(exportName);
            GUI.enabled = layout != null && layout.Decals.Count > 0 && exportName.Trim().Length > 0;
            if (GUILayout.Button("Export", GUILayout.Width(80))) { lastExportPath = LayoutFiles.Export(exportName, layout!); exportName = ""; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (lastExportPath.Length > 0) GUILayout.Label("Exported to: " + lastExportPath);
            var importable = LayoutFiles.ListImportable();
            if (importable.Count == 0) GUILayout.Label($"To import, put a shared folder in {LayoutFiles.ImportDir}");
            foreach (var dir in importable)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Path.GetFileName(dir) + (dir.StartsWith(LayoutFiles.ExportsDir) ? " (your export)" : ""));
                if (GUILayout.Button("Import here", GUILayout.Width(90))) AddDecals(car, LayoutFiles.Import(dir), replace: false);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawTemplateRow(TemplateInfo t, TrainCar car)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{t.Name}  ({t.LiveryId})");
            if (GUILayout.Button("Add", GUILayout.Width(45))) AddDecals(car, LayoutFiles.LoadTemplate(t), replace: false);
            if (GUILayout.Button("Replace", GUILayout.Width(65))) AddDecals(car, LayoutFiles.LoadTemplate(t), replace: true);
            if (confirmDeleteTemplate == t.Path)
            {
                if (GUILayout.Button("Sure?", GUILayout.Width(50))) { LayoutFiles.DeleteTemplate(t); confirmDeleteTemplate = null; }
            }
            else if (GUILayout.Button("Delete", GUILayout.Width(55))) confirmDeleteTemplate = t.Path;
            GUILayout.EndHorizontal();
        }

        private static void AddDecals(TrainCar car, IEnumerable<DecalPlacement> decals, bool replace)
        {
            var layout = Main.Layouts.GetOrCreate(car);
            Undo.Record(layout);
            I.Deselect();
            if (replace) layout.Decals.Clear();
            layout.Decals.AddRange(decals.Select(d => d.Clone()));
            DecalRenderer.Ensure(car);
        }

        private void DrawOrphans(TrainCar? car)
        {
            var orphans = Main.Layouts.Orphans;
            if (orphans.Count == 0) return;
            GUILayout.Space(6);
            GUILayout.Label("Orphaned layouts (from cars the game deleted)");
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

        // ---- Debug -------------------------------------------------------------------------

        private void DrawDebugTab(TrainCar? car)
        {
            GUILayout.Label("Aiming at: " + I.AimInfo);
            GUILayout.Label(I.MouseInfo);
            if (car != null && GUILayout.Button("Run diagnostics on " + car.ID)) lastReport = Diagnostics.Run(car);
            if (lastReport.Length > 0) GUILayout.Label(lastReport);
        }
    }
}
