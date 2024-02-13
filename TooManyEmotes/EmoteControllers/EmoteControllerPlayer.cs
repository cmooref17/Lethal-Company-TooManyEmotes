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
using TooManyEmotes.Networking;
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
            if (!initialized || playerController == null || (playerController == localPlayerController && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return;
            base.Update();
        }


        protected override void LateUpdate()
        {
            if (!initialized || playerController == null || (playerController == localPlayerController && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return;

            bool isEmoting = isPerformingEmote;
            base.LateUpdate();
            
            if (isEmoting && !isPerformingEmote && playerController.performingEmote)
            {
                playerController.performingEmote = false;
                originalAnimator.SetInteger("emoteNumber", 0);
                var currentStateInfo = originalAnimator.GetCurrentAnimatorStateInfo(0);
                animator.Play(currentStateInfo.fullPathHash, 0, 0);
                if (isLocalPlayer)
                {
                    timeSinceStartingEmote = 0f;
                    playerController.StopPerformingEmoteServerRpc();
                }
            }
        }


        protected override void TranslateAnimation()
        {
            if (!initialized || playerController == null || !playerController.performingEmote)
                return;
            base.TranslateAnimation();
        }


        protected override bool CheckIfShouldStopEmoting()
        {
            if (playerController == null || !isPerformingEmote)
                return false;
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

            //ForceSendAnimationUpdateLocal(emote);
            PerformEmote(emote);
            playerController.StartPerformingEmoteServerRpc();
            SyncPerformingEmoteManager.SendPerformingEmoteUpdateToServer(emote);
            timeSinceStartingEmote = 0;
            playerController.performingEmote = true;
            originalAnimator.SetInteger("emoteNumber", 1);
        }


        public void TrySyncingEmoteWithEmoteController(EmoteController emoteController)
        {
            if (!initialized || emoteController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return;

            if (!isLocalPlayer)
            {
                Plugin.LogWarning("Cannot run TrySyncingEmoteWithEmoteController on a character who does not belong to the local player. This is not allowed.");
                return;
            }

            Plugin.Log("Attempting to sync emote for player: " + playerController.name + " with emote controller with id: " + emoteController.GetEmoteControllerId());

            if (!CanPerformEmote() || !emoteController.IsPerformingCustomEmote())
                return;

            //ForceSendAnimationUpdateLocal(emote, syncWithPlayer);
            SyncWithEmoteController(emoteController);
            playerController.StartPerformingEmoteServerRpc();
            SyncPerformingEmoteManager.SendSyncEmoteUpdateToServer(emoteController);
            timeSinceStartingEmote = 0;
            playerController.performingEmote = true;
            originalAnimator.SetInteger("emoteNumber", 1);
        }


        public override bool CanPerformEmote()
        {
            if (!isLocalPlayer)
                return true;

            if (!initialized)
                Debug.LogError("CanPerformEmote: NOT INITIALIZED");

            if (ConfigSettings.disableEmotesForSelf.Value)
                Debug.LogError("CanPerformEmote: EMOTING FOR SELF DISABLED");

            if (LCVR_Patcher.Enabled)
                Debug.LogError("CanPerformEmote: LCVR ENABLED");

            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return false;

            bool canPerformEmote = base.CanPerformEmote();

            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            canPerformEmote &= (bool)method.Invoke(playerController, new object[] { });

            bool otherConditions = playerController.inAnimationWithEnemy == null && !(isLocalPlayer && CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
            return canPerformEmote && otherConditions;
        }


        public override void PerformEmote(UnlockableEmote emote, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
            if (playerController == null)
                Plugin.LogError("PLAYERCONTROLLER NULL IN PERFORMEMOTE");
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return;

            base.PerformEmote(emote);
            if (isPerformingEmote)
            {
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 0);
                if (isLocalPlayer)
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
            }
        }


        public override void StopPerformingEmote()
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return;

            base.StopPerformingEmote();
            if (isLocalPlayer)
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
        }


        public override ulong GetEmoteControllerId() => playerController != null ? playerController.NetworkObjectId : 0;
    }
}
