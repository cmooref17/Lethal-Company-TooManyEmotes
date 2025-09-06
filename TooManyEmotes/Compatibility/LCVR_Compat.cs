using HarmonyLib;
using LCVR.Managers;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class LCVR_Compat
    {
        internal static bool Loaded { get { return Plugin.IsModLoaded("io.daxcess.lcvr"); } }
        internal static bool VRModeEnabled { get { return VRSession.InVR; } }
        public static bool LoadedAndEnabled { get { return Loaded && VRModeEnabled; } }
    }
}
