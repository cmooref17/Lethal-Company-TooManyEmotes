using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Networking;
using UnityEngine;

namespace TooManyEmotes.Audio
{
    public static class EmoteAudioPlayerManager
    {
        public static List<EmoteAudioPlayer> allEmoteAudioPlayers = new List<EmoteAudioPlayer>();
        static EmoteAudioPlayer shipSpeakerAudioPlayer = null;

        public static float requiredEmoteRange = 10f;
        public static float maxEmoteRange = 20f;


        [HarmonyPatch(typeof(BoomboxItem), "Start")]
        [HarmonyPrefix]
        public static void AttachCustomScript(BoomboxItem __instance)
        {
            allEmoteAudioPlayers.Add(__instance.gameObject.AddComponent<EmoteAudioPlayer>());
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        public static void AttachCustomScript(StartOfRound __instance)
        {
            if (ConfigSync.isSynced && shipSpeakerAudioPlayer == null && !ConfigSync.instance.syncDisableAudioShipSpeaker)
                InitializeShipSpeakerAudioPlayer();
        }


        public static void InitializeShipSpeakerAudioPlayer()
        {
            shipSpeakerAudioPlayer = StartOfRound.Instance.speakerAudioSource.gameObject.AddComponent<EmoteAudioPlayer>();
            allEmoteAudioPlayers.Add(shipSpeakerAudioPlayer);
        }


        public static EmoteAudioPlayer GetNearestEmoteAudioPlayer(Transform transform, bool onlyAvailableEmoteAudioPlayers = false)
        {
            float distance = requiredEmoteRange;
            EmoteAudioPlayer nearestEmoteAudioPlayer = null;

            foreach (var emoteAudioPlayer in allEmoteAudioPlayers)
            {
                float dist = Vector3.Distance(transform.position, emoteAudioPlayer.transform.position);
                if (dist < distance && (!onlyAvailableEmoteAudioPlayers || emoteAudioPlayer.CanPlayMusic()))
                {
                    distance = dist;
                    nearestEmoteAudioPlayer = emoteAudioPlayer;
                }
            }

            return nearestEmoteAudioPlayer;
        }
        //public static EmoteBoombox GetNearestAvailableBoombox(Transform transform) => GetNearestBoombox(transform, true);



    }
}
