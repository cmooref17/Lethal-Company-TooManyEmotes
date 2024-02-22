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
        public static Transform propPoolParent;
        //public static HashSet<EmotePropAnimation> emoteProps = new HashSet<EmotePropAnimation>();
        public static Dictionary<string, HashSet<GameObject>> propPoolsDict = new Dictionary<string, HashSet<GameObject>>();

        public static Dictionary<string, GameObject> propPrefabs = new Dictionary<string, GameObject>();
        public static Dictionary<string, RuntimeAnimatorController> propAnimatorControllers = new Dictionary<string, RuntimeAnimatorController>();
        public static Dictionary<string, PropAnimationData> propAnimationData = new Dictionary<string, PropAnimationData>();

        /*
        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void Init()
        {
            CleanPropPools();
            propPoolParent = new GameObject("EmotePropPool").transform;
        }


        public static void BuildEmotePropList()
        {
            if (Plugin.emotePropPrefabs == null || Plugin.emotePropAnimatorControllers == null)
                return;

            CleanPropPools();
            propPrefabs.Clear();
            propAnimatorControllers.Clear();
            propAnimationData.Clear();

            if (EmotesManager.allUnlockableEmotes == null || EmotesManager.allUnlockableEmotes.Count <= 0)
            {
                Plugin.LogError("Failed to build emote prop list. Make sure you build the emote prop list after building the unlockable emotes list.");
                return;
            }

            foreach (var propPrefab in Plugin.emotePropPrefabs)
            {
                if (propPrefab == null || !propPrefab.name.ToLower().StartsWith("prop_"))
                    continue;

                string propName = propPrefab.name.Substring(5);
                propPrefabs.Add(propName, propPrefab);
            }

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
        }


        public static GameObject LoadEmoteProp(string emoteName)
        {
            if (propPoolsDict.TryGetValue(emoteName, out var pool))
            {
                foreach (var prop in pool)
                {
                    if (!prop.activeSelf)
                    {
                        prop.SetActive(true);
                        return prop;
                    }
                }
            }
            else
                propPoolsDict.Add(emoteName, new HashSet<GameObject>());

            if (!propPrefabs.TryGetValue(emoteName, out var prefab))
            {
                Plugin.LogError("Failed to instantiate emote prop for emote: " + emoteName);
                return null;
            }

            GameObject newProp = GameObject.Instantiate(prefab);
            PropObject propObject = newProp.GetComponentInChildren<PropObject>();
            if (propObject == null)
                propObject = newProp.AddComponent<PropObject>();
            propObject.InitializeEmoteProp();

            newProp.name = prefab.name;
            return newProp;
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
        */
    }
}