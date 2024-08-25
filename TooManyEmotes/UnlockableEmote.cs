using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.Props;
using UnityEngine;

namespace TooManyEmotes
{
    public class UnlockableEmote
    {
        public int emoteId;
        public string emoteName;
        public string displayName = "";
        public string displayNameColorCoded { get { return string.Format("<color={0}>{1}</color>", nameColor, displayName); } }
        public AnimationClip animationClip;
        public AnimationClip transitionsToClip = null;

        public bool humanoidAnimation { get { return animationClip.isHumanMotion; } }

        public bool purchasable = true;
        public bool requiresHeldProp = false;
        public GameObject requiredHeldPropPrefab = null;
        public bool complementary = false;
        public bool isPose = false;
        public bool canMoveWhileEmoting = false;
        public bool loopable { get { return animationClip.isLooping || (transitionsToClip != null && transitionsToClip.isLooping); } }

        public bool hasAudio { get { return audioClipName != "" || audioLoopClipName != ""; } }
        private bool _isBoomboxAudio = true;
        public bool isBoomboxAudio { get { return _isBoomboxAudio && !ConfigSettings.disableBoomboxRequirement.Value; } set { _isBoomboxAudio = value; } }
        public string audioClipName { get { return animationClip != null && AudioManager.AudioExists(animationClip.name) ? animationClip.name : (overrideAudioClipName != "" && AudioManager.AudioExists(overrideAudioClipName) ? overrideAudioClipName : ""); } }
        public string audioLoopClipName { get { return transitionsToClip != null && AudioManager.AudioExists(transitionsToClip.name) ? transitionsToClip.name : (overrideAudioLoopClipName != "" && AudioManager.AudioExists(overrideAudioLoopClipName) ? overrideAudioLoopClipName : ""); } }
        public string overrideAudioClipName = "";
        public string overrideAudioLoopClipName = "";

        public string emoteSyncGroupName = "";
        public List<UnlockableEmote> emoteSyncGroup;
        public bool inEmoteSyncGroup { get { return emoteSyncGroup != null && !randomEmote; } }
        public bool IsEmoteInEmoteGroup(UnlockableEmote emote) => this == emote || (emoteSyncGroup != null && emoteSyncGroup.Contains(emote));
        public float recordSongLoopValue = 0;
        public bool randomEmote = false;

        public List<string> propNamesInEmote;

        public bool canSyncEmote = false;
        public bool favorite { get { return EmotesManager.allFavoriteEmotes.Contains(emoteName); } }

        public int rarity = 0;

        public string rarityText
        {
            get
            {
                if (rarity == 0) return "Common";
                else if (rarity == 1) return "Rare";
                else if (rarity == 2) return "Epic";
                else if (rarity == 3) return "Legendary";
                else return "Invalid";
            }
        }
        public int price
        {
            get
            {
                int price = -1;
                if (complementary) price = 0;
                else if (rarity == 0) price = ConfigSync.instance.syncBasePriceEmoteTier0;
                else if (rarity == 1) price = ConfigSync.instance.syncBasePriceEmoteTier1;
                else if (rarity == 2) price = ConfigSync.instance.syncBasePriceEmoteTier2;
                else if (rarity == 3) price = ConfigSync.instance.syncBasePriceEmoteTier3;
                return (int)Mathf.Max(price * ConfigSync.instance.syncPriceMultiplierEmotesStore, 0);
            }
        }
        public string nameColor { get { return rarityColorCodes[rarity]; } }
        public static string[] rarityColorCodes = new string[] { ConfigSettings.emoteNameColorTier0.Value, ConfigSettings.emoteNameColorTier1.Value, ConfigSettings.emoteNameColorTier2.Value, ConfigSettings.emoteNameColorTier3.Value };

        public bool ClipIsInEmote(AnimationClip clip)
        {
            if (clip == null)
                return false;
            if (clip == animationClip || clip == transitionsToClip)
                return true;
            if (emoteSyncGroup != null)
            {
                foreach (var emote in emoteSyncGroup)
                {
                    if (clip == emote.animationClip || clip == emote.transitionsToClip)
                        return true;
                }
            }
            return false;
        }

        /*
        public void AddPropAnimationData(PropAnimationData propAnimationData)
        {
            if (propAnimationData.parentEmote != null && propAnimationData.parentEmote != this)
            {
                LogError("Error adding prop animation to unlockable emote. Prop animation data has another parent unlockable emote: " + propAnimationData.parentEmote.emoteName + ". This emote: " + emoteName);
                return;
            }
            if (propAnimationsData == null)
                propAnimationsData = new List<PropAnimationData>();
            propAnimationsData.Add(propAnimationData);
        }
        */

        public AudioClip LoadAudioClip()
        {
            if (hasAudio && audioClipName.Length > 0)
            {
                var audioClip = AudioManager.LoadAudioClip(audioClipName);
                return audioClip;
            }
            return null;
        }

        public AudioClip LoadAudioLoopClip()
        {
            if (hasAudio && transitionsToClip != null && audioLoopClipName.Length > 0)
            {
                var audioClip = AudioManager.LoadAudioClip(audioLoopClipName);
                return audioClip;
            }
            return null;
        }
    }
}