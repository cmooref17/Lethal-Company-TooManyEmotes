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
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public class EmoteControllerMaskedEnemy : EmoteController
    {
        public static Dictionary<MaskedPlayerEnemy, EmoteControllerMaskedEnemy> allMaskedEnemyEmoteControllers = new Dictionary<MaskedPlayerEnemy, EmoteControllerMaskedEnemy>();
        public MaskedPlayerEnemy maskedEnemy;

        public int id { get { return (int)maskedEnemy.NetworkObjectId; } }
        public int emoteCount = 0;
        public bool stoppedAndStaring = false;
        public bool behaviour1 = false;

        public float stopAndStareTimer { get { return (float)Traverse.Create(maskedEnemy).Field("stopAndStareTimer").GetValue(); } set { Traverse.Create(maskedEnemy).Field("stopAndStareTimer").SetValue(value); } }
        public NavMeshAgent agent { get { return maskedEnemy.agent; } }
        public PlayerControllerB lookingAtPlayer { get { return maskedEnemy.stareAtTransform?.GetComponentInParent<PlayerControllerB>(); } }
        public bool inKillAnimation { get { return (bool)Traverse.Create(maskedEnemy).Field("inKillAnimation").GetValue(); } }
        public bool handsOut { get { return (bool)Traverse.Create(maskedEnemy).Field("handsOut").GetValue(); } }
        public float localSpeed { get { return ((Vector3)Traverse.Create(maskedEnemy).Field("agentLocalVelocity").GetValue()).magnitude; } }
        public bool isMoving { get { return animator.GetBool("IsMoving"); } }

        public Vector3 emotedAtPosition;


        public override void Initialize(string sourceRootBoneName = "metarig")
        {
            base.Initialize();
            if (!initialized)
                return;

            maskedEnemy = GetComponentInParent<MaskedPlayerEnemy>();
            if (maskedEnemy == null)
            {
                Plugin.LogError("Failed to find MaskedPlayerEnemy component in parent of EmoteControllerMaskedEnemy.");
                return;
            }
            allMaskedEnemyEmoteControllers.Add(maskedEnemy, this);
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            allMaskedEnemyEmoteControllers?.Remove(maskedEnemy);
        }


        protected override bool CheckIfShouldStopEmoting()
        {
            if (!isPerformingEmote)
                return false;

            if (base.CheckIfShouldStopEmoting())
                return true;

            return maskedEnemy.isEnemyDead || (NetworkManager.Singleton.IsServer && (agent.speed > 0 || stopAndStareTimer <= 0)) || (!NetworkManager.Singleton.IsServer && Vector3.Distance(emotedAtPosition, maskedEnemy.transform.position) > 0.01f) || inKillAnimation;
        }


        public override bool IsPerformingCustomEmote() => base.IsPerformingCustomEmote();


        public override bool CanPerformEmote()
        {
            bool canPerformEmote = base.CanPerformEmote();

            canPerformEmote = canPerformEmote && lookingAtPlayer != null && (!NetworkManager.Singleton.IsServer || stopAndStareTimer >= 2) && !inKillAnimation && ((NetworkManager.Singleton.IsServer && agent.speed == 0) || (!NetworkManager.Singleton.IsServer && !isMoving)) && !maskedEnemy.isEnemyDead;
            return canPerformEmote;
        }


        public override bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1)
        {
            bool success = base.PerformEmote(emote);
            if (isPerformingEmote)
            {
                emoteCount++;
                emotedAtPosition = maskedEnemy.transform.position;
            }
            return success;
        }


        public override void StopPerformingEmote()
        {
            base.StopPerformingEmote();
            stoppedAndStaring = false;
        }


        protected override void CreateBoneMap()
        {
            boneMap = BoneMapper.CreateBoneMap(humanoidSkeleton, metarig, EmoteControllerPlayer.sourceBoneNames);
        }

        protected override ulong GetEmoteControllerId() => maskedEnemy != null ? maskedEnemy.NetworkObjectId : 0;
        protected override string GetEmoteControllerName() => maskedEnemy != null ? maskedEnemy.name : base.GetEmoteControllerName();
    }
}