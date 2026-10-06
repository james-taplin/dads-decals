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
        /// <summary>0 High, 1 Medium, 2 Low: how small (on screen) a decal can get before it's skipped.</summary>
        public int DecalDetail = 1;
        /// <summary>Decals further than this from the camera aren't drawn (metres; 0 = no limit).</summary>
        public float MaxDistance;

        public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);
    }
}
