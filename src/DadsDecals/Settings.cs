using System.Collections.Generic;
using UnityModManagerNet;

namespace DadsDecals
{
    /// <summary>Per-player preferences, saved by UMM to Mods/DadsDecals/Settings.xml.</summary>
    public sealed class DadsSettings : UnityModManager.ModSettings
    {
        /// <summary>The player's own colour swatches, as hex (RRGGBB).</summary>
        public List<string> SavedColours = new List<string>();
        public string LastFont = "Arial";
        /// <summary>Panel sections the player has folded away.</summary>
        public List<string> Collapsed = new List<string>();

        public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);
    }
}
