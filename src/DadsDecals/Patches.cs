using DV.Utils;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DadsDecals
{
    // Same save hooks LocoOwnership uses: write our block just before the game saves,
    // read it just before cars are loaded so layouts are ready when locos spawn.

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    internal static class SaveLayoutsPatch
    {
        private static void Prefix()
        {
            SingletonBehaviour<SaveGameManager>.Instance.data.SetJObject(LayoutStore.SaveKey, Main.Layouts.ToJson());
        }
    }

    [HarmonyPatch(typeof(CarsSaveManager), "Load")]
    internal static class LoadLayoutsPatch
    {
        private static void Prefix(JObject savedData)
        {
            Main.Layouts.FromJson(SingletonBehaviour<SaveGameManager>.Instance.data.GetJObject(LayoutStore.SaveKey));
        }
    }

    internal static class CarLifecycle
    {
        private static CarSpawner? spawner;

        public static void Hook()
        {
            WorldStreamingInit.LoadingFinished += OnWorldLoaded;
            // Note: WorldStreamingInit.IsLoaded is true at the main menu (no instance), so don't trust it.
            OnWorldLoaded();
        }

        public static void Unhook()
        {
            WorldStreamingInit.LoadingFinished -= OnWorldLoaded;
            if (spawner != null)
            {
                spawner.CarSpawned -= OnCarSpawned;
                spawner.CarAboutToBeDeleted -= OnCarDeleted;
                spawner = null;
            }
        }

        private static void OnWorldLoaded()
        {
            if (spawner != null) return;   // Unity null: also re-hooks after returning to menu and loading again
            spawner = UnityEngine.Object.FindObjectOfType<CarSpawner>();
            if (spawner == null) return;   // not in a world yet; LoadingFinished will call us again
            spawner.CarSpawned += OnCarSpawned;
            spawner.CarAboutToBeDeleted += OnCarDeleted;
            // Cars from the save were spawned before LoadingFinished fired.
            foreach (var car in UnityEngine.Object.FindObjectsOfType<TrainCar>())
                OnCarSpawned(car);
        }

        private static void OnCarSpawned(TrainCar car)
        {
            if (car.logicCar != null && Main.Layouts.Get(car.CarGUID) != null)
                DecalRenderer.Ensure(car);
        }

        private static void OnCarDeleted(TrainCar car)
        {
            Placement.Instance?.ForgetCar(car);
            Main.Layouts.OnCarDeleted(car);
        }
    }
}
