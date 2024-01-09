using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Networking;
using Unity.Netcode;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using static Unity.Collections.AllocatorManager;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyEmotes
    {
        public static Dictionary<MaskedPlayerEnemy, MaskedEnemyData> spawnedMaskedEnemyData = new Dictionary<MaskedPlayerEnemy, MaskedEnemyData>();
        public static int currentLevelSeed { get { return StartOfRound.Instance.randomMapSeed; } }
        public static AnimationClip defaultIdleClip;
        public static HashSet<PlayerControllerB> playersEmotedWithThisRound = new HashSet<PlayerControllerB>();

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void ResetValues(StartOfRound __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            spawnedMaskedEnemyData.Clear();
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Start")]
        [HarmonyPostfix]
        public static void InitMaskedEnemy(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            var maskedEnemyData = new MaskedEnemyData(__instance);
            spawnedMaskedEnemyData.Add(__instance, maskedEnemyData);
            if (defaultIdleClip == null)
                defaultIdleClip = maskedEnemyData.animatorController["Idle"];
        }


        [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
        [HarmonyPrefix]
        public static void OnLoadNewLevel(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            playersEmotedWithThisRound.Clear();
            MaskedEnemyData.currentId = 0;
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "OnDestroy")]
        [HarmonyPrefix]
        public static void OnDestroyMaskedEnemy(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            spawnedMaskedEnemyData.Remove(__instance);
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Update")]
        [HarmonyPostfix]
        public static void OnUpdate(MaskedPlayerEnemy __instance)
        {
            if (!NetworkManager.Singleton.IsServer || !ConfigSync.instance.syncEnableMaskedEnemiesEmoting || __instance.isEnemyDead || !spawnedMaskedEnemyData.TryGetValue(__instance, out var maskedEnemyData))
                return;

            if (maskedEnemyData.lookingAtPlayer != null && maskedEnemyData.stopAndStareTimer >= 2 && !maskedEnemyData.stoppedAndStaring && !maskedEnemyData.inKillAnimation)
            {
                maskedEnemyData.stoppedAndStaring = true;
                if (!CalculateShouldEmoteChance(maskedEnemyData))
                {
                    maskedEnemyData.emoteCount++;
                    return;
                }

                playersEmotedWithThisRound.Add(maskedEnemyData.lookingAtPlayer);
                var emote = GetRandomUnlockedEmote(maskedEnemyData.lookingAtPlayer, maskedEnemyData);
                maskedEnemyData.pendingEmote = emote;

                float delay = GetRandomEmoteDelay(maskedEnemyData);
                float duration = GetRandomEmoteDuration(maskedEnemyData);
                Plugin.LogWarning("Pre-performing emote on MaskedEnemy. Delay: " + delay + " ExtendedStopAndStareDuration: " + duration);
                maskedEnemyData.stopAndStareTimer = maskedEnemyData.stopAndStareTimer + duration;

                PerformEmoteAfterDelay(emote, maskedEnemyData, delay);
            }
            else if (maskedEnemyData.agent.speed > 0 || maskedEnemyData.stopAndStareTimer <= 0 || maskedEnemyData.inKillAnimation)
            {
                if (maskedEnemyData.stoppedAndStaring)
                    maskedEnemyData.stoppedAndStaring = false;
                if (maskedEnemyData.performingEmote != null)
                    StopEmote(maskedEnemyData);
            }
        }


        public static bool CalculateShouldEmoteChance(MaskedEnemyData maskedEnemyData)
        {
            var random = new System.Random(currentLevelSeed + 1550 + 100 * maskedEnemyData.id + maskedEnemyData.emoteCount);
            float value = (float)random.NextDouble();
            return !playersEmotedWithThisRound.Contains(maskedEnemyData.lookingAtPlayer) || value <= ConfigSync.instance.syncMaskedEnemiesEmoteChanceOnEncounter;
        }


        public static float GetRandomEmoteDelay(MaskedEnemyData maskedEnemyData)
        {
            var random = new System.Random(currentLevelSeed - 550 + 100 * maskedEnemyData.id + maskedEnemyData.emoteCount);
            Vector2 range = ConfigSync.syncMaskedEnemyEmoteRandomDelay;
            range = new Vector2(Mathf.Min(Mathf.Abs(range.x), Mathf.Abs(range.y)), Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y)));
            return (float)(random.NextDouble() * (range.y - range.x) + range.x);
        }


        public static float GetRandomEmoteDuration(MaskedEnemyData maskedEnemyData)
        {
            if (!ConfigSync.instance.syncOverrideStopAndStareDuration)
                return 0;
            var random = new System.Random(currentLevelSeed + 550 + 100 * maskedEnemyData.id + maskedEnemyData.emoteCount);
            Vector2 range = ConfigSync.syncMaskedEnemyEmoteRandomDuration;
            range = new Vector2(Mathf.Min(Mathf.Abs(range.x), Mathf.Abs(range.y)), Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y)));
            return (float)random.NextDouble() * (range.y - range.x) + range.x;
        }


        public static UnlockableEmote GetRandomUnlockedEmote(PlayerControllerB playerController, MaskedEnemyData maskedEnemyData)
        {
            var random = new System.Random(currentLevelSeed + 100 * maskedEnemyData.id + maskedEnemyData.emoteCount);

            var unlockedEmotes = StartOfRoundPatcher.unlockedEmotes;
            List<UnlockableEmote> emotePool = new List<UnlockableEmote>();
            foreach (var emote in unlockedEmotes)
            {
                if (emote.canSyncEmote)
                    emotePool.Add(emote);
            }
            if (emotePool.Count <= 0)
                emotePool = unlockedEmotes;

            int index = random.Next(emotePool.Count);
            return emotePool[index];
        }


        public static void PerformEmoteAfterDelay(UnlockableEmote emote, MaskedEnemyData maskedEnemyData, float delay)
        {
            IEnumerator PerformEmote()
            {
                yield return new WaitForSeconds(delay);
                if (maskedEnemyData.lookingAtPlayer != null && maskedEnemyData.stopAndStareTimer > 0)
                {
                    maskedEnemyData.performingEmote = emote;
                    maskedEnemyData.pendingEmote = null;
                    SetCurrentAnimationClip(emote.animationClip, maskedEnemyData);
                    maskedEnemyData.animator.Play("Idle", 0, 0);
                    EnablePlayerRigBuilder(false, maskedEnemyData);
                    maskedEnemyData.emoteCount++;

                    if (emote.transitionsToClip != null)
                        maskedEnemyData.maskedEnemy.StartCoroutine(TransitionToLoopEmote(maskedEnemyData, emote));
                    else if (!emote.animationClip.isLooping && !emote.isPose)
                        maskedEnemyData.maskedEnemy.StartCoroutine(StopEmoteAfterFinished(maskedEnemyData, emote));
                }
            }
            if (emote != null)
            {
                Plugin.LogWarning("Performing emote on masked enemy: " + maskedEnemyData.maskedEnemy.name + " after delay: " + delay + ". Emote: " + emote.emoteName);
                maskedEnemyData.maskedEnemy.StartCoroutine(PerformEmote());
            }
        }


        public static void StopEmote(MaskedEnemyData maskedEnemyData)
        {
            Plugin.LogWarning("Stopping emote on masked enemy: " + maskedEnemyData.maskedEnemy.name);
            maskedEnemyData.performingEmote = null;
            maskedEnemyData.pendingEmote = null;
            SetCurrentAnimationClip(defaultIdleClip, maskedEnemyData);
            maskedEnemyData.animator.Play("Idle", 0, 0);
            EnablePlayerRigBuilder(true, maskedEnemyData);
        }


        public static void EnablePlayerRigBuilder(bool enabled, MaskedEnemyData maskedEnemyData)
        {
            IEnumerator EnableRigBuilder()
            {
                SetCurrentAnimationClip(Plugin.idleClip, maskedEnemyData);
                maskedEnemyData.animator.Play("Idle", 0, 0);
                yield return new WaitForEndOfFrame();
                if (maskedEnemyData.performingEmote == null)
                {
                    SetCurrentAnimationClip(defaultIdleClip, maskedEnemyData);
                    int stateHash = maskedEnemyData.animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                    maskedEnemyData.animator.CrossFadeInFixedTime(stateHash, 0.1f);
                    maskedEnemyData.rigBuilder.enabled = enabled;
                }
            }

            if (enabled)
                maskedEnemyData.maskedEnemy.StartCoroutine(EnableRigBuilder());
            else
                maskedEnemyData.rigBuilder.enabled = enabled;
        }


        public static void SetCurrentAnimationClip(AnimationClip clip, MaskedEnemyData maskedEnemyData) => maskedEnemyData.animatorController["Idle"] = clip;
        public static AnimationClip GetCurrentAnimationClip(MaskedEnemyData maskedEnemyData) => maskedEnemyData.animatorController["Idle"];


        static IEnumerator TransitionToLoopEmote(MaskedEnemyData maskedEnemyData, UnlockableEmote startEmote)
        {
            AnimationClip loopEmote = startEmote.transitionsToClip;
            yield return new WaitForSeconds(startEmote.animationClip.length);
            if (maskedEnemyData.performingEmote != null && !maskedEnemyData.rigBuilder.enabled)
            {
                SetCurrentAnimationClip(loopEmote, maskedEnemyData);
                maskedEnemyData.animator.Play("Idle", 0, 0);
            }
        }


        static IEnumerator StopEmoteAfterFinished(MaskedEnemyData maskedEnemyData, UnlockableEmote emote)
        {
            yield return new WaitForSeconds(emote.animationClip.length);
            if (maskedEnemyData.performingEmote != null && !maskedEnemyData.rigBuilder.enabled)
                StopEmote(maskedEnemyData);
        }
    }


    public class MaskedEnemyData
    {
        public MaskedPlayerEnemy maskedEnemy;
        public static int currentId = 0;
        public int id = 0;
        public int emoteCount = 0;
        public UnlockableEmote performingEmote = null;
        public UnlockableEmote pendingEmote = null;
        public bool stoppedAndStaring = false;
        public bool behaviour1 = false;

        public Animator animator { get { return maskedEnemy.creatureAnimator; } }
        public AnimatorOverrideController animatorController { get { return animator.runtimeAnimatorController as AnimatorOverrideController; } }
        public float stopAndStareTimer { get { return (float)Traverse.Create(maskedEnemy).Field("stopAndStareTimer").GetValue(); } set { Traverse.Create(maskedEnemy).Field("stopAndStareTimer").SetValue(value); } }
        public bool movingTowardsPlayer { get { return (bool)Traverse.Create(maskedEnemy).Field("movingTowardsTargetPlayer").GetValue(); } set { Traverse.Create(maskedEnemy).Field("movingTowardsTargetPlayer").SetValue(value); } }
        public NavMeshAgent agent { get { return maskedEnemy.agent; } }
        public RigBuilder rigBuilder { get { return maskedEnemy.GetComponentInChildren<RigBuilder>(); } }
        public PlayerControllerB lookingAtPlayer { get { return maskedEnemy.stareAtTransform?.GetComponentInParent<PlayerControllerB>(); } }
        public bool inKillAnimation { get { return (bool)Traverse.Create(maskedEnemy).Field("inKillAnimation").GetValue(); } }
        public bool handsOut { get { return (bool)Traverse.Create(maskedEnemy).Field("handsOut").GetValue(); } }
        public float localSpeed { get { return ((Vector3)Traverse.Create(maskedEnemy).Field("agentLocalVelocity").GetValue()).magnitude; } }
        public bool isMoving { get { return animator.GetBool("IsMoving"); } }

        public MaskedEnemyData(MaskedPlayerEnemy maskedEnemy)
        {
            this.maskedEnemy = maskedEnemy;
            id = currentId++;
            animator.runtimeAnimatorController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        }
    }
}
