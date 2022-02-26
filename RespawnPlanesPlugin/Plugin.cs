using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Falcon.Game2.Arena2;
using Falcon.UniversalAircraft;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RespawnPlanesPlugin
{

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Arena.exe")]
    public class Plugin : BaseUnityPlugin
    {      
        static Dictionary<ArenaAirfieldFlight, UniFlight> ActiveFlights = new Dictionary<ArenaAirfieldFlight, UniFlight>();
        static Dictionary<ArenaAirfield, float> AirfieldCooldowns = new Dictionary<ArenaAirfield, float>();

        static ManualLogSource Log;

        [HarmonyPatch(typeof(StrategicTarget2))]
        [HarmonyPatch("InitializeForArena")]
        [HarmonyPrefix]
        static bool InitializeForArena(ArenaStrategicTarget data, StrategicTarget2 __instance)
        {
            Log?.LogDebug("InitializeForArena Patch Called");
            if (data.Airfield.RespawnTime == 0f)
            {
                data.Airfield.RespawnTime = data.RespawnTimer;
            }
            __instance.AirfieldData = data.Airfield;
            AirfieldCooldowns.Add(data.Airfield, 0f);
            Log?.LogDebug($"Airfield Cooldown for {data.DisplayName} is {data.Airfield.RespawnTime} seconds");
            return true;
        }

        [HarmonyPatch(typeof(StrategicTarget2))]
        [HarmonyPatch("SpawnFlight")]
        [HarmonyPostfix]
        static void SpawnFlight(ArenaAirfieldFlight flightParams, UniFlight __result)
        {
            Log?.LogDebug("SpawnFlight Patch Called");
            ActiveFlights[flightParams] = __result;
        }

        [HarmonyPatch(typeof(StrategicTarget2))]
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static bool Update(StrategicTarget2 __instance, ref float ___spawnCooldown)
        {
            if (!StrategicTarget2.IsArenaRunning || __instance.Faction == null)
            {
                return false;
            }
            ___spawnCooldown -= Time.deltaTime;
            AirfieldCooldowns[__instance.AirfieldData] += Time.deltaTime;

            if (!__instance.IsTargetDestroyed)
            {
                if (___spawnCooldown <= 0f)
                {
                    foreach (StrategicFormationSpawn formation in __instance.OwnedFormations)
                    {
                        var factionSpawnAllowed = !formation.IsFactionSpecific || !(formation.Faction != __instance.Faction.Name);
                        if (factionSpawnAllowed && formation.IsSpawnAllowed)
                        {
                            Log.LogDebug("Spawning Formation " + formation.Type);
                            typeof(StrategicTarget2).GetMethod("SpawnFormation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { formation });
                        }
                    }
                    ___spawnCooldown = __instance.FormationSpawnTime;
                }

                if(AirfieldCooldowns[__instance.AirfieldData] > __instance.AirfieldData.RespawnTime)
                {
                    foreach (ArenaAirfieldFlight flight in __instance.AirfieldData.DefensiveFlights)
                    {
                        var activeFlight = ActiveFlights[flight];
                        if (!(flight.Faction != __instance.Faction.Name) && (activeFlight == null || activeFlight.IsDestroyed))
                        {
                            Log.LogDebug("Spawning Flight " + flight.WingName);
                            typeof(StrategicTarget2).GetMethod("SpawnFlight", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { flight });
                        }
                    }
                    AirfieldCooldowns[__instance.AirfieldData] = 0f;
                }
            }
            
            return false;
        }


        [HarmonyPatch(typeof(StrategicTarget2))]
        [HarmonyPatch("StopArenaLogic")]
        [HarmonyPostfix]
        static void StopArenaLogic()
        {
            Log?.LogDebug("StopArenaLogic Patch Called");
            ActiveFlights.Clear();
        }
    

        private void Awake()
        {
            Log = this.Logger;
            if (!typeof(StrategicTarget2).GetMethod("SpawnFlight", BindingFlags.NonPublic | BindingFlags.Instance).ReturnType.Equals(typeof(UniFlight)) 
                || typeof(ArenaAirfield).GetField("RespawnTime", BindingFlags.Public) == null)
            {
                Log.LogError("Unable to run plugin. Make sure you're also using the patch for this mod.");
                return;
            }

            Harmony harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            harmony.GetPatchedMethods().Do((method) => Log.LogDebug(method.ToString() + " Patched"));
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
