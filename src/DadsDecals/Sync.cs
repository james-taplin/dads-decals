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
        /// <summary>A guest of a host running a compatible Dad's Decals: we show (and can edit) the host's layouts.</summary>
        Client,
        /// <summary>A guest whose host has no (or an incompatible) Dad's Decals: decals stay on this screen.</summary>
        LocalOnly,
    }

    /// <summary>
    /// Multiplayer sync state, with no Multiplayer types in it. The network side lives in
    /// DadsDecals.Multiplayer.dll, which is only loaded when the Multiplayer mod is installed and
    /// talks to us through the delegates and methods here. In Offline everything is a no-op.
    ///
    /// The host is the authority. Everyone edits their own copy; between gestures the change goes
    /// to the host (guests) or straight out to the guests (host). The host checks it, stamps who
    /// placed new decals, bumps the car's version and sends the result to everyone. Received
    /// layouts are merged into the local one by decal Id, so selections survive.
    /// </summary>
    public static class Sync
    {
        /// <summary>Bumped when the messages change shape; guests on a different protocol go local-only.</summary>
        public const int Protocol = 2;

        /// <summary>Largest picture (PNG file size) that can be shared in multiplayer.</summary>
        public const int MaxImageBytes = 2 * 1024 * 1024;

        public static SyncRole Role { get; private set; } = SyncRole.Offline;
        /// <summary>One line for the panel, e.g. "hosting; 2 guests with Dad's Decals".</summary>
        public static string Status { get; set; } = "";
        /// <summary>A short message for the player (rejected edit, picture too big...), shown in the panel.</summary>
        public static string Notice { get; private set; } = "";
        private static float noticeUntil;

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

        // What the host allows this guest (from Welcome / Permissions).
        public static bool GuestsCanEdit { get; private set; } = true;
        public static bool Blocked { get; private set; }

        /// <summary>Guests can't place or edit when the host set "view only" or blocked them.</summary>
        public static bool ReadOnly => Role == SyncRole.Client && (!GuestsCanEdit || Blocked);

        public static string ReadOnlyReason =>
            Blocked ? "The host has blocked you from placing decals in this game, so you can only look."
                    : "The host has set decals to view only for guests, so you can only look.";

        /// <summary>Host: guests with Dad's Decals currently in the game (usernames), for the settings screen.</summary>
        public static readonly List<string> Guests = new List<string>();

        // ---- links to the bridge (set by DadsDecals.Multiplayer) --------------------------------

        /// <summary>Host: send one car's layout to every guest that said hello.</summary>
        public static Action<string, int, string, Dictionary<string, string>>? BroadcastLayout;
        /// <summary>Guest: send an edit of one car to the host (car, base version, layout JSON, image hashes).</summary>
        public static Action<string, int, string, Dictionary<string, string>>? SendEdit;
        /// <summary>Host: tell every guest what they're allowed to do now.</summary>
        public static Action? PermissionsChanged;

        private static readonly Dictionary<string, int> versions = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> lastSent = new Dictionary<string, string>();
        private static readonly HashSet<string> dirty = new HashSet<string>();
        private static float nextPoll, nextSweep;

        // Guest: edits sent and not yet answered (car -> (base version, time sent)), and host
        // layouts that arrived mid-gesture and wait until the mouse is let go.
        private static readonly Dictionary<string, (int baseVersion, float at)> awaiting = new Dictionary<string, (int, float)>();
        private static readonly Dictionary<string, (int version, string json, Dictionary<string, string> hashes, bool force)> deferred =
            new Dictionary<string, (int, string, Dictionary<string, string>, bool)>();

        private static readonly JsonSerializerSettings MergeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        public static void SetRole(SyncRole role, string status)
        {
            if (role != Role) Log($"Role: {Role} -> {role}");
            Role = role;
            Status = status;
            versions.Clear();
            lastSent.Clear();
            dirty.Clear();
            awaiting.Clear();
            deferred.Clear();
            if (role != SyncRole.Client) { GuestsCanEdit = true; Blocked = false; }
            if (role != SyncRole.Host) Guests.Clear();
        }

        public static void ShowNotice(string message, float seconds = 8f)
        {
            Notice = message;
            noticeUntil = Time.unscaledTime + seconds;
            Log("Notice: " + message);
        }

        public static string CurrentNotice => Time.unscaledTime < noticeUntil ? Notice : "";

        /// <summary>Something changed this car's layout outside a panel gesture (paint-theme swap, block...).</summary>
        public static void MarkDirty(string carGuid)
        {
            if (Role == SyncRole.Host || Role == SyncRole.Client) dirty.Add(carGuid);
        }

        private static bool GestureActive => Input.GetMouseButton(0) || Input.GetMouseButton(1) || GUIUtility.hotControl != 0;

        /// <summary>
        /// Called every frame. Looks for layouts that changed since they were last sent: the host
        /// broadcasts them, a guest sends them to the host as edits. Only between gestures (no
        /// mouse button down, no slider held), so a drag goes out once, when it ends. The cars being
        /// worked on are checked often; everything else in a slow sweep.
        /// </summary>
        public static void Poll(params TrainCar?[] focus)
        {
            var hosting = Role == SyncRole.Host && BroadcastLayout != null;
            var guest = Role == SyncRole.Client && SendEdit != null;
            if (!hosting && !guest) return;
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.3f;
            if (GestureActive) return;

            if (guest)
                foreach (var kv in deferred.ToList())
                {
                    deferred.Remove(kv.Key);
                    ApplyLayout(kv.Key, kv.Value.version, kv.Value.json, kv.Value.hashes, kv.Value.force);
                }
            if (guest && ReadOnly) { dirty.Clear(); return; }

            foreach (var car in focus)
                if (car != null && car.logicCar != null) dirty.Add(car.CarGUID);
            if (Time.unscaledTime >= nextSweep)
            {
                nextSweep = Time.unscaledTime + 5f;
                foreach (var l in Main.Layouts.All) dirty.Add(l.CarGuid);
                foreach (var guid in lastSent.Keys) dirty.Add(guid);   // layouts that were removed
            }
            foreach (var guid in dirty.ToList())
            {
                if (hosting) SendIfChanged(guid);
                else SendEditIfChanged(guid);
            }
            dirty.Clear();
        }

        // ---- host side ------------------------------------------------------------------------

        private static void SendIfChanged(string carGuid)
        {
            var layout = Main.Layouts.Get(carGuid) ?? new LocoLayout { CarGuid = carGuid };
            var json = JsonConvert.SerializeObject(layout);
            if (lastSent.TryGetValue(carGuid, out var prev) ? prev == json : layout.IsEmpty) return;
            lastSent[carGuid] = json;
            versions[carGuid] = VersionOf(carGuid) + 1;
            BroadcastLayout!(carGuid, versions[carGuid], json, ImageHashes(layout));
            Log($"Sent {layout.CarId} ({layout.Decals.Count} decals) v{versions[carGuid]}");
        }

        private static int VersionOf(string carGuid) => versions.TryGetValue(carGuid, out var v) ? v : 0;

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

        /// <summary>The host's current layout of a car, for a rejection reply.</summary>
        public static (int version, string json, Dictionary<string, string> hashes) Current(string carGuid)
        {
            var layout = Main.Layouts.Get(carGuid) ?? new LocoLayout { CarGuid = carGuid };
            return (VersionOf(carGuid), JsonConvert.SerializeObject(layout), ImageHashes(layout));
        }

        /// <summary>
        /// Host: a guest's edit of one car. Returns null if accepted (it's then broadcast to
        /// everyone, the sender included), otherwise the reason to show them. Pictures the host
        /// doesn't have yet are added to <paramref name="missing"/> for the bridge to ask for.
        /// </summary>
        public static string? HostReceiveEdit(string username, string carGuid, int baseVersion, string json,
                                              Dictionary<string, string> hashes, List<string> missing)
        {
            if (Role != SyncRole.Host) return "the host isn't hosting any more";
            if (IsBlocked(username)) return "the host has blocked you from placing decals";
            if (!Main.Settings.GuestsCanEdit) return "the host has set decals to view only";
            if (json.Length > 512 * 1024) return "that layout is too big to share";
            // Send out any change the host made to this car first, so it can't be overwritten:
            // if there was one, the version moves on and the guest's edit counts as a clash.
            SendIfChanged(carGuid);
            if (baseVersion != VersionOf(carGuid)) return "someone else changed this car at the same moment; have another go";

            LocoLayout? incoming;
            try { incoming = JsonConvert.DeserializeObject<LocoLayout>(json); }
            catch (Exception e) { Log($"Bad edit from {username}: {e.Message}"); return "that edit couldn't be read"; }
            if (incoming == null) return "that edit couldn't be read";
            incoming.CarGuid = carGuid;

            // Who placed what: existing decals keep their owner, new ones belong to the sender.
            var current = Main.Layouts.Get(carGuid);
            var owners = current == null ? new Dictionary<string, string>()
                : AllDecals(current).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First().PlacedBy);
            var added = 0;
            foreach (var d in AllDecals(incoming))
            {
                if (owners.TryGetValue(d.Id, out var owner)) d.PlacedBy = owner;
                else { d.PlacedBy = username; added++; }
            }

            var target = current ?? Main.Layouts.Put(new LocoLayout { CarGuid = carGuid }, keepUndo: true);
            MergeInto(target, incoming);
            missing.AddRange(RegisterImages(hashes));
            var car = FindCar(carGuid);
            if (car != null) DecalRenderer.Ensure(car);
            Log($"Edit from {username} on {incoming.CarId}: {incoming.Decals.Count} decals ({added} new)");
            SendIfChanged(carGuid);
            return null;
        }

        public static bool IsBlocked(string username) =>
            Main.Settings.BlockedPlayers.Any(b => string.Equals(b, username, StringComparison.OrdinalIgnoreCase));

        /// <summary>Host settings changed in UMM (guests can edit / blocked list): apply and tell the guests.</summary>
        public static void HostSettingsChanged()
        {
            if (Role != SyncRole.Host) return;
            foreach (var name in Main.Settings.BlockedPlayers) RemoveDecalsBy(name);
            PermissionsChanged?.Invoke();
        }

        /// <summary>Removes every decal a player placed (they've been blocked).</summary>
        public static int RemoveDecalsBy(string username)
        {
            var removed = 0;
            foreach (var layout in Main.Layouts.All.ToList())
            {
                var n = layout.Decals.RemoveAll(d => string.Equals(d.PlacedBy, username, StringComparison.OrdinalIgnoreCase));
                foreach (var list in layout.ByTheme.Values)
                    n += list.RemoveAll(d => string.Equals(d.PlacedBy, username, StringComparison.OrdinalIgnoreCase));
                if (n == 0) continue;
                removed += n;
                Undo.Forget(layout.CarGuid);
                MarkDirty(layout.CarGuid);
            }
            if (removed > 0)
            {
                Interaction.Instance?.Deselect();
                Log($"Removed {removed} decal(s) placed by {username}");
            }
            return removed;
        }

        // ---- guest side -----------------------------------------------------------------------

        public static void SetPermissions(bool guestsCanEdit, bool blocked, bool announce = true)
        {
            var wasReadOnly = ReadOnly;
            GuestsCanEdit = guestsCanEdit;
            Blocked = blocked;
            if (ReadOnly && !wasReadOnly) Interaction.Instance?.StopAll();
            if (!announce) { Log($"Permissions: guests can edit {guestsCanEdit}, blocked {blocked}"); return; }
            if (ReadOnly && !wasReadOnly)
            {
                Interaction.Instance?.StopAll();
                ShowNotice(ReadOnlyReason);
            }
            else if (!ReadOnly && wasReadOnly) ShowNotice("The host now lets you place and edit decals.");
            Log($"Permissions: guests can edit {guestsCanEdit}, blocked {blocked}");
        }

        /// <summary>About to say hello to a host: forget whatever was loaded locally; the host's layouts follow.</summary>
        public static void BeginGuestSession()
        {
            versions.Clear();
            lastSent.Clear();
            awaiting.Clear();
            deferred.Clear();
            Main.Layouts.Clear();
            MissingImages.Clear();
        }

        private static void SendEditIfChanged(string carGuid)
        {
            if (awaiting.TryGetValue(carGuid, out var wait))
            {
                if (Time.unscaledTime - wait.at < 10f) return;   // one edit in flight per car
                awaiting.Remove(carGuid);
                Log($"No answer to an edit of {carGuid} for 10 s; sending again");
            }
            var layout = Main.Layouts.Get(carGuid) ?? new LocoLayout { CarGuid = carGuid };
            var json = JsonConvert.SerializeObject(layout);
            if (lastSent.TryGetValue(carGuid, out var prev) ? prev == json : layout.IsEmpty) return;

            var hashes = ImageHashes(layout);
            foreach (var key in hashes.Keys)
                if (ImageSize(key) > MaxImageBytes)
                    ShowNotice($"\"{key}\" is over {MaxImageBytes / (1024 * 1024)} MB, so other players won't see it. Use a smaller PNG.");

            lastSent[carGuid] = json;
            var baseVersion = VersionOf(carGuid);
            awaiting[carGuid] = (baseVersion, Time.unscaledTime);
            SendEdit!(carGuid, baseVersion, json, hashes);
            Log($"Sent edit of {layout.CarId} ({layout.Decals.Count} decals) on v{baseVersion}");
        }

        /// <summary>
        /// A layout from the host. Older versions than the one we hold are ignored (packets can
        /// arrive out of order) unless <paramref name="force"/> (a rejected edit: go back to the
        /// host's copy). Returns the picture hashes we still need to ask the host for.
        /// </summary>
        public static List<string> ApplyLayout(string carGuid, int version, string json, Dictionary<string, string> hashes, bool force = false)
        {
            var none = new List<string>();
            if (!force && versions.TryGetValue(carGuid, out var have) && version <= have) return none;
            if (Role == SyncRole.Client && GestureActive)
            {
                // Don't pull the decal out from under a drag; apply when the mouse is let go.
                deferred[carGuid] = (version, json, hashes, force || (deferred.TryGetValue(carGuid, out var d) && d.force));
                return RegisterImagesQuiet(hashes);
            }
            versions[carGuid] = version;
            if (awaiting.TryGetValue(carGuid, out var wait) && (force || version > wait.baseVersion)) awaiting.Remove(carGuid);

            LocoLayout? incoming;
            try { incoming = JsonConvert.DeserializeObject<LocoLayout>(json); }
            catch (Exception e) { Log($"Bad layout for {carGuid}: {e.Message}"); return none; }
            if (incoming == null) return none;
            incoming.CarGuid = carGuid;

            var target = Main.Layouts.Get(carGuid) ?? Main.Layouts.Put(new LocoLayout { CarGuid = carGuid }, keepUndo: true);
            MergeInto(target, incoming);
            lastSent[carGuid] = JsonConvert.SerializeObject(target);   // our copy now matches the host's

            var missing = RegisterImages(hashes);
            var car = FindCar(carGuid);
            if (car != null) DecalRenderer.Ensure(car);
            Log($"Received {incoming.CarId} ({incoming.Decals.Count} decals) v{version}{(force ? " (edit undone)" : "")}{(car == null ? ", car not spawned yet" : "")}");
            return missing;
        }

        /// <summary>
        /// Updates <paramref name="target"/> to match <paramref name="src"/>, reusing decal objects
        /// with the same Id so a selected decal stays selected.
        /// </summary>
        public static void MergeInto(LocoLayout target, LocoLayout src)
        {
            target.CarId = src.CarId;
            target.LiveryId = src.LiveryId;
            target.LinkToPaint = src.LinkToPaint;
            target.CurrentTheme = src.CurrentTheme;
            target.ByTheme = src.ByTheme ?? new Dictionary<string, List<DecalPlacement>>();
            var existing = target.Decals.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var merged = new List<DecalPlacement>(src.Decals.Count);
            foreach (var s in src.Decals)
            {
                if (existing.TryGetValue(s.Id, out var t))
                {
                    JsonConvert.PopulateObject(JsonConvert.SerializeObject(s), t, MergeSettings);
                    merged.Add(t);
                    existing.Remove(s.Id);
                }
                else merged.Add(s);
            }
            target.Decals = merged;
        }

        private static IEnumerable<DecalPlacement> AllDecals(LocoLayout l) => l.Decals.Concat(l.ByTheme.Values.SelectMany(x => x));

        private static TrainCar? FindCar(string carGuid) =>
            UnityEngine.Object.FindObjectsOfType<TrainCar>().FirstOrDefault(c => c.logicCar != null && c.CarGUID == carGuid);

        // ---- pictures ---------------------------------------------------------------------------
        //
        // Layouts name pictures by key (path under Decals/), plus the SHA-1 of the file. When our
        // file at that key is missing or different, the picture is fetched by hash and cached as
        // Decals/_Multiplayer/<hash>.png, and the key is pointed at the cached copy for drawing.

        public const string CacheFolder = "_Multiplayer";

        /// <summary>Pictures we're waiting for (key -> hash), for the log and debug zip.</summary>
        public static readonly Dictionary<string, string> MissingImages = new Dictionary<string, string>();

        private static readonly Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> pathByHash = new Dictionary<string, string>();
        private static readonly Dictionary<string, (DateTime, string)> hashCache = new Dictionary<string, (DateTime, string)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The library key to draw for a layout's image key (a downloaded copy if ours differs).</summary>
        public static string ResolveImageKey(string key) => overrides.TryGetValue(key, out var alt) ? alt : key;

        private static string CacheKey(string hash) => $"{CacheFolder}/{hash}.png";
        private static string CachePath(string hash) => Path.Combine(Main.DecalsDir, CacheFolder, hash + ".png");
        private static string PathOf(string key) => Path.Combine(Main.DecalsDir, key.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>SHA-1 of each picture a layout uses, keyed by image key.</summary>
        public static Dictionary<string, string> ImageHashes(LocoLayout layout)
        {
            var result = new Dictionary<string, string>();
            foreach (var d in AllDecals(layout))
            {
                if (d.Kind != DecalKind.Image || d.Image.Length == 0 || result.ContainsKey(d.Image)) continue;
                // A picture we're still downloading is advertised by the hash we're expecting,
                // so others can ask for it (the host queues their request until it arrives).
                var hash = HashOf(d.Image) ?? (MissingImages.TryGetValue(d.Image, out var expected) ? expected : null);
                if (hash != null) result[d.Image] = hash;
            }
            return result;
        }

        /// <summary>SHA-1 of the picture drawn for this key (our file, or the downloaded copy), or null if we have none.</summary>
        public static string? HashOf(string imageKey)
        {
            var key = ResolveImageKey(imageKey);
            var path = PathOf(key);
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (hashCache.TryGetValue(key, out var c) && c.Item1 == stamp) return c.Item2;
            var hash = Sha1(File.ReadAllBytes(path));
            hashCache[key] = (stamp, hash);
            pathByHash[hash] = path;
            return hash;
        }

        private static long ImageSize(string key)
        {
            var path = PathOf(ResolveImageKey(key));
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        private static string Sha1(byte[] bytes)
        {
            using var sha = SHA1.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Points each key at a picture with the right hash: ours if it matches, else a cached
        /// download. Returns the hashes we don't have at all (to ask for).
        /// </summary>
        private static List<string> RegisterImages(Dictionary<string, string> hashes)
        {
            var missing = new List<string>();
            foreach (var kv in hashes)
            {
                var (key, hash) = (kv.Key, kv.Value);
                if (!IsHash(hash) || key.Contains("..")) continue;
                overrides.Remove(key);
                if (HashOf(key) == hash) { MissingImages.Remove(key); continue; }
                if (File.Exists(CachePath(hash)))
                {
                    UseCached(key, hash);
                    continue;
                }
                if (!MissingImages.ContainsKey(key)) Log($"Need picture {key} ({hash.Substring(0, 8)})");
                MissingImages[key] = hash;
                if (!missing.Contains(hash)) missing.Add(hash);
            }
            return missing;
        }

        private static List<string> RegisterImagesQuiet(Dictionary<string, string> hashes) =>
            hashes.Where(kv => IsHash(kv.Value) && HashOf(kv.Key) != kv.Value && !File.Exists(CachePath(kv.Value)))
                  .Select(kv => kv.Value).Distinct().ToList();

        private static bool IsHash(string h) => h.Length == 40 && h.All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'));

        private static void UseCached(string key, string hash)
        {
            var cacheKey = CacheKey(hash);
            if (!Main.Library.TryGet(cacheKey, out _)) Main.Library.LoadOne(CachePath(hash));
            overrides[key] = cacheKey;
            MissingImages.Remove(key);
        }

        /// <summary>The PNG bytes for a hash, if we have the picture and it isn't too big to share.</summary>
        public static byte[]? ImageBytes(string hash)
        {
            if (!IsHash(hash)) return null;
            var path = File.Exists(CachePath(hash)) ? CachePath(hash) : pathByHash.TryGetValue(hash, out var p) ? p : null;
            if (path == null || !File.Exists(path) || new FileInfo(path).Length > MaxImageBytes) return null;
            var bytes = File.ReadAllBytes(path);
            return Sha1(bytes) == hash ? bytes : null;
        }

        /// <summary>A whole picture arrived. Checks it, caches it and points waiting keys at it.</summary>
        public static bool StoreImage(string hash, byte[] bytes)
        {
            if (!IsHash(hash) || bytes.Length > MaxImageBytes || Sha1(bytes) != hash) { Log($"Picture {hash.Substring(0, Math.Min(8, hash.Length))} failed its check; ignored"); return false; }
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P' || bytes[2] != (byte)'N' || bytes[3] != (byte)'G') { Log($"Picture {hash.Substring(0, 8)} isn't a PNG; ignored"); return false; }
            var path = CachePath(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
            pathByHash[hash] = path;
            foreach (var key in MissingImages.Where(kv => kv.Value == hash).Select(kv => kv.Key).ToList())
                UseCached(key, hash);
            Log($"Got picture {hash.Substring(0, 8)} ({bytes.Length / 1024} KB)");
            return true;
        }

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
                $"Guests can edit: {(Role == SyncRole.Host ? Main.Settings.GuestsCanEdit : GuestsCanEdit)}, blocked: {Blocked}, blocked list: {string.Join(", ", Main.Settings.BlockedPlayers)}",
                $"Layouts: {Main.Layouts.All.Count()}, missing pictures: {MissingImages.Count}",
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
