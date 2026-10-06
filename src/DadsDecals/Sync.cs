using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;
using UnityModManagerNet;

namespace DadsDecals
{
    public enum SyncRole
    {
        /// <summary>Single player, or Multiplayer isn't installed: everything works as before.</summary>
        Offline,
        /// <summary>Hosting a multiplayer game: our layouts are the authority and get sent to guests.</summary>
        Host,
        /// <summary>A guest of a host running Dad's Decals 0.4+: we show the host's layouts.</summary>
        Client,
        /// <summary>A guest whose host has no (or an incompatible) Dad's Decals: decals stay on this screen.</summary>
        LocalOnly,
    }

    /// <summary>
    /// Multiplayer sync state, with no Multiplayer types in it. The network side lives in
    /// DadsDecals.Multiplayer.dll, which is only loaded when the Multiplayer mod is installed and
    /// talks to us through the delegates and methods here. In Offline everything is a no-op.
    /// </summary>
    public static class Sync
    {
        /// <summary>Bumped when the messages change shape; guests on a different protocol go local-only.</summary>
        public const int Protocol = 1;

        public static SyncRole Role { get; private set; } = SyncRole.Offline;
        /// <summary>One line for the panel, e.g. "Hosting: 2 players with Dad's Decals".</summary>
        public static string Status { get; set; } = "";
        public static string ModVersion => Main.Mod.Info.Version + VersionSuffix;
        private static string VersionSuffix
        {
            get
            {
                var info = typeof(Sync).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                var dash = info.IndexOf('-');
                var plus = info.IndexOf('+');
                return dash < 0 ? "" : info.Substring(dash, (plus < 0 ? info.Length : plus) - dash);
            }
        }

        /// <summary>Guests can't place or edit yet (0.4.0-alpha.1): they see the host's decals.</summary>
        public static bool ReadOnly => Role == SyncRole.Client;

        /// <summary>Set by the bridge while hosting: send one car's layout to every guest that said hello.</summary>
        public static Action<string, int, string, Dictionary<string, string>>? BroadcastLayout;

        private static readonly Dictionary<string, int> versions = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> lastSent = new Dictionary<string, string>();
        private static readonly HashSet<string> dirty = new HashSet<string>();
        private static float nextPoll, nextSweep;

        public static void SetRole(SyncRole role, string status)
        {
            if (role != Role) Log($"Role: {Role} -> {role}");
            Role = role;
            Status = status;
            versions.Clear();
            lastSent.Clear();
            dirty.Clear();
            if (role == SyncRole.Client) Interaction.Instance?.StopAll();
        }

        // ---- host side ------------------------------------------------------------------------

        /// <summary>Something changed this car's layout outside a panel gesture (e.g. a paint-theme swap).</summary>
        public static void MarkDirty(string carGuid)
        {
            if (Role == SyncRole.Host) dirty.Add(carGuid);
        }

        /// <summary>
        /// Called every frame. While hosting, looks for layouts that changed since they were last
        /// sent and broadcasts them. Edits are only checked between gestures (no mouse button down,
        /// no slider held), so a drag is sent once, when it ends. The cars being worked on are
        /// checked often; everything else in a slow sweep (orphans applied, imports, clear all...).
        /// </summary>
        public static void Poll(params TrainCar?[] focus)
        {
            if (Role != SyncRole.Host || BroadcastLayout == null) return;
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.3f;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || GUIUtility.hotControl != 0) return;

            foreach (var car in focus)
                if (car != null && car.logicCar != null) dirty.Add(car.CarGUID);
            if (Time.unscaledTime >= nextSweep)
            {
                nextSweep = Time.unscaledTime + 5f;
                foreach (var l in Main.Layouts.All) dirty.Add(l.CarGuid);
                foreach (var guid in lastSent.Keys) dirty.Add(guid);   // layouts that were removed
            }
            foreach (var guid in dirty.ToList())
                SendIfChanged(guid);
            dirty.Clear();
        }

        private static void SendIfChanged(string carGuid)
        {
            var layout = Main.Layouts.Get(carGuid) ?? new LocoLayout { CarGuid = carGuid };
            var json = JsonConvert.SerializeObject(layout);
            if (lastSent.TryGetValue(carGuid, out var prev) ? prev == json : layout.IsEmpty) return;
            lastSent[carGuid] = json;
            versions[carGuid] = (versions.TryGetValue(carGuid, out var v) ? v : 0) + 1;
            BroadcastLayout!(carGuid, versions[carGuid], json, ImageHashes(layout));
            Log($"Sent {layout.CarId} ({layout.Decals.Count} decals) v{versions[carGuid]}");
        }

        /// <summary>
        /// Every car's layout as last sent, for a guest who just said hello. Call this before adding
        /// the new guest to the broadcast list: unsent changes go to the existing guests first, so
        /// everyone ends up on the same version.
        /// </summary>
        public static List<(string carGuid, int version, string json, Dictionary<string, string> hashes)> Snapshot()
        {
            if (Role == SyncRole.Host && BroadcastLayout != null)
                foreach (var guid in Main.Layouts.All.Select(l => l.CarGuid).Concat(lastSent.Keys).Distinct().ToList())
                    SendIfChanged(guid);
            var result = new List<(string, int, string, Dictionary<string, string>)>();
            foreach (var kv in lastSent)
            {
                var layout = Main.Layouts.Get(kv.Key);
                if (layout == null || layout.IsEmpty) continue;
                result.Add((kv.Key, versions[kv.Key], kv.Value, ImageHashes(layout)));
            }
            return result;
        }

        private static readonly Dictionary<string, (DateTime, string)> hashCache = new Dictionary<string, (DateTime, string)>();

        /// <summary>SHA-1 of each image file a layout uses, keyed by image key.</summary>
        public static Dictionary<string, string> ImageHashes(LocoLayout layout)
        {
            var result = new Dictionary<string, string>();
            foreach (var d in layout.Decals.Concat(layout.ByTheme.Values.SelectMany(l => l)))
            {
                if (d.Kind != DecalKind.Image || d.Image.Length == 0 || result.ContainsKey(d.Image)) continue;
                var hash = HashOf(d.Image);
                if (hash != null) result[d.Image] = hash;
            }
            return result;
        }

        public static string? HashOf(string imageKey)
        {
            var path = Path.Combine(Main.DecalsDir, imageKey.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (hashCache.TryGetValue(imageKey, out var c) && c.Item1 == stamp) return c.Item2;
            using var sha = SHA1.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            hashCache[imageKey] = (stamp, hash);
            return hash;
        }

        // ---- guest side -----------------------------------------------------------------------

        /// <summary>Images this guest is missing (key -> hash). Image transfer arrives in 0.4.0-alpha.2.</summary>
        public static readonly Dictionary<string, string> MissingImages = new Dictionary<string, string>();

        /// <summary>About to say hello to a host: forget whatever was loaded locally; the host's layouts follow.</summary>
        public static void BeginGuestSession()
        {
            versions.Clear();
            Main.Layouts.Clear();
            MissingImages.Clear();
        }

        /// <summary>A layout from the host. Older versions than the one we hold are ignored (packets can arrive out of order).</summary>
        public static void ApplyLayout(string carGuid, int version, string json, Dictionary<string, string> hashes)
        {
            if (versions.TryGetValue(carGuid, out var have) && version <= have) return;
            versions[carGuid] = version;
            LocoLayout? layout;
            try { layout = JsonConvert.DeserializeObject<LocoLayout>(json); }
            catch (Exception e) { Log($"Bad layout for {carGuid}: {e.Message}"); return; }
            if (layout == null) return;
            layout.CarGuid = carGuid;
            Main.Layouts.Put(layout);

            foreach (var kv in hashes)
            {
                if (HashOf(kv.Key) == kv.Value) continue;
                if (!MissingImages.ContainsKey(kv.Key)) Log($"Missing image {kv.Key} ({kv.Value.Substring(0, 8)}); image transfer arrives in a later build");
                MissingImages[kv.Key] = kv.Value;
            }

            var car = FindCar(carGuid);
            if (car != null) DecalRenderer.Ensure(car);
            Log($"Received {layout.CarId} ({layout.Decals.Count} decals) v{version}{(car == null ? ", car not spawned yet" : "")}");
        }

        private static TrainCar? FindCar(string carGuid) =>
            UnityEngine.Object.FindObjectsOfType<TrainCar>().FirstOrDefault(c => c.logicCar != null && c.CarGUID == carGuid);

        // ---- log for testers ------------------------------------------------------------------

        private static readonly List<string> log = new List<string>();
        public static IReadOnlyList<string> LogLines => log;

        public static void Log(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} {message}";
            log.Add(line);
            if (log.Count > 500) log.RemoveRange(0, log.Count - 500);
            Main.Log.Log("[MP] " + message);
        }

        public static string SaveLog()
        {
            var dir = Path.Combine(Main.Mod.Path, "Diagnostics");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"multiplayer-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllLines(path, new[]
            {
                $"Dad's Decals {ModVersion}, protocol {Protocol}, role {Role}",
                $"Status: {Status}",
                $"Layouts: {Main.Layouts.All.Count()}, missing images: {MissingImages.Count}",
                "",
            }.Concat(log));
            return path;
        }

        // ---- loading the bridge ---------------------------------------------------------------

        private static bool bridgeTried;

        /// <summary>
        /// Loads DadsDecals.Multiplayer.dll if the Multiplayer mod is installed and enabled. Called
        /// once from the first Update, after every mod has loaded (Multiplayer's API registers in its Load).
        /// </summary>
        public static void TryLoadBridge()
        {
            if (bridgeTried) return;
            bridgeTried = true;
            var mp = UnityModManager.FindMod("Multiplayer");
            if (mp == null || !mp.Enabled || !mp.Active) return;
            var path = Path.Combine(Main.Mod.Path, "DadsDecals.Multiplayer.dll");
            if (!File.Exists(path))
            {
                Main.Log.Warning("Multiplayer is installed but DadsDecals.Multiplayer.dll is missing; decals won't sync.");
                return;
            }
            try
            {
                var asm = Assembly.LoadFrom(path);
                asm.GetType("DadsDecals.Multiplayer.Bridge", true)!.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
                Log($"Multiplayer support loaded (Multiplayer {mp.Info.Version})");
            }
            catch (Exception e)
            {
                Main.Log.LogException("Loading multiplayer support", e);
            }
        }
    }
}
