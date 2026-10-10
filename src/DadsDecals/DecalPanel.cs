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
        public const string SettingsTitle = "Decal toolbox";
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
        private GUIStyle? panelStyle;
        private GUIStyle? statusStyle;
        private GUIStyle? carStyle;
        private GUIStyle? unitStyle;

        private const float LabelWidth = 110f;
        private bool settingsWanted;        // last automatic open/close decision for the settings window
        private bool settingsPlaced;
        private readonly Dictionary<string, string> numberEdits = new Dictionary<string, string>();

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

        /// <summary>Frame the panel was last drawn; Interaction pauses when it stops being drawn.</summary>
        public static int LastDrawFrame = -100;

        public void Draw(Rect rect)
        {
            LastDrawFrame = Time.frameCount;
            EnsureStyles();

            var car = CurrentCar;
            var layout = car != null ? Main.Layouts.Get(car.CarGUID) : null;
            TrackGesture(layout);

            // Solid background (the Toolbar window itself is see-through), at least as tall as the window.
            GUILayout.BeginVertical(panelStyle, GUILayout.MinHeight(Mathf.Max(100f, rect.height - 40f)));

            if (Assets.DecalShader == null)
                GUILayout.Label("Decal shader failed to load - see the mod log.");

            var newTab = GUILayout.Toolbar(tab, Tabs);
            if (newTab != tab) SwitchTab(newTab);
            DrawStatus(car, layout);

            switch (tab)
            {
                case 0: DrawPlaceTab(rect, car); break;
                case 1: DrawEditTab(rect, car, layout); break;
                case 2: DrawLayoutsTab(car, layout); break;
                case 3: DrawDebugTab(car); break;
            }
            GUILayout.EndVertical();
            SyncSettingsWindow();
        }

        /// <summary>One undo step per slider drag / text edit on the selected decal (each window gets its own events).</summary>
        private static void TrackGesture(LocoLayout? layout)
        {
            var e = Event.current;
            if (layout != null && I.Selected != null && (e.type == EventType.MouseDown || e.type == EventType.KeyDown))
                Undo.BeginGesture(layout);
            if (e.type == EventType.MouseUp) Undo.EndGesture();
        }

        /// <summary>
        /// Opens the settings window when you start placing or select a decal, and closes it when you
        /// stop. It only acts when that changes, so you can still open or close it yourself in between.
        /// </summary>
        private void SyncSettingsWindow()
        {
            var want = I.Mode == ToolMode.Place || (I.Mode == ToolMode.Edit && I.Selected != null);
            if (want == settingsWanted) return;
            settingsWanted = want;
            if (want) OpenSettings(); else PanelWindows.Settings?.Hide();
        }

        private void OpenSettings()
        {
            var w = PanelWindows.Settings;
            if (w == null) return;
            // First time, if it has never been moved, put it beside the main window instead of on top of it.
            var main = PanelWindows.Main;
            if (!settingsPlaced && main != null && main.WindowRect.HasValue && (!w.WindowRect.HasValue || w.WindowRect.Value.position == main.WindowRect.Value.position))
            {
                var m = main.WindowRect.Value;
                w.Position(new Rect(m.xMax + 8f, m.y, w.WindowRect?.width ?? 400f, w.WindowRect?.height ?? 600f));
            }
            settingsPlaced = true;
            w.Show();
        }

        private void ToggleSettings()
        {
            var w = PanelWindows.Settings;
            if (w == null) return;
            if (w.Visible) w.Hide(); else OpenSettings();
        }

        // ---- Settings window ---------------------------------------------------------------

        /// <summary>The pop-out: the selected decal's actions and settings, or the settings for the next decal.</summary>
        public void DrawSettings(Rect rect)
        {
            EnsureStyles();
            // Follows the main window: closing that closes this too.
            if (PanelWindows.Main != null && !PanelWindows.Main.Visible) { PanelWindows.Settings?.Hide(); return; }

            var car = CurrentCar;
            var layout = car != null ? Main.Layouts.Get(car.CarGUID) : null;
            TrackGesture(layout);
            GUILayout.BeginVertical(panelStyle, GUILayout.MinHeight(Mathf.Max(100f, rect.height - 40f)));

            var sel = I.Selected;
            if (I.Mode == ToolMode.Edit && sel != null && car != null && layout != null && I.SelectedCar == car && layout.Decals.Contains(sel))
            {
                GUILayout.Label($"<b>Selected</b>  {Safe(sel.DisplayName)}", RichLabel);
                if (!DrawSelectedActions(car, layout, sel))
                {
                    // Deleted: stop here this frame so the layout doesn't change mid-draw.
                    GUILayout.EndVertical();
                    return;
                }
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
            else if (I.Mode == ToolMode.Edit)
                GUILayout.Label("Select a decal on the car, or in the Decals list, to change it here.", RichLabel);
            else
            {
                GUILayout.Label($"<b>Next decal</b>  {Safe(I.Template.DisplayName)}", RichLabel);
                DrawStyle(I.Template, car, "place", null);
            }
            GUILayout.EndVertical();
        }

        private void EnsureStyles()
        {
            swatchStyle ??= new GUIStyle(GUI.skin.box) { normal = { background = Texture2D.whiteTexture } };
            selectedStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            sectionStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            if (panelStyle == null)
            {
                // Unity's own dark grey, just opaque, so scenery doesn't show through the text.
                var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixel(0, 0, new Color(0.18f, 0.18f, 0.19f, 1f));
                tex.Apply();
                panelStyle = new GUIStyle { normal = { background = tex }, padding = new RectOffset(6, 6, 6, 6) };
            }
            statusStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, padding = new RectOffset(8, 8, 5, 5) };
            carStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            unitStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(2, 0, 0, 0) };
        }

        /// <summary>One line saying what the mouse will do now, plus the car being worked on.</summary>
        private void DrawStatus(TrainCar? car, LocoLayout? layout)
        {
            string what;
            if (I.Mode == ToolMode.Place)
            {
                var over = I.Car != null ? I.Car.GetComponent<DecalRenderer>() : null;
                var aim = over != null && over.GhostDraws > 0 ? $"  ·  over {I.Car!.ID}" : "  ·  point at a car";
                what = $"<color=#5DCAA5>●</color> <b>Placing</b> {Safe(I.Template.DisplayName)}{aim}\n" +
                       "Left-click places · right-click stops · Ctrl+wheel rotates · Shift+wheel resizes";
            }
            else if (I.Mode == ToolMode.Edit && I.Selected != null)
                what = $"<color=#5DCAA5>●</color> <b>Editing</b> {Safe(I.Selected.DisplayName)}\n" +
                       "Drag moves · Ctrl+wheel rotates · Shift+wheel resizes · Delete removes · right-click deselects";
            else if (I.Mode == ToolMode.Edit)
                what = "<b>Edit</b>  ·  click a decal on the car to select it";
            else if (tab == 0)
                what = "<b>Place</b>  ·  pick an image, or write some text, then click a car";
            else
                what = "";

            GUILayout.BeginHorizontal(statusStyle);
            GUILayout.Label(what, RichLabel, GUILayout.ExpandWidth(true));
            GUILayout.Label(car != null ? $"{car.ID} · {car.carLivery?.id} · {layout?.Decals.Count ?? 0} decal(s)" : "Point at a car, or get on one", carStyle);
            GUILayout.EndHorizontal();
        }

        private static GUIStyle? richLabel;
        private static GUIStyle RichLabel => richLabel ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };

        /// <summary>Text from the player shown inside rich text: stop it being read as markup.</summary>
        private static string Safe(string s) => s.Replace("<", "‹").Replace(">", "›");

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

            GUILayout.BeginHorizontal();
            I.KeepLevel = GUILayout.Toggle(I.KeepLevel, "Keep level");
            I.SnapRotation = GUILayout.Toggle(I.SnapRotation, "Snap 15°");
            I.MirrorPlace = GUILayout.Toggle(I.MirrorPlace, "Mirror to other side");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (I.Mode == ToolMode.Place && GUILayout.Button("Stop placing")) I.StopPlacing();
            if (GUILayout.Button(PanelWindows.Settings != null && PanelWindows.Settings.Visible ? "Hide toolbox" : "Open toolbox")) ToggleSettings();
            GUILayout.EndHorizontal();

            if (t.Kind == DecalKind.Image) DrawPalette(rect.width);
            else
            {
                t.Text ??= new TextSpec { Font = Main.Settings.LastFont };
                if (Section("place.text", "Text")) DrawTextEditor(t.Text, car, "place");
                if (I.Mode != ToolMode.Place && GUILayout.Button("Place this text")) I.StartPlacing();
            }
        }

        private void DrawPalette(float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload folder", GUILayout.Width(110)))
            {
                I.StopPlacing();
                Main.Library.Reload();
            }
            GUILayout.EndHorizontal();

            if (Main.Library.Images.Count == 0)
                GUILayout.Label($"No PNGs yet. Put some in:\n{Main.DecalsDir}");

            var perRow = Mathf.Max(1, (int)((width - 20f) / (ThumbSize + 6f)));
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
            s.Outline = Slider(id + ".outline", "Outline", s.Outline, 0f, 0.15f, 0f, "%", 100f, "0");
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
                d.Size[0] = Slider(id + ".w", "Width", d.Size[0], 0.02f, 5f, 0.6f, "m");
                I.LockAspect = GUILayout.Toggle(I.LockAspect, "Keep image proportions");
                if (!I.LockAspect) d.Size[1] = Slider(id + ".h", "Height", d.Size[1], 0.02f, 5f, 0.6f, "m");

                d.Angle = Slider(id + ".rot", "Rotation", d.Angle, -180f, 180f, 0f, "°", 1f, "0.#");
                if (I.SnapRotation) d.Angle = Mathf.Round(d.Angle / 15f) * 15f;
                d.Size[2] = Slider(id + ".depth", "Depth", d.Size[2], 0.05f, 3f, 1f, "m");
                d.WrapAngle = Slider(id + ".wrap", "Wrap angle", d.WrapAngle, 5f, 89f, 75f, "°", 1f, "0");
                d.Opacity = Slider(id + ".opacity", "Opacity", d.Opacity, 0f, 1f, 1f, "%", 100f, "0");
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
                d.Smoothness = Slider(id + ".smooth", "Smoothness", d.Smoothness, 0f, 1f, 0.35f, "%", 100f, "0");
                d.Metallic = Slider(id + ".metal", "Metallic", d.Metallic, 0f, 1f, 0f, "%", 100f, "0");
                d.Glow = Slider(id + ".glow", "Glow", d.Glow, 0f, 3f, 0f);
            }

            if (Section("style.weathering", "Weathering"))
            {
                GUILayout.BeginHorizontal();
                d.Grime = Slider(id + ".grime", "Grime", d.Grime, 0f, 1f, 0f, "%", 100f, "0");
                if (GUILayout.Button("New grime", GUILayout.Width(80))) { d.GrimeSeed = UnityEngine.Random.Range(0f, 100f); GUI.changed = true; }
                GUILayout.EndHorizontal();
                ColourField("Grime colour", id + ".grime", d.GrimeColor);
                GUILayout.BeginHorizontal();
                d.Chipping = Slider(id + ".chip", "Chipping", d.Chipping, 0f, 1f, 0f, "%", 100f, "0");
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

        /// <summary>
        /// A settings row: label, slider, a box to type an exact value, its unit, and Reset.
        /// <paramref name="scale"/> converts the stored value to the shown one (100 for percentages).
        /// </summary>
        private float Slider(string id, string label, float value, float min, float max, float defaultValue, string unit = "", float scale = 1f, string format = "0.##")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.MinWidth(60f));

            // While the box has focus, keep what's being typed; otherwise show the current value.
            var shown = (value * scale).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            var focused = GUI.GetNameOfFocusedControl() == id;
            if (!focused || !numberEdits.TryGetValue(id, out var text)) text = shown;
            GUI.SetNextControlName(id);
            var typed = GUILayout.TextField(text, 8, GUILayout.Width(56f));
            numberEdits[id] = typed;
            if (typed != text && float.TryParse(typed.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                value = Mathf.Clamp(parsed / scale, min, max);
            GUILayout.Label(unit, unitStyle, GUILayout.Width(18f));

            if (GUILayout.Button(new GUIContent("Reset", $"Back to {(defaultValue * scale).ToString(format, System.Globalization.CultureInfo.InvariantCulture)}{unit}"), GUILayout.Width(48f)))
            {
                value = defaultValue;
                numberEdits.Remove(id);
                GUI.FocusControl(null);
                GUI.changed = true;
            }
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
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
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

            c[0] = Slider(id + ".r", "  Red", c[0], 0f, 1f, 1f, "", 255f, "0");
            c[1] = Slider(id + ".g", "  Green", c[1], 0f, 1f, 1f, "", 255f, "0");
            c[2] = Slider(id + ".b", "  Blue", c[2], 0f, 1f, 1f, "", 255f, "0");

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

        // The buttons are disabled when the move would do nothing, so every click is a real change.
        private static void Restack(LocoLayout layout, DecalPlacement sel, Layers.Move move)
        {
            Undo.Record(layout);
            Layers.Apply(layout.Decals, sel, move);
        }

        private void DrawEditTab(Rect rect, TrainCar? car, LocoLayout? layout)
        {
            if (I.Mode != ToolMode.Edit) I.StartEditing();
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

            if (I.Selected != null && I.SelectedCar == car && GUILayout.Button(PanelWindows.Settings != null && PanelWindows.Settings.Visible ? "Hide toolbox" : "Open toolbox"))
                ToggleSettings();

            if (Section("edit.list", $"Decals on this car ({layout.Decals.Count}), top layer first"))
            {
                // Bottom-first list order is the stacking order; show it top-first like a layer list.
                for (var i = layout.Decals.Count - 1; i >= 0; i--)
                {
                    var d = layout.Decals[i];
                    var label = $"{i + 1}. {d.DisplayName}{(d.PairId.Length > 0 ? "  (mirrored pair)" : "")}";
                    if (GUILayout.Button(label, d == I.Selected ? selectedStyle : GUI.skin.button)) I.Select(car, d);
                }
            }
        }

        /// <summary>Settings window, top: attach to body/bogie, duplicate, mirror, delete and layer for the selected decal. False if it was deleted.</summary>
        private bool DrawSelectedActions(TrainCar car, LocoLayout layout, DecalPlacement sel)
        {
            // Attach to body / bogie, keeping it where it is in the world.
            var choices = DecalTargets.AnchorChoices(car);
            var current = Math.Max(0, choices.FindIndex(c => c.path == sel.Anchor));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Attached to", GUILayout.Width(LabelWidth));
            var picked = GUILayout.Toolbar(current, choices.Select(c => c.label).ToArray());
            GUILayout.EndHorizontal();
            if (picked != current)
            {
                Undo.Record(layout);
                Reanchor(car, sel, choices[picked].path);
                Interaction.SyncTwin(layout, sel);
            }

            // Stacking order: which decal shows on top where decals overlap.
            var (layer, layers) = Layers.Position(layout.Decals, sel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Layer {layer} of {layers}", GUILayout.Width(LabelWidth));
            GUI.enabled = layer > 1;
            if (GUILayout.Button("To back")) Restack(layout, sel, Layers.Move.ToBack);
            if (GUILayout.Button("Backward")) Restack(layout, sel, Layers.Move.Backward);
            GUI.enabled = layer < layers;
            if (GUILayout.Button("Forward")) Restack(layout, sel, Layers.Move.Forward);
            if (GUILayout.Button("To front")) Restack(layout, sel, Layers.Move.ToFront);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

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
                return false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            return true;
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

        private static readonly string[] DetailNames = { "High", "Medium", "Low" };

        private void DrawDebugTab(TrainCar? car)
        {
            if (Section("debug.perf", "Performance"))
            {
                var s = Main.Settings;
                GUILayout.Label("Decal detail: skips decals that are only a few pixels big on screen (Medium: under 2 px, Low: under 6 px). High draws everything.");
                var detail = GUILayout.Toolbar(s.DecalDetail, DetailNames);
                GUILayout.Label(s.MaxDistance > 0 ? $"Max draw distance: {s.MaxDistance:0} m" : "Max draw distance: no limit");
                var dist = Mathf.Round(GUILayout.HorizontalSlider(s.MaxDistance, 0f, 2000f) / 50f) * 50f;
                if (detail != s.DecalDetail || !Mathf.Approximately(dist, s.MaxDistance))
                {
                    s.DecalDetail = detail;
                    s.MaxDistance = dist;
                    s.Save(Main.Mod);
                }
                GUILayout.Label($"Last frame: {RenderStats.LastDecals} decal(s) drawn on {RenderStats.LastDraws} part(s) across {RenderStats.LastCars} car(s), {RenderStats.LastCulled} skipped.");
                GUILayout.Label($"Dad's Decals CPU time: {RenderStats.AverageMs:0.00} ms per frame (average).");
            }

            var aimed = I.Car != null ? I.Car.GetComponent<DecalRenderer>() : null;
            GUILayout.Label($"Aiming at: {I.AimInfo}  |  preview on {(aimed != null ? aimed.GhostDraws : 0)} part(s)");
            GUILayout.Label(I.MouseInfo);
            if (car != null && GUILayout.Button("Run diagnostics on " + car.ID)) lastReport = Diagnostics.Run(car);
            if (lastReport.Length > 0) GUILayout.Label(lastReport);
        }
    }
}
