using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public class EmoteSyncGroup
    {
        public static int currentEmoteSyncId = 0;
        public static Dictionary<int, EmoteSyncGroup> allEmoteSyncGroups = new Dictionary<int, EmoteSyncGroup>();

        public int syncId = -1;
        public List<EmoteController> syncGroup;
        public EmoteController leadEmoteController { get { return syncGroup != null && syncGroup.Count > 0 ? syncGroup[0] : null; } }
        public Dictionary<UnlockableEmote, EmoteController> leadEmoteControllerByEmote = new Dictionary<UnlockableEmote, EmoteController>();
        
        public EmoteAudioSource leadEmoteAudioSource { get { if (currentEmoteAudioSources != null && currentEmoteAudioSources.Count > 0) { foreach (var emoteController in syncGroup) { if (emoteController?.personalEmoteAudioSource != null && currentEmoteAudioSources.ContainsValue(emoteController.personalEmoteAudioSource)) return emoteController.personalEmoteAudioSource; } } return null; } }
        public Dictionary<UnlockableEmote, EmoteAudioSource> currentEmoteAudioSources;

        public float timeStartedEmote;
        public UnlockableEmote performingEmote;
        public bool useAudio = true;
        public EmoteAudioPlayer currentAudioPlayer;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void Init()
        {
            currentEmoteSyncId = 0;
            allEmoteSyncGroups?.Clear();
        }


        public static EmoteSyncGroup CreateEmoteSyncGroup(EmoteController emoteController, bool useAudio = true)
        {
            var emoteSyncGroup = new EmoteSyncGroup(emoteController, useAudio);
            return emoteSyncGroup;
        }


        public EmoteSyncGroup(EmoteController emoteController, bool useAudio = true)
        {
            syncGroup = new List<EmoteController> { emoteController };
            syncId = currentEmoteSyncId++;
            performingEmote = emoteController.performingEmote;
            leadEmoteControllerByEmote.Add(performingEmote, emoteController);
            this.useAudio = useAudio;

            if (useAudio && performingEmote.hasAudio)
            {
                if (!performingEmote.isBoomboxAudio)
                {
                    currentEmoteAudioSources = new Dictionary<UnlockableEmote, EmoteAudioSource>();
                    if (emoteController.personalEmoteAudioSource == null)
                    {
                        LogError("Attempted to perform emote with personal audio source, which is null.");
                        this.useAudio = false;
                    }
                    else
                    {
                        currentEmoteAudioSources[performingEmote] = emoteController.personalEmoteAudioSource;
                        emoteController.personalEmoteAudioSource.SyncWithEmoteControllerAudio(emoteController);
                        emoteController.personalEmoteAudioSource.AddToEmoteSyncGroup(this);
                    }
                }
                else
                {
                    var newAudioPlayer = EmoteAudioPlayerManager.GetNearestEmoteAudioPlayer(emoteController.transform);
                    if (newAudioPlayer != null && newAudioPlayer.CanPlayMusic())
                        AssignExternalAudioPlayer(newAudioPlayer);
                    else
                        LogWarning("Performing emote with no music. No available boomboxes found nearby. Don't worry. Everything will be okay.");
                }
                UpdateAudioVolume();
            }
            allEmoteSyncGroups[syncId] = this;
            timeStartedEmote = Time.time;
        }


        public void AddToEmoteSyncGroup(EmoteController emoteController)
        {
            if (syncGroup != null && !syncGroup.Contains(emoteController))
            {
                syncGroup.Add(emoteController);
                if (!leadEmoteControllerByEmote.ContainsKey(emoteController.performingEmote) || leadEmoteControllerByEmote[emoteController.performingEmote] == null)
                    leadEmoteControllerByEmote[emoteController.performingEmote] = emoteController;
                if (useAudio && performingEmote.hasAudio)
                {
                    if (!performingEmote.isBoomboxAudio)
                    {
                        if (currentEmoteAudioSources != null && (!currentEmoteAudioSources.ContainsKey(emoteController.performingEmote) || currentEmoteAudioSources[emoteController.performingEmote] == null))
                        {
                            currentEmoteAudioSources[emoteController.performingEmote] = emoteController.personalEmoteAudioSource;
                            emoteController.personalEmoteAudioSource.SyncWithEmoteSyncGroup(this, emoteController);
                            emoteController.personalEmoteAudioSource.AddToEmoteSyncGroup(this);
                        }
                    }
                    else if (currentAudioPlayer == null)
                    {
                        var newAudioPlayer = EmoteAudioPlayerManager.GetNearestEmoteAudioPlayer(emoteController.transform);
                        if (newAudioPlayer != null && newAudioPlayer.CanPlayMusic())
                            AssignExternalAudioPlayer(newAudioPlayer);
                    }
                    UpdateAudioVolume();
                }
            }
        }


        public void RemoveFromEmoteSyncGroup(EmoteController emoteController)
        {
            if (syncGroup != null)
            {
                syncGroup.Remove(emoteController);
                if (syncGroup.Count <= 0)
                {
                    DestroyEmoteSyncGroup();
                    return;
                }

                if (leadEmoteControllerByEmote.ContainsValue(emoteController))
                {
                    var emote = leadEmoteControllerByEmote.FirstOrDefault(x => x.Value == emoteController).Key;
                    leadEmoteControllerByEmote.Remove(emote);

                    // Replace lead emote controller for that emote if possible
                    foreach (var otherEmoteController in syncGroup)
                    {
                        if (otherEmoteController == null || otherEmoteController.performingEmote == null)
                            continue;
                        if (otherEmoteController.performingEmote == emote)
                        {
                            leadEmoteControllerByEmote[emote] = otherEmoteController;
                            break;
                        }
                    }
                }
                
                if (currentEmoteAudioSources != null)
                {
                    if (currentEmoteAudioSources.ContainsValue(emoteController.personalEmoteAudioSource))
                    {
                        emoteController.personalEmoteAudioSource.StopAudio();
                        emoteController.personalEmoteAudioSource.RemoveFromEmoteSyncGroup();

                        var emote = currentEmoteAudioSources.FirstOrDefault(x => x.Value == emoteController.personalEmoteAudioSource).Key;
                        currentEmoteAudioSources.Remove(emote);

                        if (useAudio && emote.hasAudio && !emote.isBoomboxAudio) // just double checking
                        {
                            // Replace emote audio source for that emote if possible
                            foreach (var otherEmoteController in syncGroup)
                            {
                                if (otherEmoteController == null || otherEmoteController.performingEmote == null || otherEmoteController == emoteController)
                                    continue;

                                var emoteAudioSource = otherEmoteController.personalEmoteAudioSource;
                                if (otherEmoteController.performingEmote == emote && emoteAudioSource != null && emoteAudioSource.CanPlayMusic())
                                {
                                    currentEmoteAudioSources[emote] = emoteAudioSource;
                                    emoteAudioSource.SyncWithEmoteControllerAudio(otherEmoteController);
                                    break;
                                }
                            }
                        }
                    }
                    UpdateAudioVolume();
                }
            }
        }


        public void DestroyEmoteSyncGroup()
        {
            LogVerbose("Cleaning up emote sync group with id: " + syncId);
            if (currentEmoteAudioSources != null)
            {
                foreach (var emoteAudioSource in currentEmoteAudioSources.Values)
                {
                    if (emoteAudioSource != null)
                    {
                        emoteAudioSource.StopAudio();
                        emoteAudioSource.RemoveFromEmoteSyncGroup();
                    }
                }
                currentEmoteAudioSources = null;
            }
            if (currentAudioPlayer != null)
            {
                currentAudioPlayer.StopAudio();
                currentAudioPlayer.RemoveFromEmoteSyncGroup();
            }
            allEmoteSyncGroups.Remove(syncId);
            syncGroup = null;
            syncId = -1;
        }


        public void UpdateAudioVolume()
        {
            if (currentEmoteAudioSources != null)
            {
                foreach (var emoteAudioSource in currentEmoteAudioSources.Values)
                {
                    if (emoteAudioSource != null)
                        emoteAudioSource.UpdateVolume();
                }
            }
            if (currentAudioPlayer != null)
                currentAudioPlayer.UpdateVolume();
        }


        public void AssignExternalAudioPlayer(EmoteAudioPlayer audioPlayer)
        {
            if (currentAudioPlayer != null)
            {
                currentAudioPlayer.StopAudio();
                currentAudioPlayer.RemoveFromEmoteSyncGroup();
                currentAudioPlayer = null;
            }
            if (useAudio && performingEmote.hasAudio && performingEmote.isBoomboxAudio)
            {
                currentAudioPlayer = audioPlayer;
                if (currentAudioPlayer != null)
                {
                    currentAudioPlayer.SyncWithEmoteControllerAudio(leadEmoteController);
                    currentAudioPlayer.AddToEmoteSyncGroup(this);
                    currentAudioPlayer.UpdateVolume();
                }
            }
        }
    }
}
