using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Networking;
using Unity.Collections;
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


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(StartOfRound __instance)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnMaskedEnemyEmoteClientRpc", OnMaskedEnemyEmoteClientRpc);
            }
        }


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
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting || __instance.isEnemyDead || !spawnedMaskedEnemyData.TryGetValue(__instance, out var maskedEnemyData))
                return;

            if (NetworkManager.Singleton.IsServer && CanPerformEmote(maskedEnemyData) && !maskedEnemyData.stoppedAndStaring)
            {
                maskedEnemyData.stoppedAndStaring = true;
                if (!CalculateShouldEmoteChance(maskedEnemyData))
                {
                    maskedEnemyData.emoteCount++;
                    return;
                }

                playersEmotedWithThisRound.Add(maskedEnemyData.lookingAtPlayer);
                var emote = GetRandomUnlockedEmote(maskedEnemyData);
                maskedEnemyData.pendingEmote = emote;

                float delay = GetRandomEmoteDelay(maskedEnemyData);
                float duration = GetRandomEmoteDuration(maskedEnemyData);
                Plugin.LogWarning("Pre-performing emote on MaskedEnemy. Delay: " + delay + " ExtendedStopAndStareDuration: " + duration);
                maskedEnemyData.stopAndStareTimer = maskedEnemyData.stopAndStareTimer + duration;

                PerformEmoteAfterDelay(emote, maskedEnemyData, delay);
            }
            else if ((NetworkManager.Singleton.IsServer && (maskedEnemyData.agent.speed > 0 || maskedEnemyData.stopAndStareTimer <= 0)) || (!NetworkManager.Singleton.IsServer && maskedEnemyData.isMoving) || maskedEnemyData.inKillAnimation)
            {
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


        public static UnlockableEmote GetRandomUnlockedEmote(MaskedEnemyData maskedEnemyData)
        {
            var playerController = maskedEnemyData.maskedEnemy.mimickingPlayer;
            if (playerController == null)
                playerController = maskedEnemyData.lookingAtPlayer;
            if (playerController == null)
                return null;

            var emotesList = StartOfRoundPatcher.unlockedEmotes;
            if (!ConfigSync.instance.syncShareEverything && playerController != StartOfRound.Instance.localPlayerController)
                StartOfRoundPatcher.unlockedEmotesByPlayer.TryGetValue(playerController.playerUsername, out emotesList);
            if (emotesList == null)
                emotesList = StartOfRoundPatcher.unlockedEmotes;


            var random = new System.Random(currentLevelSeed + 100 * maskedEnemyData.id + maskedEnemyData.emoteCount);

            List<UnlockableEmote> emotePool = new List<UnlockableEmote>();
            foreach (var emote in emotesList)
            {
                if (emote.canSyncEmote)
                    emotePool.Add(emote);
            }
            if (emotePool.Count <= 0)
                emotePool = emotesList;

            int index = random.Next(emotePool.Count);
            return emotePool[index];
        }


        private static void SendUpdateMaskedEnemyEmoteToClients(MaskedEnemyData maskedEnemyData, int emoteId)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(maskedEnemyData.maskedEnemy.NetworkObjectId);
            writer.WriteValueSafe(emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnMaskedEnemyEmoteClientRpc", writer);
        }


        private static void OnMaskedEnemyEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            ulong maskedEnemyNetworkId;
            int emoteId;
            reader.ReadValue(out maskedEnemyNetworkId);
            reader.ReadValue(out emoteId);

            Plugin.Log("Receiving update for masked enemy emote from server. Masked enemy id: " + maskedEnemyNetworkId + " EmoteId: " + emoteId);
            foreach (var maskedEnemyData in spawnedMaskedEnemyData.Values)
            {
                if (maskedEnemyData.maskedEnemy.NetworkObjectId == maskedEnemyNetworkId)
                {
                    TryPerformEmote(maskedEnemyData, emoteId);
                    return;
                }
            }
            Plugin.LogError("Failed to find masked enemy with id: " + maskedEnemyNetworkId);
        }


        public static bool CanPerformEmote(MaskedEnemyData maskedEnemyData)
        {
            if (maskedEnemyData.lookingAtPlayer != null && (!NetworkManager.Singleton.IsServer || maskedEnemyData.stopAndStareTimer >= 2) && !maskedEnemyData.inKillAnimation && ((NetworkManager.Singleton.IsServer && maskedEnemyData.agent.speed == 0) || (!NetworkManager.Singleton.IsServer && !maskedEnemyData.isMoving)))
            {
                return true;
            }
            return false;
        }


        private static void TryPerformEmote(MaskedEnemyData maskedEnemyData, int emoteId) => TryPerformEmote(maskedEnemyData, StartOfRoundPatcher.allUnlockableEmotes[emoteId]);
        private static void TryPerformEmote(MaskedEnemyData maskedEnemyData, UnlockableEmote emote)
        {
            if (emote != null && CanPerformEmote(maskedEnemyData) && emote != maskedEnemyData.performingEmote)
            {
                Plugin.Log("Performing emote... Emote: " + emote.emoteName);
                maskedEnemyData.performingEmote = emote;
                maskedEnemyData.pendingEmote = null;
                SetCurrentAnimationClip(emote.animationClip, maskedEnemyData);
                maskedEnemyData.animator.Play("Idle", 0);
                EnableMaskedEnemyRigBuilder(false, maskedEnemyData);
                maskedEnemyData.emoteCount++;

                if (emote.transitionsToClip != null)
                    maskedEnemyData.maskedEnemy.StartCoroutine(TransitionToLoopEmote(maskedEnemyData, emote));
                else if (!emote.animationClip.isLooping && !emote.isPose)
                    maskedEnemyData.maskedEnemy.StartCoroutine(StopEmoteAfterFinished(maskedEnemyData, emote));
            }
        }


        public static void PerformEmoteAfterDelay(UnlockableEmote emote, MaskedEnemyData maskedEnemyData, float delay)
        {
            IEnumerator PerformEmote()
            {
                yield return new WaitForSeconds(delay);
                if (CanPerformEmote(maskedEnemyData))
                {
                    TryPerformEmote(maskedEnemyData, emote.emoteId);
                    if (NetworkManager.Singleton.IsServer)
                        SendUpdateMaskedEnemyEmoteToClients(maskedEnemyData, emote.emoteId);
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
            EnableMaskedEnemyRigBuilder(true, maskedEnemyData);
        }


        public static void EnableMaskedEnemyRigBuilder(bool enabled, MaskedEnemyData maskedEnemyData)
        {
            IEnumerator EnableRigBuilder()
            {
                int currentHash = maskedEnemyData.animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                float normalizedTime = maskedEnemyData.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                SetCurrentAnimationClip(Plugin.idleClip, maskedEnemyData);
                maskedEnemyData.animator.Play("Idle", 0, 0);
                yield return new WaitForEndOfFrame();
                if (maskedEnemyData.performingEmote == null)
                {
                    SetCurrentAnimationClip(defaultIdleClip, maskedEnemyData);
                    maskedEnemyData.animator.Play(currentHash, 0, normalizedTime);
                    maskedEnemyData.rigBuilder.enabled = enabled;
                }
            }

            if (enabled)
                maskedEnemyData.maskedEnemy.StartCoroutine(EnableRigBuilder());
            else
                maskedEnemyData.rigBuilder.enabled = enabled;
        }


        public static void SetCurrentAnimationClip(AnimationClip clip, MaskedEnemyData maskedEnemyData, string stateName = "Idle") => maskedEnemyData.animatorController[stateName] = clip;
        public static AnimationClip GetCurrentAnimationClip(MaskedEnemyData maskedEnemyData, string stateName = "Idle") => maskedEnemyData.animatorController[stateName];


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
        public int id { get { return (int)maskedEnemy.NetworkObjectId; } }
        public int emoteCount = 0;
        public UnlockableEmote performingEmote = null;
        public UnlockableEmote pendingEmote = null;
        public bool stoppedAndStaring = false;
        public bool behaviour1 = false;

        public Animator animator { get { return maskedEnemy.creatureAnimator; } }
        public AnimatorOverrideController animatorController { get { return animator.runtimeAnimatorController as AnimatorOverrideController; } }
        public float stopAndStareTimer { get { return (float)Traverse.Create(maskedEnemy).Field("stopAndStareTimer").GetValue(); } set { Traverse.Create(maskedEnemy).Field("stopAndStareTimer").SetValue(value); } }
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
            animator.runtimeAnimatorController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        }
    }
}
