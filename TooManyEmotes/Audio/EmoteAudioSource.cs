﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.UI;
using UnityEngine;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Audio
{
    public class EmoteAudioSource : MonoBehaviour
    {
        public static HashSet<EmoteAudioSource> allEmoteAudioSources = new HashSet<EmoteAudioSource>();
        public static HashSet<EmoteAudioSource> playingAudioSources = new HashSet<EmoteAudioSource>();
        public bool isPlayingAudio
        {
            get { return playingAudioSources.Contains(this); }
            set { if (value) playingAudioSources.Add(this); else playingAudioSources.Remove(this); }
        }
        public AudioSource audioSource;
        public AudioSource audioLoopSource;
        private float playAudioLoopSourceAtTime = 0;

        public EmoteSyncGroup currentEmoteSyncGroup;
        public UnlockableEmote currentEmote { get { return currentEmoteSyncGroup?.performingEmote; } }

        protected virtual void Awake()
        {
            allEmoteAudioSources.Add(this);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioLoopSource = gameObject.AddComponent<AudioSource>();

            ResetAudioSettings();
        }


        protected virtual void ResetAudioSettings()
        {
            audioSource.volume = ConfigSettings.baseEmoteAudioVolume.Value;
            audioSource.dopplerLevel = 0;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = ConfigSettings.emoteAudioMinDistance.Value;
            audioSource.maxDistance = ConfigSettings.emoteAudioMaxDistance.Value;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.loop = false;
            audioSource.playOnAwake = false;

            audioLoopSource.volume = ConfigSettings.baseEmoteAudioVolume.Value;
            audioLoopSource.dopplerLevel = 0;
            audioLoopSource.spatialBlend = 1.0f;
            audioLoopSource.minDistance = ConfigSettings.emoteAudioMinDistance.Value;
            audioLoopSource.maxDistance = ConfigSettings.emoteAudioMaxDistance.Value;
            audioLoopSource.rolloffMode = AudioRolloffMode.Linear;
            audioLoopSource.loop = true;
            audioLoopSource.playOnAwake = false;

            UpdateVolume();
        }


        [HarmonyPatch(typeof(QuickMenuManager), "CloseQuickMenu")]
        [HarmonyPostfix]
        private static void OnCloseQuickMenu()
        {
            foreach (var emoteAudioSource in allEmoteAudioSources)
                emoteAudioSource.ResetAudioSettings();
        }


        protected virtual void OnDestroy()
        {
            StopAudio();
            allEmoteAudioSources.Remove(this);
            if (currentEmoteSyncGroup != null)
                RemoveFromEmoteSyncGroup();
        }


        protected virtual void OnEnable() { }
        protected virtual void OnDisable()
        {
            StopAudio();
            if (currentEmoteSyncGroup != null)
                RemoveFromEmoteSyncGroup();
        }


        protected virtual void Update()
        {
            if (isPlayingAudio && CheckIfShouldStopAudio())
                StopAudio();
        }


        public virtual bool CanPlayMusic() => !isPlayingAudio;


        public virtual bool CheckIfShouldStopAudio() => false;
        //public virtual bool CheckIfShouldStopAudio() => isPlayingAudio && isActiveAndEnabled && (currentEmoteSyncGroup == null || currentEmoteSyncGroup.performingEmote == null || currentEmoteSyncGroup.currentEmoteAudioSources == null || currentEmoteSyncGroup.currentEmoteAudioSources.Count <= 0);


        public virtual bool SyncWithEmoteControllerAudio(EmoteController emoteController)
        {
            if (emoteController == null || !emoteController.IsPerformingCustomEmote())
                return false;

            isPlayingAudio = false;
            audioSource.Stop();
            audioLoopSource.Stop();
            audioSource.mute = false;
            audioLoopSource.mute = false;

            var emote = emoteController.performingEmote;

            bool success = SetAudioFromEmote(emote);
            if (success)
            {
                var currentAnimationClip = emoteController.GetCurrentAnimationClip();
                float playAtTime = emoteController.currentAnimationTime;
                
                if (currentAnimationClip == emote.transitionsToClip || currentAnimationClip.isLooping)
                {
                    if (audioLoopSource.clip != null)
                    {
                        audioLoopSource.time = playAtTime;
                        playAudioLoopSourceAtTime = Time.time - audioLoopSource.time;
                        audioLoopSource.Play();
                    }
                }
                else
                {
                    if (audioSource.clip != null)
                    {
                        float dTime = 0;
                        if (emote.transitionsToClip != null) // sync end of audio clip with start of loop animation (if exists)
                            dTime = currentAnimationClip.length - playAtTime - audioSource.clip.length;

                        if (dTime > 0)
                        {
                            audioSource.time = 0;
                            audioSource.PlayScheduled(AudioSettings.dspTime + dTime);
                        }
                        else
                        {
                            audioSource.time = -dTime;
                            audioSource.Play();
                        }
                    }
                    if (emote.transitionsToClip != null)
                    {
                        audioLoopSource.time = 0;
                        float dTime = audioSource.clip != null ? currentAnimationClip.length - emoteController.currentAnimationTime : currentAnimationClip.length - emoteController.currentAnimationTime;
                        playAudioLoopSourceAtTime = Time.time + dTime;
                        audioLoopSource.PlayScheduled(AudioSettings.dspTime + dTime);
                    }
                }

                isPlayingAudio = true;
                return true;
            }
            return false;
        }


        public virtual bool SyncWithEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup, EmoteController playAudioFromEmoteController = null)
        {
            if (emoteSyncGroup == null || emoteSyncGroup.performingEmote == null || emoteSyncGroup.leadEmoteController == null || !emoteSyncGroup.leadEmoteController.IsPerformingCustomEmote())
                return false;

            var emoteController = emoteSyncGroup.leadEmoteController;
            var emote = playAudioFromEmoteController != null ? playAudioFromEmoteController.performingEmote : emoteController.performingEmote;

            var referenceEmoteAudioSource = emoteSyncGroup.leadEmoteAudioSource;

            if (referenceEmoteAudioSource != null)
            {
                bool audioSourceIsPlaying = referenceEmoteAudioSource.audioSource.isPlaying;
                bool audioLoopSourceIsPlaying = referenceEmoteAudioSource.audioLoopSource.isPlaying;
                float audioSourceTime = referenceEmoteAudioSource.audioSource.time;
                float audioLoopSourceTime = referenceEmoteAudioSource.audioLoopSource.time;

                isPlayingAudio = false;
                audioSource.Stop();
                audioLoopSource.Stop();
                audioSource.mute = false;
                audioLoopSource.mute = false;

                bool success = SetAudioFromEmote(emote);
                if (success)
                {
                    audioSource.time = audioSourceTime;
                    audioLoopSource.time = audioLoopSourceTime;
                    if (audioSource.clip != null && audioSourceIsPlaying)
                    {
                        audioSource.Play();
                        if (audioLoopSource.clip != null && referenceEmoteAudioSource.playAudioLoopSourceAtTime > Time.time)
                        {
                            playAudioLoopSourceAtTime = referenceEmoteAudioSource.playAudioLoopSourceAtTime;
                            audioLoopSource.PlayScheduled(AudioSettings.dspTime + (audioSource.clip.length - audioSource.time));
                        }
                    }
                    else if (audioLoopSource.clip != null && audioLoopSourceIsPlaying)
                    {
                        audioLoopSource.Play();
                    }

                    isPlayingAudio = true;
                    return true;
                }
            }
            return false;
        }


        public virtual bool SyncWithEmoteController(UnlockableEmote emote, EmoteController playAudioFromEmoteController)
        {
            if (emote == null || playAudioFromEmoteController == null || !playAudioFromEmoteController.IsPerformingCustomEmote())
                return false;

            isPlayingAudio = false;
            audioSource.Stop();
            audioLoopSource.Stop();
            audioSource.mute = false;
            audioLoopSource.mute = false;

            bool success = SetAudioFromEmote(emote);
            if (success)
            {
                var currentAnimationClip = playAudioFromEmoteController.GetCurrentAnimationClip();
                float playAtTime = playAudioFromEmoteController.currentAnimationTime;

                if (currentAnimationClip == emote.transitionsToClip || currentAnimationClip.isLooping)
                {
                    if (audioLoopSource.clip != null)
                    {
                        audioLoopSource.time = playAtTime;
                        playAudioLoopSourceAtTime = Time.time - audioLoopSource.time;
                        audioLoopSource.Play();
                    }
                }
                else
                {
                    if (audioSource.clip != null)
                    {
                        float dTime = 0;
                        if (emote.transitionsToClip != null) // sync end of audio clip with start of loop animation (if exists)
                            dTime = currentAnimationClip.length - playAtTime - audioSource.clip.length;

                        if (dTime > 0)
                        {
                            audioSource.time = 0;
                            audioSource.PlayScheduled(AudioSettings.dspTime + dTime);
                        }
                        else
                        {
                            audioSource.time = -dTime;
                            audioSource.Play();
                        }
                    }
                    if (emote.transitionsToClip != null)
                    {
                        audioLoopSource.time = 0;
                        float dTime = audioSource.clip != null ? currentAnimationClip.length - playAudioFromEmoteController.currentAnimationTime : currentAnimationClip.length - playAudioFromEmoteController.currentAnimationTime;
                        playAudioLoopSourceAtTime = Time.time + dTime;
                        audioLoopSource.PlayScheduled(AudioSettings.dspTime + dTime);
                    }
                }

                isPlayingAudio = true;
                return true;
            }
            return false;
        }


        public virtual void RefreshAudio()
        {
            //if (!isPlayingAudio) return;
            if (currentEmoteSyncGroup != null && currentEmote != null)
            {
                SetAudioFromEmote(currentEmote);
                SyncWithEmoteControllerAudio(currentEmoteSyncGroup.leadEmoteController);
            }
        }


        protected virtual bool SetAudioFromEmote(UnlockableEmote emote)
        {
            audioSource.clip = null;
            audioSource.mute = AudioManager.muteEmoteAudio || (AudioManager.dmcaFreeMode && audioSource.clip != null && AudioManager.IsClipDMCA(audioSource.clip));
            audioSource.time = 0;
            audioLoopSource.loop = false;

            audioLoopSource.clip = null;
            audioLoopSource.mute = audioSource.mute;
            audioLoopSource.time = 0;
            audioLoopSource.loop = true;

            if (emote != null && emote.hasAudio)
            {
                var audioClip = emote.LoadAudioClip();
                var audioLoopClip = emote.LoadAudioLoopClip();

                if (audioLoopClip != null)
                {
                    audioSource.clip = audioClip;
                    audioLoopSource.clip = audioLoopClip;
                }
                else if (audioClip != null)
                {
                    if (emote.animationClip.isLooping)
                        audioLoopSource.clip = audioClip;
                    else
                        audioSource.clip = audioClip;
                }
                else
                    return false;

                return true;
            }

            return false;
        }


        public virtual void StopAudio()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
            if (audioLoopSource != null)
            {
                audioLoopSource.Stop();
                audioLoopSource.clip = null;
            }
            isPlayingAudio = false;
        }


        public void UpdateVolume()
        {
            float volume = AudioManager.emoteVolumeMultiplier * (ConfigSettings.baseEmoteAudioVolume.Value + ((currentEmoteSyncGroup?.syncGroup != null && currentEmoteSyncGroup?.syncGroup.Count > 0) ? ConfigSettings.emoteAudioIncreasePerPlayerSyncing.Value * (currentEmoteSyncGroup.syncGroup.Count - 1) : 0));
            volume = Mathf.Min(volume, ConfigSettings.emoteAudioMaxVolume.Value);
            if (audioSource != null)
                audioSource.volume = volume;
            if (audioLoopSource != null)
                audioLoopSource.volume = volume;

            audioSource.mute = AudioManager.muteEmoteAudio;
            audioLoopSource.mute = AudioManager.muteEmoteAudio;
        }


        public virtual void AddToEmoteSyncGroup(int emoteSyncId)
        {
            if (EmoteSyncGroup.allEmoteSyncGroups.TryGetValue(emoteSyncId, out var emoteSyncGroup))
                currentEmoteSyncGroup = emoteSyncGroup;
        }


        public virtual void AddToEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup)
        {
            currentEmoteSyncGroup = emoteSyncGroup;
        }


        public virtual void RemoveFromEmoteSyncGroup()
        {
            currentEmoteSyncGroup = null;
        }
    }
}
