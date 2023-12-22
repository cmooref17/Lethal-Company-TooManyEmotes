using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    public static class BoomboxMusicPlayer
    {
        public static List<BoomboxItem> allBoomboxes = new List<BoomboxItem>();

        [HarmonyPatch(typeof(BoomboxItem), "Start")]
        [HarmonyPostfix]
        public static void AddBoomboxToPool(BoomboxItem __instance)
        {
            //allBoomboxes.Add(__instance);
        }


        // Likely won't be used for much emotes (or any) for copyright reasons. This was mainly a test method
        public static void OnPlayEmoteWithMusic(UnlockableEmote emote, PlayerControllerB playerController)
        {
            if (!Plugin.musicClips.ContainsKey(emote.emoteName))
                return;

            BoomboxItem boombox = GetNearestBoombox(playerController);
            if (boombox == null || (boombox.isPlayingMusic && Plugin.musicClips.ContainsKey(boombox.boomboxAudio.clip.name)))
                return;

            AudioClip musicClip = Plugin.musicClips[emote.emoteName];
            boombox.boomboxAudio.PlayOneShot(musicClip);
        }


        public static BoomboxItem GetNearestBoombox(PlayerControllerB playerController)
        {
            BoomboxItem nearestBoombox = null;
            float nearestDistance = 10;
            foreach (var boombox in allBoomboxes)
            {
                float distance = Vector3.Distance(playerController.transform.position, boombox.transform.position);
                if (boombox != null && distance < nearestDistance)
                {
                    nearestBoombox = boombox;
                    nearestDistance = distance;
                }
            }
            return nearestBoombox;
        }
    }
}
