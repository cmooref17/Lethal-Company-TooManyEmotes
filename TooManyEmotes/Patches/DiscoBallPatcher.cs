using GameNetcodeStuff;
using HarmonyLib;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using UnityEngine;
using System.Collections.Generic;
using TooManyEmotes.Audio;
using TooManyEmotes.UI;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class DiscoBallPatcher
    {
        public static Transform discoBallTransform;
        public static AudioSource audioSource;
        private static bool prevIsMuted = false;
        private static bool isMuted = false;

        private static float muteEmoteDistance = 20;

        private static HashSet<EmoteController> nearbyPerformingEmoteControllers = new HashSet<EmoteController>();


        [HarmonyPatch(typeof(AutoParentToShip), "Awake")]
        [HarmonyPostfix]
        private static void InitDiscoBall(AutoParentToShip __instance)
        {
            if (!__instance.name.ToLower().StartsWith("discoball"))
                return;

            if (discoBallTransform != null)
            {
                if (audioSource != null)
                    return;
            }

            var gameObject = discoBallTransform ? discoBallTransform.gameObject : __instance.gameObject;
            discoBallTransform = null;
            audioSource = null;
            isMuted = false;
            nearbyPerformingEmoteControllers.Clear();

            audioSource = gameObject.GetComponentInChildren<AudioSource>();
            if (audioSource)
            {
                discoBallTransform = gameObject.transform;
                Log("Found disco ball.");
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "LateUpdate")]
        [HarmonyPostfix]
        private static void CheckForStateUpdates(StartOfRound __instance)
        {
            if (!discoBallTransform)
                return;

            foreach (var emoteSyncGroup in EmoteSyncGroup.allEmoteSyncGroups.Values)
            {
                if (emoteSyncGroup.useAudio && emoteSyncGroup.performingEmote != null && emoteSyncGroup.performingEmote.hasAudio && emoteSyncGroup.syncGroup != null && emoteSyncGroup.syncGroup.Count > 0)
                {
                    foreach (var emoteController in emoteSyncGroup.syncGroup)
                    {
                        if (nearbyPerformingEmoteControllers.Contains(emoteController))
                            continue;

                        float distance = Vector3.Distance(emoteController.transform.position, discoBallTransform.position);
                        if (distance < muteEmoteDistance)
                            OnPerformEmote(emoteController);
                    }
                }
            }

            if (nearbyPerformingEmoteControllers.Count > 0)
            {
                HashSet<EmoteController> elementsToRemove = null;
                foreach (var emoteController in nearbyPerformingEmoteControllers)
                {
                    float distance = Vector3.Distance(emoteController.transform.position, discoBallTransform.position);
                    if (distance >= muteEmoteDistance)
                    {
                        if (elementsToRemove == null)
                            elementsToRemove = new HashSet<EmoteController>();
                        elementsToRemove.Add(emoteController);
                    }
                }
                if (elementsToRemove != null)
                {
                    foreach (var removeElement in elementsToRemove)
                        OnStopPerformingEmote(removeElement);
                }
            }
        }


        internal static void OnPerformEmote(EmoteController emoteController)
        {
            if (!discoBallTransform || !emoteController)
                return;

            if (emoteController.IsPerformingCustomEmote() && emoteController.performingEmote.hasAudio)
            {
                if (emoteController.emoteSyncGroup != null && emoteController.emoteSyncGroup.useAudio)
                {
                    if (Vector3.Distance(emoteController.transform.position, discoBallTransform.position) < muteEmoteDistance)
                    {
                        nearbyPerformingEmoteControllers.Add(emoteController);
                        if (!isMuted && nearbyPerformingEmoteControllers.Count > 0)
                            MuteDiscoBall();
                    }
                }
            }
        }


        internal static void OnStopPerformingEmote(EmoteController emoteController)
        {
            if (!nearbyPerformingEmoteControllers.Contains(emoteController))
                return;

            nearbyPerformingEmoteControllers.Remove(emoteController);
            if (discoBallTransform && nearbyPerformingEmoteControllers.Count <= 0 && isMuted)
                UnmuteDiscoBall();
        }


        internal static void OnUpdateMuteEmotes()
        {
            if (!audioSource || !isMuted)
                return;

            if (AudioManager.muteEmoteAudio == audioSource.mute)
                audioSource.mute = AudioManager.muteEmoteAudio ? prevIsMuted : true;
        }


        private static void MuteDiscoBall(bool mute = true)
        {
            if (!discoBallTransform || !audioSource || isMuted == mute)
                return;

            if (mute)
            {
                prevIsMuted = audioSource.mute;
                if (!AudioManager.muteEmoteAudio)
                    audioSource.mute = true;
            }
            else
                audioSource.mute = prevIsMuted;

            isMuted = mute;
            Log("Updating disco ball mute value to: " + mute);
        }
        private static void UnmuteDiscoBall() => MuteDiscoBall(false);
    }
}
