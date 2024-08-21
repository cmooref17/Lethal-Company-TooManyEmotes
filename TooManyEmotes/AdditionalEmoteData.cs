using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using TooManyEmotes.Props;
using Unity.Netcode;
using UnityEngine;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [HarmonyPatch]
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
            AssignPropToEmote("trombone.prop", "sad_trombone");
            AssignPropToEmote("old_chair.prop", "ma-ya-hi");
            AssignPropToEmote("baseball_bat.prop", "miracle_trickshot");
            AssignPropToEmote("dumbbell.prop", "pumping_iron");
            AssignPropToEmote("paddle.prop", "paddle_royale");
            AssignPropToEmote("gamepad.prop", "controller_crew");
            AssignPropToEmote("jar_of_dirt.prop", "jar_of_dirt");

            AssignPropToEmote("travelers.banjo.prop", "travelers.banjo");
            AssignPropToEmote("travelers.harmonica.prop", "travelers.harmonica");
            AssignPropToEmote("travelers.drums.prop", "travelers.drums");
            AssignPropToEmote("travelers.flute.prop", "travelers.flute");
            AssignPropToEmote("travelers.whistle.prop", "travelers.whistle");
            AssignPropToEmote("travelers.piano.prop", "travelers.piano");
            AssignPropToEmote("travelers.bow.prop", "travelers.bow");
        }


        public static void SetAdditionalMusicData()
        {
            if (EmotesManager.allUnlockableEmotesDict == null)
                return;

            SetEmoteDoesNotUseBoombox("jug_band.jug");
            SetEmoteDoesNotUseBoombox("jug_band.guitar");
            SetEmoteDoesNotUseBoombox("jug_band.banjo");
            SetEmoteDoesNotUseBoombox("jug_band.fiddle");

            SetEmoteDoesNotUseBoombox("hand_signals");
            SetEmoteDoesNotUseBoombox("red_card");
            SetEmoteDoesNotUseBoombox("sexy_saxophone.sexy_sax");
            SetEmoteDoesNotUseBoombox("sexy_saxophone.epic_sax");
            SetEmoteDoesNotUseBoombox("sad_trombone");
            SetEmoteDoesNotUseBoombox("snake_summoner");
            SetEmoteDoesNotUseBoombox("miracle_trickshot");
            SetEmoteDoesNotUseBoombox("junk_food");
            SetEmoteDoesNotUseBoombox("pumping_iron");
            SetEmoteDoesNotUseBoombox("paddle_royale");
            SetEmoteDoesNotUseBoombox("wolf_howl");
            SetEmoteDoesNotUseBoombox("jar_of_dirt");

            SetEmoteDoesNotUseBoombox("travelers.banjo");
            SetEmoteDoesNotUseBoombox("travelers.harmonica");
            SetEmoteDoesNotUseBoombox("travelers.drums");
            SetEmoteDoesNotUseBoombox("travelers.flute");
            SetEmoteDoesNotUseBoombox("travelers.whistle");
            SetEmoteDoesNotUseBoombox("travelers.piano");
            SetEmoteDoesNotUseBoombox("travelers.bow");

            //AssignMusicToEmote("smug_dance", "starlit_loop");
            AssignMusicToEmote("jug_band.jug", "jug_band.jug");
            AssignMusicToEmote("jug_band.guitar", "jug_band.guitar");
            AssignMusicToEmote("jug_band.banjo", "jug_band.banjo");
            AssignMusicToEmote("jug_band.fiddle", "jug_band.fiddle");

            AssignMusicToEmote("travelers.banjo", "travelers.banjo");
            AssignMusicToEmote("travelers.harmonica", "travelers.harmonica");
            AssignMusicToEmote("travelers.drums", "travelers.drums");
            AssignMusicToEmote("travelers.flute", "travelers.flute");
            AssignMusicToEmote("travelers.whistle", "travelers.whistle");
            AssignMusicToEmote("travelers.piano", "travelers.piano");
            AssignMusicToEmote("travelers.bow", "travelers.bow");
        }


        public static void AssignPropToEmote(string propName, string emoteName)
        {
            if (!EmotePropManager.emotePropsDataDict.TryGetValue(propName, out var propPrefab))
            {
                LogWarning("Failed to assign prop: " + propName + " to emote. Prop does not exist!");
                return;
            }
            if (!EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
            {
                LogWarning("Failed to assign prop: " + propName + " to emote: " + emoteName + ". Emote does not exist!");
                return;
            }
            if (!EmotePropManager.emotePropsDataDict.TryGetValue(propName, out var propData))
            {
                LogWarning("Failed to assign prop: " + propName + " to emote: " + emoteName + ". Prop data does not exist for: " + propName);
                return;
            }

            if (emote.propNamesInEmote == null)
                emote.propNamesInEmote = new List<string>();
            emote.propNamesInEmote.Add(propName);

            if (propData.parentEmotes == null)
                propData.parentEmotes = new List<UnlockableEmote>();
            if (!propData.parentEmotes.Contains(emote))
                propData.parentEmotes.Add(emote);
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
                emote.overrideAudioLoopClipName = audioLoopName;
            }
        }

    }
}