using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Audio
{
    public static class BoomboxManager
    {
        public static List<EmoteBoombox> allBoomboxes = new List<EmoteBoombox>();

        public static float requiredEmoteRange = 10f;
        public static float maxEmoteRange = 20f;


        [HarmonyPatch(typeof(BoomboxItem), "Start")]
        [HarmonyPrefix]
        public static void AttachCustomScript(BoomboxItem __instance)
        {
            allBoomboxes.Add(__instance.gameObject.AddComponent<EmoteBoombox>());
        }


        public static EmoteBoombox GetNearestBoombox(Transform transform, bool onlyAvailableBoomboxes = false)
        {
            float distance = requiredEmoteRange;
            EmoteBoombox nearestBoombox = null;

            foreach (var boombox in allBoomboxes)
            {
                float dist = Vector3.Distance(transform.position, boombox.transform.position);
                if (dist < distance && (!onlyAvailableBoomboxes || boombox.CanPlayMusic()))
                {
                    distance = dist;
                    nearestBoombox = boombox;
                }
            }

            return nearestBoombox;
        }
        public static EmoteBoombox GetNearestAvailableBoombox(Transform transform) => GetNearestBoombox(transform, true);



    }
}
