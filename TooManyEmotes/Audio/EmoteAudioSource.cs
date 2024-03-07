using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.UI;
using UnityEngine;

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

        protected virtual void Awake()
        {
            allEmoteAudioSources.Add(this);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = ConfigSettings.baseEmoteAudioVolume.Value;
            audioSource.dopplerLevel = 0;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = ConfigSettings.emoteAudioMinDistance.Value;
            audioSource.maxDistance = ConfigSettings.emoteAudioMaxDistance.Value;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.loop = false;

            audioLoopSource = gameObject.AddComponent<AudioSource>();
            audioLoopSource.volume = ConfigSettings.baseEmoteAudioVolume.Value;
            audioSource.dopplerLevel = 0;
            audioLoopSource.spatialBlend = 1.0f;
            audioLoopSource.minDistance = ConfigSettings.emoteAudioMinDistance.Value;
            audioLoopSource.maxDistance = ConfigSettings.emoteAudioMaxDistance.Value;
            audioLoopSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioLoopSource.loop = true;
        }


        protected virtual void OnDestroy()
        {
            allEmoteAudioSources.Remove(this);
        }


        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }


        protected virtual void Update() { }


        public virtual bool PlayEmoteAudio(UnlockableEmote emote)
        {
            if (emote == null || !emote.hasAudio)
                return false;

            audioSource.Stop();
            audioLoopSource.Stop();

            if (SetAudioFromEmote(emote))
            {
                if (audioSource.clip != null)
                {
                    float dTime = emote.animationClip.length - audioSource.clip.length;
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
                    if (audioLoopSource.clip != null)
                    {
                        dTime = emote.animationClip.length;
                        playAudioLoopSourceAtTime = Time.time + dTime;
                        audioLoopSource.PlayScheduled(AudioSettings.dspTime + dTime);
                    }
                }
                else
                    audioLoopSource.Play();

                isPlayingAudio = true;
                return true;
            }

            return false;
        }


        public virtual bool SyncWithEmoteControllerAudio(EmoteController emoteController)
        {
            if (emoteController == null || !emoteController.IsPerformingCustomEmote())
                return false;

            audioSource.Stop();
            audioLoopSource.Stop();
            var emote = emoteController.performingEmote;

            bool success = SetAudioFromEmote(emote);
            if (success)
            {
                var currentAnimationClip = emoteController.GetCurrentAnimationClip();
                float playAtTimeNormalized = emoteController.currentAnimationTimeNormalized % 1;
                // Playing start clip
                if (currentAnimationClip == emote.transitionsToClip || currentAnimationClip.isLooping)
                {
                    audioLoopSource.time = audioLoopSource.clip.length * playAtTimeNormalized;
                    playAudioLoopSourceAtTime = Time.time - audioLoopSource.time;
                    audioLoopSource.Play();
                }
                // Playing start clip
                else
                {
                    if (audioSource.clip != null)
                    {
                        audioSource.time = audioSource.clip.length * playAtTimeNormalized;
                        float dTime = 0;
                        if (emote.transitionsToClip != null) // sync end of audio clip with start of loop animation (if exists)
                            dTime = currentAnimationClip.length - emoteController.currentAnimationTime - audioSource.clip.length;

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
                        //float dTime = audioSource.clip != null ? audioSource.clip.length - emoteController.currentAnimationTime : currentAnimationClip.length - emoteController.currentAnimationTime;
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

            audioSource.Stop();
            audioLoopSource.Stop();

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


        protected virtual bool SetAudioFromEmote(UnlockableEmote emote)
        {
            if (emote != null && emote.hasAudio)
            {
                var audioClip = emote.LoadAudioClip();
                var audioClipLoop = emote.LoadAudioLoopClip();

                if (audioClipLoop != null)
                {
                    audioSource.clip = audioClip;
                    audioLoopSource.clip = audioClipLoop;
                }
                else if (audioSource != null)
                {
                    if (emote.animationClip.isLooping)
                    {
                        audioLoopSource.clip = audioClip;
                        audioSource.clip = null;
                    }
                    else
                    {
                        audioSource.clip = audioClip;
                        audioLoopSource.clip = null;
                    }
                }
                else
                    return false;

                audioSource.time = 0;
                audioLoopSource.time = 0;
                return true;
            }

            return false;
        }


        public virtual void StopAudio()
        {
            Plugin.Log("Stopping audio on emote boombox.");
            audioSource.Stop();
            audioLoopSource.Stop();
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
