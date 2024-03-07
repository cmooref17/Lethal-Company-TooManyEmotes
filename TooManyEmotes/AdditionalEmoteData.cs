using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using TooManyEmotes.Props;
using UnityEngine;

namespace TooManyEmotes
{
    public static class AdditionalEmoteData
    {
        public static void SetAdditionalEmoteData()
        {
            //SetCanMoveWhileEmoting("feelin'_jaunty");
        }


        public static void SetAdditionalPropData()
        {
            if (EmotesManager.allUnlockableEmotesDict == null)
                return;

            AssignPropToEmote("jug_prop", "jug_band.jug");
            AssignPropToEmote("guitar_prop", "jug_band.guitar");
            AssignPropToEmote("banjo_prop", "jug_band.banjo");
            AssignPropToEmote("fiddle_prop", "jug_band.fiddle");
            AssignPropToEmote("sexy_saxophone.sexy_sax.prop", "sexy_saxophone.sexy_sax");
            AssignPropToEmote("sexy_saxophone.epic_sax.prop", "sexy_saxophone.epic_sax");
        }


        public static void SetAdditionalMusicData()
        {
            if (EmotesManager.allUnlockableEmotesDict == null)
                return;

            SetEmoteDoesNotUseBoombox("hand_signals");
            SetEmoteDoesNotUseBoombox("red_card");
            SetEmoteDoesNotUseBoombox("sexy_saxophone.sexy_sax");
            SetEmoteDoesNotUseBoombox("sexy_saxophone.epic_sax");
            SetEmoteDoesNotUseBoombox("jug_band.jug");
            SetEmoteDoesNotUseBoombox("jug_band.guitar");
            SetEmoteDoesNotUseBoombox("jug_band.banjo");
            SetEmoteDoesNotUseBoombox("jug_band.fiddle");

            //AssignMusicToEmote("smug_dance", "starlit_loop");
            AssignMusicToEmote("jug_band.jug", "jug_band.jug");
            AssignMusicToEmote("jug_band.guitar", "jug_band.guitar");
            AssignMusicToEmote("jug_band.banjo", "jug_band.banjo");
            AssignMusicToEmote("jug_band.fiddle", "jug_band.fiddle");
        }


        public static void AssignPropToEmote(string propName, string emoteName)
        {
            if (!EmotePropManager.propPrefabs.ContainsKey(propName))
            {
                Plugin.LogWarning("Failed to assign prop: " + propName + " to emote. Prop does not exist!");
                return;
            }
            if (!EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
            {
                Plugin.LogWarning("Failed to assign prop: " + propName + " to emote: " + emoteName + ". Emote does not exist!");
                return;
            }

            if (emote.propNamesInEmote == null)
                emote.propNamesInEmote = new List<string>();
            emote.propNamesInEmote.Add(propName);
        }


        public static void SetEmoteDoesNotUseBoombox(string emoteName)
        {
            if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                emote.isBoomboxAudio = false;
        }


        public static void SetCanMoveWhileEmoting(string emoteName)
        {
            if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                emote.canMoveWhileEmoting = true;
        }


        public static void AssignMusicToEmote(string emoteName, string audioName, string audioLoopName = "")
        {
            if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote) && AudioManager.AudioExists(audioName))
            {
                emote.overrideAudioClipName = audioName;
                emote.overrideAudioClipLoopName = audioLoopName;
            }
        }

    }
}