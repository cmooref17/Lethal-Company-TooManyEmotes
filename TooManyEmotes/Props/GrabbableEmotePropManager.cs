using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace TooManyEmotes.Props
{
    public static class GrabbableEmotePropManager
    {
        public static List<EmotePropData> emotePropsData { get { return EmotePropManager.emotePropsData; } }
        public static Dictionary<string, EmotePropData> emotePropsDataDict { get { return EmotePropManager.emotePropsDataDict; } }

        public static List<EmotePropData> grabbableEmotePropsData = new List<EmotePropData>();

        private static Item defaultPropItemData;
        private static Item defaultPropItemDataTwoHanded;

        public static int startGrabbableItemId { get { return StartOfRound.Instance?.allItemsList?.itemsList != null && numGrabbableEmoteProps > 0 && grabbableEmotePropsData[0] != null ? StartOfRound.Instance.allItemsList.itemsList.IndexOf(grabbableEmotePropsData[0].itemData) : -1; } }
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
                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (defaultPropItemData != null && defaultPropItemDataTwoHanded != null)
                        break;

                    if (networkPrefab?.Prefab != null && networkPrefab.Prefab.TryGetComponent<GrabbableObject>(out var grabbableObject) && grabbableObject.itemProperties != null)
                    {
                        string itemName = grabbableObject.itemProperties.itemName.ToLower();
                        if (itemName == "airhorn" || itemName == "v-type engine")
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

                            if (itemName == "airhorn" && defaultPropItemData == null)
                            {
                                itemData.twoHanded = false;
                                defaultPropItemData = itemData;
                            }
                            else if (itemName == "v-type engine" && defaultPropItemDataTwoHanded == null)
                            {
                                itemData.twoHanded = true;
                                defaultPropItemDataTwoHanded = itemData;
                            }
                        }
                    }
                }

                if (!defaultPropItemData)
                    Plugin.LogError("Failed to create default prop item data.");
                if (!defaultPropItemDataTwoHanded)
                    Plugin.LogError("Failed to create default two-handed prop item data.");
            }

            CreateGrabbablePropData("trombone.prop", value: 80, rarity: 12, weight: 1.05f, positionOffset: new Vector3(-0.155f, 0.325f, -0.015f), rotationOffset: new Vector3(-90, -80, 0));
            CreateGrabbablePropData("sexy_saxophone.sexy_sax.prop", value: 120, rarity: 10, weight: 1.05f, positionOffset: new Vector3(-0.15f, 0.08f, -0.055f), rotationOffset: new Vector3(0, 100, 80));
            CreateGrabbablePropData("baseball_bat.prop", value: 80, rarity: 12, weight: 1.03f, positionOffset: new Vector3(0.3f, 0.2f, 0.02f), rotationOffset: new Vector3(0, 0, -160));

            CreateGrabbablePropData("junk_food.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(-0.02f, 0.05f, -0.03f), rotationOffset: new Vector3(-10, 110, -10));
            CreateGrabbablePropData("red_card.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(0.08f, 0.075f, -0.075f), rotationOffset: new Vector3(-10, 100, -10));

            CreateGrabbablePropData("perfect_score.prop", value: 60, rarity: 15, weight: 1.05f, positionOffset: new Vector3(-0.1f, 0.025f, -0.027f), rotationOffset: new Vector3(-10, 110, -10));
            CreateGrabbablePropData("old_chair.prop", value: 100, rarity: 12, weight: 1.15f, two_handed: true, positionOffset: new Vector3(0, 0.1f, 0.6f), rotationOffset: new Vector3(85, 180, 0));
        }


        private static void CreateGrabbablePropData(string propName, bool isScrap = true, int purchasePrice = 0, int value = 100, int rarity = 10, float weight = 1, bool two_handed = false, Vector3 positionOffset = default, Vector3 rotationOffset = default)
        {
            if (!emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                Plugin.LogWarning("Failed to assign prop as scrap: " + propName + ". Prop does not exist!");
                return;
            }

            if (propData.isGrabbableObject || grabbableEmotePropsData.Contains(propData))
            {
                if (!grabbableEmotePropsData.Contains(propData))
                    grabbableEmotePropsData.Add(propData);
                return;
            }

            propData.isGrabbableObject = false; // for now

            if (propData.parentEmotes != null && propData.parentEmotes.Count > 0)
            {
                foreach (var _emote in propData.parentEmotes)
                    _emote.requiredHeldPropPrefab = propData.propPrefab;
            }
            else
            {
                Plugin.LogError("Failed to assign emote to emote prop. Could not find emote for prop: " + propName + ". Continuing anyways.");
                propData.isGrabbableObject = false;
                return;
            }

            try
            {
                Plugin.Log("Creating item data for emote prop: " + propName + ". Assigned emote: " + propData.parentEmotes[0].emoteName);

                var networkObject = propData.propPrefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Plugin.LogError("Cannot register grabbable emote prop prefab without a NetworkObject component.");
                    return;
                }

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
                grabbableEmoteProp.gameObject.layer = LayerMask.NameToLayer("Props");

                grabbableEmoteProp.sfxAudioSource = propData.propPrefab.AddComponent<AudioSource>();

                var scanNode = propData.propPrefab.GetComponentInChildren<ScanNodeProperties>();
                if (scanNode == null)
                    scanNode = propData.propPrefab.AddComponent<ScanNodeProperties>();
                grabbableEmoteProp.scanNodeProperties = scanNode;

                scanNode.scrapValue = value;
                scanNode.headerText = itemName;
                scanNode.subText = "Value: " + value;

                propData.itemName = itemName;
                propData.grabbablePropObject = grabbableEmoteProp;
                propData.isScrap = isScrap;
                propData.rarity = rarity;
                propData.twoHanded = two_handed;
                propData.minValue = Mathf.Max(value - 20, 0);
                propData.maxValue = Mathf.Max(value + 20, 0);
                propData.weight = weight;
                propData.positionOffset = positionOffset;
                propData.rotationOffset = rotationOffset;
                propData.restingRotation = propData.propPrefab.transform.eulerAngles;
                propData.verticalOffset = propData.propPrefab.transform.position.y;

                var referenceItemData = propData.twoHanded ? defaultPropItemDataTwoHanded : defaultPropItemData;
                if (referenceItemData == null)
                {
                    Plugin.LogError("Failed to create ItemData for prop: " + propData.propName);
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

                itemData.name = propData.itemName + "Prop";
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

                NetworkManager.Singleton.AddNetworkPrefab(propData.propPrefab);
                propData.isGrabbableObject = true;
                if (!grabbableEmotePropsData.Contains(propData))
                    grabbableEmotePropsData.Add(propData);
            }
            catch (Exception e)
            {
                Plugin.LogError("Failed to create item data for emote prop: " + propName + ". Error: " + e);
                propData.isGrabbableObject = false;
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        private static void RegisterGrabbableEmoteProps(StartOfRound __instance)
        {
            if (grabbableEmotePropsData == null)
            {
                Plugin.LogError("Failed to register grabbable emote props.");
                return;
            }

            foreach (var grabbablePropData in grabbableEmotePropsData)
            {
                if (!grabbablePropData.isGrabbableObject)
                    continue;

                Plugin.Log("Registering grabbable emote prop: " + grabbablePropData.propName);

                if (!StartOfRound.Instance.allItemsList.itemsList.Contains(grabbablePropData.itemData))
                    StartOfRound.Instance.allItemsList.itemsList.Add(grabbablePropData.itemData);

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
            var itemsList = StartOfRound.Instance?.allItemsList?.itemsList;
            if (grabbableEmotePropsData == null || itemsList == null)
                return;

            foreach (var grabbablePropdata in grabbableEmotePropsData)
            {
                if (grabbablePropdata?.itemData != null)
                    itemsList.Remove(grabbablePropdata.itemData);
            }
        }


        public static void AddGrabbableEmotePropsMoons()
        {
            if (grabbableEmotePropsData == null || StartOfRound.Instance?.levels == null)
            {
                Plugin.LogError("Error adding grabbable emote props to moons.");
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
                    }
                }

                foreach (var level in StartOfRound.Instance.levels)
                {
                    if (!level.spawnableScrap.Contains(grabbablePropData.itemRarityData))
                        level.spawnableScrap.Add(grabbablePropData.itemRarityData);
                }
            }
        }


        public static void RemoveGrabbableEmotePropsMoons()
        {
            if (emotePropsData == null || StartOfRound.Instance?.levels == null)
            {
                Plugin.LogError("Error removing grabbable emote props from moons.");
                return;
            }

            foreach (var emotePropData in emotePropsData)
            {
                foreach (var emote in emotePropData.parentEmotes)
                {
                    emote.purchasable = true;
                    emote.requiresHeldProp = false;
                }
                foreach (var level in StartOfRound.Instance.levels)
                {
                    if (level.spawnableScrap.Contains(emotePropData.itemRarityData))
                    {
                        level.spawnableScrap.Remove(emotePropData.itemRarityData);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(GameNetworkManager), "SaveItemsInShip")]
        [HarmonyPostfix]
        private static void OnSaveGrabbableShipObjects(GameNetworkManager __instance)
        {
            if (StartOfRound.Instance?.allItemsList?.itemsList != null && startGrabbableItemId >= 0 && numGrabbableEmoteProps > 0)
            {
                var itemIds = ES3.Load("shipGrabbableItemIDs", __instance.currentSaveFileName, new int[0]);
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
                        Plugin.Log("Saving " + grabbableEmotePropIdIndexes.Count + " grabbable prop items on ship. Grabbable props start id: " + startGrabbableItemId + " Num grabbable prop ids: " + numGrabbableEmoteProps);
                        ES3.Save("TooManyEmotes.GrabbablePropIndexes", grabbableEmotePropIdIndexes.ToArray(), __instance.currentSaveFileName);
                        ES3.Save("TooManyEmotes.StartGrabbablePropItemId", startGrabbableItemId, __instance.currentSaveFileName);
                        ES3.Save("TooManyEmotes.NumGrabbableProps", numGrabbableEmoteProps, __instance.currentSaveFileName);
                        return;
                    }
                }
            }

            ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.StartGrabbablePropItemId", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.NumGrabbableProps", __instance.currentSaveFileName);
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadShipGrabbableItems")]
        [HarmonyPrefix]
        private static void OnLoadGrabbableShipObjects(StartOfRound __instance)
        {
            if (__instance?.allItemsList?.itemsList == null)
                return;

            if (grabbableEmotePropsData == null || startGrabbableItemId < 0 || numGrabbableEmoteProps <= 0)
                return;

            if (!ES3.KeyExists("shipGrabbableItemIDs", GameNetworkManager.Instance.currentSaveFileName))
                return;

            if (!ES3.KeyExists("TooManyEmotes.GrabbablePropIndexes", GameNetworkManager.Instance.currentSaveFileName))
                return;

            // Correct index in case of id shifts

            var itemIds = ES3.Load<int[]>("shipGrabbableItemIDs", GameNetworkManager.Instance.currentSaveFileName);
            var grabbablePropIndexes = ES3.Load<int[]>("TooManyEmotes.GrabbablePropIndexes", GameNetworkManager.Instance.currentSaveFileName);
            int startId = ES3.Load<int>("TooManyEmotes.StartGrabbablePropItemId", GameNetworkManager.Instance.currentSaveFileName);
            int numProps = ES3.Load<int>("TooManyEmotes.NumGrabbableProps", GameNetworkManager.Instance.currentSaveFileName);

            // No change needed
            if (startId == startGrabbableItemId && numProps == numGrabbableEmoteProps)
                return;

            int numValuesChanged = 0;
            foreach (int index in grabbablePropIndexes)
            {
                if (index < 0 || index >= itemIds.Length)
                    continue;

                int id = itemIds[index];
                int newId = id;
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
                ES3.Save("shipGrabbableItemIDs", itemIds, GameNetworkManager.Instance.currentSaveFileName);
                Plugin.LogWarning("Item list has changed. Updating grabbable emote prop start id from: " + startId + " to: " + startGrabbableItemId);
            }
        }
    }
}
