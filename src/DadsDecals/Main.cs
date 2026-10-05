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
                Directory.CreateDirectory(DecalsDir);
                Library = new DecalLibrary(DecalsDir);
                Library.Reload();

                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                CarLifecycle.Hook();

                panel = new DecalPanel();
                ModToolbarAPI.Register(modEntry)
                    .AddPanelControl("Decals", null, "Dad's Decals", panel.Draw, "Dad's Decals", 360, 480)
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
            Library?.Dispose();
            return true;
        }
    }
}
