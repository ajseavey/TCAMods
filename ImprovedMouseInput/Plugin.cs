using BepInEx;
using Falcon.Controls;
using Falcon.Game2;
using HarmonyLib;
using UnityEngine;

namespace ImprovedMouseInput
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [HarmonyPatch(typeof(PlayerInput), "Update")]
    public class Plugin : BaseUnityPlugin
    {
        [HarmonyPostfix]
        private static void Update(PlayerInput __instance, float ___mouseX, float ___mouseY, ref StickAndRudder ___rawAnalogStick)
        {
            if (!__instance.IsAnyInputBlocked && __instance.UseMouseJoystick)
            {
                float pitch = Mathf.Pow(Mathf.Abs(___rawAnalogStick.Pitch), GameSettings.Controls.StickCurve) * Mathf.Sign(___rawAnalogStick.Pitch);
                if (Mathf.Abs(pitch) > Mathf.Abs(__instance.FlightInput.Pitch))
                {
                    __instance.FlightInput.Pitch = pitch;
                }

                if (GameSettings.Controls.HorizontalAxis == Falcon.Game2.Settings.MouseHorizontalAxis.Roll)
                {
                    float roll = Mathf.Pow(Mathf.Abs(___rawAnalogStick.Roll), GameSettings.Controls.StickCurve) * Mathf.Sign(___rawAnalogStick.Roll);
                    if (Mathf.Abs(roll) > Mathf.Abs(__instance.FlightInput.Roll))
                    {
                        __instance.FlightInput.Roll = roll;
                    }
                }
            }
        }

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
