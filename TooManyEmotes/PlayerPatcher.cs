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
    internal class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static AnimatorOverrideController localPlayerAnimatorOverrideController;
        public static AnimationClip defaultDance1Clip;
        public static int emoteStateHash { get { return Animator.StringToHash(string.Format("{0}.Dance1", localPlayerController.playerBodyAnimator.GetLayerName(1))); } } //Animator.StringToHash("EmotesNoArms.Dance1");

        public static HashSet<PlayerControllerB> performingCustomEmotes;
        public static bool performingCustomEmoteLocal { get { return localPlayerController != null ? performingCustomEmotes.Contains(localPlayerController) : false; } set { if (localPlayerController == null) return; if (value) performingCustomEmotes.Add(localPlayerController); else performingCustomEmotes.Remove(localPlayerController); } }
        static float timeStartedLastEmote;
        public static float timeSinceStartedEmote { get { return Time.time - timeStartedLastEmote; } }


        [HarmonyPatch(typeof(PlayerControllerB), "Awake")]
        [HarmonyPostfix]
        public static void InitializePlayer(PlayerControllerB __instance) {
            performingCustomEmotes = new HashSet<PlayerControllerB>();
            timeStartedLastEmote = 0;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance) {
            localPlayerAnimatorOverrideController = (AnimatorOverrideController)StartOfRound.Instance.localClientAnimatorController;
            defaultDance1Clip = localPlayerAnimatorOverrideController["Dance1"];
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static bool PerformCustomEmoteLocal(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {

            if (__instance == localPlayerController && (context.performed || emoteID < 0) && CallCheckConditionsForEmote(localPlayerController))
            {
                localPlayerController.performingEmote = true;
                localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                // Performing custom emote
                if (emoteID < 0)
                {
                    int emoteIndex = Mathf.Abs(emoteID) - 1;
                    if (emoteIndex >= 0 && emoteIndex < StartOfRoundPatcher.unlockedEmotes.Count)
                    {
                        UnlockableEmote emote = StartOfRoundPatcher.unlockedEmotes[emoteIndex];
                        if (emote != null /* && emote != GetCurrentlyPlayingEmote(localPlayerController) */)
                        {
                            Plugin.Log("Starting custom emote for local player");
                            OnUpdateCustomEmote(emote.emoteId, localPlayerController);
                            ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                            ForceSendAnimationUpdateLocal(emote.emoteId);
                        }
                    }
                }
                // Normal emote
                else if (performingCustomEmoteLocal)
                {
                    Plugin.Log("Stopping custom emote for local player");
                    OnUpdateCustomEmote(-1, localPlayerController);
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                    ForceSendAnimationUpdateLocal(-1);
                }
                else
                    return true;

                return false;
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
                animationSpeed += (emoteId + 1) / 1000f;
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
            if (__instance != localPlayerController && !__instance.performingEmote && performingCustomEmotes.Contains(__instance))
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
            float customEmoteId = ((animationSpeed - speed) * 1000) - 1;
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
                EnablePlayerRigBuilderNextFrame(playerController, false);
                UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotes[emoteId];
                SetCurrentAnimationClip(emote.animationClip, playerController);
                performingCustomEmotes.Add(playerController);
                playerController.performingEmote = true;
                playerController.playerBodyAnimator.SetInteger("emoteNumber", 1);

                playerController.playerBodyAnimator.StopPlayback();
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                playerController.playerBodyAnimator.playbackTime = 0;

                

                if (emote.transitionsToClip != null)
                {
                    playerController.StartCoroutine(TransitionToLoopEmote(playerController, emote));
                }
                else if (!emote.animationClip.isLooping)
                    playerController.StartCoroutine(StopEmoteAfterFinished(playerController, emote));

                if (playerController == localPlayerController)
                {
                    timeStartedLastEmote = Time.time;
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                }
            }
            else
            {
                EnablePlayerRigBuilderNextFrame(playerController, true);
                SetCurrentAnimationClip(defaultDance1Clip, playerController);
                performingCustomEmotes.Remove(playerController);

                //playerController.playerBodyAnimator.StopPlayback();
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                playerController.playerBodyAnimator.playbackTime = 0;


                if (playerController == localPlayerController)
                    ThirdPersonEmoteController.OnStopCustomEmoteLocal();
            }
        }

        public static void EnablePlayerRigBuilderNextFrame(PlayerControllerB playerController, bool enabled = true) {
            IEnumerator EnablePlayerRigBuilderNextFrame() {
                yield return null;
                playerController.GetComponentInChildren<RigBuilder>().enabled = enabled;
            }
            playerController.StartCoroutine(EnablePlayerRigBuilderNextFrame());
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void CheckIfFinishedPerformingEmoteDirty(PlayerControllerB __instance) {
            if (performingCustomEmotes.Contains(__instance) && !__instance.performingEmote && !IsCurrentlyPlayingCustomEmote(__instance))
            {
                OnUpdateCustomEmote(-1, __instance);
            }

            /*
            if (performingCustomEmotes.Contains(__instance))
            {
                UnlockableEmote emote = GetCurrentlyPlayingEmote(__instance);
                if (emote != null && __instance.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime == 1)
                {
                    AnimationClip currentClip = GetCurrentAnimationClip(__instance);
                    if (emote.transitionsToClip != null && currentClip == emote.animationClip)
                    {
                        SetCurrentAnimationClip(emote.transitionsToClip, __instance);
                        __instance.playerBodyAnimator.Play("Dance1", 1, 0);
                    }
                    else if (!currentClip.isLooping)
                    {
                        Plugin.Log("22222 Time: " + __instance.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime);
                        OnUpdateCustomEmote(-1, __instance);
                        __instance.playerBodyAnimator.SetInteger("emoteNumber", 0);
                        __instance.playerBodyAnimator.SetLayerWeight(1, 0);
                        __instance.performingEmote = false;
                    }
                }
            }
            */
        }


        public static UnlockableEmote GetCurrentlyPlayingEmote(PlayerControllerB playerController) {
            AnimationClip animationClip = GetCurrentAnimationClip(playerController);
            if (animationClip != null)
            {
                string clipName = animationClip.name.Replace("_loop", "_start");
                if (StartOfRoundPatcher.allUnlockableEmotesDict.ContainsKey(clipName))
                {
                    UnlockableEmote emote = StartOfRoundPatcher.allUnlockableEmotesDict[clipName];
                    return emote;
                }
            }
            return null;
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
            //playerController.performingEmote && playerController.playerBodyAnimator.GetInteger("emoteNumber") == 1 && (Plugin.customAnimationClips.Contains(GetCurrentAnimationClip(playerController)) || Plugin.customAnimationClipsLoopDict.ContainsValue(GetCurrentAnimationClip(playerController)));
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
            /*
            if (playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 1)
            {
                SetCurrentAnimationClip(loopEmote, playerController);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
            }
            */
            /*
            //yield return new WaitForSeconds(startEmote.animationClip.length);
            int emoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
            //AnimationClip currentClip = GetCurrentAnimationClip(playerController);
            if (emoteNumber > 0 && startEmote.animationClip == currentClip && playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 0.8f)
            {
                SetCurrentAnimationClip(loopEmote, playerController);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
            }
            */
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

            /*
            yield return new WaitForSeconds(emote.animationClip.length);
            if (playerController.playerBodyAnimator.GetInteger("emoteNumber") == 1 && GetCurrentAnimationClip(playerController) == emote.animationClip && playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime == 0.8f)
            {
                playerController.performingEmote = false;
                playerController.StopPerformingEmoteServerRpc();
            }
            */
        }
    }
}