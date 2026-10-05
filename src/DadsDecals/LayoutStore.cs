using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DadsDecals
{
    /// <summary>One placed decal, stored relative to the transform it is attached to.</summary>
    public sealed class DecalPlacement
    {
        public string Image = "";          // DecalImage.Key
        public string TargetPath = "";     // transform path under the TrainCar root
        public float[] Position = { 0, 0, 0 };
        public float[] Rotation = { 0, 0, 0, 1 };  // quaternion xyzw
        public float[] Size = { 1, 1, 0.2f };      // width, height, projection depth (metres)
        public float[] Tint = { 1, 1, 1, 1 };
        public float Opacity = 1f;
        public float WrapAngle = 75f;      // max surface angle (degrees) the decal will wrap onto
        public bool MirrorX;
        public bool MirrorY;
    }

    public sealed class LocoLayout
    {
        public string CarGuid = "";
        public string CarId = "";          // e.g. L-014, for display only; changes if the loco is regenerated
        public string LiveryId = "";
        public List<DecalPlacement> Decals = new List<DecalPlacement>();
    }

    /// <summary>
    /// Per-loco layouts, keyed by TrainCar.CarGUID and stored in the save game.
    /// The GUID survives save/load (CarSpawner.SpawnLoadedCar restores it) but is lost if the
    /// game deletes and regenerates the loco, so deleted locos' layouts are kept as "orphans"
    /// the player can re-apply.
    /// </summary>
    public sealed class LayoutStore
    {
        public const string SaveKey = "MOD_DADSDECALS";
        private const int FormatVersion = 1;
        private const int MaxOrphans = 20;

        private readonly Dictionary<string, LocoLayout> layouts = new Dictionary<string, LocoLayout>();
        private readonly List<LocoLayout> orphans = new List<LocoLayout>();

        public IReadOnlyList<LocoLayout> Orphans => orphans;

        public LocoLayout? Get(string carGuid) => layouts.TryGetValue(carGuid, out var l) ? l : null;

        public LocoLayout GetOrCreate(TrainCar car)
        {
            if (!layouts.TryGetValue(car.CarGUID, out var layout))
            {
                layout = new LocoLayout { CarGuid = car.CarGUID };
                layouts[car.CarGUID] = layout;
            }
            layout.CarId = car.ID;
            layout.LiveryId = car.carLivery != null ? car.carLivery.id : "";
            return layout;
        }

        /// <summary>Called when the game deletes a car. Keeps its layout around for re-applying.</summary>
        public void OnCarDeleted(TrainCar car)
        {
            if (!layouts.TryGetValue(car.CarGUID, out var layout)) return;
            layouts.Remove(car.CarGUID);
            if (layout.Decals.Count == 0) return;
            orphans.Insert(0, layout);
            if (orphans.Count > MaxOrphans) orphans.RemoveRange(MaxOrphans, orphans.Count - MaxOrphans);
            Main.Log.Log($"Loco {layout.CarId} was deleted; kept its {layout.Decals.Count} decal(s) as an orphaned layout");
        }

        /// <summary>Adds an orphaned layout's decals to this car and drops the orphan.</summary>
        public void ApplyOrphan(LocoLayout orphan, TrainCar car)
        {
            GetOrCreate(car).Decals.AddRange(orphan.Decals);
            orphans.Remove(orphan);
        }

        public void ForgetOrphan(LocoLayout orphan) => orphans.Remove(orphan);

        public void Clear()
        {
            layouts.Clear();
            orphans.Clear();
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["version"] = FormatVersion,
                ["layouts"] = JArray.FromObject(layouts.Values.Where(l => l.Decals.Count > 0)),
                ["orphans"] = JArray.FromObject(orphans),
            };
        }

        public void FromJson(JObject? data)
        {
            Clear();
            if (data == null) return;
            foreach (var l in data["layouts"]?.ToObject<List<LocoLayout>>() ?? new List<LocoLayout>())
                if (!string.IsNullOrEmpty(l.CarGuid)) layouts[l.CarGuid] = l;
            orphans.AddRange(data["orphans"]?.ToObject<List<LocoLayout>>() ?? new List<LocoLayout>());
            Main.Log.Log($"Loaded {layouts.Count} loco layout(s), {orphans.Count} orphan(s) from save");
        }
    }
}
