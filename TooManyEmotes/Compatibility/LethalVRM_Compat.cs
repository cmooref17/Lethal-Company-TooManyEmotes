using HarmonyLib;
using LCVR.Player;
using System.Diagnostics.Eventing.Reader;
using TooManyEmotes.Patches;
using TooManyEmotes.UI;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class LethalVRM_Compat
    {
        internal static bool Enabled { get { return Plugin.IsModLoaded("Ooseykins.LethalVRM") || Plugin.IsModLoaded("OomJan.BetterLethalVRM"); } }
        
        internal static void DisplayVRMModel()
        {
            ThirdPersonEmoteController.emoteCamera.cullingMask |= 1 << 23;
        }


        internal static void HideVRMModel()
        {
            ThirdPersonEmoteController.emoteCamera.cullingMask &= ~(1 << 23);
        }
    }
}
