using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>
    /// Stacking order of decals on one car. The order is the layout's list order: index 0 is the
    /// bottom, the last decal is on top, and a mirrored pair counts as one layer (at its first
    /// member's position). Each layer draws in its own render queue, so overlaps resolve the same
    /// way from every camera angle; with one shared queue the game sorts by distance instead.
    /// </summary>
    internal static class Layers
    {
        /// <summary>Lowest queue for placed decals (AlphaTest). Every queue up to 2500 still gets shadows and fog.</summary>
        public const int BottomQueue = 2450;
        /// <summary>The placement preview, always on top (the shader's own queue, AlphaTest+49).</summary>
        public const int GhostQueue = 2499;
        private const int Slots = GhostQueue - BottomQueue;   // 49 distinct layers for placed decals

        private static readonly Dictionary<string, int> pairRank = new Dictionary<string, int>();
        private static readonly List<int> ranks = new List<int>();

        /// <summary>Fills <paramref name="queues"/> with the render queue for each decal in <paramref name="decals"/>.</summary>
        public static void Assign(List<DecalPlacement> decals, Dictionary<DecalPlacement, int> queues)
        {
            queues.Clear();
            pairRank.Clear();
            ranks.Clear();
            var n = 0;
            foreach (var d in decals)
            {
                if (d.PairId.Length > 0 && pairRank.TryGetValue(d.PairId, out var r)) { ranks.Add(r); continue; }
                if (d.PairId.Length > 0) pairRank[d.PairId] = n;
                ranks.Add(n++);
            }
            // More layers than queues: neighbouring layers share a queue (overlaps between those
            // two fall back to distance sorting).
            for (var i = 0; i < decals.Count; i++)
                queues[decals[i]] = BottomQueue + (n <= Slots ? ranks[i] : ranks[i] * Slots / n);
        }

        /// <summary>The selected decal's layer, 1 = bottom, and how many layers the car has.</summary>
        public static (int layer, int count) Position(List<DecalPlacement> decals, DecalPlacement selected)
        {
            var units = Units(decals);
            var i = units.FindIndex(u => u.Contains(selected));
            return (i + 1, units.Count);
        }

        public enum Move { ToBack, Backward, Forward, ToFront }

        /// <summary>Moves the selected decal (and its mirrored twin) in the stacking order. Returns false if it was already there.</summary>
        public static bool Apply(List<DecalPlacement> decals, DecalPlacement selected, Move move)
        {
            var units = Units(decals);
            var from = units.FindIndex(u => u.Contains(selected));
            if (from < 0) return false;
            var to = move switch
            {
                Move.ToBack => 0,
                Move.Backward => from - 1,
                Move.Forward => from + 1,
                _ => units.Count - 1,
            };
            to = Mathf.Clamp(to, 0, units.Count - 1);
            if (to == from) return false;
            var unit = units[from];
            units.RemoveAt(from);
            units.Insert(to, unit);
            decals.Clear();
            foreach (var u in units) decals.AddRange(u);
            return true;
        }

        /// <summary>The list grouped into layers: each decal alone, or a mirrored pair together, bottom first.</summary>
        private static List<List<DecalPlacement>> Units(List<DecalPlacement> decals)
        {
            var units = new List<List<DecalPlacement>>();
            var byPair = new Dictionary<string, List<DecalPlacement>>();
            foreach (var d in decals)
            {
                if (d.PairId.Length > 0 && byPair.TryGetValue(d.PairId, out var existing)) { existing.Add(d); continue; }
                var unit = new List<DecalPlacement> { d };
                if (d.PairId.Length > 0) byPair[d.PairId] = unit;
                units.Add(unit);
            }
            return units;
        }

        // ---- per-queue materials ---------------------------------------------------------------

        // renderQueue is a material setting (not a property-block one), so each base material gets
        // a copy per queue in use. Copies follow the base's texture, and are dropped when the base
        // material is destroyed (text cache eviction, image reload).
        private static readonly Dictionary<Material, Material?[]> variants = new Dictionary<Material, Material?[]>();

        public static Material AtQueue(Material baseMaterial, int queue)
        {
            if (queue == GhostQueue) return baseMaterial;   // the shader's own queue
            if (!variants.TryGetValue(baseMaterial, out var arr))
            {
                if (variants.Count >= 64) Prune();
                arr = new Material?[Slots + 1];
                variants[baseMaterial] = arr;
            }
            var i = queue - BottomQueue;
            var v = arr[i];
            if (v == null)
            {
                v = new Material(baseMaterial) { name = baseMaterial.name + " @" + queue, renderQueue = queue };
                arr[i] = v;
            }
            if (v.mainTexture != baseMaterial.mainTexture) v.mainTexture = baseMaterial.mainTexture;
            return v;
        }

        private static void Prune()
        {
            foreach (var key in variants.Keys.Where(k => k == null).ToList())
            {
                foreach (var v in variants[key]) if (v != null) Object.Destroy(v);
                variants.Remove(key);
            }
        }

        public static void Clear()
        {
            foreach (var arr in variants.Values)
                foreach (var v in arr) if (v != null) Object.Destroy(v);
            variants.Clear();
        }
    }
}
