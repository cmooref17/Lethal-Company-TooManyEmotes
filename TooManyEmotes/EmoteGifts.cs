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
using System.Security.Policy;
using System.Runtime.InteropServices;
using Unity.Collections;
using System.ComponentModel;
using Unity.Mathematics;
using DunGen;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using UnityEngine.Rendering;

namespace TooManyEmotes.Patches
{
    /*
    [HarmonyPatch]
    public static class EmoteGifts {

        public static Item emoteGiftItem;
        public static GameObject emoteGiftPrefab;
        public static Material emoteGiftMaterial;
        public static Material cubeMaterial;
        public static Color emissiveColor = new Color(0, 0, 0.1f, 1);
        public static float emissiveColorIntensity = 100f;
        public static float bloomIntensity = 0.1f;

        public static List<UnlockableEmote> spawningEmoteGifts = new List<UnlockableEmote>();
        public static List<GiftBoxItem> spawnedEmoteGifts = new List<GiftBoxItem>();


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void CreateEmoteGiftItem(StartOfRound __instance)
        {

            var volume = GameObject.FindObjectOfType<Volume>();
            
            Bloom bloom;
            if (!volume.profile.TryGet<Bloom>(out bloom))
                bloom = volume.profile.Add<Bloom>(true);

            bloom.active = true;
            bloom.intensity.value = 0.1f;

            if (emoteGiftItem == null || emoteGiftPrefab == null)
            {
                for (int i = 0; i < __instance.allItemsList.itemsList.Count; i++)
                {
                    var item = __instance.allItemsList.itemsList[i];
                    if (item.name == "GiftBox")
                    {
                        emoteGiftItem = ScriptableObject.Instantiate(item);
                        emoteGiftItem.name = "EmoteGiftItem";
                        emoteGiftItem.itemName = "EmoteGift";
                        emoteGiftItem.creditsWorth = 0;
                        break;
                    }
                }
            }

            if (emoteGiftItem != null && emoteGiftPrefab == null)
            {
                emoteGiftPrefab = GameObject.Instantiate(emoteGiftItem.spawnPrefab, Vector3.down * 1000, Quaternion.identity);
                emoteGiftPrefab.name = "EmoteGiftItem";
                emoteGiftPrefab.GetComponent<GrabbableObject>().itemProperties = emoteGiftItem;
                emoteGiftItem.spawnPrefab = emoteGiftPrefab;
            }

            cubeMaterial = new Material(Shader.Find("HDRP/Lit"));
            cubeMaterial.EnableKeyword("_EMISSION");
        }

        
        [HarmonyPatch(typeof(RoundManager), "Update")]
        [HarmonyPostfix]
        public static void SetValues(RoundManager __instance)
        {
            if (cubeMaterial == null)
                return;

            cubeMaterial.EnableKeyword("_EMISSION");
            cubeMaterial.SetColor("_EmissiveColor", emissiveColor * emissiveColorIntensity);

            var volume = GameObject.FindObjectOfType<Volume>();

            Bloom bloom;
            if (!volume.profile.TryGet<Bloom>(out bloom))
                bloom = volume.profile.Add<Bloom>(true);

            bloom.active = true;
            bloom.intensity.value = bloomIntensity;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnSpawnEmoteGiftsClientRpc", OnSpawnEmoteGiftsClientRpc);
            }
        }


        [HarmonyPatch(typeof(GrabbableObject), "Update")]
        [HarmonyPrefix]
        public static bool RotateEmoteGiftObject(GrabbableObject __instance)
        {
            if (!(__instance is GiftBoxItem) || !__instance.name.StartsWith("Emote") || __instance.fallTime < 1 || !__instance.reachedFloorTarget || __instance.isHeld || !__instance.hasHitGround)
                return true;

            float yValue = Mathf.Sin(__instance.targetFloorPosition.x + __instance.targetFloorPosition.z + Time.time) + 1;
            Vector3 position = __instance.targetFloorPosition + Vector3.up * yValue * 0.25f;
            __instance.transform.localPosition = position;

            __instance.transform.Rotate(Vector3.forward * Time.deltaTime);

            return false;
        }


        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        [HarmonyPostfix]
        public static void SpawnEmoteGifts(RoundManager __instance)
        {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost || ConfigSync.instance.syncUnlockEverything)
                return;

            if (emoteGiftItem == null || emoteGiftPrefab == null)
            {
                Plugin.LogError("Cannot spawn emote gifts. EmoteGiftItem is null!");
                return;
            }

            System.Random random = new System.Random(TerminalPatcher.emoteStoreSeed + 1);
            spawningEmoteGifts.Clear();

            for (int i = 0; i < 50; i++)
            {
                UnlockableEmote emote = null;
                if (ConfigSync.instance.syncDisableRaritySystem)
                    emote = TerminalPatcher.GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allUnlockableEmotes, random);
                else
                {
                    double itemRarity = random.NextDouble();
                    float threshold = 1 - ConfigSync.instance.syncRotationChanceEmoteTier3;
                    if (itemRarity >= threshold)
                        emote = GetRandomEmoteNotUnlockedNotSpawned(StartOfRoundPatcher.allEmotesTier3, random);
                    if (emote == null)
                    {
                        threshold -= ConfigSync.instance.syncRotationChanceEmoteTier2;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlockedNotSpawned(StartOfRoundPatcher.allEmotesTier2, random);
                    }
                    if (emote == null)
                    {
                        threshold -= ConfigSync.instance.syncRotationChanceEmoteTier1;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlockedNotSpawned(StartOfRoundPatcher.allEmotesTier1, random);
                    }
                    if (emote == null)
                        emote = GetRandomEmoteNotUnlockedNotSpawned(StartOfRoundPatcher.allEmotesTier0, random);
                }

                if (emote != null)
                    spawningEmoteGifts.Add(emote);
            }

            var spawnedEmoteGifts = new List<NetworkObjectReference>();
            RandomScrapSpawn randomScrapSpawn = null;
            RandomScrapSpawn[] source = UnityEngine.Object.FindObjectsOfType<RandomScrapSpawn>();
            var usedSpawns = new List<RandomScrapSpawn>();

            for (int i = 0; i < spawningEmoteGifts.Count; i++)
            {
                var emote = spawningEmoteGifts[i];
                List<RandomScrapSpawn> spawns = (emoteGiftItem.spawnPositionTypes != null && emoteGiftItem.spawnPositionTypes.Count != 0) ? source.Where((RandomScrapSpawn x) => emoteGiftItem.spawnPositionTypes.Contains(x.spawnableItems) && !x.spawnUsed).ToList() : source.ToList();
                if (spawns.Count <= 0)
                {
                    Plugin.Log("No tiles containing a scrap spawn with item type: EmoteGift");
                    continue;
                }

                if (usedSpawns.Count > 0 && spawns.Contains(randomScrapSpawn))
                {
                    spawns.RemoveAll((RandomScrapSpawn x) => usedSpawns.Contains(x));
                    if (spawns.Count <= 0)
                    {
                        usedSpawns.Clear();
                        i--;
                        continue;
                    }
                }

                randomScrapSpawn = spawns[__instance.AnomalyRandom.Next(0, spawns.Count)];
                usedSpawns.Add(randomScrapSpawn);

                Vector3 position = __instance.GetRandomNavMeshPositionInRadiusSpherical(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, __instance.navHit) + Vector3.up * emoteGiftItem.verticalOffset;
                GameObject emoteGiftObject = UnityEngine.Object.Instantiate(emoteGiftPrefab, position, Quaternion.Euler(emoteGiftItem.restingRotation), __instance.spawnedScrapContainer);
                emoteGiftObject.name = "EmoteGift_" + emote.emoteName;
                GrabbableObject grabbableObject = emoteGiftObject.GetComponent<GrabbableObject>();
                grabbableObject.fallTime = 0f;
                grabbableObject.scrapValue = (int)(__instance.AnomalyRandom.Next((int)((emote.price / 4) * 0.8f), (int)((emote.price / 4) * 1.2f)) * __instance.scrapValueMultiplier);

                grabbableObject.floorYRot = emote.rarity;
                grabbableObject.customGrabTooltip = emote.emoteName;

                NetworkObject networkObject = emoteGiftObject.GetComponent<NetworkObject>();
                networkObject.Spawn();
                spawnedEmoteGifts.Add(networkObject);
            }

            __instance.StartCoroutine(WaitForScrapToSpawnToSync(spawnedEmoteGifts.ToArray()));
        }


        public static UnlockableEmote GetRandomEmoteNotUnlockedNotSpawned(List<UnlockableEmote> emoteList, System.Random random)
        {
            var notUnlocked = new List<UnlockableEmote>();
            foreach (var emote in emoteList)
            {
                if (!StartOfRoundPatcher.unlockedEmotes.Contains(emote) && !spawningEmoteGifts.Contains(emote))
                    notUnlocked.Add(emote);
            }
            if (notUnlocked.Count > 0)
            {
                int emoteIndex = random.Next(notUnlocked.Count);
                return notUnlocked[emoteIndex];
            }
            return null;
        }


        static IEnumerator WaitForScrapToSpawnToSync(NetworkObjectReference[] spawnedEmoteGifts)
        {
            yield return new WaitForSeconds(12f);
            if (spawnedEmoteGifts.Length > 0 )
            {
                Plugin.Log("Attemping to spawn " + spawnedEmoteGifts.Length + " emote gifts.");
                SyncSpawnedEmoteGiftsWithClients(spawnedEmoteGifts);
            }
            else
                Plugin.LogError("Failed to spawn emote gifts.");
        }


        static void SyncSpawnedEmoteGiftsWithClients(NetworkObjectReference[] spawnedEmoteGiftsNet)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            System.Random random = new System.Random(TerminalPatcher.emoteStoreSeed + 2);

            var writer = new FastBufferWriter(sizeof(int) + Marshal.SizeOf(typeof(NetworkObjectReference)) * spawnedEmoteGiftsNet.Length, Allocator.Temp);
            writer.WriteValueSafe(spawnedEmoteGiftsNet.Length);
            spawnedEmoteGifts.Clear();
            for (int i = 0; i < spawnedEmoteGiftsNet.Length; i++)
            {
                writer.WriteValueSafe(spawnedEmoteGiftsNet[i]);
                GiftBoxItem emoteGiftObject = null;
                if (spawnedEmoteGiftsNet[i].TryGet(out var networkObject))
                    emoteGiftObject = networkObject.GetComponent<GiftBoxItem>();
                if (emoteGiftObject != null)
                {
                    int price = random.Next(80, 120);
                    emoteGiftObject.SetScrapValue(price);
                    OnSpawnEmoteGiftLocal(emoteGiftObject);
                }    
                else
                    Plugin.LogError("Error getting EmoteGift from NetworkObject.");
            }

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnSpawnEmoteGiftsClientRpc", writer);
        }


        static void OnSpawnEmoteGiftsClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            spawnedEmoteGifts.Clear();
            System.Random random = new System.Random(TerminalPatcher.emoteStoreSeed + 2);

            if (reader.TryBeginRead(sizeof(int)))
            {
                int numObjects;
                reader.ReadValueSafe(out numObjects);

                if (reader.TryBeginRead(Marshal.SizeOf(typeof(NetworkObjectReference)) * numObjects))
                {
                    for (int i = 0; i < numObjects; i++)
                    {
                        NetworkObjectReference networkObjectReference;
                        GiftBoxItem emoteGiftObject = null;
                        reader.ReadValueSafe(out networkObjectReference);
                        if (networkObjectReference.TryGet(out var networkObject))
                        {
                            if (networkObject != null)
                                emoteGiftObject = networkObject.GetComponent<GiftBoxItem>();
                        }
                        if (emoteGiftObject != null)
                        {
                            int price = random.Next(80, 120);
                            emoteGiftObject.SetScrapValue(price);
                            OnSpawnEmoteGiftLocal(emoteGiftObject);
                        }
                        else
                            Plugin.LogError("Error getting EmoteGift from NetworkObject.");
                    }
                }
            }
        }


        static void OnSpawnEmoteGiftLocal(GiftBoxItem emoteGift)
        {
            if (emoteGift == null)
                return;

            string emoteName = emoteGift.name.Replace("EmoteGift_", "");
            var emote = StartOfRoundPatcher.allUnlockableEmotesDict[emoteName];
            if (emote == null)
            {
                Plugin.LogError("Got null emote from EmoteGift: " + emoteGift.name);
                return;
            }

            //emoteGift.transform.localScale = Vector3.one;

            emoteGift.scrapValue = (int)(emote.price * emoteGift.scrapValue / 100f);

            Color lightColor;
            ColorUtility.TryParseHtmlString(emote.nameColor, out lightColor);


            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = emoteGift.transform;
            cube.transform.SetLocalPositionAndRotation(Vector3.forward * 24, Quaternion.identity);
            cube.transform.localScale = new Vector3(8, 8, 24);
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = cubeMaterial;
            renderer.sharedMaterial.EnableKeyword("_EMISSION");

            if (emoteGiftMaterial == null)
            {
                
            }


            spawnedEmoteGifts.Add(emoteGift);
        }
    }
    */
}
