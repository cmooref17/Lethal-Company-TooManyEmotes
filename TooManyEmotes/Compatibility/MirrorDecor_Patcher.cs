using HarmonyLib;
using TooManyEmotes.Patches;
using UnityEngine.Rendering;
using static TooManyEmotes.CustomLogging;

/*namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    public static class MirrorDecor_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("quackandcheese.mirrordecor"); } }

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void ApplyPatch()
        {
            if (Enabled)
            {
                ThirdPersonEmoteController.localPlayerBodyLayer = 23;
                ThirdPersonEmoteController.defaultShadowCastingMode = ShadowCastingMode.On;
                Log("Applied patch for MirrorDecor");
            }
        }
    }
}*/
