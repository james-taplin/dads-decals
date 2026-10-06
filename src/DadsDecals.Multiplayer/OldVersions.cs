using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MPAPI.Interfaces;

namespace DadsDecals.Multiplayer
{
    /// <summary>
    /// Pre-0.4 Dad's Decals has no multiplayer-compatibility key, so Multiplayer treats it as
    /// required and refuses a guest who has it when the host (0.4+, marked "Client") doesn't list
    /// it. Multiplayer compares mod names only. This lets such guests in anyway, then whispers a
    /// polite note that their version isn't supported in multiplayer.
    ///
    /// Patches Multiplayer.API.ModCompatibilityManager.ValidateClientMods (an internal class), found
    /// by name. If it's ever renamed the patch is skipped and Multiplayer's own message shows instead.
    /// </summary>
    internal static class OldVersions
    {
        private const string TargetType = "Multiplayer.API.ModCompatibilityManager";
        private static readonly Queue<string> pending = new Queue<string>();

        public static int Pending => pending.Count;

        public static void Patch()
        {
            try
            {
                var method = AccessTools.Method(AccessTools.TypeByName(TargetType), "ValidateClientMods");
                if (method == null) { Sync.Log("Old-version check unavailable (Multiplayer changed); pre-0.4 guests get Multiplayer's usual message"); return; }
                new Harmony(Main.Mod.Info.Id + ".multiplayer").Patch(method, postfix: new HarmonyMethod(typeof(OldVersions), nameof(Postfix)));
            }
            catch (Exception e)
            {
                Main.Log.LogException("Patching the old-version check", e);
            }
        }

        // __0 is the guest's ModInfo[]; __result is a ModValidationResult. Both are Multiplayer
        // types, so they're handled by reflection.
        private static void Postfix(object __0, object __result)
        {
            try
            {
                if (!(__0 is Array mods) || __result == null) return;
                string? oldVersion = null;
                foreach (var mod in mods)
                {
                    var t = Traverse.Create(mod);
                    if (t.Field("Id").GetValue<string>() != Main.Mod.Info.Id) continue;
                    var version = t.Field("Version").GetValue<string>() ?? "";
                    if (IsBefore04(version)) oldVersion = version;
                }
                if (oldVersion == null) return;

                var result = Traverse.Create(__result);
                var missing = result.Property("Missing").GetValue() as IList;
                var extra = result.Property("Extra").GetValue() as IList;
                if (missing == null || extra == null) return;
                for (var i = missing.Count - 1; i >= 0; i--)
                    if (Traverse.Create(missing[i]).Field("Id").GetValue<string>() == Main.Mod.Info.Id) missing.RemoveAt(i);
                result.Property("IsValid").SetValue(missing.Count == 0 && extra.Count == 0);

                pending.Enqueue(oldVersion);
                Sync.Log($"Letting in a guest with Dad's Decals {oldVersion} (pre-0.4, no sync); they'll get a note once they've loaded");
            }
            catch (Exception e)
            {
                Main.Log.LogException("Old-version check", e);
            }
        }

        /// <summary>
        /// Ten seconds after a guest finishes loading: if they never said hello but a pre-0.4 guest
        /// was let in, tell them. (If two people join at the same moment the note could reach the
        /// wrong one of them; it's harmless.)
        /// </summary>
        public static void TellIfStillSilent(IServer? server, IPlayer player, HashSet<byte> helloed)
        {
            if (server == null || pending.Count == 0 || helloed.Contains(player.PlayerId)) return;
            var version = pending.Dequeue();
            server.SendWhisperChatMessage(
                $"Hi {player.DisplayName}! You're running Dad's Decals {version}, and versions before 0.4 aren't supported in multiplayer, " +
                "so you won't see decals on this server. Updating to 0.4 or newer will fix that. Sorry for the hassle!", player);
            Sync.Log($"Told {player.Username} that Dad's Decals {version} isn't supported in multiplayer");
        }

        private static bool IsBefore04(string version)
        {
            var parts = version.Split('.', '-');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return false;
            return major == 0 && minor < 4;
        }
    }
}
