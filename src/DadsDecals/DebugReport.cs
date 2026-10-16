using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityModManagerNet;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace DadsDecals
{
    /// <summary>
    /// One-click bug report: zips the files a tester would otherwise have to hunt for (game log,
    /// previous game log, UMM log, Dad's Decals diagnostics and multiplayer logs, settings) plus a
    /// summary of versions and installed mods, into Mods/DadsDecals/Diagnostics/debug-*.zip.
    /// </summary>
    internal static class DebugReport
    {
        public static string Folder => Path.Combine(Main.Mod.Path, "Diagnostics");

        public static string Create(TrainCar? car)
        {
            Directory.CreateDirectory(Folder);
            if (Sync.Role != SyncRole.Offline || Sync.LogLines.Count > 0) Sync.SaveLog();
            if (car != null) Diagnostics.Run(car);

            var path = Path.Combine(Folder, $"debug-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                AddText(zip, "summary.txt", Summary(car));

                var gameLog = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(gameLog))
                {
                    AddFile(zip, "Player.log", gameLog);
                    AddFile(zip, "Player-prev.log", Path.Combine(Path.GetDirectoryName(gameLog)!, "Player-prev.log"));
                }
                AddFile(zip, "UnityModManager-Log.txt", Path.Combine(Application.dataPath, "Managed", "UnityModManager", "Log.txt"));
                AddFile(zip, "DadsDecals-Settings.xml", Path.Combine(Main.Mod.Path, "Settings.xml"));

                // Our own diagnostics and multiplayer logs from the last two days.
                foreach (var f in Directory.GetFiles(Folder, "*.txt").Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddDays(-2)))
                    AddFile(zip, "dadsdecals/" + Path.GetFileName(f), f);
            }
            Main.Log.Log("Debug zip written to " + path);
            return path;
        }

        private static string Summary(TrainCar? car)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Dad's Decals debug report, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Dad's Decals {Sync.ModVersion} (multiplayer protocol {Sync.Protocol})");
            sb.AppendLine($"Game {Application.version}, Unity {Application.unityVersion}");
            sb.AppendLine($"Multiplayer: role {Sync.Role}{(Sync.Status.Length > 0 ? ", " + Sync.Status : "")}");
            sb.AppendLine($"Layouts: {Main.Layouts.All.Count()} car(s), {Main.Layouts.All.Sum(l => l.Decals.Count)} decal(s), {Main.Layouts.Orphans.Count} orphan(s); missing images: {Sync.MissingImages.Count}");
            foreach (var kv in Sync.MissingImages) sb.AppendLine($"  missing {kv.Key} ({kv.Value})");
            sb.AppendLine($"Decal images loaded: {Main.Library.Images.Count}");
            sb.AppendLine($"Car in the panel: {(car != null ? $"{car.ID} ({car.carLivery?.id})" : "none")}");
            sb.AppendLine($"Settings: detail {Main.Settings.DecalDetail}, max distance {Main.Settings.MaxDistance:0} m");
            sb.AppendLine();
            sb.AppendLine("Mods (enabled first):");
            foreach (var m in UnityModManager.modEntries.OrderByDescending(m => m.Active).ThenBy(m => m.Info.Id))
                sb.AppendLine($"  {(m.Active ? "on " : "off")}  {m.Info.Id} {m.Info.Version}{(m.ErrorOnLoading ? "  (FAILED TO LOAD)" : "")}");
            return sb.ToString();
        }

        private static void AddText(ZipArchive zip, string name, string text)
        {
            using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            w.Write(text);
        }

        // The game keeps Player.log open while it runs, so read with sharing allowed.
        private static void AddFile(ZipArchive zip, string name, string source)
        {
            try
            {
                if (!File.Exists(source)) return;
                using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var output = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                input.CopyTo(output);
            }
            catch (Exception e)
            {
                AddText(zip, name + ".error.txt", $"Couldn't read {source}: {e.Message}");
            }
        }
    }
}
