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

namespace TooManyEmotes.Patches {

    [HarmonyPatch]
    public class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static AnimatorOverrideController localPlayerAnimatorOverrideController;
        public static AnimationClip defaultDance1Clip;
        public static int emoteStateHash { get { return Animator.StringToHash(string.Format("{0}.Dance1", localPlayerController.playerBodyAnimator.GetLayerName(1))); } }

        public static Dictionary<PlayerControllerB, UnlockableEmote> performingCustomEmotes = new Dictionary<PlayerControllerB, UnlockableEmote>();
        public static UnlockableEmote performingCustomEmoteLocal { get { return localPlayerController != null ? performingCustomEmotes[localPlayerController] : null; } set { if (localPlayerController == null) return; performingCustomEmotes[localPlayerController] = value; } }

        public static PlayerControllerB lookingAtPlayerSyncableEmote = null;

        public static Transform localItemHolder;
        public static Transform serverItemHolder;

        [HarmonyPatch(typeof(PlayerControllerB), "Awake")]
        [HarmonyPostfix]
        public static void OnPlayerStart(PlayerControllerB __instance)
        {
            if (!performingCustomEmotes.ContainsKey(__instance))
                performingCustomEmotes.Add(__instance, null);
            else
                performingCustomEmotes[__instance] = null;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance) {
            localPlayerAnimatorOverrideController = (AnimatorOverrideController)StartOfRound.Instance.localClientAnimatorController;
            defaultDance1Clip = localPlayerAnimatorOverrideController["Dance1"];
            localItemHolder = __instance.localItemHolder;
            serverItemHolder =  __instance.serverItemHolder;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static bool PerformCustomEmoteLocal(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {

            if (__instance == localPlayerController && (context.performed || emoteID < 0) && CallCheckConditionsForEmote(localPlayerController))
            {
                localPlayerController.performingEmote = true;
                // Performing custom emote
                if (emoteID < 0)
                {
                    localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                    emoteID = Mathf.Abs(emoteID) - 1;
                    var emote = StartOfRoundPatcher.allUnlockableEmotes[emoteID];
                    if (emote != null && StartOfRoundPatcher.unlockedEmotes.Contains(emote))
                    {
                        if (emote.randomEmotePool != null && emote.randomEmotePool.Count >= 1)
                        {
                            int randomIndex = UnityEngine.Random.Range(0, emote.randomEmotePool.Count);
                            var randomEmote = emote.randomEmotePool[randomIndex];
                            if (randomEmote != null)
                                emote = randomEmote;
                        }

                        OnUpdateCustomEmote(emote.emoteId, localPlayerController);
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
            if (__instance != localPlayerController && !__instance.performingEmote && performingCustomEmotes[__instance] != null)
                OnUpdateCustomEmote(-1, __instance);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "UpdatePlayerAnimationClientRpc")]
        [HarmonyPrefix]
        public static void UpdatePlayerAnimationClientRpcPrefix(int animationState, ref float animationSpeed, PlayerControllerB __instance)
        {
            if (__instance == localPlayerController)
                return;

            if ((NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost) && (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() != 2)
                return;

            if (animationState != emoteStateHash)
                return;

            // Let's do some fun logic
            float speed = Mathf.Floor(animationSpeed * 10) / 10;
            float customEmoteId = ((animationSpeed - speed) * 10000) - 1;
            int emoteId = Mathf.RoundToInt(customEmoteId);
            animationSpeed = 1;
            if (customEmoteId >= 0 && customEmoteId < StartOfRoundPatcher.allUnlockableEmotes.Count)
                OnUpdateCustomEmote(emoteId, __instance);
            else
                OnUpdateCustomEmote(-1, __instance);
        }


        public static void OnUpdateCustomEmote(int emoteId, PlayerControllerB playerController)
        {
            Plugin.Log("OnUpdateCustomEmote for player: " + playerController.name + ". Emote id: " + emoteId);
            if (emoteId != -1)
            {
                EnablePlayerRigBuilder(playerController, false);
                UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotes[emoteId];
                SetCurrentAnimationClip(emote.animationClip, playerController);
                performingCustomEmotes[playerController] = emote;
                playerController.performingEmote = true;
                playerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);

                //BoomboxMusicPlayer.OnPlayEmoteWithMusic(emote, playerController);

                if (emote.transitionsToClip != null)
                    playerController.StartCoroutine(TransitionToLoopEmote(playerController, emote));
                else if (!emote.animationClip.isLooping && !emote.isPose)
                    playerController.StartCoroutine(StopEmoteAfterFinished(playerController, emote));
                else if (emote.isPose)
                {
                    //playerController.playerBodyAnimator.SetLayerWeight(1, 0.5f);
                }

                if (playerController == localPlayerController)
                {
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                    if (playerController.localItemHolder == playerController.currentlyHeldObjectServer?.parentObject)
                        playerController.currentlyHeldObjectServer.parentObject = playerController.serverItemHolder;
                }
            }
            else
            {
                EnablePlayerRigBuilder(playerController, true);
                SetCurrentAnimationClip(defaultDance1Clip, playerController);
                performingCustomEmotes[playerController] = null;
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);

                playerController.playerBodyAnimator.SetLayerWeight(1, 1);

                if (playerController == localPlayerController)
                {
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                    if (playerController.serverItemHolder == playerController.currentlyHeldObjectServer?.parentObject)
                        playerController.currentlyHeldObjectServer.parentObject = playerController.localItemHolder;
                }
            }
        }

        public static void EnablePlayerRigBuilder(PlayerControllerB playerController, bool enabled = true) {
            IEnumerator EnablePlayerRigBuilderNextFrame() {
                yield return null;
                playerController.GetComponentInChildren<RigBuilder>().enabled = enabled;
            }
            playerController.StartCoroutine(EnablePlayerRigBuilderNextFrame());
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void OnPlayerUpdate(PlayerControllerB __instance) {
            if (performingCustomEmotes.ContainsKey(__instance) && performingCustomEmotes[__instance] != null && !__instance.performingEmote && !IsCurrentlyPlayingCustomEmote(__instance))
            {
                OnUpdateCustomEmote(-1, __instance);
            }
            /*
            if (__instance == localPlayerController)
            {
                lookingAtPlayerSyncableEmote = null;
                if (localPlayerController.cursorTip.text.Contains("Sync emote"))
                    localPlayerController.cursorTip.text = "";
                if (Physics.Raycast(localPlayerController.gameplayCamera.transform.position, localPlayerController.gameplayCamera.transform.forward * 3f, out var hit, 3, 1 << LayerMask.NameToLayer("Player")) && performingCustomEmoteLocal == null && !__instance.isPlayerDead)
                {
                    PlayerControllerB hitPlayer = hit.collider.gameObject.GetComponentInParent<PlayerControllerB>();
                    if (hitPlayer != null && hitPlayer != localPlayerController)
                    {
                        var performingEmote = performingCustomEmotes[hitPlayer];
                        if (performingEmote != null && performingEmote.canSyncEmote)
                        {
                            lookingAtPlayerSyncableEmote = hitPlayer;
                            localPlayerController.cursorTip.text = "[E] Sync emote";
                        }
                    }
                }
            }
            */
        }

        /*
        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool OnSyncEmoteWithPlayer(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || __instance.isPlayerDead || !context.performed || lookingAtPlayerSyncableEmote == null || !__instance.cursorTip.text.Contains("Sync emote"))
                return true;
            
            var emote = performingCustomEmotes[lookingAtPlayerSyncableEmote];
            if (emote != null)
            {
                __instance.PerformEmote(context, -(emote.emoteId + 1));
            }

            return false;
        }
        */


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


        static IEnumerator TransitionToLoopEmote(PlayerControllerB playerController, UnlockableEmote startEmote) {
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


        static IEnumerator StopEmoteAfterFinished(PlayerControllerB playerController, UnlockableEmote emote) {
            yield return new WaitForSeconds(emote.animationClip.length);
            AnimationClip currentClip = GetCurrentAnimationClip(playerController);
            int emoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
            if (currentClip == emote.animationClip && (playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 0.9f || playerController != localPlayerController))
            {
                playerController.performingEmote = false;
                playerController.StopPerformingEmoteServerRpc();
            }
        }
    }
}