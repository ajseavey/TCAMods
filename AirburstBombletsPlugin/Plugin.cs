using BepInEx;
using HarmonyLib;
using Falcon.Stores;
using Falcon.Utilities;
using UnityEngine;
using System.Reflection;

namespace AirburstBombletsPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(Bomblet), "CalculateFuzeDetonations")]
        [HarmonyPrefix]
        public static bool CalculateFuzeDetonations(Bomblet __instance, int layerMask, StoreData ___storeData, Collider[] ___overlapHits)
        {
            if (___storeData.Warhead.Bomblet == null)
            {
                return false;
            }

            BombletData bomblet = ___storeData.Warhead.Bomblet;
            MonoBehaviour instanceBase = __instance as MonoBehaviour;

            MethodInfo Explode = typeof(Bomblet).GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (bomblet.Fuze == FuzeType.Airburst && TerrainTools.GetHeightAboveTerrainAtPosition(instanceBase.transform.position) < bomblet.BurstHeight)
            {
                Explode.Invoke(__instance, new object[] { instanceBase.transform.position  }); 
            }

            if (bomblet.Fuze == FuzeType.Proximity)
            {
                Vector3 vector5 = instanceBase.transform.position;
                float proximityDistance = bomblet.ProximityDistance;
                if (Physics.OverlapSphereNonAlloc(vector5, proximityDistance, ___overlapHits, layerMask) > 0)
                {
                    Explode.Invoke(__instance, new object[] { instanceBase.transform.position });
                }
            }

            return false;
        }

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
