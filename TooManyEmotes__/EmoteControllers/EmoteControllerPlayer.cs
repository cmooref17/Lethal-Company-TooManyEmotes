using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace TooManyEmotes
{
    public class EmoteControllerPlayer : EmoteController
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static Dictionary<PlayerControllerB, EmoteControllerPlayer> allPlayerEmoteControllers = new Dictionary<PlayerControllerB, EmoteControllerPlayer>();
        public static EmoteControllerPlayer emoteControllerLocal { get { return localPlayerController != null && allPlayerEmoteControllers.ContainsKey(localPlayerController) ? allPlayerEmoteControllers[localPlayerController] : null; } }
        public static int emoteStateHash { get { return localPlayerController != null ? Animator.StringToHash(localPlayerController.playerBodyAnimator.GetLayerName(1) + ".Dance1") : -1; } }

        public PlayerControllerB playerController;
        public bool isLocalPlayer { get { return playerController == StartOfRound.Instance?.localPlayerController; } }
        public ulong clientId { get { return playerController.actualClientId; } }
        public ulong playerId { get { return playerController.playerClientId; } }
        public ulong steamId { get { return playerController.playerSteamId; } }
        public string username { get { return playerController.playerUsername; } }

        public float timeSinceStartingEmote { get { return (float)Traverse.Create(playerController).Field("timeSinceStartingEmote").GetValue(); } set { Traverse.Create(playerController).Field("timeSinceStartingEmote").SetValue(value); } }


        protected override void Awake()
        {
            base.Awake();
            if (!initialized)
                return;

            try
            {
                playerController = GetComponentInParent<PlayerControllerB>();
                if (playerController == null)
                {
                    Plugin.LogError("Failed to find PlayerControllerB component in parent of EmoteControllerPlayer.");
                    return;
                }
                allPlayerEmoteControllers.Add(playerController, this);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to initialize EmoteControllerPlayer: " + playerController.name + ". Error: " + e);
            }
        }


        protected override void Start()
        {
            base.Start();
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            allPlayerEmoteControllers?.Remove(playerController);
        }


        protected override void AddGroundContactPoints()
        {
            base.AddGroundContactPoints();
        }


        protected override void Update()
        {
            base.Update();
            if (playerController == null)
                return;
        }


        protected override void LateUpdate()
        {
            if (playerController == null)
                return;

            bool emoting = isPerformingEmote;
            base.LateUpdate();
            
            if (emoting && !isPerformingEmote && playerController.performingEmote)
            {
                playerController.performingEmote = false;
                originalAnimator.SetInteger("emoteNumber", 0);
                if (isLocalPlayer)
                {
                    timeSinceStartingEmote = 0f;
                    playerController.StopPerformingEmoteServerRpc();
                }
            }
        }


        protected override void TranslateAnimation()
        {
            if (playerController == null || !playerController.performingEmote)
                return;
            base.TranslateAnimation();
        }


        protected override bool CheckIfShouldStopEmoting()
        {
            Plugin.Log("111");
            if (playerController == null || !isPerformingEmote)
            {
                Plugin.Log("222");
                return false;
            }

            Plugin.Log("333 " + playerController.performingEmote + " " + (performingEmote == null));
            return base.CheckIfShouldStopEmoting() || !playerController.performingEmote || performingEmote == null;
        }


        public override bool IsPerformingCustomEmote()
        {
            return base.IsPerformingCustomEmote() && playerController.performingEmote;
        }


        public void TryPerformingEmoteLocal(UnlockableEmote emote)
        {
            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return;
            if (!isLocalPlayer)
            {
                Plugin.LogWarning("Cannot run TryPerformEmoteLocal on a character who does not belong to the local player. This is not allowed.");
                return;
            }

            Plugin.Log("Attempting to emote for player: " + playerController.name);

            if (!CanPerformEmote())
                return;

            if (emote.randomEmotePool != null && emote.randomEmotePool.Count > 0)
                emote = emote.randomEmotePool[UnityEngine.Random.Range(0, emote.randomEmotePool.Count)];

            timeSinceStartingEmote = 0;
            ForceSendAnimationUpdateLocal(emote);
            PerformEmote(emote);
        }


        public void TrySyncingEmoteWithEmoteController(EmoteController emoteController)
        {
            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return;
            if (!isLocalPlayer)
            {
                Plugin.LogWarning("Cannot run TrySyncingEmoteWithEmoteController on a character who does not belong to the local player. This is not allowed.");
                return;
            }

            if (!CanPerformEmote() || emoteController == null || !emoteController.IsPerformingCustomEmote())
                return;

            var emote = emoteController.performingEmote;
            var overrideAnimationClip = emoteController.GetCurrentAnimationClip();
            var playAtTime = emoteController.normalizedTimeAnimation;

            PlayerControllerB syncWithPlayer = null;
            EmoteControllerPlayer playerEmoteController = emoteController as EmoteControllerPlayer;
            if (playerEmoteController != null)
                syncWithPlayer = playerEmoteController.playerController;

            timeSinceStartingEmote = 0;
            ForceSendAnimationUpdateLocal(emote, syncWithPlayer);
            PerformEmote(emote, overrideAnimationClip, playAtTime);
        }


        public override bool CanPerformEmote()
        {
            if (!isLocalPlayer)
                return true;

            bool canPerformEmote = base.CanPerformEmote();

            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            canPerformEmote &= (bool)method.Invoke(playerController, new object[] { });

            bool otherConditions = playerController.inAnimationWithEnemy == null && !(isLocalPlayer && CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
            return canPerformEmote && otherConditions;
        }


        void ForceSendAnimationUpdateLocal(UnlockableEmote emote, PlayerControllerB syncWithPlayer = null)
        {
            if (!isLocalPlayer)
                return;

            int emoteId = emote.emoteId;
            Traverse.Create(playerController).Field("updatePlayerAnimationsInterval").SetValue(0);
            List<int> previousAnimationStateHash = (List<int>)Traverse.Create(localPlayerController).Field("previousAnimationStateHash").GetValue();
            List<int> currentAnimationStateHash = (List<int>)Traverse.Create(localPlayerController).Field("currentAnimationStateHash").GetValue();
            previousAnimationStateHash[1] = emoteStateHash;
            currentAnimationStateHash[1] = emoteStateHash;

            float animationSpeed = 1;
            if (emoteId != -1)
                animationSpeed += (emoteId + 1) / 10000f;
            if (syncWithPlayer != null && syncWithPlayer != playerController)
            {
                float appendValue = (syncWithPlayer.playerClientId + 1) / 1000000f;
                animationSpeed += appendValue;
            }

            Traverse.Create(localPlayerController).Field("previousAnimationSpeed").SetValue(1);

            localPlayerController.StartPerformingEmoteServerRpc();
            Plugin.LogWarning("ForceSendingAnimation. Anim speed: " + animationSpeed);
            MethodInfo method = localPlayerController.GetType().GetMethod("UpdatePlayerAnimationServerRpc", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(localPlayerController, new object[] { emoteStateHash, animationSpeed });
        }


        public override void PerformEmote(UnlockableEmote emote, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
            base.PerformEmote(emote);
            if (isPerformingEmote)
            {
                playerController.performingEmote = true;
                if (isLocalPlayer)
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
            }
        }


        public override void StopPerformingEmote()
        {
            base.StopPerformingEmote();
            if (isLocalPlayer)
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
        }


        public override ulong GetEmoteControllerId() => playerController != null ? playerController.NetworkObjectId : 0;
    }
}
