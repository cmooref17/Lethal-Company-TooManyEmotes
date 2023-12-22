using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using System.Collections;
using Unity.Netcode;
using UnityEngine.UIElements;
using UnityEngine;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class EmoteGifts {
        /*
        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPostfix]
        public static void SpawnEmoteGifts(RoundManager __instance)
        {
            Item giftItem = null;
            for (int i = 0; i < StartOfRound.Instance.allItemsList.itemsList.Count; i++) {
                var item = StartOfRound.Instance.allItemsList.itemsList[i];
                if (item.name == "GiftItem")
                {
                    giftItem = item;
                    break;
                }
            }
            if (giftItem == null)
                return;

            System.Random random = new System.Random(TerminalPatcher.emoteStoreSeed + 1);
            var emotes = new List<UnlockableEmote>();

            for (int i = 0; i < 5; i++)
            {
                UnlockableEmote emote = null;
                if (ConfigSync.syncDisableRaritySystem)
                    emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allUnlockableEmotes, random);
                else
                {
                    double itemRarity = random.NextDouble();
                    float threshold = 1 - ConfigSync.syncRotationChanceEmoteTier3;
                    if (itemRarity >= threshold)
                        emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allEmotesTier3, random);
                    if (emote == null)
                    {
                        threshold -= ConfigSync.syncRotationChanceEmoteTier2;
                        if (itemRarity >= threshold)
                            emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allEmotesTier2, random);
                    }
                    if (emote == null)
                    {
                        threshold -= ConfigSync.syncRotationChanceEmoteTier1;
                        if (itemRarity >= threshold)
                            emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allEmotesTier1, random);
                    }
                    if (emote == null)
                        emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allEmotesTier0, random);
                }

                if (emote != null)
                    emotes.Add(emote);
            }

            RandomScrapSpawn randomScrapSpawn = null;
            RandomScrapSpawn[] source = UnityEngine.Object.FindObjectsOfType<RandomScrapSpawn>();

            var list3 = new List<NetworkObjectReference>();
            var usedSpawns = new List<RandomScrapSpawn>();

            for (int i = 0; i < emotes.Count; i++)
            {
                var emote = emotes[i];

                Vector3 position = __instance.GetRandomNavMeshPositionInRadiusSpherical(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, __instance.navHit) + Vector3.up * giftItem.verticalOffset;
                GameObject obj = UnityEngine.Object.Instantiate(giftItem.spawnPrefab, position, Quaternion.identity, __instance.spawnedScrapContainer);
                GrabbableObject component = obj.GetComponent<GrabbableObject>();
                component.transform.rotation = Quaternion.Euler(component.itemProperties.restingRotation);
                component.fallTime = 0f;
                //list.Add((int)((float)AnomalyRandom.Next(ScrapToSpawn[i].minValue, ScrapToSpawn[i].maxValue) * scrapValueMultiplier));
                //num += list[list.Count - 1];
                component.scrapValue = emote.price / 4;
                NetworkObject component2 = obj.GetComponent<NetworkObject>();
                component2.Spawn();
                list3.Add(component2);
            }
        }
        */
    }
}
