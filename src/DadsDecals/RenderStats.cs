using System.Diagnostics;
using UnityEngine;

namespace DadsDecals
{
    /// <summary>Per-frame totals across every car, shown in the Debug tab's Performance section.</summary>
    internal static class RenderStats
    {
        private static int frame = -1;
        private static int cars, decals, culled, draws;
        private static long ticks;
        private static readonly Stopwatch watch = new Stopwatch();

        public static int LastCars { get; private set; }
        public static int LastDecals { get; private set; }
        public static int LastCulled { get; private set; }
        public static int LastDraws { get; private set; }
        /// <summary>CPU time Dad's Decals spent drawing, smoothed over about half a second.</summary>
        public static float AverageMs { get; private set; }

        /// <summary>Called by each car's renderer at the start of its LateUpdate.</summary>
        public static void BeginCar()
        {
            if (Time.frameCount != frame)
            {
                if (frame >= 0)
                {
                    LastCars = cars; LastDecals = decals; LastCulled = culled; LastDraws = draws;
                    var ms = ticks * 1000f / Stopwatch.Frequency;
                    AverageMs = Mathf.Lerp(AverageMs, ms, 0.05f);
                }
                frame = Time.frameCount;
                cars = decals = culled = draws = 0;
                ticks = 0;
            }
            cars++;
            watch.Restart();
        }

        public static void EndCar()
        {
            watch.Stop();
            ticks += watch.ElapsedTicks;
        }

        public static void Decal(int partsDrawn) { decals++; draws += partsDrawn; }
        public static void Culled() => culled++;
    }
}
