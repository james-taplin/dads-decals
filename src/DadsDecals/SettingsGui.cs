using System;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DadsDecals
{
    /// <summary>Dad's Decals' page in UMM's mod settings (Ctrl+F10): the host's multiplayer controls.</summary>
    internal static class SettingsGui
    {
        private static string blockName = "";

        public static void Draw(UnityModManager.ModEntry entry)
        {
            var s = Main.Settings;
            GUILayout.Label("Multiplayer: these only matter when you host a game with Derail Valley Multiplayer.");

            var canEdit = GUILayout.Toggle(s.GuestsCanEdit, " Guests can place and edit decals (off: they can only look)");
            if (canEdit != s.GuestsCanEdit)
            {
                s.GuestsCanEdit = canEdit;
                Changed();
            }

            GUILayout.Space(8);
            GUILayout.Label("Blocked players can't place or edit decals, and everything they placed is removed. " +
                            "Unblocking lets them place again; removed decals don't come back.");

            if (Sync.Role == SyncRole.Host && Sync.Guests.Count > 0)
            {
                GUILayout.Label("Guests in this game with Dad's Decals:");
                foreach (var guest in Sync.Guests.ToList())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("    " + guest, GUILayout.Width(260));
                    if (Sync.IsBlocked(guest)) GUILayout.Label("blocked");
                    else if (GUILayout.Button("Block", GUILayout.Width(90))) Block(guest);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Block a player by name:", GUILayout.Width(170));
            blockName = GUILayout.TextField(blockName, 64, GUILayout.Width(200));
            if (GUILayout.Button("Block", GUILayout.Width(90))) { Block(blockName); blockName = ""; }
            GUILayout.EndHorizontal();

            if (s.BlockedPlayers.Count == 0) GUILayout.Label("Nobody is blocked.");
            else
            {
                GUILayout.Label("Blocked:");
                foreach (var name in s.BlockedPlayers.ToList())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("    " + name, GUILayout.Width(260));
                    if (GUILayout.Button("Unblock", GUILayout.Width(90)))
                    {
                        s.BlockedPlayers.Remove(name);
                        Changed();
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void Block(string name)
        {
            name = name.Trim();
            if (name.Length == 0 || Sync.IsBlocked(name)) return;
            Main.Settings.BlockedPlayers.Add(name);
            Changed();
        }

        private static void Changed()
        {
            Main.Settings.Save(Main.Mod);
            try { Sync.HostSettingsChanged(); }
            catch (Exception e) { Main.Log.LogException("Applying multiplayer settings", e); }
        }
    }
}
