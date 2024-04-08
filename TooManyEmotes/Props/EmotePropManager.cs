using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TooManyEmotes.Config;
using Unity.Netcode;
using UnityEngine;

namespace TooManyEmotes.Props
{
    [HarmonyPatch]
    internal static class EmotePropManager
    {
        public static AssetBundle propAssetBundle;
        
        public static HashSet<EmotePropData> emotePropsData = new HashSet<EmotePropData>();
        public static Dictionary<string, EmotePropData> emotePropsDataDict = new Dictionary<string, EmotePropData>();

        private static Item defaultPropItemData;
        private static Item defaultPropItemDataTwoHanded;
        private static AudioSource defaultAudioSource;

        public static bool registeredGrabbableEmoteProps = false;

        //public static Dictionary<string, RuntimeAnimatorController> propAnimatorControllers = new Dictionary<string, RuntimeAnimatorController>();
        //public static Dictionary<RuntimeAnimatorController, GameObject> animatorControllerToProp = new Dictionary<RuntimeAnimatorController, GameObject>();

        public static Transform propPoolParent;
        public static Dictionary<string, HashSet<PropObject>> propPoolsDict = new Dictionary<string, HashSet<PropObject>>();


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void Init(StartOfRound __instance)
        {
            CleanPropPools();
            propPoolParent = new GameObject("EmotePropPool").transform;
        }


        [HarmonyPatch(typeof(StartOfRound), "OnDisable")]
        [HarmonyPrefix]
        private static void Reset()
        {
            UnregisterGrabbableEmoteProps();
            RemoveGrabbableEmotePropsMoons();
        }


        public static void LoadPropAssets()
        {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(Plugin.instance.Info.Location), "Assets/emote_props");
                propAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                var prefabs = propAssetBundle.LoadAllAssets<GameObject>();
                foreach (var prefab in prefabs)
                {
                    if (emotePropsDataDict.ContainsKey(prefab.name))
                        continue;

                    var emotePropData = new EmotePropData()
                    {
                        propPrefab = prefab,
                        propName = prefab.name
                    };

                    if (!prefab.GetComponent<NetworkObject>())
                        prefab.AddComponent<NetworkObject>();

                    emotePropsData.Add(emotePropData);
                    emotePropsDataDict.Add(emotePropData.propName, emotePropData);
                }
                Plugin.Log("Loaded " + emotePropsData.Count + " emote props.");
            }
            catch
            {
                Plugin.LogError("Failed to load emotes props asset bundle: emote_props.");
            }
        }


        public static void BuildEmotePropList()
        {
            if (emotePropsData == null)
                return;

            CleanPropPools();
            propPoolsDict.Clear();

            if (EmotesManager.allUnlockableEmotes == null || EmotesManager.allUnlockableEmotes.Count <= 0)
            {
                Plugin.LogError("Failed to build emote prop list. Make sure you build the emote prop list after building the unlockable emotes list.");
                return;
            }

            foreach (var propData in emotePropsData)
            {
                if (propData == null)
                    continue;

                string emoteName = propData.propName;
                if (emoteName.Contains("."))
                {
                    var args = emoteName.Split('.');
                    if (args.Length > 0 && args[0].Length > 0)
                        emoteName = args[0];
                }

                if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                {
                    if (emote.propNamesInEmote == null)
                        emote.propNamesInEmote = new List<string>();
                    emote.propNamesInEmote.Add(propData.propName);

                    if (propData.parentEmotes == null)
                        propData.parentEmotes = new List<UnlockableEmote>();
                    propData.parentEmotes.Add(emote);
                }
            }
        }





        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        [HarmonyPostfix]
        private static void CreatePropItemData()
        {
            if (!ConfigSettings.enableGrabbableEmoteProps.Value)
                return;

            CreateGrabbablePropData("trombone.prop", value: 80, rarity: 12, weight: 1.05f, positionOffset: new Vector3(-0.155f, 0.325f, -0.015f), rotationOffset: new Vector3(-90, -80, 0));
            CreateGrabbablePropData("sexy_saxophone.sexy_sax.prop", value: 120, rarity: 10, weight: 1.05f, positionOffset: new Vector3(-0.15f, 0.08f, -0.055f), rotationOffset: new Vector3(0, 100, 80));
            CreateGrabbablePropData("baseball_bat.prop", value: 80, rarity: 12, weight: 1.03f, positionOffset: new Vector3(0.3f, 0.2f, 0.02f), rotationOffset: new Vector3(0, 0, -160));

            CreateGrabbablePropData("junk_food.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(-0.02f, 0.05f, -0.03f), rotationOffset: new Vector3(-10, 110, -10));
            CreateGrabbablePropData("red_card.prop", value: 60, rarity: 15, weight: 0, positionOffset: new Vector3(0.08f, 0.075f, -0.075f), rotationOffset: new Vector3(-10, 100, -10));

            CreateGrabbablePropData("perfect_score.prop", value: 60, rarity: 15, weight: 1.05f, positionOffset: new Vector3(-0.1f, 0.025f, -0.027f), rotationOffset: new Vector3(-10, 110, -10));
            CreateGrabbablePropData("old_chair.prop", value: 100, rarity: 12, weight: 1.15f, two_handed: true, positionOffset: new Vector3(0, 0.1f, 0.6f), rotationOffset: new Vector3(85, 180, 0));
        }


        private static void CreateGrabbablePropData(string propName, bool isScrap = true, int value = 100, int rarity = 10, float weight = 1, bool two_handed = false, Vector3 positionOffset = default, Vector3 rotationOffset = default)
        {
            if (!emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                Plugin.LogWarning("Failed to assign prop as scrap: " + propName + ". Prop does not exist!");
                return;
            }

            if (propData.isGrabbableObject)
            {
                Plugin.LogWarning("Already created grabbable prop data for prop with name: " + propName);
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

                NetworkManager.Singleton.AddNetworkPrefab(propData.propPrefab);
                propData.isGrabbableObject = true;

            }
            catch (Exception e)
            {
                Plugin.LogError("Failed to create item data for emote prop: " + propName + ". Error: " + e);
                propData.isGrabbableObject = false;
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        private static void RegisterGrabbableEmoteProps()
        {
            if (StartOfRound.Instance?.allItemsList == null || emotePropsData == null)
            {
                Plugin.LogError("Failed to register grabbable emote props.");
                return;
            }

            // Create default prop item data
            if (defaultPropItemData == null || defaultPropItemDataTwoHanded == null)
            {
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    string itemName = item.itemName.ToLower();
                    if (itemName == "airhorn" || itemName == "v-type engine")
                    {
                        defaultAudioSource = item.spawnPrefab.GetComponent<AudioSource>();
                        var itemData = Item.Instantiate(item);
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
                            defaultPropItemData = itemData;
                        else if (itemName == "v-type engine" && defaultPropItemDataTwoHanded == null)
                            defaultPropItemDataTwoHanded = itemData;
                    }
                    if (defaultPropItemData != null && defaultPropItemDataTwoHanded != null)
                        break;
                }

                if (!defaultPropItemData)
                    Plugin.LogError("Failed to create default prop item data.");
                if (!defaultPropItemDataTwoHanded)
                    Plugin.LogError("Failed to create default two-handed prop item data.");
            }

            foreach (var emotePropData in emotePropsData)
            {
                if (!emotePropData.isGrabbableObject || emotePropData.registered)
                    continue;

                Plugin.Log("Registering grabbable emote prop: " + emotePropData.propName);

                var referenceItemData = emotePropData.twoHanded ? defaultPropItemDataTwoHanded : defaultPropItemData;
                if (referenceItemData == null)
                {
                    Plugin.LogError("Failed to create ItemData for prop: " + emotePropData.propName);
                    emotePropData.isGrabbableObject = false;
                    continue;
                }

                Item itemData = emotePropData.itemData;
                if (itemData == null)
                {
                    itemData = Item.Instantiate(referenceItemData);
                    emotePropData.itemData = itemData;
                    if (emotePropData.grabbablePropObject)
                        emotePropData.grabbablePropObject.itemProperties = itemData;
                }

                itemData.name = emotePropData.itemName + "Prop";
                itemData.itemName = emotePropData.itemName;
                itemData.spawnPrefab = emotePropData.propPrefab;
                itemData.weight = emotePropData.weight;
                itemData.isScrap = emotePropData.isScrap;
                itemData.twoHanded = emotePropData.twoHanded;
                itemData.minValue = Mathf.Max(emotePropData.minValue, 0);
                itemData.maxValue = Mathf.Max(emotePropData.maxValue, 0);
                itemData.positionOffset = emotePropData.positionOffset;
                itemData.rotationOffset = emotePropData.rotationOffset;
                itemData.restingRotation = emotePropData.restingRotation;
                itemData.verticalOffset = emotePropData.verticalOffset;

                if (!StartOfRound.Instance.allItemsList.itemsList.Contains(itemData))
                    StartOfRound.Instance.allItemsList.itemsList.Add(itemData);

                SpawnableItemWithRarity itemRarityData = emotePropData.itemRarityData;
                if (itemRarityData == null)
                {
                    itemRarityData = new SpawnableItemWithRarity();
                    emotePropData.itemRarityData = itemRarityData;
                    itemRarityData.spawnableItem = itemData;
                    itemRarityData.rarity = emotePropData.rarity;
                }
            }
        }


        public static void UnregisterGrabbableEmoteProps()
        {
            var itemsList = StartOfRound.Instance?.allItemsList?.itemsList;
            if (emotePropsData != null && itemsList != null)
            {
                foreach (var propData in emotePropsData)
                {
                    if (!propData.itemData)
                        continue;
                    itemsList.Remove(propData.itemData);
                }
            }
        }


        public static void AddGrabbableEmotePropsMoons()
        {
            if (emotePropsData == null || StartOfRound.Instance?.levels == null)
            {
                Plugin.LogError("Error adding grabbable emote props to moons.");
                return;
            }

            foreach (var emotePropData in emotePropsData)
            {
                foreach (var emote in emotePropData.parentEmotes)
                {
                    emote.purchasable = false;
                    emote.requiresHeldProp = true;
                }
                foreach (var level in StartOfRound.Instance.levels)
                {
                    if (!level.spawnableScrap.Contains(emotePropData.itemRarityData))
                        level.spawnableScrap.Add(emotePropData.itemRarityData);
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


        public static PropObject LoadEmoteProp(string propName)
        {
            if (!emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                Plugin.LogError("Failed to instantiate emote prop: " + propName + ". Prop does not exist!");
                return null;
            }

            if (!propPoolsDict.TryGetValue(propName, out var pool))
            {
                pool = new HashSet<PropObject>();
                propPoolsDict.Add(propName, pool);
            }

            foreach (var prop in pool)
            {
                if (!prop.active)
                {
                    prop.active = true;
                    return prop;
                }
            }

            GameObject newProp = GameObject.Instantiate(propData.propPrefab);
            newProp.name = propData.propPrefab.name + ".animation_prop";

            
            var grabbablePropObject = newProp.GetComponent<GrabbablePropObject>();
            if (grabbablePropObject != null)
            {
                if (grabbablePropObject.sfxAudioSource)
                    GameObject.Destroy(grabbablePropObject.sfxAudioSource);
            }
            GameObject.DestroyImmediate(newProp.GetComponent<GrabbablePropObject>());
            GameObject.DestroyImmediate(newProp.GetComponent<NetworkObject>());
            GameObject.Destroy(newProp.GetComponent<ScanNodeProperties>());
            GameObject.Destroy(newProp.GetComponent<Collider>());

            PropObject propObject = newProp.GetComponentInChildren<PropObject>();
            if (propObject == null)
                propObject = newProp.AddComponent<PropObject>();

            propObject.active = true;
            pool.Add(propObject);
            propObject.InitializeEmoteProp();

            return propObject;
        }


        internal static void CleanPropPools()
        {
            foreach (var pool in propPoolsDict.Values)
            {
                pool.RemoveWhere(prop => prop == null);
            }
        }


        internal static void DestroyAllProps()
        {
            foreach (var pool in propPoolsDict.Values)
            {
                foreach (var prop in pool)
                    GameObject.DestroyImmediate(prop);
                pool.Clear();
            }
        }
    }
}