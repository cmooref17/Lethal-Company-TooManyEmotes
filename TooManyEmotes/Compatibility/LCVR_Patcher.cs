using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using LCVR;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    public static class LCVR_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("io.daxcess.lcvr") && !DisableVR; } }
        public static bool DisableVR;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        public static void CheckIfVRIsEnabled()
        {
            if (Plugin.IsModLoaded("io.daxcess.lcvr"))
                ApplyPatch();
        }


        private static void ApplyPatch()
        {
            DisableVR = LCVR.Plugin.Config.DisableVR.Value;
            Plugin.LogWarning("LCVR is Enabled");
            if (DisableVR)
                Plugin.LogWarning("VR mode is Enabled in the LCVR Config. Emotes will be disabled on self.");
            else
                Plugin.LogWarning("VR mode is Disabled in the LCVR Config. Emotes will play normally on self.");
        }
    }
}
