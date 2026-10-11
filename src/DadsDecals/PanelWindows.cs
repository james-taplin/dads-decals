using System.Linq;
using DerailValleyModToolbar;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>Finds our two Mod Toolbar windows: the main Dad's Decals window (DD) and the Decal toolbox pop-out (DT).</summary>
    internal static class PanelWindows
    {
        private static ModToolbarWindow? main, settings;

        public static ModToolbarWindow? Main => main != null ? main : main = Find(DecalPanel.Title);
        public static ModToolbarWindow? Settings => settings != null ? settings : settings = Find(DecalPanel.SettingsTitle);

        private static ModToolbarWindow? Find(string title) =>
            Object.FindObjectsOfType<ModToolbarWindow>().FirstOrDefault(w => w.Title == title);

        /// <summary>True when the GUI-space point is over one of our open windows.</summary>
        public static bool Contains(Vector2 gui) => Over(Main, gui) || Over(Settings, gui);

        private static bool Over(ModToolbarWindow? w, Vector2 gui) =>
            w != null && w.Visible && w.WindowRect.HasValue && w.WindowRect.Value.Contains(gui);
    }
}
