using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DadsDecals
{
    public static class DecalKind
    {
        public const string Image = "image";
        public const string Text = "text";
    }

    /// <summary>Settings for a text decal. The text may contain tokens like {n}, resolved per car.</summary>
    public sealed class TextSpec
    {
        public string Text = "{n}";
        public string Font = "Arial";
        public bool Bold = true;
        public bool Italic;
        public float[] Color = { 1, 1, 1, 1 };
        public float[] OutlineColor = { 0, 0, 0, 1 };
        public float Outline;              // outline thickness as a fraction of the letter height (0..0.15)
        public string Align = "center";    // left | center | right (multi-line text)

        public TextSpec Clone()
        {
            var c = (TextSpec)MemberwiseClone();
            c.Color = (float[])Color.Clone();
            c.OutlineColor = (float[])OutlineColor.Clone();
            return c;
        }
    }

    /// <summary>One placed decal, stored relative to its anchor transform (car body or a bogie).</summary>
    public sealed class DecalPlacement
    {
        public string Kind = DecalKind.Image;
        public string Image = "";          // DecalImage.Key, for image decals
        public TextSpec? Text;             // for text decals

        public string Anchor = "";         // transform path under the TrainCar root; "" = car body
        public float[] Position = { 0, 0, 0 };
        public float[] Rotation = { 0, 0, 0, 1 };  // base orientation (quaternion xyzw), without Angle
        public float Angle;                // roll around the projection axis, degrees
        public float[] Size = { 0.6f, 0.6f, 1f };  // width, height, projection depth (metres)

        public float[] Tint = { 1, 1, 1, 1 };
        public float Opacity = 1f;
        public float WrapAngle = 75f;      // max surface angle (degrees) the decal will wrap onto
        public bool MirrorX;
        public bool MirrorY;

        public float Smoothness = 0.35f;
        public float Metallic;
        public float Grime;                // procedural grime strength
        public float GrimeSeed;
        public float[] GrimeColor = { 0.18f, 0.15f, 0.12f, 1f };
        public float Chipping;
        public float ChipSeed;
        public float Glow;

        public string PairId = "";         // shared by a decal and its linked mirror twin

        public DecalPlacement Clone()
        {
            var c = (DecalPlacement)MemberwiseClone();
            c.Text = Text?.Clone();
            c.Position = (float[])Position.Clone();
            c.Rotation = (float[])Rotation.Clone();
            c.Size = (float[])Size.Clone();
            c.Tint = (float[])Tint.Clone();
            c.GrimeColor = (float[])GrimeColor.Clone();
            return c;
        }

        /// <summary>Copies everything except where it is (pose, anchor) and its pairing.</summary>
        public void CopyStyleFrom(DecalPlacement o)
        {
            Kind = o.Kind;
            Image = o.Image;
            Text = o.Text?.Clone();
            Size = new[] { o.Size[0], o.Size[1], o.Size[2] };
            Tint = (float[])o.Tint.Clone();
            Opacity = o.Opacity;
            WrapAngle = o.WrapAngle;
            MirrorX = o.MirrorX;
            MirrorY = o.MirrorY;
            Smoothness = o.Smoothness;
            Metallic = o.Metallic;
            Grime = o.Grime;
            GrimeSeed = o.GrimeSeed;
            GrimeColor = (float[])o.GrimeColor.Clone();
            Chipping = o.Chipping;
            ChipSeed = o.ChipSeed;
            Glow = o.Glow;
        }

        /// <summary>
        /// This decal reflected across its anchor's centre line (local X = 0), so it lands on the
        /// other side of the car. Reflecting by M = diag(-1,1,1) conjugates the rotation to
        /// (x, -y, -z, w) and negates the roll; the image still reads correctly from outside.
        /// </summary>
        public DecalPlacement Mirrored()
        {
            var m = Clone();
            m.Position = new[] { -Position[0], Position[1], Position[2] };
            m.Rotation = new[] { Rotation[0], -Rotation[1], -Rotation[2], Rotation[3] };
            m.Angle = -Angle;
            return m;
        }

        public string DisplayName => Kind == DecalKind.Text ? $"Text \"{Text?.Text}\"" : Image;
    }

    public sealed class LocoLayout
    {
        public string CarGuid = "";
        public string CarId = "";          // e.g. L-014, for display only; changes if the loco is regenerated
        public string LiveryId = "";
        public List<DecalPlacement> Decals = new List<DecalPlacement>();

        /// <summary>When on, each paint theme has its own set of decals, swapped when the paint changes.</summary>
        public bool LinkToPaint;
        public string CurrentTheme = "";
        public Dictionary<string, List<DecalPlacement>> ByTheme = new Dictionary<string, List<DecalPlacement>>();

        public bool IsEmpty => Decals.Count == 0 && ByTheme.Values.All(l => l.Count == 0);
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
        private const int FormatVersion = 2;
        private const int MaxOrphans = 20;

        private readonly Dictionary<string, LocoLayout> layouts = new Dictionary<string, LocoLayout>();
        private readonly List<LocoLayout> orphans = new List<LocoLayout>();

        public IReadOnlyList<LocoLayout> Orphans => orphans;
        public IEnumerable<LocoLayout> All => layouts.Values;

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
            Undo.Forget(car.CarGUID);
            if (layout.IsEmpty) return;
            orphans.Insert(0, layout);
            if (orphans.Count > MaxOrphans) orphans.RemoveRange(MaxOrphans, orphans.Count - MaxOrphans);
            Main.Log.Log($"Loco {layout.CarId} was deleted; kept its {layout.Decals.Count} decal(s) as an orphaned layout");
        }

        /// <summary>Adds an orphaned layout's decals to this car and drops the orphan.</summary>
        public void ApplyOrphan(LocoLayout orphan, TrainCar car)
        {
            var layout = GetOrCreate(car);
            Undo.Record(layout);
            layout.Decals.AddRange(orphan.Decals.Select(d => d.Clone()));
            orphans.Remove(orphan);
        }

        public void ForgetOrphan(LocoLayout orphan) => orphans.Remove(orphan);

        public void Clear()
        {
            layouts.Clear();
            orphans.Clear();
            Undo.Clear();
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["version"] = FormatVersion,
                ["layouts"] = JArray.FromObject(layouts.Values.Where(l => !l.IsEmpty)),
                ["orphans"] = JArray.FromObject(orphans),
            };
        }

        public void FromJson(JObject? data)
        {
            Clear();
            if (data == null) return;
            // v1 had no Kind/Angle/finish fields; their defaults are the v1 behaviour, so it loads as-is.
            foreach (var l in data["layouts"]?.ToObject<List<LocoLayout>>() ?? new List<LocoLayout>())
                if (!string.IsNullOrEmpty(l.CarGuid)) layouts[l.CarGuid] = l;
            orphans.AddRange(data["orphans"]?.ToObject<List<LocoLayout>>() ?? new List<LocoLayout>());
            Main.Log.Log($"Loaded {layouts.Count} loco layout(s), {orphans.Count} orphan(s) from save");
        }
    }

    /// <summary>Per-car undo/redo of the decal list (in memory only, up to 50 steps each way).</summary>
    public static class Undo
    {
        private const int Max = 50;

        private sealed class Stacks
        {
            public readonly List<List<DecalPlacement>> Back = new List<List<DecalPlacement>>();
            public readonly List<List<DecalPlacement>> Forward = new List<List<DecalPlacement>>();
        }

        private static readonly Dictionary<string, Stacks> stacks = new Dictionary<string, Stacks>();

        private static Stacks For(LocoLayout l)
        {
            if (!stacks.TryGetValue(l.CarGuid, out var s)) stacks[l.CarGuid] = s = new Stacks();
            return s;
        }

        private static List<DecalPlacement> Snapshot(LocoLayout l) => l.Decals.Select(d => d.Clone()).ToList();

        /// <summary>Call before changing a layout's decals.</summary>
        public static void Record(LocoLayout l)
        {
            var s = For(l);
            s.Back.Add(Snapshot(l));
            if (s.Back.Count > Max) s.Back.RemoveAt(0);
            s.Forward.Clear();
        }

        public static bool CanUndo(LocoLayout l) => For(l).Back.Count > 0;
        public static bool CanRedo(LocoLayout l) => For(l).Forward.Count > 0;

        public static void UndoStep(LocoLayout l)
        {
            var s = For(l);
            if (s.Back.Count == 0) return;
            s.Forward.Add(Snapshot(l));
            l.Decals = s.Back[s.Back.Count - 1];
            s.Back.RemoveAt(s.Back.Count - 1);
        }

        public static void RedoStep(LocoLayout l)
        {
            var s = For(l);
            if (s.Forward.Count == 0) return;
            s.Back.Add(Snapshot(l));
            l.Decals = s.Forward[s.Forward.Count - 1];
            s.Forward.RemoveAt(s.Forward.Count - 1);
        }

        // A slider drag or text edit in the panel is one undo step: snapshot on mouse/key down,
        // push it only if something actually changed before the gesture ends.
        private static readonly Dictionary<string, List<DecalPlacement>> pending = new Dictionary<string, List<DecalPlacement>>();

        public static void BeginGesture(LocoLayout l)
        {
            pending[l.CarGuid] = Snapshot(l);   // always fresh: a MouseUp outside the panel never reaches us
        }

        public static void Changed(LocoLayout l)
        {
            var s = For(l);
            if (pending.TryGetValue(l.CarGuid, out var snap))
            {
                pending.Remove(l.CarGuid);
                s.Back.Add(snap);
                if (s.Back.Count > Max) s.Back.RemoveAt(0);
                s.Forward.Clear();
            }
        }

        public static void EndGesture() => pending.Clear();

        public static void Forget(string carGuid)
        {
            stacks.Remove(carGuid);
            pending.Remove(carGuid);
        }

        public static void Clear()
        {
            stacks.Clear();
            pending.Clear();
        }
    }
}
