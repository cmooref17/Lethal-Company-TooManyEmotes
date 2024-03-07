using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Audio
{
    public class EmoteBoombox : EmoteAudioSource
    {
        public BoomboxItem boomboxItem;
        
        public int emoteSyncId { get { return currentEmoteSyncGroup.syncId; } }


        protected override void Awake()
        {
            base.Awake();
            boomboxItem = GetComponent<BoomboxItem>();
        }



        protected override void OnDestroy()
        {
            StopAudio();
            BoomboxManager.allBoomboxes.Remove(this);
            base.OnDestroy();
        }


        protected override void OnEnable() { base.OnEnable(); }
        protected override void OnDisable() { base.OnDisable(); }


        protected override void Update()
        {
            base.Update();
            if (isPlayingAudio && CheckIfShouldStopAudio())
                StopAudio();
        }


        public bool CanPlayMusic()
        {
            return !isPlayingAudio;
        }


        public bool CheckIfShouldStopAudio()
        {
            if (!isPlayingAudio)
                return false;

            if (boomboxItem == null || !boomboxItem.isActiveAndEnabled)
            {
                return true;
            }

            //if (!boomboxItem.isPlayingMusic) return false;

            if (boomboxItem.itemProperties.requiresBattery && boomboxItem.insertedBattery != null && boomboxItem.insertedBattery.empty)
            {
                return true;
            }

            if (GetNearestEmoteControllerWithinRange() == null)
            {
                return true;
            }

            return false;
        }


        public EmoteController GetNearestEmoteControllerWithinRange()
        {
            if (currentEmoteSyncGroup == null)
            {
                return null;
            }

            float distance = BoomboxManager.maxEmoteRange;
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


        public override bool PlayEmoteAudio(UnlockableEmote emote)
        {
            bool result = base.PlayEmoteAudio(emote);
            if (result)
                boomboxItem.boomboxAudio.mute = true;
            return result;
        }


        public override bool SyncWithEmoteControllerAudio(EmoteController emoteController)
        {
            bool result = base.SyncWithEmoteControllerAudio(emoteController);
            if (result)
                boomboxItem.boomboxAudio.mute = true;
            return result;
        }


        protected override bool SetAudioFromEmote(UnlockableEmote emote) => base.SetAudioFromEmote(emote);


        public override void StopAudio()
        {
            base.StopAudio();
            boomboxItem.boomboxAudio.mute = false;
        }


        public override void AddToEmoteSyncGroup(int emoteSyncId) => base.AddToEmoteSyncGroup(emoteSyncId);
        public override void AddToEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup) => base.AddToEmoteSyncGroup(emoteSyncGroup);
        public override void RemoveFromEmoteSyncGroup() => base.RemoveFromEmoteSyncGroup();
    }
}
