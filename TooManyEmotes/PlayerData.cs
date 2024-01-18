using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace TooManyEmotes
{
    public class PlayerData
    {
        public PlayerControllerB playerController;
        public bool isLocalPlayer { get { return playerController == StartOfRound.Instance?.localPlayerController; } }
        public string name { get { return playerController.name; } }
        public ulong clientId { get { return playerController.actualClientId; } }
        public ulong playerId { get { return playerController.playerClientId; } }
        public ulong steamId { get { return playerController.playerSteamId; } }
        public string username { get { return playerController.playerUsername; } }

        public Animator animator { get { return playerController.playerBodyAnimator; } }
        public AnimatorOverrideController animatorController { get { return animator.runtimeAnimatorController as AnimatorOverrideController; } set { animator.runtimeAnimatorController = value; } }
        public bool animatorControllerIsOverride { get { return animator.runtimeAnimatorController != null && animator.runtimeAnimatorController is AnimatorOverrideController; } }
        public RigBuilder rigBuilder { get { return playerController.GetComponentInChildren<RigBuilder>(); } }


        public int currentEmoteNumber { get { return animator.GetInteger("emoteNumber"); } set { animator.SetInteger("emoteNumber", value); } }
        public bool isPerformingEmote { get { return performingEmote != null; } }
        public UnlockableEmote performingEmote;

        public float normalizedTimeAnimation { get { return animator.GetCurrentAnimatorStateInfo(1).normalizedTime; } }



        public void ConvertAnimatorControllerToOverride()
        {
            Plugin.LogWarning("Converting animator controller to override for player: " + playerController.name);
            if (animator != null && !(animator.runtimeAnimatorController is AnimatorOverrideController))
                animator.runtimeAnimatorController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        }


        public void EnableRigBuilder(bool enable)
        {
            IEnumerator EnableRigBuilder()
            {
                TryGetCurrentAnimationClip(out var currentAnimationClip);
                var currentEmoteNumber = playerController.playerBodyAnimator.GetInteger("emoteNumber");
                int prevState = playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).shortNameHash;
                float normalizedTime = playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime;

                SetCurrentAnimationClip(Plugin.idleClip);
                playerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
                yield return new WaitForEndOfFrame();

                SetCurrentAnimationClip(currentAnimationClip);
                playerController.playerBodyAnimator.SetInteger("emoteNumber", currentEmoteNumber);
                var currentState = playerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(1).shortNameHash;
                if (currentState != prevState)
                    playerController.playerBodyAnimator.CrossFadeInFixedTime(prevState, 0.1f, 1);
                else
                    playerController.playerBodyAnimator.Play(prevState, 1, normalizedTime);

                rigBuilder.enabled = enable;
            }

            Plugin.Log((enable ? "Enabling " : "Disabling ") + "rigbuilder for player with id: " + playerController.actualClientId);

            if (enable)
                playerController.StartCoroutine(EnableRigBuilder());
            else
                rigBuilder.enabled = enable;
        }

        public bool TryGetCurrentAnimationClip(out AnimationClip animationClip, string stateName = "Dance1")
        {
            animationClip = null;
            try 
            {
                if (!(animator.runtimeAnimatorController is AnimatorOverrideController))
                    ConvertAnimatorControllerToOverride();
                animationClip = animatorController[stateName];
            } catch { }
            return animationClip != null;
        }


        public void SetCurrentAnimationClip(AnimationClip clip, string stateName = "Dance1")
        {
            if (!(animator.runtimeAnimatorController is AnimatorOverrideController))
                ConvertAnimatorControllerToOverride();
            animatorController[stateName] = clip;
        }


        public bool IsEmoteLoaded() => TryGetLoadedEmote(out var emote);
        public bool TryGetLoadedEmote(out UnlockableEmote emote)
        {
            emote = null;
            if (TryGetCurrentAnimationClip(out var animationClip))
            {
                string clipName = animationClip.name.Replace("_loop", "").Replace("_start", "").Replace("_pose", "");
                if (StartOfRoundPatcher.allUnlockableEmotesDict.TryGetValue(clipName, out var currentEmote))
                    emote = currentEmote;
            }
            return emote != null;
        }


        public void PlayEmoteAtTime(UnlockableEmote emote, AnimationClip overrideClip = null, float normalizedTime = 0, bool playEmoteEndOfFrame = false)
        {
            IEnumerator PlayEmoteEndOfFrame()
            {
                yield return new WaitForEndOfFrame();
                PlayEmoteAtTime(emote, overrideClip, normalizedTime);
            }

            if (playEmoteEndOfFrame)
            {
                playerController.StartCoroutine(PlayEmoteEndOfFrame());
                return;
            }

            AnimationClip clip = overrideClip != null ? overrideClip : emote.animationClip;
            SetCurrentAnimationClip(clip);
            playerController.playerBodyAnimator.Play("Dance1", 1, normalizedTime);

            if (clip == emote.animationClip && emote.transitionsToClip != null)
                playerController.StartCoroutine(TransitionToLoopEmote(emote));
            else if (!clip.isLooping && !emote.isPose)
                playerController.StartCoroutine(StopEmoteAfterFinished(emote));

            if (isLocalPlayer)
            {
                ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                if (playerController.localItemHolder == playerController.currentlyHeldObjectServer?.parentObject)
                    playerController.currentlyHeldObjectServer.parentObject = playerController.serverItemHolder;
            }
        }


        IEnumerator TransitionToLoopEmote(UnlockableEmote emote)
        {
            yield return new WaitForSeconds(emote.animationClip.length);
            if (TryGetCurrentAnimationClip(out var currentAnimationClip) && currentAnimationClip == emote.animationClip && (normalizedTimeAnimation >= 0.9f || !isLocalPlayer))
            {
                SetCurrentAnimationClip(emote.transitionsToClip != null ? emote.transitionsToClip : PlayerPatcher.defaultDance1Clip);
                playerController.playerBodyAnimator.Play("Dance1", 1, 0);
            }
        }


        IEnumerator StopEmoteAfterFinished(UnlockableEmote emote)
        {
            yield return new WaitForSeconds(emote.animationClip.length);
            if (TryGetCurrentAnimationClip(out var currentAnimationClip) && currentAnimationClip == emote.animationClip && (normalizedTimeAnimation >= 0.9f || !isLocalPlayer))
            {
                playerController.performingEmote = false;
                if (playerController.IsOwner)
                    playerController.StopPerformingEmoteServerRpc();
            }
        }


        public PlayerData(PlayerControllerB playerController)
        {
            this.playerController = playerController;
        }
    }
}
