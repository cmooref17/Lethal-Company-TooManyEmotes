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

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class SyncWithEmoteControllerManager
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static int syncableEmoteLayerMask = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemies"));
        public static EmoteController lookingAtSyncableEmoteController = null;


        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        public static void CheckIfLookingAtSyncableEmoteController(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || EmoteControllerPlayer.emoteControllerLocal == null || ConfigSettings.disableEmotesForSelf.Value || Compatibility.LCVR_Patcher.Enabled)
                return;

            if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                localPlayerController.cursorTip.text = "";

            if (!EmoteControllerPlayer.emoteControllerLocal.IsPerformingCustomEmote() && !__instance.isPlayerDead && Physics.Raycast(localPlayerController.gameplayCamera.transform.position + localPlayerController.gameplayCamera.transform.forward * 0.5f, localPlayerController.gameplayCamera.transform.forward * 4.5f, out var hit, 4.5f, syncableEmoteLayerMask))
            {
                try
                {
                    EmoteController syncWithEmoteController = hit.collider.GetComponentInChildren<EmoteController>() ?? hit.collider.GetComponentInParent<EmoteController>();
                    //Plugin.LogWarning("SourceEmoteController: " + EmoteControllerPlayer.emoteControllerLocal.emoteControllerName + " SyncWithController: " + syncWithEmoteController.emoteControllerName + " EQual?: " + (EmoteControllerPlayer.emoteControllerLocal == syncWithEmoteController));
                    if (CanSyncWithEmoteController(EmoteControllerPlayer.emoteControllerLocal, syncWithEmoteController))
                    {
                        if (!(syncWithEmoteController is EmoteControllerMaskedEnemy) || ConfigSettings.enableSyncingEmotesWithMaskedEnemies.Value)
                        {
                            lookingAtSyncableEmoteController = syncWithEmoteController;
                            localPlayerController.cursorTip.text = "[E] Sync emote";
                            return;
                        }
                    }
                }
                catch (Exception e) { }

                /*
                var maskedEnemy = hit.collider.gameObject.GetComponentInParent<MaskedPlayerEnemy>();
                if (ConfigSettings.enableSyncingEmotesWithMaskedEnemies.Value && maskedEnemy != null && EmoteControllerMaskedEnemy.allMaskedEnemyEmoteControllers.TryGetValue(maskedEnemy, out var emoteControllerMaskedEnemy) && emoteControllerMaskedEnemy.IsPerformingCustomEmote() && emoteControllerMaskedEnemy.performingEmote.canSyncEmote)
                {
                    lookingAtSyncableEmoteController = emoteControllerMaskedEnemy;
                    localPlayerController.cursorTip.text = "[E] Sync emote";
                    return;
                }
                PlayerControllerB hitPlayer = hit.collider.gameObject.GetComponentInParent<PlayerControllerB>();
                if (hitPlayer != null && hitPlayer != localPlayerController)
                {
                    if (EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(hitPlayer, out var emoteControllerPlayer) && emoteControllerPlayer.IsPerformingCustomEmote() && emoteControllerPlayer.performingEmote.canSyncEmote)
                    {
                        if (SessionManager.unlockedEmotes.Contains(emoteControllerPlayer.performingEmote) || ConfigSync.instance.syncSyncUnsharedEmotes)
                        {
                            lookingAtSyncableEmoteController = emoteControllerPlayer;
                            localPlayerController.cursorTip.text = "[E] Sync emote";
                        }
                    }
                }
                */
            }
            ResetState();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool SyncWithEmoteController_performed(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || !context.performed)
                return true;

            if (EmoteControllerPlayer.emoteControllerLocal != null && lookingAtSyncableEmoteController != null && !ConfigSettings.disableEmotesForSelf.Value && !Compatibility.LCVR_Patcher.Enabled && !__instance.isPlayerDead)
            {
                bool canSync = CanSyncWithEmoteController(EmoteControllerPlayer.emoteControllerLocal, lookingAtSyncableEmoteController);
                if (canSync)
                {
                    Plugin.Log("[SyncWithEmoteController_performed] Attempting to sync with emote controller: " + lookingAtSyncableEmoteController);
                    EmoteControllerPlayer.emoteControllerLocal.TrySyncingEmoteWithEmoteController(lookingAtSyncableEmoteController);
                    ResetState();
                    return false;
                }
            }
            ResetState();
            return true;
        }


        public static bool CanSyncWithEmoteController(EmoteController sourceEmoteController, EmoteController syncWithEmoteController)
        {
            if (sourceEmoteController == EmoteControllerPlayer.emoteControllerLocal && (ConfigSettings.disableEmotesForSelf.Value || Compatibility.LCVR_Patcher.Enabled))
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
