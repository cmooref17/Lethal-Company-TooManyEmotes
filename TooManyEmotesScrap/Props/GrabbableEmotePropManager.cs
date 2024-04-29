using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using TooManyEmotes;
using TooManyEmotes.Props;
using TooManyEmotes.Networking;
using static TooManyEmotes.HelperTools;
using static TooManyEmotesScrap.CustomLogging;
using GameNetcodeStuff;

namespace TooManyEmotesScrap.Props
{
    public static class GrabbableEmotePropManager
    {
        private static Dictionary<string, Item> originalItems = new Dictionary<string, Item>();
        public static List<EmotePropData> emotePropsData { get { return EmotePropManager.emotePropsData; } }
        public static Dictionary<string, EmotePropData> emotePropsDataDict { get { return EmotePropManager.emotePropsDataDict; } }

        public static List<EmotePropData> grabbableEmotePropsData = new List<EmotePropData>();

        private static string defaultItemName = "dust pan";
        private static string defaultTwoHandedItemName = "v-type engine";
        private static Item defaultPropItemData;
        private static Item defaultPropItemDataTwoHanded;
        private static GameObject scanNodePrefab;

        public static int startGrabbableItemId { get { return allItems != null && numGrabbableEmoteProps > 0 && grabbableEmotePropsData[0] != null ? allItems.IndexOf(grabbableEmotePropsData[0].itemData) : -1; } }
        public static int numGrabbableEmoteProps { get { return grabbableEmotePropsData != null ? grabbableEmotePropsData.Count : 0; } }


        [HarmonyPatch(typeof(StartOfRound), "OnDisable")]
        [HarmonyPrefix]
        private static void Reset()
        {
            UnregisterGrabbableEmoteProps();
            RemoveGrabbableEmotePropsMoons();
        }

        
        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        [HarmonyPostfix]
        private static void CreateAllGrabbablePropsData(GameNetworkManager __instance)
        {
            if (defaultPropItemData == null || defaultPropItemDataTwoHanded == null)
            {
                if (NetworkManager.Singleton?.NetworkConfig?.Prefabs?.Prefabs != null)
                {
                    foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                    {
                        if (networkPrefab?.Prefab != null && networkPrefab.Prefab.TryGetComponent<GrabbableObject>(out var grabbableObject) && grabbableObject?.itemProperties != null && !string.IsNullOrEmpty(grabbableObject.itemProperties.itemName))
                        {
                            string itemName = grabbableObject.itemProperties.itemName.ToLower();
                            if (!originalItems.ContainsKey(itemName))
                                originalItems.Add(itemName, grabbableObject.itemProperties);


                            if (itemName == defaultItemName || itemName == defaultTwoHandedItemName)
                            {
                                var itemData = Item.Instantiate(grabbableObject.itemProperties);
                                itemData.itemName = "";
                                itemData.spawnPrefab = null;
                                itemData.isScrap = true;
                                itemData.itemSpawnsOnGround = true;
                                itemData.isConductiveMetal = false;
                                itemData.canBeGrabbedBeforeGameStart = true;
                                itemData.requiresBattery = false;
                                itemData.creditsWorth = 100;
                                itemData.minValue = 80;
                                itemData.maxValue = 120;
                                itemData.weight = 1;
                                itemData.toolTips = new string[] { "Perform emote [RMB]" };

                                if (itemName == defaultItemName && defaultPropItemData == null)
                                {
                                    itemData.twoHanded = false;
                                    defaultPropItemData = itemData;
                                    if (scanNodePrefab == null)
                                    {
                                        scanNodePrefab = grabbableObject.GetComponentInChildren<ScanNodeProperties>()?.gameObject;
                                        if (scanNodePrefab != null)
                                        {
                                            scanNodePrefab = GameObject.Instantiate(scanNodePrefab);
                                            scanNodePrefab.name = "ScanNode";
                                            scanNodePrefab.layer = 22; // ScanNode layer
                                            var scanNode = scanNodePrefab.GetComponentInChildren<ScanNodeProperties>();
                                            scanNode.headerText = "Prop";
                                            scanNode.subText = "Value:";
                                            scanNode.minRange = 1;
                                            scanNode.maxRange = 15;
                                            scanNode.scrapValue = 0;
                                        }
                                    }
                                }
                                else if (itemName == defaultTwoHandedItemName && defaultPropItemDataTwoHanded == null)
                                {
                                    itemData.twoHanded = true;
                                    defaultPropItemDataTwoHanded = itemData;
                                }
                            }
                        }
                    }

                    if (scanNodePrefab == null)
                        LogError("Failed to create ScanNode prefab.");
                    if (!defaultPropItemData)
                        LogError("Failed to create default prop item data.");
                    if (!defaultPropItemDataTwoHanded)
                        LogError("Failed to create default two-handed prop item data.");
                }
            }

            CreateGrabbablePropData("sexy_saxophone.sexy_sax.prop", value: 120, rarity: 10, weight: 1.05f, positionOffset: new Vector3(-0.15f, 0.08f, -0.055f), rotationOffset: new Vector3(0, 100, 80), copySfxFromItem: "big bolt");
            CreateGrabbablePropData("trombone.prop", value: 80, rarity: 12, weight: 1.05f, positionOffset: new Vector3(-0.155f, 0.325f, -0.015f), rotationOffset: new Vector3(-90, -80, 0), copySfxFromItem: "big bolt");
            CreateGrabbablePropData("baseball_bat.prop", value: 80, rarity: 12, weight: 1.03f, positionOffset: new Vector3(0.3f, 0.2f, 0.02f), rotationOffset: new Vector3(0, 0, -160), copySfxFromItem: "dust pan");

            CreateGrabbablePropData("junk_food.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(-0.02f, 0.05f, -0.03f), rotationOffset: new Vector3(-10, 110, -10));
            CreateGrabbablePropData("red_card.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(0.08f, 0.075f, -0.075f), rotationOffset: new Vector3(-10, 100, -10));

            CreateGrabbablePropData("perfect_score.prop", value: 60, rarity: 15, weight: 1.05f, positionOffset: new Vector3(-0.1f, 0.025f, -0.027f), rotationOffset: new Vector3(-10, 110, -10), copySfxFromItem: "toy robot");
            CreateGrabbablePropData("old_chair.prop", value: 100, rarity: 12, weight: 1.15f, two_handed: true, positionOffset: new Vector3(0, 0.1f, 0.6f), rotationOffset: new Vector3(85, 180, 0), copySfxFromItem: "toy robot");

            CreateGrabbablePropData("paddle.prop", value: 60, rarity: 15, weight: 1.01f, positionOffset: new Vector3(0.15f, 0.08f, -0.01f), rotationOffset: new Vector3(0, 95, -10), copySfxFromItem: "airhorn");
            CreateGrabbablePropData("dumbbell.prop", value: 120, rarity: 10, weight: 1.2f, positionOffset: new Vector3(0.015f, -0.08f, 0.02f), rotationOffset: new Vector3(0, 10, 0), copySfxFromItem: "big bolt");

            CreateGrabbablePropData("gamepad.prop", value: 120, rarity: 10, weight: 1.01f, positionOffset: new Vector3(-0.03f, 0.15f, -0.09f), rotationOffset: new Vector3(160, 104, 0), copySfxFromItem: "airhorn");
        }


        private static void CreateGrabbablePropData(string propName, bool isScrap = true, int purchasePrice = 0, int value = 100, int rarity = 10, float weight = 1, bool two_handed = false, Vector3 positionOffset = default, Vector3 rotationOffset = default, string copySfxFromItem = "")
        {
            if (!emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                LogWarning("Failed to assign prop as scrap: " + propName + ". Prop does not exist!");
                return;
            }

            if (propData.isGrabbableObject || grabbableEmotePropsData.Contains(propData))
            {
                if (!grabbableEmotePropsData.Contains(propData))
                    grabbableEmotePropsData.Add(propData);
                return;
            }

            AudioClip grabSfx = null;
            AudioClip dropSfx = null;
            AudioClip pocketSfx = null;
            AudioClip throwSfx = null;
            if (copySfxFromItem != "")
            {
                if (originalItems.TryGetValue(copySfxFromItem, out var copySfxItemData))
                {
                    grabSfx = copySfxItemData.grabSFX;
                    dropSfx = copySfxItemData.dropSFX;
                    pocketSfx = copySfxItemData.pocketSFX;
                    throwSfx = copySfxItemData.throwSFX;
                }
                else
                    LogError("Could not copy item SFX from item: \"" + copySfxFromItem + "\". Item does not exist? This is okay.");
            }

            propData.isGrabbableObject = false; // for now

            if (propData.parentEmotes != null && propData.parentEmotes.Count > 0)
            {
                foreach (var _emote in propData.parentEmotes)
                    _emote.requiredHeldPropPrefab = propData.propPrefab;
            }
            else
            {
                LogError("Failed to assign emote to emote prop. Could not find emote for prop: " + propName + ". Continuing anyways.");
                propData.isGrabbableObject = false;
                return;
            }

            try
            {
                Log("Creating item data for emote prop: " + propName + ". Assigned emote: " + propData.parentEmotes[0].emoteName);

                var networkObject = propData.propPrefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    LogError("Cannot register grabbable emote prop prefab without a NetworkObject component.");
                    return;
                }

                networkObject.AutoObjectParentSync = false;
                networkObject.DontDestroyWithOwner = true;

                string itemName = propName.Split('.')[0].Replace('_', ' ');
                itemName = char.ToUpper(itemName[0]) + itemName.Substring(1).ToLower();

                var grabbableEmoteProp = propData.propPrefab.GetComponent<GrabbablePropObject>();
                if (grabbableEmoteProp == null)
                    grabbableEmoteProp = propData.propPrefab.AddComponent<GrabbablePropObject>();
                grabbableEmoteProp.emotePropData = propData;
                grabbableEmoteProp.itemProperties = null;
                grabbableEmoteProp.grabbable = true;
                grabbableEmoteProp.mainObjectRenderer = propData.propPrefab.GetComponentInChildren<MeshRenderer>();
                grabbableEmoteProp.tag = "PhysicsProp";
                grabbableEmoteProp.gameObject.layer = 6; // Props layer

                grabbableEmoteProp.sfxAudioSource = propData.propPrefab.AddComponent<AudioSource>();
                grabbableEmoteProp.sfxAudioSource.minDistance = 4;
                grabbableEmoteProp.sfxAudioSource.maxDistance = 18;
                grabbableEmoteProp.sfxAudioSource.spatialBlend = 1;
                grabbableEmoteProp.sfxAudioSource.dopplerLevel = 0;

                var scanNode = propData.propPrefab.GetComponentInChildren<ScanNodeProperties>();
                if (scanNode == null)
                {
                    var scanNodeGameobject = GameObject.Instantiate(scanNodePrefab);
                    scanNodeGameobject.name = "ScanNode";

                    var scanNodeCollider = scanNodeGameobject.GetComponent<BoxCollider>();
                    var propCollider = grabbableEmoteProp.GetComponentInChildren<BoxCollider>();
                    scanNodeCollider.center = propCollider.center;
                    scanNodeCollider.size = propCollider.size;

                    scanNodeGameobject.transform.SetParent(propData.propPrefab.transform);
                    scanNodeGameobject.transform.position = scanNodeGameobject.transform.parent.position + Vector3.up * scanNodeCollider.size.y;
                    scanNodeCollider.center = new Vector3(scanNodeCollider.center.x, 0, scanNodeCollider.center.z);
                    scanNodeGameobject.transform.localRotation = Quaternion.identity;
                    scanNodeGameobject.transform.localScale = Vector3.one;
                    scanNode = scanNodeGameobject.GetComponent<ScanNodeProperties>();
                }
                grabbableEmoteProp.scanNodeProperties = scanNode;

                scanNode.scrapValue = value;
                scanNode.headerText = itemName;
                scanNode.subText = "Value:";
                scanNode.creatureScanID = -1;
                scanNode.requiresLineOfSight = true;
                scanNode.maxRange = 15;
                scanNode.minRange = 0;
                scanNode.nodeType = 2;

                propData.itemName = itemName;
                propData.grabbablePropObject = grabbableEmoteProp;
                propData.isScrap = isScrap;
                propData.rarity = rarity;
                propData.twoHanded = two_handed;
                propData.minValue = Mathf.Max(value - 20, 0);
                propData.maxValue = Mathf.Max(value + 20, 0);
                propData.weight = Mathf.Clamp(weight, 1, 10);
                propData.positionOffset = positionOffset;
                propData.rotationOffset = rotationOffset;
                propData.restingRotation = propData.propPrefab.transform.eulerAngles;
                propData.verticalOffset = propData.propPrefab.transform.position.y;

                var referenceItemData = propData.twoHanded ? defaultPropItemDataTwoHanded : defaultPropItemData;
                if (referenceItemData == null)
                {
                    LogError("Failed to create ItemData for prop: " + propData.propName);
                    return;
                }

                Item itemData = propData.itemData;
                if (itemData == null)
                {
                    itemData = Item.Instantiate(referenceItemData);
                    propData.itemData = itemData;
                    if (propData.grabbablePropObject)
                        propData.grabbablePropObject.itemProperties = itemData;
                }

                itemData.name = propData.itemName + ".prop";
                itemData.itemName = propData.itemName;
                itemData.spawnPrefab = propData.propPrefab;
                itemData.saveItemVariable = false;
                itemData.weight = propData.weight;
                itemData.isScrap = propData.isScrap;
                itemData.twoHanded = propData.twoHanded;
                itemData.minValue = Mathf.Max(propData.minValue, 0);
                itemData.maxValue = Mathf.Max(propData.maxValue, 0);
                itemData.positionOffset = propData.positionOffset;
                itemData.rotationOffset = propData.rotationOffset;
                itemData.restingRotation = propData.restingRotation;
                itemData.verticalOffset = propData.verticalOffset;
                itemData.grabSFX = grabSfx;
                itemData.dropSFX = dropSfx;
                itemData.pocketSFX = pocketSfx;
                itemData.throwSFX = throwSfx;

                NetworkManager.Singleton.AddNetworkPrefab(propData.propPrefab);
                propData.isGrabbableObject = true;
                if (!grabbableEmotePropsData.Contains(propData))
                    grabbableEmotePropsData.Add(propData);
            }
            catch (Exception e)
            {
                LogError("Failed to create item data for emote prop: " + propName + ". Error: " + e);
                propData.isGrabbableObject = false;
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        private static void RegisterGrabbableEmoteProps(StartOfRound __instance)
        {
            if (grabbableEmotePropsData == null)
            {
                LogError("Failed to register grabbable emote props.");
                return;
            }

            foreach (var grabbablePropData in grabbableEmotePropsData)
            {
                if (!grabbablePropData.isGrabbableObject)
                    continue;

                Log("Registering grabbable emote prop: " + grabbablePropData.propName);

                if (!allItems.Contains(grabbablePropData.itemData))
                    allItems.Add(grabbablePropData.itemData);

                SpawnableItemWithRarity itemRarityData = grabbablePropData.itemRarityData;
                if (itemRarityData == null)
                {
                    itemRarityData = new SpawnableItemWithRarity();
                    grabbablePropData.itemRarityData = itemRarityData;
                    itemRarityData.spawnableItem = grabbablePropData.itemData;
                    itemRarityData.rarity = grabbablePropData.rarity;
                }
            }
        }


        public static void UnregisterGrabbableEmoteProps()
        {
            if (grabbableEmotePropsData == null || allItems == null)
                return;

            foreach (var grabbablePropdata in grabbableEmotePropsData)
            {
                if (grabbablePropdata?.itemData != null)
                    allItems.Remove(grabbablePropdata.itemData);
            }
        }


        public static void AddGrabbableEmotePropsMoons()
        {
            if (grabbableEmotePropsData == null || selectableLevels == null)
            {
                LogError("Error adding grabbable emote props to moons.");
                return;
            }

            foreach (var grabbablePropData in grabbableEmotePropsData)
            {
                if (grabbablePropData.parentEmotes != null)
                {
                    foreach (var emote in grabbablePropData.parentEmotes)
                    {
                        emote.purchasable = false;
                        emote.requiresHeldProp = true;

                        if (emote.emoteSyncGroup != null)
                        {
                            foreach (var emote2 in emote.emoteSyncGroup)
                            {
                                if (emote2 == emote)
                                    continue;

                                emote2.purchasable = false;
                            }
                        }
                    }
                }

                foreach (var level in selectableLevels)
                {
                    if (!level.spawnableScrap.Contains(grabbablePropData.itemRarityData))
                        level.spawnableScrap.Add(grabbablePropData.itemRarityData);
                }
            }
        }


        public static void RemoveGrabbableEmotePropsMoons()
        {
            if (emotePropsData == null || selectableLevels == null)
            {
                LogError("Error removing grabbable emote props from moons.");
                return;
            }

            foreach (var emotePropData in emotePropsData)
            {
                foreach (var emote in emotePropData.parentEmotes)
                {
                    emote.purchasable = true;
                    emote.requiresHeldProp = false;
                }
                foreach (var level in selectableLevels)
                {
                    if (level.spawnableScrap.Contains(emotePropData.itemRarityData))
                    {
                        level.spawnableScrap.Remove(emotePropData.itemRarityData);
                    }
                }
            }
        }


        public static void CheckIfShouldRemovePropEmotesFromUnlockedEmotes()
        {
            if (ConfigSync.instance.syncUnlockEverything && !ConfigSync.instance.syncRemoveGrabbableEmotesPartyPooperMode)
                return;

            foreach (var emotePropData in grabbableEmotePropsData)
            {
                if (emotePropData.parentEmotes != null)
                {
                    foreach (var emote in emotePropData.parentEmotes)
                    {
                        if (emote != null && emote.requiresHeldProp)
                        {
                            SessionManager.RemoveEmoteLocal(emote);
                            if (emote.complementary)
                                EmotesManager.complementaryEmotes.Remove(emote);
                            if (emote.emoteSyncGroup != null)
                            {
                                foreach (var emote2 in emote.emoteSyncGroup)
                                {
                                    if (emote2 == emote)
                                        continue;

                                    SessionManager.RemoveEmoteLocal(emote2);
                                    if (emote2.complementary)
                                        EmotesManager.complementaryEmotes.Remove(emote2);
                                }
                            }
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(GameNetworkManager), "SaveItemsInShip")]
        [HarmonyPostfix]
        private static void OnSaveGrabbableShipObjects(GameNetworkManager __instance)
        {
            if (allItems != null && startGrabbableItemId >= 0 && numGrabbableEmoteProps > 0)
            {
                var itemIds = ES3.Load("shipGrabbableItemIDs", currentSaveFileName, new int[0]);
                if (itemIds.Length > 0)
                {
                    List<int> grabbableEmotePropIdIndexes = new List<int>();
                    for (int i = 0; i < itemIds.Length; i++)
                    {
                        int id = itemIds[i];
                        if (id >= startGrabbableItemId && id < startGrabbableItemId + numGrabbableEmoteProps)
                            grabbableEmotePropIdIndexes.Add(i);
                    }

                    if (grabbableEmotePropIdIndexes.Count > 0)
                    {
                        Log("Saving " + grabbableEmotePropIdIndexes.Count + " grabbable prop items on ship. Grabbable props start id: " + startGrabbableItemId + " Num grabbable prop ids: " + numGrabbableEmoteProps);
                        ES3.Save("TooManyEmotes.GrabbablePropIndexes", grabbableEmotePropIdIndexes.ToArray(), currentSaveFileName);
                        ES3.Save("TooManyEmotes.StartGrabbablePropItemId", startGrabbableItemId, currentSaveFileName);
                        ES3.Save("TooManyEmotes.NumGrabbableProps", numGrabbableEmoteProps, currentSaveFileName);
                        return;
                    }
                }
            }

            ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.NumGrabbableProps", currentSaveFileName);
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadShipGrabbableItems")]
        [HarmonyPrefix]
        private static void OnLoadGrabbableShipObjects(StartOfRound __instance)
        {
            if (allItems == null)
                return;

            LogWarning("AAA");
            if (grabbableEmotePropsData == null || startGrabbableItemId < 0 || numGrabbableEmoteProps <= 0)
                return;

            LogWarning("BBB");
            if (!ES3.KeyExists("shipGrabbableItemIDs", currentSaveFileName))
                return;

            LogWarning("CCC");
            if (!ES3.KeyExists("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName))
                return;

            // Correct index in case of id shifts

            var itemIds = ES3.Load<int[]>("shipGrabbableItemIDs", currentSaveFileName);
            var grabbablePropIndexes = ES3.Load<int[]>("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
            int startId = ES3.Load<int>("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
            int numProps = ES3.Load<int>("TooManyEmotes.NumGrabbableProps", currentSaveFileName);

            LogWarning("DDD");
            // No change needed
            if (startId == startGrabbableItemId && numProps == numGrabbableEmoteProps)
                return;

            LogWarning("EEE OldStart: " + startId + " CurrentStartId: " + startGrabbableItemId + " NumProps: " + numProps + " CurrentNumProps: " + numGrabbableEmoteProps);
            int numValuesChanged = 0;
            foreach (int index in grabbablePropIndexes)
            {
                LogWarning("FFF: " + index);
                if (index < 0 || index >= itemIds.Length)
                    continue;

                int id = itemIds[index];
                int newId = id;
                LogWarning("GGG: " + id + " " + newId);
                if (id >= startId && id < startId + numProps)
                {
                    newId = id - startId + startGrabbableItemId;
                    if (newId >= startGrabbableItemId + numGrabbableEmoteProps)
                        newId = startGrabbableItemId;
                    itemIds[index] = newId;
                    numValuesChanged++;
                }
            }

            if (numValuesChanged > 0)
            {
                ES3.Save("shipGrabbableItemIDs", itemIds, currentSaveFileName);
                LogWarning("Item list has changed since last session. Updating grabbable emote prop start id from: " + startId + " to: " + startGrabbableItemId);
            }
        }
    }
}
