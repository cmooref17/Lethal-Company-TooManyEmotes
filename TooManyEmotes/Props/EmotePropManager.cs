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
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Props
{
    [HarmonyPatch]
    internal static class EmotePropManager
    {
        public static AssetBundle propAssetBundle;
        
        public static List<EmotePropData> emotePropsData = new List<EmotePropData>();
        public static Dictionary<string, EmotePropData> emotePropsDataDict = new Dictionary<string, EmotePropData>();

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
                Log("Loaded " + emotePropsData.Count + " emote props.");
            }
            catch
            {
                LogError("Failed to load emotes props asset bundle: emote_props.");
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
                LogError("Failed to build emote prop list. Make sure you build the emote prop list after building the unlockable emotes list.");
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


        public static PropObject LoadEmoteProp(string propName)
        {
            if (!emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                LogError("Failed to instantiate emote prop: " + propName + ". Prop does not exist!");
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