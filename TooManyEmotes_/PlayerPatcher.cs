using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using UnityEngine.Rendering;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.CompatibilityPatcher;
using System.Reflection;
using Unity.Netcode;
using MoreCompany.Cosmetics;
using UnityEditor.Animations;
using TMPro;
using UnityEngine.Scripting;
using UnityEditor.PackageManager;
using TooManyEmotes.Input;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        //public static AnimatorOverrideController localPlayerAnimatorOverrideController;
        public static AnimationClip defaultDance1Clip;
        public static int emoteStateHash { get { return localPlayerController != null ? Animator.StringToHash(localPlayerController.playerBodyAnimator.GetLayerName(1) + ".Dance1") : -1; } }

        //public static Dictionary<PlayerControllerB, UnlockableEmote> performingEmotes = new Dictionary<PlayerControllerB, UnlockableEmote>();
        //public static UnlockableEmote performingCustomEmoteLocal { get { return localPlayerController != null ? performingEmotes[localPlayerController] : null; } set { if (localPlayerController == null) return; performingEmotes[localPlayerController] = value; } }

        public static int syncableEmoteLayerMask = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemies"));

        public static Dictionary<PlayerControllerB, PlayerData> allPlayerData = new Dictionary<PlayerControllerB, PlayerData>();
        public static PlayerData playerDataLocal { get { return localPlayerController != null ? allPlayerData[localPlayerController] : null; } }

        public static AnimatorOverrideController localAnimatorController { get { return playerDataLocal.animatorController; } }
        public static AnimatorOverrideController otherAnimatorController { get { return StartOfRound.Instance?.otherClientsAnimatorController as AnimatorOverrideController; } }

        public static PlayerControllerB lookingAtPlayerSyncableEmote = null;
        public static MaskedPlayerEnemy lookingAtMaskedEnemySyncableEmote = null;
        public static bool syncingEmoteWithPlayer = false;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void ResetValues()
        {
            //performingEmotes.Clear();
            allPlayerData.Clear();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        public static void OnPlayerStart(PlayerControllerB __instance)
        {
            allPlayerData[__instance] = new PlayerData(__instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance)
        {
            Plugin.Log("Initializing local player.");
            var playerData = allPlayerData[__instance];
            if (!playerData.animatorControllerIsOverride)
                playerData.ConvertAnimatorControllerToOverride();
            if (!ConfigSettings.disableEmotesForSelf.Value)
            {
                playerData.ConvertAnimatorControllerToOverride();
                defaultDance1Clip = playerData.animatorController["Dance1"];
            }
            else
                defaultDance1Clip = otherAnimatorController["Dance1"];

            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var textComponent = HUDManager.Instance.controlTipLines[i];
                if (textComponent.text == "")
                {
                    int bindingIndex = StartOfRound.Instance.localPlayerUsingController ? 1 : 0;
                    HUDManager.Instance.controlTipLines[i].text = string.Format("[{0}]: Open Emote Radial Menu", ConfigSettings.GetDisplayName(InputUtilsCompat.Enabled ? Keybinds.OpenEmoteMenuAction.bindings[bindingIndex].path : Keybinds.OpenEmoteMenuAction.bindings[bindingIndex].path));
                    break;
                }
            }

            UpdateKeybindDisplayNames.UpdateControlTipLines();
        }


        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPrefix]
        public static void OnPlayerDC(int playerObjectNumber, ulong clientId, StartOfRound __instance)
        {
            PlayerControllerB playerController = __instance.allPlayerObjects[playerObjectNumber].GetComponent<PlayerControllerB>();
            //if (playerController != null && performingEmotes.TryGetValue(playerController, out var emote) && emote != null)
            if (playerController != null && allPlayerData[playerController].isPerformingEmote)
                OnUpdateCustomEmote(-1, playerController);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPrefix]
        public static void OnPlayerDeath(Vector3 bodyVelocity, PlayerControllerB __instance)
        {
            Plugin.LogWarning("Player died while emoting. What a loser... I mean, I hope this handles smoothly.");
            //if (performingEmotes.TryGetValue(__instance, out var emote) && emote != null)
            if (allPlayerData[__instance].isPerformingEmote)
                OnUpdateCustomEmote(-1, __instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static bool PerformCustomEmoteLocalPrefix(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || __instance != localPlayerController)
                return true;

            var syncWithPlayerData = lookingAtPlayerSyncableEmote != null ? allPlayerData[lookingAtPlayerSyncableEmote] : null;

            if (emoteID > 2)
            {
                //if (performingCustomEmoteLocal != null)
                return !playerDataLocal.isPerformingEmote;
            }
            
            if (emoteID < 0)
            {
                // Prevent the emote if performing an emote from another mod, such as MoreEmotes
                if (localPlayerController.playerBodyAnimator.GetInteger("emoteNumber") > 2)
                    return false;

                if (CallCheckConditionsForEmote(localPlayerController))
                {
                    localPlayerController.performingEmote = true;
                    localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                    emoteID = Mathf.Abs(emoteID) - 1;
                    var emote = StartOfRoundPatcher.allUnlockableEmotes[emoteID];
                    if (emote != null)
                    {
                        if (emote.randomEmotePool != null && emote.randomEmotePool.Count >= 1)
                        {
                            int randomIndex = UnityEngine.Random.Range(0, emote.randomEmotePool.Count);
                            var randomEmote = emote.randomEmotePool[randomIndex];
                            if (randomEmote != null)
                                emote = randomEmote;
                        }
                        PlayerControllerB syncWithPlayer = null;
                        //if (syncingEmoteWithPlayer && lookingAtPlayerSyncableEmote != null && performingEmotes.TryGetValue(lookingAtPlayerSyncableEmote, out var syncEmote) && syncEmote.canSyncEmote)
                        if (syncingEmoteWithPlayer && syncWithPlayerData?.performingEmote != null && syncWithPlayerData.performingEmote.canSyncEmote)
                        {
                            emote = syncWithPlayerData.performingEmote;
                            syncWithPlayer = lookingAtPlayerSyncableEmote;
                        }
                        OnUpdateCustomEmote(emote.emoteId, localPlayerController, syncWithPlayer);
                        ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                        ForceSendAnimationUpdateLocal(emote.emoteId);
                    }
                    return false;
                }
            }
            if (playerDataLocal.isPerformingEmote)
            {
                OnUpdateCustomEmote(-1, localPlayerController);
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                ForceSendAnimationUpdateLocal(-1);
                return false;
            }

            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPostfix]
        public static void PerformCustomEmoteLocalPostfix(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {
            syncingEmoteWithPlayer = false;
            lookingAtPlayerSyncableEmote = null;
            lookingAtMaskedEnemySyncableEmote = null;
        }


        public static bool CallCheckConditionsForEmote(PlayerControllerB playerController)
        {
            bool otherConditions = playerController.inAnimationWithEnemy == null && !(playerController == localPlayerController && CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            return (bool)method.Invoke(playerController, new object[] { }) && otherConditions;
        }


        static void ForceSendAnimationUpdateLocal(int emoteId)
        {
            Traverse.Create(localPlayerController).Field("updatePlayerAnimationsInterval").SetValue(0);
            List<int> previousAnimationStateHash = (List<int>)Traverse.Create(localPlayerController).Field("previousAnimationStateHash").GetValue();
            List<int> currentAnimationStateHash = (List<int>)Traverse.Create(localPlayerController).Field("currentAnimationStateHash").GetValue();
            previousAnimationStateHash[1] = emoteStateHash;
            currentAnimationStateHash[1] = emoteStateHash;

            float animationSpeed = 1;
            if (emoteId != -1)
                animationSpeed += (emoteId + 1) / 10000f;
            if (syncingEmoteWithPlayer && lookingAtPlayerSyncableEmote != null)
            {
                float oldAnimationSpeed = animationSpeed;
                float appendValue = (lookingAtPlayerSyncableEmote.playerClientId + 1) / 1000000f;
                animationSpeed += appendValue;
            }

            Traverse.Create(localPlayerController).Field("previousAnimationSpeed").SetValue(1);

            localPlayerController.StartPerformingEmoteServerRpc();
            MethodInfo method = localPlayerController.GetType().GetMethod("UpdatePlayerAnimationServerRpc", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(localPlayerController, new object[] { emoteStateHash, animationSpeed });
        }
        

        [HarmonyPatch(typeof(PlayerControllerB), "StartPerformingEmoteClientRpc")]
        [HarmonyPostfix]
        public static void OnStartPerformingEmoteClientRpc(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController && allPlayerData.TryGetValue(__instance, out var playerData) && playerData.TryGetLoadedEmote(out var emote))
                OnUpdateCustomEmote(emote.emoteId, __instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "StopPerformingEmoteClientRpc")]
        [HarmonyPostfix]
        public static void OnStopPerformingEmoteClientRpc(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController && !__instance.performingEmote && allPlayerData[__instance].isPerformingEmote)
                OnUpdateCustomEmote(-1, __instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "UpdatePlayerAnimationClientRpc")]
        [HarmonyPrefix]
        public static void UpdatePlayerAnimationClientRpcPrefix(int animationState, ref float animationSpeed, PlayerControllerB __instance)
        {
            if (localPlayerController == null || __instance == localPlayerController)
                return;

            if ((NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost) && (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() != 2)
                return;

            if (animationState != emoteStateHash)
                return;

            // Let's do some fun logic
            int clientId = Mathf.RoundToInt(animationSpeed * 10000 % 1 * 100) - 1;
            if (clientId >= 0 && StartOfRoundPatcher.TryGetPlayerByClientId((ulong)clientId, out var syncWithPlayer))
            {
                Plugin.Log("Player syncing emote with another player. Player id syncing emote: " + __instance.actualClientId + " Syncing with player id: " + clientId);
                OnUpdateCustomEmote(0, __instance, syncWithPlayer);
                return;
            }

            int emoteId = Mathf.RoundToInt(animationSpeed * 10 % 1 * 1000) - 1;
            animationSpeed = 1;
            if (emoteId >= 0 && emoteId < StartOfRoundPatcher.allUnlockableEmotes.Count)
                OnUpdateCustomEmote(emoteId, __instance);
            else
                OnUpdateCustomEmote(-1, __instance);
        }


        public static void OnUpdateCustomEmote(int emoteId, PlayerControllerB playerController, PlayerControllerB syncWithPlayer = null) => OnUpdateCustomEmote(emoteId, allPlayerData[playerController], syncWithPlayer != null ? allPlayerData[syncWithPlayer] : null);
        public static void OnUpdateCustomEmote(int emoteId, PlayerData playerData, PlayerData syncWithPlayerData = null)
        {
            if (playerData == null || (playerData.playerController == localPlayerController && ConfigSettings.disableEmotesForSelf.Value))
                return;

            Plugin.Log("OnUpdateCustomEmote for player: " + playerData.name + " " + (syncWithPlayerData != null && syncWithPlayerData.isPerformingEmote ? ("Syncing with player: " + syncWithPlayerData.playerController.name + " Emote: " + syncWithPlayerData.performingEmote.emoteName + " EmoteId: " + syncWithPlayerData.performingEmote.emoteId) : ("Emote id: " + emoteId)));
            if (emoteId != -1)
            {
                playerData.EnableRigBuilder(false);
                UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotes[emoteId];
                var animationClip = emote.animationClip;
                float normalizedTime = 0;
                if (syncWithPlayerData != null && syncWithPlayerData.isPerformingEmote)
                {
                    if (syncWithPlayerData.TryGetCurrentAnimationClip(out var currentAnimationClip) && syncWithPlayerData.performingEmote.ClipIsInEmote(currentAnimationClip))
                    {                        
                        emote = syncWithPlayerData.performingEmote;
                        animationClip = currentAnimationClip;
                        normalizedTime = emote.loopable ? syncWithPlayerData.normalizedTimeAnimation : 0;
                        Plugin.Log("Syncing player emote. Emote: " + emote.emoteName + " OverrideAnimationClip: " + animationClip.name + " EmoteTime: " + normalizedTime);
                    }
                    else
                        Plugin.LogError("Failed to get current animation clip from player: " + syncWithPlayerData.name + "." + (animationClip == null ? " Got null clip!" : "") + ((currentAnimationClip != null && currentAnimationClip != emote.animationClip && currentAnimationClip != emote.transitionsToClip ? " Clip does not match starting or loop clip! Playing clip: " + currentAnimationClip.name + " EmoteStartClip: " + emote.animationClip.name + " LoopClip: " + (emote.transitionsToClip != null ? emote.transitionsToClip.name : "NULL. This is probably okay?") : "")));
                }

                playerData.performingEmote = emote;
                playerData.currentEmoteNumber = 1;
                Plugin.Log("Setting animation clip for player with id: " + playerData.clientId + " Clip: " + (animationClip != null ? animationClip.name : emote.animationClip.name));
                playerData.SetCurrentAnimationClip(animationClip != null ? animationClip : emote.animationClip);
                playerData.PlayEmoteAtTime(emote, overrideClip: animationClip != emote.animationClip ? animationClip : null, normalizedTime: normalizedTime, playEmoteEndOfFrame : normalizedTime != 0 || emote.isPose);
            }
            else
            {
                playerData.SetCurrentAnimationClip(defaultDance1Clip);
                playerData.performingEmote = null;

                playerData.animator.CrossFadeInFixedTime("Dance1", 0.1f);
                playerData.EnableRigBuilder(true);

                if (playerData.playerController == localPlayerController)
                {
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                    if (localPlayerController.serverItemHolder == localPlayerController.currentlyHeldObjectServer?.parentObject)
                        localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.localItemHolder;
                }
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "CheckConditionsForEmote")]
        [HarmonyPostfix]
        public static void AllowMovingInEmoteConditions(ref bool __result, PlayerControllerB __instance)
        {
            if (!ConfigSync.instance.syncEnableMovingWhileEmoting || __result)
                return;
            if (!allPlayerData.TryGetValue(__instance, out var playerData) || !playerData.isPerformingEmote)
                return;

            bool isJumping = (bool)Traverse.Create(__instance).Field("isJumping").GetValue();
            bool result = !(__instance.inSpecialInteractAnimation || __instance.isPlayerDead || isJumping || __instance.isCrouching || __instance.isClimbingLadder || __instance.isGrabbingObjectAnimation || __instance.inTerminalMenu || __instance.isTypingChat);
            if (result)
                __result = true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void CheckIfStopPerformingEmoteDirty(PlayerControllerB __instance)
        {
            if (allPlayerData.TryGetValue(__instance, out var playerData) && playerData.isPerformingEmote && !__instance.performingEmote)
                OnUpdateCustomEmote(-1, __instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        public static void CheckIfLookingAtPlayerSyncableEmote(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value)
                return;

            lookingAtPlayerSyncableEmote = null;
            lookingAtMaskedEnemySyncableEmote = null;
            if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                localPlayerController.cursorTip.text = "";

            if (Physics.Raycast(localPlayerController.gameplayCamera.transform.position + localPlayerController.gameplayCamera.transform.forward * 0.5f, localPlayerController.gameplayCamera.transform.forward * 4.5f, out var hit, 4.5f, syncableEmoteLayerMask) && !playerDataLocal.isPerformingEmote && !__instance.isPlayerDead)
            {
                MaskedPlayerEnemy maskedEnemy = hit.collider.gameObject.GetComponentInParent<MaskedPlayerEnemy>();
                if (ConfigSettings.enableSyncingEmotesWithMaskedEnemies.Value && maskedEnemy != null && MaskedEnemyEmotes.spawnedMaskedEnemyData.TryGetValue(maskedEnemy, out var maskedEnemyData) && maskedEnemyData.performingEmote != null && maskedEnemyData.performingEmote.canSyncEmote)
                {
                    lookingAtMaskedEnemySyncableEmote = maskedEnemy;
                    localPlayerController.cursorTip.text = "[E] Sync emote";
                    return;
                }
                PlayerControllerB hitPlayer = hit.collider.gameObject.GetComponentInParent<PlayerControllerB>();
                if (hitPlayer != null && hitPlayer != localPlayerController)
                {
                    if (allPlayerData.TryGetValue(hitPlayer, out var playerData) && playerData.isPerformingEmote && playerData.performingEmote.canSyncEmote && (StartOfRoundPatcher.unlockedEmotes.Contains(playerData.performingEmote) || ConfigSync.instance.syncSyncUnsharedEmotes))
                    {
                        lookingAtPlayerSyncableEmote = hitPlayer;
                        localPlayerController.cursorTip.text = "[E] Sync emote";
                    }
                }
            }
        }


        public static UnlockableEmote GetCurrentlyPlayingEmote(PlayerControllerB playerController)
        {
            if (allPlayerData.TryGetValue(playerController, out var playerData) && playerData.TryGetLoadedEmote(out var emote))
                return emote;
            return null;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool OnSyncEmoteWithPlayer(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (allPlayerData.TryGetValue(__instance, out var playerData) && playerData == playerDataLocal && !ConfigSettings.disableEmotesForSelf.Value && context.performed && !__instance.isPlayerDead && (lookingAtPlayerSyncableEmote != null || lookingAtMaskedEnemySyncableEmote != null) && __instance.cursorTip.text.Contains("Sync emote"))
            {
                UnlockableEmote emote = null;
                if (lookingAtPlayerSyncableEmote != null && allPlayerData.TryGetValue(lookingAtPlayerSyncableEmote, out var syncWithPlayerData) && syncWithPlayerData.isPerformingEmote)
                {
                    emote = syncWithPlayerData.performingEmote;
                    // Hello darkness my old friend
                }
                else if (lookingAtMaskedEnemySyncableEmote != null && MaskedEnemyEmotes.spawnedMaskedEnemyData.TryGetValue(lookingAtMaskedEnemySyncableEmote, out var maskedEnemyData))
                    emote = maskedEnemyData.performingEmote;

                if (emote != null)
                {
                    syncingEmoteWithPlayer = true;
                    __instance.PerformEmote(context, -(emote.emoteId + 1));
                }

                return false;
            }
            return true;
        }
    }
}