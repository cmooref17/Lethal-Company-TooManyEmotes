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
        public static Dictionary<PlayerControllerB, EmoteControllerPlayer> allPlayerEmoteControllers = new Dictionary<PlayerControllerB, EmoteControllerPlayer>();
        public PlayerControllerB playerController;

        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static EmoteControllerPlayer emoteControllerLocal { get { return localPlayerController != null && allPlayerEmoteControllers.ContainsKey(localPlayerController) ? allPlayerEmoteControllers[localPlayerController] : null; } }

        public bool isLocalPlayer { get { return playerController == StartOfRound.Instance?.localPlayerController; } }
        public ulong clientId { get { return playerController.actualClientId; } }
        public ulong playerId { get { return playerController.playerClientId; } }
        public ulong steamId { get { return playerController.playerSteamId; } }
        public string username { get { return playerController.playerUsername; } }

        public Animator originalAnimator;
        public float timeSinceStartingEmote { get { return (float)Traverse.Create(playerController).Field("timeSinceStartingEmote").GetValue(); } set { Traverse.Create(playerController).Field("timeSinceStartingEmote").SetValue(value); } }


        public static List<string> sourceBoneNames = new List<string>
        {
            "spine", "spine.001", "spine.002", "spine.003", "spine.004", "CameraContainer",
            "shoulder.L", "arm.L_upper", "arm.L_lower", "hand.L", "finger1.L", "finger1.L.001", "finger2.L", "finger2.L.001", "finger3.L", "finger3.L.001", "finger4.L", "finger4.L.001", "finger5.L", "finger5.L.001",
            "shoulder.R", "arm.R_upper", "arm.R_lower", "hand.R", "finger1.R", "finger1.R.001", "finger2.R", "finger2.R.001", "finger3.R", "finger3.R.001", "finger4.R", "finger4.R.001", "finger5.R", "finger5.R.001",
            "thigh.L", "shin.L", "foot.L", "heel.02.L", "toe.L",
            "thigh.R", "shin.R", "foot.R", "heel.02.R", "toe.R"
        };


        public override void Initialize(string sourceRootBoneName = "metarig")
        {
            base.Initialize();
            if (!initialized)
                return;

            originalAnimator = metarig.GetComponentInChildren<Animator>();
            playerController = GetComponentInParent<PlayerControllerB>();
            if (playerController == null)
            {
                Plugin.LogError("Failed to find PlayerControllerB component in parent of EmoteControllerPlayer.");
                return;
            }
            allPlayerEmoteControllers.Add(playerController, this);
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


        /*
        protected override void AddGroundContactPoints()
        {
            base.AddGroundContactPoints();
        }
        */


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
            if (!initialized || playerController == null)
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
            return base.IsPerformingCustomEmote();
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

            if (emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 0)
            {
                if (emote.randomEmote)
                    emote = emote.emoteSyncGroup[UnityEngine.Random.Range(0, emote.emoteSyncGroup.Count)];
                else
                    emote = emote.emoteSyncGroup[0];
            }

            // Playing an emote on top of the current emote will cycle emotes in the emote group
            if (isPerformingEmote && performingEmote.emoteSyncGroup != null && performingEmote.emoteSyncGroup.Contains(emote))
            {
                int overrideEmoteId = performingEmote.emoteSyncGroup.IndexOf(performingEmote);
                if (overrideEmoteId != -1)
                {
                    overrideEmoteId = (overrideEmoteId + 1) % performingEmote.emoteSyncGroup.Count;
                    if (performingEmote.emoteSyncGroup[overrideEmoteId] != null)
                        emote = performingEmote.emoteSyncGroup[overrideEmoteId];
                }
            }

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

            Plugin.Log("Attempting to sync emote for player: " + playerController.name + " with emote controller with id: " + emoteController.emoteControllerId);

            if (!CanPerformEmote() || !emoteController.IsPerformingCustomEmote())
                return;

            /*
            short overrideEmoteId = -1;
            if (emoteController.emoteSyncGroup != null)
            {
                emoteController.emoteSyncGroup.AddToEmoteSyncGroup(this);
                emoteSyncGroup = emoteController.emoteSyncGroup;
                if (!emoteController.performingEmote.randomEmote)
                {
                    overrideEmoteId = (short)emoteSyncGroup.syncGroup.IndexOf(this);
                    if (overrideEmoteId != -1)
                        overrideEmoteId %= (short)emoteController.performingEmote.emoteSyncGroup.Count;
                }
            }
            */
            SyncWithEmoteController(emoteController);
            if (performingEmote != null)
            {
                playerController.StartPerformingEmoteServerRpc();

                short overrideEmoteId = -1;
                if (performingEmote.inEmoteSyncGroup)
                    overrideEmoteId = (short)performingEmote.emoteSyncGroup.IndexOf(performingEmote);
                SyncPerformingEmoteManager.SendSyncEmoteUpdateToServer(emoteController, overrideEmoteId);
                timeSinceStartingEmote = 0;
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 1);
            }
        }


        public override bool CanPerformEmote()
        {
            if (!isLocalPlayer)
                return true;

            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return false;

            bool canPerformEmote = base.CanPerformEmote();

            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            canPerformEmote &= (bool)method.Invoke(playerController, new object[] { });

            bool otherConditions = playerController.inAnimationWithEnemy == null && !(isLocalPlayer && CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
            return canPerformEmote && otherConditions;
        }


        public override bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1)
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return false;

            bool success = base.PerformEmote(emote);
            if (isPerformingEmote)
            {
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 0);
                if (isLocalPlayer)
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
            }
            return success;
        }


        public override bool SyncWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return false;

            bool success = base.SyncWithEmoteController(emoteController, overrideEmoteId);
            if (isPerformingEmote)
            {
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 0);
                if (isLocalPlayer)
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
            }
            return success;
        }


        public override void StopPerformingEmote()
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)))
                return;

            base.StopPerformingEmote();
            if (isLocalPlayer)
                StartCoroutine(StopEmoteCameraEndOfFrame());
        }


        IEnumerator StopEmoteCameraEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!isPerformingEmote)
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
        }


        protected override void CreateBoneMap()
        {
            boneMap = BoneMapper.CreateBoneMap(humanoidSkeleton, metarig, sourceBoneNames);
        }


        protected override ulong GetEmoteControllerId() => playerController != null ? playerController.NetworkObjectId : 0;
        protected override string GetEmoteControllerName() => playerController != null ? playerController.playerUsername : base.GetEmoteControllerName();
    }
}