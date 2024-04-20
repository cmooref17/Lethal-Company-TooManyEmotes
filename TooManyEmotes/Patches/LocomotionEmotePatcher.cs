using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class LocomotionEmotePatcher
    {
        [HarmonyPatch(typeof(PlayerControllerB), "CheckConditionsForEmote")]
        [HarmonyPostfix]
        public static void AllowMovingInEmoteConditions(ref bool __result, PlayerControllerB __instance)
        {
            if (__result)
                return;

            //if (EmoteController.allEmoteControllers.TryGetValue(__instance.gameObject, out var emoteController) && emoteController.IsPerformingCustomEmote() && (ConfigSync.instance.syncEnableMovingWhileEmoting || emoteController.performingEmote.canMoveWhileEmoting))
            if (EmoteController.allEmoteControllers.TryGetValue(__instance.gameObject, out var emoteController) && emoteController.IsPerformingCustomEmote() && (ThirdPersonEmoteController.isMovingWhileEmoting || emoteController.performingEmote.canMoveWhileEmoting))
            {
                bool result = !(__instance.inSpecialInteractAnimation || __instance.isPlayerDead || __instance.isCrouching || __instance.isClimbingLadder || __instance.isGrabbingObjectAnimation || __instance.inTerminalMenu || __instance.isTypingChat);
                if (result)
                    __result = true;
            }
        }


        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPrefix]
        public static bool CancelMovingEmote()
        {
            if (localPlayerController == null || emoteControllerLocal == null)
                return true;

            if (emoteControllerLocal.IsPerformingCustomEmote() && ThirdPersonEmoteController.isMovingWhileEmoting)
            {
                emoteControllerLocal.StopPerformingEmote();
                return false;
            }
            return true;
        }
    }
}
