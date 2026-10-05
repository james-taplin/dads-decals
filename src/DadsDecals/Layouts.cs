using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DadsDecals
{
    /// <summary>
    /// "Link layout to paint": each paint theme keeps its own decals. Theme names come from
    /// TrainCar.PaintExterior.CurrentTheme, which also covers Skin Manager skins (they're PaintThemes).
    /// </summary>
    internal static class PaintLink
    {
        public static string CurrentTheme(TrainCar car)
        {
            var theme = car.PaintExterior != null ? car.PaintExterior.CurrentTheme : null;
            return theme != null ? theme.AssetName ?? theme.name : "";
        }

        public static void SetLinked(LocoLayout layout, TrainCar car, bool linked)
        {
            if (layout.LinkToPaint == linked) return;
            layout.LinkToPaint = linked;
            layout.CurrentTheme = CurrentTheme(car);
            if (!linked) layout.ByTheme.Clear();
        }

        public static void OnThemeChanged(LocoLayout layout, string newTheme)
        {
            if (!layout.LinkToPaint || newTheme == layout.CurrentTheme) return;
            Interaction.Instance?.Deselect();
            layout.ByTheme[layout.CurrentTheme] = layout.Decals;
            layout.Decals = layout.ByTheme.TryGetValue(newTheme, out var list) ? list : new List<DecalPlacement>();
            layout.ByTheme.Remove(newTheme);
            Undo.Forget(layout.CarGuid);
            Main.Log.Log($"{layout.CarId}: paint changed {layout.CurrentTheme} -> {newTheme}, swapped to {layout.Decals.Count} decal(s)");
            layout.CurrentTheme = newTheme;
        }
    }

    internal sealed class TemplateInfo
    {
        public string Path = "";
        public string Name = "";
        public string LiveryId = "";
    }

    internal sealed class TemplateFile
    {
        public int Version = 2;
        public string Name = "";
        public string LiveryId = "";
        public List<DecalPlacement> Decals = new List<DecalPlacement>();
    }

    /// <summary>Templates, export and import: layouts as files under the mod folder, shared across saves.</summary>
    internal static class LayoutFiles
    {
        public static string TemplatesDir => Path.Combine(Main.Mod.Path, "Templates");
        public static string ExportsDir => Path.Combine(Main.Mod.Path, "Exports");
        public static string ImportDir => Path.Combine(Main.Mod.Path, "Import");

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { Formatting = Formatting.Indented };

        public static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var s = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return s.Length == 0 ? "untitled" : s;
        }

        // ---- templates -----------------------------------------------------------------------

        public static void SaveTemplate(string name, LocoLayout layout)
        {
            var dir = Path.Combine(TemplatesDir, SafeName(layout.LiveryId));
            Directory.CreateDirectory(dir);
            var file = new TemplateFile { Name = name, LiveryId = layout.LiveryId, Decals = layout.Decals.Select(d => d.Clone()).ToList() };
            File.WriteAllText(Path.Combine(dir, SafeName(name) + ".json"), JsonConvert.SerializeObject(file, Json));
            Main.Log.Log($"Saved template '{name}' for {layout.LiveryId} ({file.Decals.Count} decals)");
        }

        public static List<TemplateInfo> ListTemplates()
        {
            if (!Directory.Exists(TemplatesDir)) return new List<TemplateInfo>();
            return Directory.GetFiles(TemplatesDir, "*.json", SearchOption.AllDirectories)
                .Select(p => new TemplateInfo
                {
                    Path = p,
                    Name = Path.GetFileNameWithoutExtension(p),
                    LiveryId = Path.GetFileName(Path.GetDirectoryName(p)) ?? "",
                })
                .OrderBy(t => t.LiveryId).ThenBy(t => t.Name)
                .ToList();
        }

        public static List<DecalPlacement> LoadTemplate(TemplateInfo t)
        {
            try
            {
                return JsonConvert.DeserializeObject<TemplateFile>(File.ReadAllText(t.Path))?.Decals ?? new List<DecalPlacement>();
            }
            catch (Exception e)
            {
                Main.Log.LogException("Reading template " + t.Path, e);
                return new List<DecalPlacement>();
            }
        }

        public static void DeleteTemplate(TemplateInfo t)
        {
            if (File.Exists(t.Path)) File.Delete(t.Path);
        }

        // ---- export / import -----------------------------------------------------------------

        /// <summary>Writes Exports/&lt;name&gt;/layout.json plus copies of the PNGs it uses.</summary>
        public static string Export(string name, LocoLayout layout)
        {
            var dir = Path.Combine(ExportsDir, SafeName(name));
            var images = Path.Combine(dir, "images");
            Directory.CreateDirectory(images);
            foreach (var key in layout.Decals.Where(d => d.Kind == DecalKind.Image).Select(d => d.Image).Distinct())
            {
                var src = Path.Combine(Main.DecalsDir, key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(src)) continue;
                var dst = Path.Combine(images, key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);
            }
            var file = new TemplateFile { Name = name, LiveryId = layout.LiveryId, Decals = layout.Decals.Select(d => d.Clone()).ToList() };
            File.WriteAllText(Path.Combine(dir, "layout.json"), JsonConvert.SerializeObject(file, Json));
            Main.Log.Log($"Exported {file.Decals.Count} decals to {dir}");
            return dir;
        }

        /// <summary>Shared layouts ready to import: folders with a layout.json in Import/ or Exports/.</summary>
        public static List<string> ListImportable()
        {
            var dirs = new List<string>();
            foreach (var root in new[] { ImportDir, ExportsDir })
                if (Directory.Exists(root))
                    dirs.AddRange(Directory.GetDirectories(root).Where(d => File.Exists(Path.Combine(d, "layout.json"))));
            return dirs;
        }

        /// <summary>Copies the folder's images into Decals/Imported/&lt;name&gt;/ and returns its decals with remapped image keys.</summary>
        public static List<DecalPlacement> Import(string dir)
        {
            var name = SafeName(Path.GetFileName(dir));
            var file = JsonConvert.DeserializeObject<TemplateFile>(File.ReadAllText(Path.Combine(dir, "layout.json")));
            var decals = file?.Decals ?? new List<DecalPlacement>();
            var images = Path.Combine(dir, "images");
            var prefix = "Imported/" + name + "/";
            var copied = false;
            foreach (var d in decals.Where(d => d.Kind == DecalKind.Image))
            {
                var src = Path.Combine(images, d.Image.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(src)) continue;
                var newKey = prefix + d.Image;
                var dst = Path.Combine(Main.DecalsDir, newKey.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(dst))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst);
                    copied = true;
                }
                d.Image = newKey;
            }
            if (copied) Main.Library.Reload();
            Main.Log.Log($"Imported {decals.Count} decals from {dir}");
            return decals;
        }
    }
}
