using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public static class EmotePropManager
    {
        public static Dictionary<string, HashSet<GameObject>> propPoolsDict = new Dictionary<string, HashSet<GameObject>>();

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void Init()
        {
            CleanPropPools();
        }

        // Returns a cached emote prop that is currently disabled, or instantiates a new one
        public static GameObject GetEmoteProp(string emoteName)
        {
            if (propPoolsDict.TryGetValue(emoteName, out var pool))
            {
                foreach (var prop in pool)
                {
                    if (!prop.activeSelf)
                        return prop;
                }
            }
            else
                propPoolsDict.Add(emoteName, new HashSet<GameObject>());

            if (!Plugin.emotePropPrefabs.TryGetValue(emoteName, out var prefab))
            {
                Plugin.LogError("Failed to instantiate emote prop for emote: " + emoteName);
                return null;
            }

            GameObject newProp = GameObject.Instantiate(prefab);
            newProp.name = prefab.name;
            newProp.SetActive(false);
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
    }
}
