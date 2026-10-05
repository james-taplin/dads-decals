using System;
using System.IO;
using System.Reflection;
using DerailValleyModToolbar;
using HarmonyLib;
using UnityModManagerNet;

namespace DadsDecals
{
    public static class Main
    {
        public static UnityModManager.ModEntry Mod { get; private set; } = null!;
        public static UnityModManager.ModEntry.ModLogger Log => Mod.Logger;

        public static DecalLibrary Library { get; private set; } = null!;
        public static DadsSettings Settings { get; private set; } = null!;
        public static LayoutStore Layouts { get; } = new LayoutStore();

        /// <summary>User folder for decal PNGs: Mods/DadsDecals/Decals (subfolders = categories).</summary>
        public static string DecalsDir => Path.Combine(Mod.Path, "Decals");

        private static Harmony? harmony;
        private static DecalPanel? panel;

        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            try
            {
                Settings = UnityModManager.ModSettings.Load<DadsSettings>(modEntry);
                Assets.Load();
                Directory.CreateDirectory(DecalsDir);
                Library = new DecalLibrary(DecalsDir);
                Library.Reload();

                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                CarLifecycle.Hook();
                Interaction.Create();

                panel = new DecalPanel();
                ModToolbarAPI.Register(modEntry)
                    .AddPanelControl("Decals", null, "Dad's Decals", panel.Draw, DecalPanel.Title, 460, 640)
                    .Finish();

                modEntry.OnUnload = Unload;
                Log.Log("Loaded");
            }
            catch (Exception e)
            {
                Log.LogException("Failed to load", e);
                harmony?.UnpatchAll(modEntry.Info.Id);
                return false;
            }
            return true;
        }

        private static bool Unload(UnityModManager.ModEntry modEntry)
        {
            ModToolbarAPI.Unregister(modEntry);
            CarLifecycle.Unhook();
            harmony?.UnpatchAll(modEntry.Info.Id);
            if (Interaction.Instance != null) UnityEngine.Object.Destroy(Interaction.Instance.gameObject);
            TextDecals.ClearCache();
            foreach (var r in UnityEngine.Object.FindObjectsOfType<DecalRenderer>()) UnityEngine.Object.Destroy(r);
            Library?.Dispose();
            Assets.Unload();
            return true;
        }
    }
}
