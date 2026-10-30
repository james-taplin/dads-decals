using System;
using System.Collections.Generic;
using System.Linq;
using MPAPI;
using MPAPI.Interfaces;
using MPAPI.Types;
using UnityEngine;

namespace DadsDecals.Multiplayer
{
    /// <summary>
    /// The network side of multiplayer sync (0.5.1-beta): handshake, host snapshot, live host
    /// updates, guest edits through the host, permissions and picture transfer. Loaded by
    /// Sync.TryLoadBridge only when the Multiplayer mod is installed and enabled.
    /// </summary>
    public static class Bridge
    {
        private static IServer? server;
        private static IClient? client;
        private static Runner? runner;

        /// <summary>Guests (player ids) who said hello with a matching protocol; only they get layouts.</summary>
        private static readonly HashSet<byte> helloed = new HashSet<byte>();

        public static void Init()
        {
            if (!MultiplayerAPI.IsMultiplayerLoaded)
            {
                Sync.Log("Multiplayer is installed but its API isn't ready; decals won't sync");
                return;
            }
            // Also set in info.json; this covers a Multiplayer build that reads it before we load.
            MultiplayerAPI.Instance.SetModCompatibility(Main.Mod.Info.Id, MultiplayerCompatibility.Client);

            var go = new GameObject("DadsDecals_Multiplayer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();

            MultiplayerAPI.ServerStarted += OnServerStarted;
            MultiplayerAPI.ServerStopped += OnServerStopped;
            MultiplayerAPI.ClientStarted += OnClientStarted;
            MultiplayerAPI.ClientStopped += OnClientStopped;
            if (MultiplayerAPI.Server != null) OnServerStarted(MultiplayerAPI.Server);
            if (MultiplayerAPI.Client != null) OnClientStarted(MultiplayerAPI.Client);

            OldVersions.Patch();
            Sync.Log($"Multiplayer API {MultiplayerAPI.SupportedApiVersion} (Multiplayer {MultiplayerAPI.MultiplayerVersion}), protocol {Sync.Protocol}");
        }

        // ---- host ------------------------------------------------------------------------------

        private static void OnServerStarted(IServer s)
        {
            server = s;
            client = null;   // in case the host's own client reported in first
            helloed.Clear();
            imagesFromGuests.Clear();
            waitingForImage.Clear();
            s.RegisterSerializablePacket<HelloPacket>(OnHello);
            s.RegisterSerializablePacket<CarLayoutEditPacket>(OnEdit);
            s.RegisterSerializablePacket<ImageRequestPacket>(OnImageRequestFromGuest);
            s.RegisterSerializablePacket<ImageChunkPacket>(OnImageChunkFromGuest);
            s.OnPlayerDisconnected += OnPlayerDisconnected;
            s.OnPlayerReady += OnPlayerReady;
            Sync.BroadcastLayout = Broadcast;
            Sync.PermissionsChanged = SendPermissionsToAll;
            Sync.SetRole(SyncRole.Host, HostStatus());
        }

        private static void OnServerStopped()
        {
            if (server != null)
            {
                server.OnPlayerDisconnected -= OnPlayerDisconnected;
                server.OnPlayerReady -= OnPlayerReady;
            }
            server = null;
            helloed.Clear();
            Sync.BroadcastLayout = null;
            Sync.PermissionsChanged = null;
            Sync.SetRole(SyncRole.Offline, "");
        }

        private static string HostStatus() =>
            helloed.Count == 0
                ? "hosting; no guests with Dad's Decals yet"
                : $"hosting; {helloed.Count} guest(s) with Dad's Decals" +
                  (Main.Settings.GuestsCanEdit ? " can see and place decals" : " can see your decals (view only)");

        private static void RefreshGuests()
        {
            Sync.Guests.Clear();
            if (server == null) return;
            foreach (var id in helloed)
            {
                var p = server.GetPlayer(id);
                if (p != null) Sync.Guests.Add(p.Username);
            }
            Sync.Status = HostStatus();
        }

        private static void OnHello(HelloPacket hello, IPlayer sender)
        {
            if (server == null) return;
            Sync.Log($"Hello from {sender.Username}: Dad's Decals {hello.Version}, protocol {hello.Protocol}");
            server.SendSerializablePacketToPlayer(new WelcomePacket
            {
                Protocol = Sync.Protocol,
                HostVersion = Sync.ModVersion,
                GuestsCanEdit = Main.Settings.GuestsCanEdit,
                Blocked = Sync.IsBlocked(sender.Username),
            }, sender);
            if (hello.Protocol != Sync.Protocol) return;   // they go local-only and tell their player why

            // Snapshot before adding them: pending changes reach the existing guests first.
            var snapshot = Sync.Snapshot();
            helloed.Add(sender.PlayerId);
            foreach (var (carGuid, version, json, hashes) in snapshot)
                server.SendSerializablePacketToPlayer(new CarLayoutPacket { CarGuid = carGuid, Version = version, Json = json, Hashes = hashes }, sender);
            Sync.Log($"Sent {snapshot.Count} layout(s) to {sender.Username}");
            RefreshGuests();
        }

        private static void Broadcast(string carGuid, int version, string json, Dictionary<string, string> hashes)
        {
            if (server == null || helloed.Count == 0) return;
            var packet = new CarLayoutPacket { CarGuid = carGuid, Version = version, Json = json, Hashes = hashes };
            foreach (var id in helloed.ToList())
            {
                var player = server.GetPlayer(id);
                if (player == null) { helloed.Remove(id); continue; }
                server.SendSerializablePacketToPlayer(packet, player);
            }
        }

        private static void SendPermissionsToAll()
        {
            if (server == null) return;
            foreach (var id in helloed.ToList())
            {
                var player = server.GetPlayer(id);
                if (player == null) continue;
                server.SendSerializablePacketToPlayer(new PermissionsPacket
                {
                    GuestsCanEdit = Main.Settings.GuestsCanEdit,
                    Blocked = Sync.IsBlocked(player.Username),
                }, player);
            }
            Sync.Status = HostStatus();
        }

        private static void OnEdit(CarLayoutEditPacket edit, IPlayer sender)
        {
            if (server == null || !helloed.Contains(sender.PlayerId)) return;
            var missing = new List<string>();
            var reason = Sync.HostReceiveEdit(sender.Username, edit.CarGuid, edit.Version, edit.Json, edit.Hashes, missing);
            if (reason != null)
            {
                Sync.Log($"Turned down an edit from {sender.Username}: {reason}");
                var (version, json, hashes) = Sync.Current(edit.CarGuid);
                server.SendSerializablePacketToPlayer(new EditRejectedPacket
                {
                    CarGuid = edit.CarGuid, Version = version, Json = json, Hashes = hashes, Reason = reason,
                }, sender);
                return;
            }
            // Pictures the guest used that we don't have: ask them; we pass them on to whoever asks.
            if (missing.Count > 0)
                server.SendSerializablePacketToPlayer(new ImageRequestPacket { Hashes = missing }, sender);
        }

        private static void OnPlayerDisconnected(IPlayer player)
        {
            if (helloed.Remove(player.PlayerId)) Sync.Log($"{player.Username} left");
            foreach (var key in imagesFromGuests.Keys.Where(k => k.player == player.PlayerId).ToList()) imagesFromGuests.Remove(key);
            RefreshGuests();
        }

        private static void OnPlayerReady(IPlayer player)
        {
            if (OldVersions.Pending > 0) runner?.After(10f, () => OldVersions.TellIfStillSilent(server, player, helloed));
        }

        // Host pictures: guests ask for hashes; ones we don't have yet (a guest is still sending
        // them) are queued and sent when they arrive.
        private static readonly Dictionary<string, HashSet<byte>> waitingForImage = new Dictionary<string, HashSet<byte>>();
        private static readonly Dictionary<(byte player, string hash), Assembly> imagesFromGuests = new Dictionary<(byte, string), Assembly>();

        private static void OnImageRequestFromGuest(ImageRequestPacket request, IPlayer sender)
        {
            if (server == null || !helloed.Contains(sender.PlayerId)) return;
            foreach (var hash in request.Hashes.Distinct().Take(256))
            {
                var bytes = Sync.ImageBytes(hash);
                if (bytes != null) SendImage(hash, bytes, chunk => server.SendSerializablePacketToPlayer(chunk, sender));
                else
                {
                    if (!waitingForImage.TryGetValue(hash, out var who)) waitingForImage[hash] = who = new HashSet<byte>();
                    who.Add(sender.PlayerId);
                }
            }
        }

        private static void OnImageChunkFromGuest(ImageChunkPacket chunk, IPlayer sender)
        {
            if (server == null || !helloed.Contains(sender.PlayerId)) return;
            var key = (sender.PlayerId, chunk.Hash);
            if (!imagesFromGuests.TryGetValue(key, out var asm)) imagesFromGuests[key] = asm = new Assembly(chunk.Count);
            if (!asm.Add(chunk)) { imagesFromGuests.Remove(key); return; }
            if (!asm.Complete) return;
            imagesFromGuests.Remove(key);
            var bytes = asm.Bytes();
            if (!Sync.StoreImage(chunk.Hash, bytes)) return;
            if (waitingForImage.TryGetValue(chunk.Hash, out var who))
            {
                waitingForImage.Remove(chunk.Hash);
                foreach (var id in who)
                {
                    var player = server.GetPlayer(id);
                    if (player != null) SendImage(chunk.Hash, bytes, c => server.SendSerializablePacketToPlayer(c, player));
                }
            }
        }

        private static void SendImage(string hash, byte[] bytes, Action<ImageChunkPacket> send)
        {
            var count = Math.Max(1, (bytes.Length + ImageChunkPacket.ChunkSize - 1) / ImageChunkPacket.ChunkSize);
            for (var i = 0; i < count; i++)
            {
                var start = i * ImageChunkPacket.ChunkSize;
                var len = Math.Min(ImageChunkPacket.ChunkSize, bytes.Length - start);
                var part = new byte[len];
                Buffer.BlockCopy(bytes, start, part, 0, len);
                send(new ImageChunkPacket { Hash = hash, Index = i, Count = count, Bytes = part });
            }
        }

        // ---- guest -----------------------------------------------------------------------------

        private static float helloSentAt = -1;
        private static bool welcomed;
        private static readonly HashSet<string> requested = new HashSet<string>();
        private static readonly Dictionary<string, Assembly> imagesFromHost = new Dictionary<string, Assembly>();

        private static void OnClientStarted(IClient c)
        {
            if (server != null || MultiplayerAPI.Instance.IsHost) return;   // the host's own client: it already has everything
            client = c;
            helloSentAt = -1;
            welcomed = false;
            requested.Clear();
            imagesFromHost.Clear();
            c.RegisterSerializablePacket<WelcomePacket>(OnWelcome);
            c.RegisterSerializablePacket<CarLayoutPacket>(OnCarLayout);
            c.RegisterSerializablePacket<EditRejectedPacket>(OnEditRejected);
            c.RegisterSerializablePacket<PermissionsPacket>(OnPermissions);
            c.RegisterSerializablePacket<ImageRequestPacket>(OnImageRequestFromHost);
            c.RegisterSerializablePacket<ImageChunkPacket>(OnImageChunkFromHost);
            Sync.SetRole(SyncRole.Client, "connecting; waiting for the world to load");
            Sync.SetPermissions(false, false, announce: false);   // look only until the host says otherwise
        }

        private static void OnClientStopped()
        {
            if (client == null) return;
            client = null;
            Sync.SendEdit = null;
            if (server == null) Sync.SetRole(SyncRole.Offline, "");
        }

        /// <summary>Called every frame by the runner.</summary>
        internal static void Tick()
        {
            if (client == null || welcomed) return;
            if (helloSentAt < 0)
            {
                // Hello once our world (and the host's cars) have loaded.
                if (PlayerManager.PlayerTransform == null || UnityEngine.Object.FindObjectOfType<CarSpawner>() == null) return;
                Sync.BeginGuestSession();
                client.SendSerializablePacketToServer(new HelloPacket { Protocol = Sync.Protocol, Version = Sync.ModVersion });
                helloSentAt = Time.unscaledTime;
                Sync.Status = "connected; waiting for the host's decals";
                Sync.Log("Sent hello to the host");
            }
            else if (Time.unscaledTime - helloSentAt > 10f)
            {
                welcomed = true;   // stop waiting
                Sync.SetRole(SyncRole.LocalOnly,
                    "the host doesn't have a compatible Dad's Decals, so decals you place stay on your screen only");
            }
        }

        private static void OnWelcome(WelcomePacket welcome)
        {
            welcomed = true;
            if (welcome.Protocol != Sync.Protocol)
            {
                Sync.SetRole(SyncRole.LocalOnly,
                    $"this server's Dad's Decals ({welcome.HostVersion}) uses a different multiplayer version from yours ({Sync.ModVersion}). " +
                    "Decals you place stay on your screen only; matching versions will fix that");
                return;
            }
            Sync.SendEdit = SendEdit;
            Sync.SetPermissions(welcome.GuestsCanEdit, welcome.Blocked, announce: welcome.Blocked || !welcome.GuestsCanEdit);
            Sync.Status = $"connected; host has Dad's Decals {welcome.HostVersion}";
            Sync.Log($"Welcome from host (Dad's Decals {welcome.HostVersion}); guests can edit {welcome.GuestsCanEdit}, blocked {welcome.Blocked}");
        }

        private static void SendEdit(string carGuid, int baseVersion, string json, Dictionary<string, string> hashes)
        {
            client?.SendSerializablePacketToServer(new CarLayoutEditPacket { CarGuid = carGuid, Version = baseVersion, Json = json, Hashes = hashes });
        }

        private static void OnCarLayout(CarLayoutPacket packet)
        {
            if (Sync.Role != SyncRole.Client) return;
            Request(Sync.ApplyLayout(packet.CarGuid, packet.Version, packet.Json, packet.Hashes));
        }

        private static void OnEditRejected(EditRejectedPacket packet)
        {
            if (Sync.Role != SyncRole.Client) return;
            Sync.ShowNotice($"Your change wasn't saved: {packet.Reason}.");
            Request(Sync.ApplyLayout(packet.CarGuid, packet.Version, packet.Json, packet.Hashes, force: true));
        }

        private static void OnPermissions(PermissionsPacket packet)
        {
            if (Sync.Role == SyncRole.Client) Sync.SetPermissions(packet.GuestsCanEdit, packet.Blocked);
        }

        private static void Request(List<string> hashes)
        {
            var fresh = hashes.Where(h => requested.Add(h)).ToList();
            if (fresh.Count > 0 && client != null)
                client.SendSerializablePacketToServer(new ImageRequestPacket { Hashes = fresh });
        }

        private static void OnImageRequestFromHost(ImageRequestPacket request)
        {
            if (client == null) return;
            foreach (var hash in request.Hashes.Distinct().Take(256))
            {
                var bytes = Sync.ImageBytes(hash);
                if (bytes != null) SendImage(hash, bytes, chunk => client.SendSerializablePacketToServer(chunk));
                else Sync.Log($"The host asked for picture {hash.Substring(0, Math.Min(8, hash.Length))} but it's missing or over {Sync.MaxImageBytes / (1024 * 1024)} MB");
            }
        }

        private static void OnImageChunkFromHost(ImageChunkPacket chunk)
        {
            if (!imagesFromHost.TryGetValue(chunk.Hash, out var asm)) imagesFromHost[chunk.Hash] = asm = new Assembly(chunk.Count);
            if (!asm.Add(chunk)) { imagesFromHost.Remove(chunk.Hash); return; }
            if (!asm.Complete) return;
            imagesFromHost.Remove(chunk.Hash);
            if (!Sync.StoreImage(chunk.Hash, asm.Bytes())) requested.Remove(chunk.Hash);
        }
    }

    /// <summary>Collects a picture's chunks (they can arrive in any order).</summary>
    internal sealed class Assembly
    {
        private readonly byte[]?[] parts;
        private int received;

        public Assembly(int count) => parts = new byte[]?[Math.Max(1, count)];

        public bool Complete => received == parts.Length;

        /// <summary>False if the chunk doesn't fit this picture (bad count/index, or too big overall).</summary>
        public bool Add(ImageChunkPacket chunk)
        {
            var maxChunks = (Sync.MaxImageBytes + ImageChunkPacket.ChunkSize - 1) / ImageChunkPacket.ChunkSize;
            if (chunk.Count != parts.Length || chunk.Count > maxChunks || chunk.Index < 0 || chunk.Index >= parts.Length) return false;
            if (parts[chunk.Index] == null) received++;
            parts[chunk.Index] = chunk.Bytes;
            return true;
        }

        public byte[] Bytes()
        {
            var all = new byte[parts.Sum(p => p!.Length)];
            var at = 0;
            foreach (var p in parts) { Buffer.BlockCopy(p!, 0, all, at, p!.Length); at += p.Length; }
            return all;
        }
    }

    /// <summary>Frame tick and simple timers for the bridge.</summary>
    internal sealed class Runner : MonoBehaviour
    {
        private readonly List<(float at, Action action)> timers = new List<(float, Action)>();

        public void After(float seconds, Action action) => timers.Add((Time.unscaledTime + seconds, action));

        private void Update()
        {
            try { Bridge.Tick(); }
            catch (Exception e) { Main.Log.LogException("Multiplayer tick", e); }
            for (var i = timers.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < timers[i].at) continue;
                var action = timers[i].action;
                timers.RemoveAt(i);
                try { action(); }
                catch (Exception e) { Main.Log.LogException("Multiplayer timer", e); }
            }
        }
    }
}
