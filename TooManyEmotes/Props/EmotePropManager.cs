using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Props
{
    [HarmonyPatch]
    public static class EmotePropManager
    {
        public static bool initialized = false;
        public static Dictionary<string, GameObject> propPrefabs = new Dictionary<string, GameObject>();

        public static Transform propPoolParent;
        public static Dictionary<string, HashSet<PropObject>> propPoolsDict = new Dictionary<string, HashSet<PropObject>>();

        //public static Dictionary<string, RuntimeAnimatorController> propAnimatorControllers = new Dictionary<string, RuntimeAnimatorController>();
        //public static Dictionary<string, PropAnimationData> propAnimationData = new Dictionary<string, PropAnimationData>();


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void Init()
        {
            CleanPropPools();
            propPoolParent = new GameObject("EmotePropPool").transform;
        }


        public static void BuildEmotePropList()
        {
            if (Plugin.emotePropPrefabs == null)
                return;

            CleanPropPools();
            propPrefabs.Clear();

            if (EmotesManager.allUnlockableEmotes == null || EmotesManager.allUnlockableEmotes.Count <= 0)
            {
                Plugin.LogError("Failed to build emote prop list. Make sure you build the emote prop list after building the unlockable emotes list.");
                return;
            }

            foreach (var propPrefab in Plugin.emotePropPrefabs)
            {
                if (propPrefab == null)
                    continue;

                propPrefabs.Add(propPrefab.name, propPrefab);
                string emoteName = propPrefab.name;
                if (propPrefab.name.Contains("."))
                {
                    var args = propPrefab.name.Split('.');
                    if (args.Length > 0 && args[0].Length > 0)
                        emoteName = args[0];
                }
                if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                {
                    if (emote.propNamesInEmote == null)
                        emote.propNamesInEmote = new List<string>();
                    emote.propNamesInEmote.Add(propPrefab.name);
                }
            }
            initialized = true;

            /*
            foreach (var propAC in Plugin.emotePropAnimatorControllers)
            {
                if (propAC == null)
                    continue;

                string propName = propAC.name.ToLower();
                if (!propName.StartsWith("prop_") || !propName.Contains("_emote_"))
                    continue;

                propName = propName.Substring(5, propName.IndexOf("_emote_"));
                int indexEmoteName = propName.IndexOf("_emote_");
                string emoteName = propName.Substring(indexEmoteName + 7);
                propName = propName.Substring(0, indexEmoteName);


                if (propPrefabs.TryGetValue(propName, out var propPrefab) && EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                {
                    var emotePropAnimation = new PropAnimationData()
                    {
                        parentEmote = emote,
                        prefab = propPrefab,
                        animatorController = propAC
                    };
                    emote.AddPropAnimationData(emotePropAnimation);
                }
            }
            */
        }


        public static PropObject LoadEmoteProp(string propName)
        {
            if (!propPrefabs.TryGetValue(propName, out var prefab))
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

            GameObject newProp = GameObject.Instantiate(prefab);
            newProp.name = prefab.name;

            PropObject propObject = newProp.GetComponentInChildren<PropObject>();
            if (propObject == null)
                propObject = newProp.AddComponent<PropObject>();

            propObject.active = true;
            pool.Add(propObject);
            propObject.InitializeEmoteProp();

            return propObject;
        }


        public static void CleanPropPools()
        {
            foreach (var pool in propPoolsDict.Values)
            {
                pool.RemoveWhere(prop => prop == null);
            }
        }


        public static void DestroyAllProps()
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