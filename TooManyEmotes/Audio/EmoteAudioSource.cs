using HarmonyLib;
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
        public AudioSource audioSource;
        public AudioSource audioLoopSource;
        public bool isPlayingAudio;
        float playAudioLoopSourceAtTime = 0;

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

            audioLoopSource.volume = ConfigSettings.baseEmoteAudioVolume.Value;
            audioLoopSource.dopplerLevel = 0;
            audioLoopSource.spatialBlend = 1.0f;
            audioLoopSource.minDistance = ConfigSettings.emoteAudioMinDistance.Value;
            audioLoopSource.maxDistance = ConfigSettings.emoteAudioMaxDistance.Value;
            audioLoopSource.rolloffMode = AudioRolloffMode.Linear;
            audioLoopSource.loop = true;

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

            isPlayingAudio = false;
            audioSource.Stop();
            audioLoopSource.Stop();
            audioSource.mute = false;
            audioLoopSource.mute = false;

            //float timeSinceStartedEmote = Time.time - emoteSyncGroup.timeStartedEmote;

            bool success = SetAudioFromEmote(emote);
            if (success)
            {
                var referenceEmoteAudioSource = emoteSyncGroup.leadEmoteAudioSource;
                if (referenceEmoteAudioSource != null)
                {
                    audioSource.time = referenceEmoteAudioSource.audioSource.time;
                    audioLoopSource.time = referenceEmoteAudioSource.audioLoopSource.time;
                    if (audioSource.clip != null && referenceEmoteAudioSource.audioSource.isPlaying)
                    {
                        audioSource.Play();
                        if (audioLoopSource.clip != null && referenceEmoteAudioSource.playAudioLoopSourceAtTime > Time.time)
                        {
                            playAudioLoopSourceAtTime = referenceEmoteAudioSource.playAudioLoopSourceAtTime;
                            audioLoopSource.PlayScheduled(AudioSettings.dspTime + (audioSource.clip.length - audioSource.time));
                        }
                    }
                    else if (audioLoopSource.clip != null && referenceEmoteAudioSource.audioLoopSource.isPlaying)
                    {
                        audioLoopSource.Play();
                    }
                }
                /*
                else if (audioSource.clip != null && timeSinceStartedEmote < audioSource.clip.length)
                {
                    audioSource.time = timeSinceStartedEmote; // % audioSource.clip.length;
                    audioSource.Play();
                    if (audioLoopSource.clip != null)
                    {
                        float dTime = audioSource.clip.length - audioSource.time;
                        playAudioLoopSourceAtTime = Time.time + dTime;
                        audioLoopSource.PlayScheduled(AudioSettings.dspTime + playAudioLoopSourceAtTime);
                    }
                }
                else if (audioLoopSource.clip != null)
                {
                    if (audioSource.clip != null)
                        timeSinceStartedEmote -= audioSource.clip.length;
                    else if (emote.transitionsToClip != null)
                        timeSinceStartedEmote -= emote.transitionsToClip.length;

                    float playAtTime = timeSinceStartedEmote % audioLoopSource.clip.length;
                    audioLoopSource.time = playAtTime;
                    audioLoopSource.Play();
                }
                */

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
            audioSource.mute = false;
            audioSource.time = 0;

            audioLoopSource.clip = null;
            audioLoopSource.mute = false;
            audioLoopSource.time = 0;

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

                // Mute dmca clip in case they were incorrectly loaded?
                audioSource.mute = AudioManager.dmcaFreeMode && audioSource.clip != null && AudioManager.IsClipDMCA(audioSource.clip);
                audioLoopSource.mute = AudioManager.dmcaFreeMode && audioLoopSource.clip != null && AudioManager.IsClipDMCA(audioLoopSource.clip);

                return true;
            }

            return false;
        }


        public virtual void StopAudio()
        {
            if (isPlayingAudio)
                Log("Stopping audio on emote audio source.");
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
