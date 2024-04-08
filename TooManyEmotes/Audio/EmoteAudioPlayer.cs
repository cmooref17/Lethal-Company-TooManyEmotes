using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Audio
{
    public class EmoteAudioPlayer : EmoteAudioSource
    {
        public GrabbableObject grabbableAudioPlayer;
        public AudioSource[] existingAudioSources;
        public Dictionary<AudioSource, bool> previouslyMutedAudioSources = new Dictionary<AudioSource, bool>();
        
        public int emoteSyncId { get { return currentEmoteSyncGroup.syncId; } }


        protected override void Awake()
        {
            grabbableAudioPlayer = GetComponent<GrabbableObject>();
            existingAudioSources = GetComponentsInChildren<AudioSource>();
            foreach (var audioSource in existingAudioSources)
                previouslyMutedAudioSources[audioSource] = audioSource.mute;
            base.Awake();
        }


        protected override void OnDestroy()
        {
            EmoteAudioPlayerManager.allEmoteAudioPlayers.Remove(this);
            base.OnDestroy();
        }


        protected override void OnEnable() { base.OnEnable(); }
        protected override void OnDisable() { base.OnDisable(); }


        protected override void Update() { base.Update(); }


        public override bool CanPlayMusic()
        {
            bool result = base.CanPlayMusic();
            if (!result)
                return false;

            if (grabbableAudioPlayer is BoomboxItem)
            {
                BoomboxItem boomboxItem = grabbableAudioPlayer as BoomboxItem;
                if (boomboxItem.isPlayingMusic)
                    return false;
            }
            return true;
        }


        public override bool CheckIfShouldStopAudio()
        {
            bool result = base.CheckIfShouldStopAudio();
            if (result)
                return true;

            if (grabbableAudioPlayer != null)
            {
                if (grabbableAudioPlayer is BoomboxItem)
                {
                    BoomboxItem boomboxItem = grabbableAudioPlayer as BoomboxItem;
                    if (boomboxItem.isPlayingMusic)
                        return true;
                }
                if (grabbableAudioPlayer.itemProperties.requiresBattery && grabbableAudioPlayer.insertedBattery != null && grabbableAudioPlayer.insertedBattery.empty)
                    return true;
            }

            if (GetNearestEmoteControllerWithinRange() == null)
                return true;

            return false;
        }


        public EmoteController GetNearestEmoteControllerWithinRange()
        {
            if (currentEmoteSyncGroup == null)
                return null;

            float distance = EmoteAudioPlayerManager.maxEmoteRange;
            EmoteController nearestEmoteController = null;

            foreach (var emoteController in currentEmoteSyncGroup.syncGroup)
            {
                float dist = Vector3.Distance(transform.position, emoteController.transform.position);
                if (dist < distance)
                {
                    nearestEmoteController = emoteController;
                    distance = dist;
                }
            }

            return nearestEmoteController;
        }


        public override bool SyncWithEmoteControllerAudio(EmoteController emoteController)
        {
            if (!isPlayingAudio)
            {
                foreach (var audioSource in existingAudioSources)
                    audioSource.mute = previouslyMutedAudioSources[audioSource];
            }
            bool result = base.SyncWithEmoteControllerAudio(emoteController);
            if (result)
            {
                if (grabbableAudioPlayer != null && grabbableAudioPlayer is BoomboxItem)
                {
                    foreach (var audioSource in existingAudioSources)
                        audioSource.mute = true;
                }
            }
            return result;
        }


        protected override bool SetAudioFromEmote(UnlockableEmote emote) => base.SetAudioFromEmote(emote);


        public override void StopAudio()
        {
            base.StopAudio();
            foreach (var audioSource in existingAudioSources)
                audioSource.mute = previouslyMutedAudioSources[audioSource];
        }


        public override void AddToEmoteSyncGroup(int emoteSyncId) => base.AddToEmoteSyncGroup(emoteSyncId);
        public override void AddToEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup) => base.AddToEmoteSyncGroup(emoteSyncGroup);
        public override void RemoveFromEmoteSyncGroup() => base.RemoveFromEmoteSyncGroup();
    }
}
