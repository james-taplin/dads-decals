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
    /// The network side of multiplayer sync (0.4.0-alpha.1): handshake, host snapshot and live
    /// host updates. Guests are read-only in this build. Loaded by Sync.TryLoadBridge only when the
    /// Multiplayer mod is installed and enabled.
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
            s.RegisterSerializablePacket<HelloPacket>(OnHello);
            s.OnPlayerDisconnected += OnPlayerDisconnected;
            s.OnPlayerReady += OnPlayerReady;
            Sync.BroadcastLayout = Broadcast;
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
            Sync.SetRole(SyncRole.Offline, "");
        }

        private static string HostStatus() =>
            helloed.Count == 0
                ? "hosting; no guests with Dad's Decals yet"
                : $"hosting; {helloed.Count} guest(s) with Dad's Decals can see your decals";

        private static void OnHello(HelloPacket hello, IPlayer sender)
        {
            if (server == null) return;
            Sync.Log($"Hello from {sender.Username}: Dad's Decals {hello.Version}, protocol {hello.Protocol}");
            server.SendSerializablePacketToPlayer(new WelcomePacket { Protocol = Sync.Protocol, HostVersion = Sync.ModVersion }, sender);
            if (hello.Protocol != Sync.Protocol) return;   // they go local-only and tell their player why

            // Snapshot before adding them: pending changes reach the existing guests first.
            var snapshot = Sync.Snapshot();
            helloed.Add(sender.PlayerId);
            foreach (var (carGuid, version, json, hashes) in snapshot)
                server.SendSerializablePacketToPlayer(new CarLayoutPacket { CarGuid = carGuid, Version = version, Json = json, Hashes = hashes }, sender);
            Sync.Log($"Sent {snapshot.Count} layout(s) to {sender.Username}");
            Sync.Status = HostStatus();
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

        private static void OnPlayerDisconnected(IPlayer player)
        {
            if (helloed.Remove(player.PlayerId)) Sync.Log($"{player.Username} left");
            Sync.Status = HostStatus();
        }

        private static void OnPlayerReady(IPlayer player)
        {
            if (OldVersions.Pending > 0) runner?.After(10f, () => OldVersions.TellIfStillSilent(server, player, helloed));
        }

        // ---- guest -----------------------------------------------------------------------------

        private static float helloSentAt = -1;
        private static bool welcomed;

        private static void OnClientStarted(IClient c)
        {
            if (server != null || MultiplayerAPI.Instance.IsHost) return;   // the host's own client: it already has everything
            client = c;
            helloSentAt = -1;
            welcomed = false;
            c.RegisterSerializablePacket<WelcomePacket>(OnWelcome);
            c.RegisterSerializablePacket<CarLayoutPacket>(OnCarLayout);
            Sync.SetRole(SyncRole.Client, "connecting; waiting for the world to load");
        }

        private static void OnClientStopped()
        {
            if (client == null) return;
            client = null;
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
                    "the host doesn't have Dad's Decals 0.4 or newer, so decals you place stay on your screen only");
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
            Sync.Status = $"connected; showing the host's decals (host has Dad's Decals {welcome.HostVersion})";
            Sync.Log($"Welcome from host (Dad's Decals {welcome.HostVersion})");
        }

        private static void OnCarLayout(CarLayoutPacket packet)
        {
            if (Sync.Role != SyncRole.Client) return;
            Sync.ApplyLayout(packet.CarGuid, packet.Version, packet.Json, packet.Hashes);
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
