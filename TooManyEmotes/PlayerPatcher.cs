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

namespace TooManyEmotes.Patches {

    [HarmonyPatch]
    public class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static AnimatorOverrideController localPlayerAnimatorOverrideController;
        public static AnimationClip defaultDance1Clip;
        public static int emoteStateHash { get { return localPlayerController != null ? Animator.StringToHash(string.Format("{0}.Dance1", localPlayerController.playerBodyAnimator.GetLayerName(1))) : -1; } }

        public static Dictionary<PlayerControllerB, UnlockableEmote> performingEmotes = new Dictionary<PlayerControllerB, UnlockableEmote>();
        public static UnlockableEmote performingCustomEmoteLocal { get { return localPlayerController != null ? performingEmotes[localPlayerController] : null; } set { if (localPlayerController == null) return; performingEmotes[localPlayerController] = value; } }

        public static int syncableEmoteLayerMask = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemies"));
        public static PlayerControllerB lookingAtPlayerSyncableEmote = null;
        public static MaskedPlayerEnemy lookingAtMaskedEnemySyncableEmote = null;
        public static bool syncingEmoteWithPlayer = false;

        public static Transform localItemHolder;
        public static Transform serverItemHolder;



        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void ResetValues(StartOfRound __instance)
        {
            performingEmotes.Clear();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        public static void OnPlayerStart(PlayerControllerB __instance)
        {
            if (!performingEmotes.ContainsKey(__instance))
                performingEmotes.Add(__instance, null);
            else
                performingEmotes[__instance] = null;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance) {
            if (!ConfigSettings.disableEmotesForSelf.Value)
            {
                localPlayerAnimatorOverrideController = (AnimatorOverrideController)StartOfRound.Instance.localClientAnimatorController;
                defaultDance1Clip = localPlayerAnimatorOverrideController["Dance1"];
            }
            else
                defaultDance1Clip = ((AnimatorOverrideController)StartOfRound.Instance.otherClientsAnimatorController)["Dance1"];

            localItemHolder = __instance.localItemHolder;
            serverItemHolder =  __instance.serverItemHolder;

            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var textComponent = HUDManager.Instance.controlTipLines[i];
                if (textComponent.text == "")
                {
                    HUDManager.Instance.controlTipLines[i].text = string.Format("[{0}]: Open Emote Radial Menu", ConfigSettings.GetDisplayName(ConfigSettings.openEmoteMenuKeybind.Value));
                    break;
                }
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPrefix]
        public static void OnLocalClientReady(int playerObjectNumber, ulong clientId, StartOfRound __instance)
        {
            PlayerControllerB playerController = __instance.allPlayerObjects[playerObjectNumber].GetComponent<PlayerControllerB>();
            if (playerController != null && performingEmotes.TryGetValue(playerController, out var emote) && emote != null)
                OnUpdateCustomEmote(-1, playerController);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static bool PerformCustomEmoteLocalPrefix(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return true;

            if (__instance == localPlayerController && (context.performed || emoteID < 0) && CallCheckConditionsForEmote(localPlayerController) && localPlayerController.inAnimationWithEnemy == null)
            {
                localPlayerController.performingEmote = true;
                // Performing custom emote
                if (emoteID < 0)
                {
                    localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                    emoteID = Mathf.Abs(emoteID) - 1;
                    var emote = StartOfRoundPatcher.allUnlockableEmotes[emoteID];
                    if (emote != null /* && StartOfRoundPatcher.unlockedEmotes.Contains(emote) */)
                    {
                        if (emote.randomEmotePool != null && emote.randomEmotePool.Count >= 1)
                        {
                            int randomIndex = UnityEngine.Random.Range(0, emote.randomEmotePool.Count);
                            var randomEmote = emote.randomEmotePool[randomIndex];
                            if (randomEmote != null)
                                emote = randomEmote;
                        }
                        PlayerControllerB syncWithPlayer = null;
                        if (syncingEmoteWithPlayer && lookingAtPlayerSyncableEmote != null && performingEmotes.TryGetValue(lookingAtPlayerSyncableEmote, out var syncEmote) && syncEmote.canSyncEmote)
                        {
                            emote = syncEmote;
                            syncWithPlayer = lookingAtPlayerSyncableEmote;
                        }
                        OnUpdateCustomEmote(emote.emoteId, localPlayerController, syncWithPlayer);
                        ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                        ForceSendAnimationUpdateLocal(emote.emoteId);
                    }
                    return false;
                }
                // Normal emote
                else if (performingCustomEmoteLocal != null)
                {
                    OnUpdateCustomEmote(-1, localPlayerController);
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                    ForceSendAnimationUpdateLocal(-1);
                    return false;
                }
            }
            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPostfix]
        public static void PerformCustomEmoteLocalPostfix(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {
            syncingEmoteWithPlayer = false;
        }




        public static bool CallCheckConditionsForEmote(PlayerControllerB playerController) {
            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            return (bool)method.Invoke(localPlayerController, new object[] { });
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
        public static void OnStartPerformingEmoteClientRpc(PlayerControllerB __instance) {
            if (__instance != localPlayerController && IsCurrentlyPlayingCustomEmote(__instance))
            {
                UnlockableEmote emote = GetCurrentlyPlayingEmote(__instance);
                OnUpdateCustomEmote(emote.emoteId, __instance);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "StopPerformingEmoteClientRpc")]
        [HarmonyPostfix]
        public static void OnStopPerformingEmoteClientRpc(PlayerControllerB __instance) {
            if (__instance != localPlayerController && !__instance.performingEmote && performingEmotes[__instance] != null)
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
            if (clientId >= 0)
            {
                foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
                {
                    if ((ulong)clientId == playerController.playerClientId)
                    {
                        OnUpdateCustomEmote(0, __instance, playerController);
                        return;
                    }
                }
            }

            int emoteId = Mathf.RoundToInt(animationSpeed * 10 % 1 * 1000) - 1;
            animationSpeed = 1;
            if (emoteId >= 0 && emoteId < StartOfRoundPatcher.allUnlockableEmotes.Count)
                OnUpdateCustomEmote(emoteId, __instance);
            else
                OnUpdateCustomEmote(-1, __instance);
        }


        public static void OnUpdateCustomEmote(int emoteId, PlayerControllerB playerController, PlayerControllerB syncWithPlayer = null)
        {
            if (playerController == localPlayerController && ConfigSettings.disableEmotesForSelf.Value)
                return;

            Plugin.Log("OnUpdateCustomEmote for player: " + playerController.name + ". Emote id: " + emoteId);
            if (emoteId != -1)
            {
                EnablePlayerRigBuilder(playerController, false);
                UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotes[emoteId];
                var animationClip = emote.animationClip;
                float normalizedTime = 0;
                if (syncWithPlayer != null && performingEmotes.ContainsKey(syncWithPlayer))
                {
                    var syncEmote = performingEmotes[syncWithPlayer];
                    if (syncEmote != null && syncEmote.canSyncEmote)
                    {
                        emote = syncEmote;
                        var currentAnimationClip = GetCurrentAnimationClip(syncWithPlayer);
                        if (currentAnimationClip != null && (currentAnimationClip == emote.animationClip || currentAnimationClip == emote.transitionsToClip))
                        {
                            animationClip = currentAnimationClip;
                            normalizedTime = !emote.isPose ? syncWithPlayer.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime % 1 : 0;
                            Plugin.Log("Syncing player emote. PlayerId: " + playerController.actualClientId + " SyncWithPlayerId: " + syncWithPlayer.actualClientId + " Emote: " + emote.emoteName + " OverrideAnimClip: " + animationClip.name + " EmoteTime: " + normalizedTime);
                        }
                    }
                }

                performingEmotes[playerController] = emote;
                playerController.performingEmote = true;
                playerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                SetCurrentAnimationClip(animationClip != null ? animationClip : emote.animationClip, playerController);
                if (normalizedTime != 0 || emote.isPose)
                    playerController.StartCoroutine(PlayEmoteAtTimeDelayed(emote, playerController, overrideClip : animationClip != emote.animationClip ? animationClip : null, normalizedTime: normalizedTime));
                else
                    PlayEmoteAtTime(emote, playerController, overrideClip: animationClip != emote.animationClip ? animationClip : null, normalizedTime: normalizedTime);
            }
            else
            {
                SetCurrentAnimationClip(defaultDance1Clip, playerController);
                performingEmotes[playerController] = null;

                //playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                playerController.playerBodyAnimator.CrossFadeInFixedTime("Dance1", 0.1f);
                EnablePlayerRigBuilder(playerController, true);

                if (playerController == localPlayerController)
                {
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                    if (playerController.serverItemHolder == playerController.currentlyHeldObjectServer?.parentObject)
                        playerController.currentlyHeldObjectServer.parentObject = playerController.localItemHolder;
                }
            }
        }


        static IEnumerator PlayEmoteAtTimeDelayed(UnlockableEmote emote, PlayerControllerB playerController, AnimationClip overrideClip = null, float normalizedTime = 0)
        {
            yield return new WaitForEndOfFrame();
            PlayEmoteAtTime(emote, playerController, overrideClip, normalizedTime);
        }


        public static void PlayEmoteAtTime(UnlockableEmote emote, PlayerControllerB playerController, AnimationClip overrideClip = null, float normalizedTime = 0)
        {
            AnimationClip clip = emote.animationClip;
            if (overrideClip != null)
                clip = overrideClip;

            SetCurrentAnimationClip(clip, playerController);
            if (normalizedTime == 0)
            {
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                //playerController.playerBodyAnimator.CrossFadeInFixedTime("Dance1", 0.1f, 1);
            }
            else
                playerController.playerBodyAnimator.Play("Dance1", 1, normalizedTime);
            //BoomboxMusicPlayer.OnPlayEmoteWithMusic(emote, playerController);

            playerController.playerBodyAnimator.SetLayerWeight(1, 1);
            if (clip == emote.animationClip && emote.transitionsToClip != null && normalizedTime == 0)
                playerController.StartCoroutine(TransitionToLoopEmote(playerController, emote));
            else if (!clip.isLooping && !emote.isPose)
                playerController.StartCoroutine(StopEmoteAfterFinished(playerController, emote));

            if (playerController == localPlayerController)
            {
                ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                if (playerController.localItemHolder == playerController.currentlyHeldObjectServer?.parentObject)
                    playerController.currentlyHeldObjectServer.parentObject = playerController.serverItemHolder;
            }
        }


        public static void EnablePlayerRigBuilder(PlayerControllerB playerController, bool enabled = true)
        {
            IEnumerator EnableRigBuilder()
            {
                var currentAnimationClip = GetCurrentAnimationClip(playerController);
                var currentEmoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");

                SetCurrentAnimationClip(Plugin.idleClip, playerController);
                playerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                yield return new WaitForEndOfFrame();

                SetCurrentAnimationClip(currentAnimationClip, playerController);
                playerController.playerBodyAnimator.SetInteger("emoteNumber", currentEmoteNumber);
                if (currentEmoteNumber <= 0)
                    playerController.playerBodyAnimator.Play("Dance1", 0, 0);
                else
                    playerController.playerBodyAnimator.Play("Dance1", 0, 0);

                playerController.GetComponentInChildren<RigBuilder>().enabled = enabled;
            }

            if (enabled)
                playerController.StartCoroutine(EnableRigBuilder());
            else
                playerController.GetComponentInChildren<RigBuilder>().enabled = enabled;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void CheckIfStopPerformingEmoteDirty(PlayerControllerB __instance)
        {
            if (performingEmotes[__instance] != null)
            {
                if (!IsCurrentlyPlayingCustomEmote(__instance) && !__instance.performingEmote)
                    OnUpdateCustomEmote(-1, __instance);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        public static void CheckIfLookingAtPlayerSyncableEmote(PlayerControllerB __instance)
        {
            if (__instance == localPlayerController && !ConfigSettings.disableEmotesForSelf.Value)
            {
                lookingAtPlayerSyncableEmote = null;
                lookingAtMaskedEnemySyncableEmote = null;
                if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                    localPlayerController.cursorTip.text = "";
                if (Physics.Raycast(localPlayerController.gameplayCamera.transform.position + localPlayerController.gameplayCamera.transform.forward * 0.5f, localPlayerController.gameplayCamera.transform.forward * 4.5f, out var hit, 4.5f, syncableEmoteLayerMask) && performingCustomEmoteLocal == null && !__instance.isPlayerDead)
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
                        if (performingEmotes.TryGetValue(hitPlayer, out var performingEmote) && performingEmote != null && performingEmote.canSyncEmote && (StartOfRoundPatcher.unlockedEmotes.Contains(performingEmote) || ConfigSync.instance.syncSyncUnsharedEmotes))
                        {
                            lookingAtPlayerSyncableEmote = hitPlayer;
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
            if (__instance == localPlayerController && !ConfigSettings.disableEmotesForSelf.Value && context.performed && !__instance.isPlayerDead && (lookingAtPlayerSyncableEmote != null || lookingAtMaskedEnemySyncableEmote != null) && __instance.cursorTip.text.Contains("Sync emote"))
            {
                UnlockableEmote emote = null;
                if (lookingAtPlayerSyncableEmote != null && performingEmotes.TryGetValue(lookingAtPlayerSyncableEmote, out emote))
                {
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


        public static void SetCurrentAnimationClip(AnimationClip clip, PlayerControllerB playerController) {
            if (!(playerController.playerBodyAnimator.runtimeAnimatorController is AnimatorOverrideController))
                playerController.playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(playerController.playerBodyAnimator.runtimeAnimatorController);
            ((AnimatorOverrideController)playerController.playerBodyAnimator.runtimeAnimatorController)["Dance1"] = clip;
        }


        public static AnimationClip GetCurrentAnimationClip(PlayerControllerB playerController) {
            if (!(playerController.playerBodyAnimator.runtimeAnimatorController is AnimatorOverrideController))
                playerController.playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(playerController.playerBodyAnimator.runtimeAnimatorController);
            return ((AnimatorOverrideController)playerController.playerBodyAnimator.runtimeAnimatorController)["Dance1"];
        }

        public static UnlockableEmote GetCurrentlyPlayingEmote(PlayerControllerB playerController) {
            AnimationClip animationClip = GetCurrentAnimationClip(playerController);
            if (animationClip != null)
            {
                string clipName = animationClip.name.Replace("_loop", "");
                if (StartOfRoundPatcher.allUnlockableEmotesDict.ContainsKey(clipName))
                {
                    UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotesDict[clipName];
                    return emote;
                }
            }
            return null;
        }


        public static bool IsCurrentlyPlayingCustomEmote(PlayerControllerB playerController) {
            if (playerController.performingEmote)
            {
                int emoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
                if (emoteNumber > 0)
                {
                    AnimationClip currentClip = GetCurrentAnimationClip(playerController);
                    if (Plugin.customAnimationClips.Contains(currentClip) || Plugin.customAnimationClipsLoopDict.ContainsValue(currentClip))
                        return true;
                }
            }
            return false;
        }


        static IEnumerator TransitionToLoopEmote(PlayerControllerB playerController, UnlockableEmote startEmote)
        {
            AnimationClip loopEmote = startEmote.transitionsToClip;
            yield return new WaitForSeconds(startEmote.animationClip.length);
            AnimationClip currentClip = GetCurrentAnimationClip(playerController);
            int emoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
            if (currentClip == startEmote.animationClip && (playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 0.9f || playerController != localPlayerController))
            {
                SetCurrentAnimationClip(loopEmote, playerController);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
            }
        }


        static IEnumerator StopEmoteAfterFinished(PlayerControllerB playerController, UnlockableEmote emote)
        {
            yield return new WaitForSeconds(emote.animationClip.length);
            AnimationClip currentClip = GetCurrentAnimationClip(playerController);
            int emoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
            if (currentClip == emote.animationClip && (playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 0.9f || playerController != localPlayerController))
            {
                playerController.performingEmote = false;
                if (playerController.IsOwner)
                    playerController.StopPerformingEmoteServerRpc();
            }
        }
    }
}