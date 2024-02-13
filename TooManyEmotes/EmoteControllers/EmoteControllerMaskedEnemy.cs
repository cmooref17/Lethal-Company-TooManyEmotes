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
        public UnlockableEmote pendingEmote = null;
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


        protected override void Awake()
        {
            base.Awake();
            if (!initialized)
                return;

            try
            {
                maskedEnemy = GetComponentInParent<MaskedPlayerEnemy>();
                if (maskedEnemy == null)
                {
                    Plugin.LogError("Failed to find MaskedPlayerEnemy component in parent of EmoteControllerMaskedEnemy.");
                    return;
                }
                allMaskedEnemyEmoteControllers.Add(maskedEnemy, this);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to initialize EmoteControllerMaskedEnemy. Error: " + e);
            }
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

            return (NetworkManager.Singleton.IsServer && (agent.speed > 0 || stopAndStareTimer <= 0)) || (!NetworkManager.Singleton.IsServer && Vector3.Distance(emotedAtPosition, maskedEnemy.transform.position) > 0.01f) || inKillAnimation;
        }


        public override bool IsPerformingCustomEmote() => base.IsPerformingCustomEmote();


        public override bool CanPerformEmote()
        {
            bool canPerformEmote = base.CanPerformEmote();
            canPerformEmote &= lookingAtPlayer != null && (!NetworkManager.Singleton.IsServer || stopAndStareTimer >= 2) && !inKillAnimation && ((NetworkManager.Singleton.IsServer && agent.speed == 0) || (!NetworkManager.Singleton.IsServer && !isMoving));
            return canPerformEmote;
        }


        public override void PerformEmote(UnlockableEmote emote, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
            base.PerformEmote(emote, overrideAnimationClip, playAtTimeNormalized);
            if (isPerformingEmote)
                emotedAtPosition = maskedEnemy.transform.position;
        }


        public override void StopPerformingEmote()
        {
            base.StopPerformingEmote();
            stoppedAndStaring = false;
            pendingEmote = null;
        }


        public override ulong GetEmoteControllerId() => maskedEnemy != null ? maskedEnemy.NetworkObjectId : 0;
    }
}
