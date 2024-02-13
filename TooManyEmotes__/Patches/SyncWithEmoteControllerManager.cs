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
            if (__instance != localPlayerController || EmoteControllerPlayer.emoteControllerLocal == null || ConfigSettings.disableEmotesForSelf.Value)
                return;

            ResetState();
            if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                localPlayerController.cursorTip.text = "";

            if (Physics.Raycast(localPlayerController.gameplayCamera.transform.position + localPlayerController.gameplayCamera.transform.forward * 0.5f, localPlayerController.gameplayCamera.transform.forward * 4.5f, out var hit, 4.5f, syncableEmoteLayerMask) && !EmoteControllerPlayer.emoteControllerLocal.IsPerformingCustomEmote() && !__instance.isPlayerDead)
            {
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
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool OnSyncEmoteWithPlayer(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (context.performed && EmoteControllerPlayer.emoteControllerLocal != null && !ConfigSettings.disableEmotesForSelf.Value && !__instance.isPlayerDead && lookingAtSyncableEmoteController != null && __instance.cursorTip.text.Contains("Sync emote"))
            {
                if (lookingAtSyncableEmoteController != null && lookingAtSyncableEmoteController.IsPerformingCustomEmote())
                {
                    EmoteControllerPlayer.emoteControllerLocal.TrySyncingEmoteWithEmoteController(lookingAtSyncableEmoteController);
                    return false;
                }
            }
            ResetState();
            return true;
        }


        public static void ResetState()
        {
            lookingAtSyncableEmoteController = null;
        }
    }
}
