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
using UnityEngine;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyPatcher
    {
        public static int currentLevelSeed { get { return StartOfRound.Instance.randomMapSeed; } }
        public static AnimationClip defaultIdleClip;
        public static HashSet<PlayerControllerB> playersEmotedWithThisRound = new HashSet<PlayerControllerB>();


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(StartOfRound __instance)
        {
            if (!isServer)
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnMaskedEnemyEmoteClientRpc", OnMaskedEnemyEmoteClientRpc);
        }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void ResetValues(StartOfRound __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Start")]
        [HarmonyPostfix]
        public static void InitMaskedEnemy(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            __instance.gameObject.AddComponent<EmoteControllerMaskedEnemy>();
        }


        [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
        [HarmonyPrefix]
        public static void OnLoadNewLevel()
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting)
                return;
            playersEmotedWithThisRound.Clear();
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Update")]
        [HarmonyPostfix]
        public static void OnUpdate(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSync.instance.syncEnableMaskedEnemiesEmoting || __instance.isEnemyDead || !EmoteControllerMaskedEnemy.allMaskedEnemyEmoteControllers.TryGetValue(__instance, out var emoteController))
                return;

            if (isServer && !emoteController.stoppedAndStaring && emoteController.CanPerformEmote())
            {
                emoteController.stoppedAndStaring = true;
                if (!CalculateShouldEmoteChance(emoteController))
                {
                    emoteController.emoteCount++;
                    return;
                }

                playersEmotedWithThisRound.Add(emoteController.lookingAtPlayer);
                var emote = GetRandomUnlockedEmote(emoteController);

                float delay = GetRandomEmoteDelay(emoteController);
                float duration = GetRandomEmoteDuration(emoteController);
                Log("Pre-performing emote on MaskedEnemy. Delay: " + delay + " ExtendedStopAndStareDuration: " + duration);
                emoteController.stopAndStareTimer = emoteController.stopAndStareTimer + duration;

                PerformEmoteAfterDelay(emote, emoteController, delay);
            }
            else if (emoteController.stopAndStareTimer <= 0 && emoteController.stoppedAndStaring)
                emoteController.stoppedAndStaring = false;
        }


        public static bool CalculateShouldEmoteChance(EmoteControllerMaskedEnemy emoteController)
        {
            bool shouldEmote = ConfigSync.instance.syncMaskedEnemiesAlwaysEmoteOnFirstEncounter && !playersEmotedWithThisRound.Contains(emoteController.lookingAtPlayer);
            if (!shouldEmote)
            {
                var random = new System.Random(currentLevelSeed + 1550 + 100 * emoteController.id + emoteController.emoteCount);
                float value = (float)random.NextDouble();
                shouldEmote = value <= ConfigSync.instance.syncMaskedEnemiesEmoteChanceOnEncounter;
            }
            Log("Calculating if the masked enemy (" + emoteController.maskedEnemy.name + ") should emote: " + shouldEmote);
            return shouldEmote;
        }


        public static float GetRandomEmoteDelay(EmoteControllerMaskedEnemy emoteController)
        {
            var random = new System.Random(currentLevelSeed - 550 + 100 * emoteController.id + emoteController.emoteCount);
            Vector2 range = ConfigSync.syncMaskedEnemyEmoteRandomDelay;
            range = new Vector2(Mathf.Min(Mathf.Abs(range.x), Mathf.Abs(range.y)), Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y)));
            return (float)(random.NextDouble() * (range.y - range.x) + range.x);
        }


        public static float GetRandomEmoteDuration(EmoteControllerMaskedEnemy emoteController)
        {
            if (!ConfigSync.instance.syncOverrideStopAndStareDuration)
                return 0;
            var random = new System.Random(currentLevelSeed + 550 + 100 * emoteController.id + emoteController.emoteCount);
            Vector2 range = ConfigSync.syncMaskedEnemyEmoteRandomDuration;
            range = new Vector2(Mathf.Min(Mathf.Abs(range.x), Mathf.Abs(range.y)), Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y)));
            return (float)random.NextDouble() * (range.y - range.x) + range.x;
        }


        public static UnlockableEmote GetRandomUnlockedEmote(EmoteControllerMaskedEnemy emoteController)
        {
            var playerController = emoteController.maskedEnemy.mimickingPlayer;
            if (playerController == null)
                playerController = emoteController.lookingAtPlayer;
            if (playerController == null)
                return null;

            var emotesList = SessionManager.unlockedEmotes;
            if (!ConfigSync.instance.syncShareEverything && playerController != localPlayerController)
                SessionManager.unlockedEmotesByPlayer.TryGetValue(playerController.playerUsername, out emotesList);
            if (emotesList == null)
                emotesList = SessionManager.unlockedEmotes;

            var random = new System.Random(currentLevelSeed + 100 * emoteController.id + emoteController.emoteCount);
            var emote = emotesList[random.Next(emotesList.Count)];

            if (emote.randomEmote && emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 0)
                emote = emote.emoteSyncGroup[random.Next(emote.emoteSyncGroup.Count)];
            return emote;
        }


        private static void SendUpdateMaskedEnemyEmoteToClients(EmoteControllerMaskedEnemy emoteController, int emoteId)
        {
            if (!isServer)
                return;

            var writer = new FastBufferWriter(sizeof(ulong) + sizeof(short), Allocator.Temp);
            writer.WriteValueSafe(emoteController.maskedEnemy.NetworkObjectId);
            writer.WriteValueSafe((short)emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnMaskedEnemyEmoteClientRpc", writer);
        }


        private static void OnMaskedEnemyEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out ulong maskedEnemyNetworkId);
            reader.ReadValue(out short emoteId);

            Log("Receiving update for masked enemy emote from server. Masked enemy id: " + maskedEnemyNetworkId + " EmoteId: " + emoteId);
            foreach (var emoteController in EmoteControllerMaskedEnemy.allMaskedEnemyEmoteControllers.Values)
            {
                if (emoteController.maskedEnemy.NetworkObjectId == maskedEnemyNetworkId)
                {
                    emoteController.PerformEmote(EmotesManager.allUnlockableEmotes[emoteId]);
                    return;
                }
            }
            LogError("Failed to find masked enemy with id: " + maskedEnemyNetworkId);
        }

        
        public static void PerformEmoteAfterDelay(UnlockableEmote emote, EmoteControllerMaskedEnemy emoteController, float delay)
        {
            IEnumerator PerformEmote()
            {
                yield return new WaitForSeconds(delay);
                if (emoteController.lookingAtPlayer != null)
                {
                    float distanceToTarget = Vector3.Distance(emoteController.lookingAtPlayer.transform.position, emoteController.maskedEnemy.transform.position);
                    if (distanceToTarget > 25f)
                    {
                        //LogWarning("Failed to perform emote on masked enemy. Target player is too far away. Distance: " + distanceToTarget + ". This is okay.");
                    }
                    else if (!emoteController.CanPerformEmote())
                    {
                        //LogWarning("Failed to perform emote on masked enemy: " + emoteController.maskedEnemy.name);
                    }
                    else
                    {
                        emoteController.PerformEmote(emote);
                        if (isServer)
                        {
                            SyncPerformingEmoteManager.ServerSendPerformingEmoteUpdateToClients(emoteController, emote);
                            //SendUpdateMaskedEnemyEmoteToClients(emoteController, emote.emoteId);
                        }
                    }
                }
                else
                {
                    //LogWarning("PerformEmoteDelayed - Not looking at player.");
                }
                    
            }
            if (emote != null)
                emoteController.maskedEnemy.StartCoroutine(PerformEmote());
        }
    }
}
