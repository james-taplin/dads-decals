using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>Toolbar panel. Milestone 1: decal palette, current-loco info and diagnostics.</summary>
    internal sealed class DecalPanel
    {
        private const float ThumbSize = 64f;

        private string? selectedImage;
        private string lastReport = "";

        public void Draw(Rect rect)
        {
            var car = PlayerManager.Car != null && PlayerManager.Car.IsLoco ? PlayerManager.Car : PlayerManager.LastLoco;

            GUILayout.Label("Loco");
            if (car == null)
            {
                GUILayout.Label("Get on a loco to edit its decals.");
            }
            else
            {
                var layout = Main.Layouts.Get(car.CarGUID);
                GUILayout.Label($"{car.ID}  ({car.carLivery?.id})  - {layout?.Decals.Count ?? 0} decal(s)");
                if (GUILayout.Button("Run diagnostics (writes to mod log)"))
                    lastReport = Diagnostics.Run(car);
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Decals");
            if (GUILayout.Button("Reload folder", GUILayout.Width(110)))
                Main.Library.Reload();
            GUILayout.EndHorizontal();

            if (Main.Library.Images.Count == 0)
                GUILayout.Label($"No PNGs yet. Put some in:\n{Main.DecalsDir}");

            var perRow = Mathf.Max(1, (int)((rect.width - 30f) / (ThumbSize + 6f)));
            foreach (var group in Main.Library.Images.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                if (group.Key.Length > 0) GUILayout.Label(group.Key);
                var items = group.OrderBy(i => i.Name).ToList();
                for (var i = 0; i < items.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    foreach (var img in items.Skip(i).Take(perRow))
                    {
                        var selected = img.Key == selectedImage;
                        var content = new GUIContent(img.Texture, img.Name);
                        if (GUILayout.Toggle(selected, content, "Button", GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize)) && !selected)
                            selectedImage = img.Key;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            if (selectedImage != null)
                GUILayout.Label("Selected: " + selectedImage);

            if (lastReport.Length > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(lastReport);
            }
        }
    }
}

