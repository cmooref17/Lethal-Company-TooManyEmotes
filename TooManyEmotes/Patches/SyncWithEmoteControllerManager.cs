using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class SyncWithEmoteControllerManager
    {
        public static int syncableEmoteLayerMask = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemies"));
        public static EmoteController lookingAtSyncableEmoteController = null;


        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        public static void CheckIfLookingAtSyncableEmoteController(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || emoteControllerLocal == null || ConfigSettings.disableEmotesForSelf.Value || Compatibility.LCVR_Compat.LoadedAndEnabled)
                return;

            if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                localPlayerController.cursorTip.text = "";

            if (!emoteControllerLocal.IsPerformingCustomEmote() && !__instance.isPlayerDead && Physics.Raycast(localPlayerController.gameplayCamera.transform.position + localPlayerController.gameplayCamera.transform.forward * 0.5f, localPlayerController.gameplayCamera.transform.forward * 4.5f, out var hit, 4.5f, syncableEmoteLayerMask))
            {
                try
                {
                    EmoteController syncWithEmoteController = hit.collider.GetComponentInChildren<EmoteController>() ?? hit.collider.GetComponentInParent<EmoteController>();
                    if (CanSyncWithEmoteController(emoteControllerLocal, syncWithEmoteController))
                    {
                        if (!(syncWithEmoteController is EmoteControllerMaskedEnemy) || ConfigSettings.enableSyncingEmotesWithMaskedEnemies.Value)
                        {
                            lookingAtSyncableEmoteController = syncWithEmoteController;
                            localPlayerController.cursorTip.text = "[E] Sync emote";
                            return;
                        }
                    }
                }
                catch { }
            }
            ResetState();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool SyncWithEmoteController_performed(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || !context.performed)
                return true;

            if (emoteControllerLocal != null && lookingAtSyncableEmoteController != null && !ConfigSettings.disableEmotesForSelf.Value && !Compatibility.LCVR_Compat.LoadedAndEnabled && !__instance.isPlayerDead)
            {
                bool canSync = CanSyncWithEmoteController(emoteControllerLocal, lookingAtSyncableEmoteController);
                if (canSync)
                {
                    Log("[SyncWithEmoteController_performed] Attempting to sync with emote controller: " + lookingAtSyncableEmoteController);
                    emoteControllerLocal.TrySyncingEmoteWithEmoteController(lookingAtSyncableEmoteController);
                    ResetState();
                    return false;
                }
            }
            ResetState();
            return true;
        }


        public static bool CanSyncWithEmoteController(EmoteController sourceEmoteController, EmoteController syncWithEmoteController)
        {
            if (sourceEmoteController == emoteControllerLocal && (ConfigSettings.disableEmotesForSelf.Value || Compatibility.LCVR_Compat.LoadedAndEnabled))
            {
                return false;
            }

            if (sourceEmoteController == null || syncWithEmoteController == null || sourceEmoteController == syncWithEmoteController)
            {
                return false;
            }

            return !sourceEmoteController.IsPerformingCustomEmote() && syncWithEmoteController.IsPerformingCustomEmote() && syncWithEmoteController.performingEmote.canSyncEmote;
        }


        public static void ResetState()
        {
            lookingAtSyncableEmoteController = null;
        }
    }
}
