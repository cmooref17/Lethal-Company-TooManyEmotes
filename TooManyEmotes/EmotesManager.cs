using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using System.Collections;
using Unity.Netcode;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using Unity.Collections;
using TooManyEmotes.Audio;
//using static UnityEditor.Progress;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public static class EmotesManager
    {
        public static List<UnlockableEmote> allUnlockableEmotes;
        public static Dictionary<string, UnlockableEmote> allUnlockableEmotesDict;
        public static List<UnlockableEmote> complementaryEmotes;
        public static List<string> allFavoriteEmotes;

        public static List<UnlockableEmote> allEmotesTier0;
        public static List<UnlockableEmote> allEmotesTier1;
        public static List<UnlockableEmote> allEmotesTier2;
        public static List<UnlockableEmote> allEmotesTier3;


        public static void BuildEmotesList()
        {
            allUnlockableEmotes = new List<UnlockableEmote>();
            allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();

            complementaryEmotes = new List<UnlockableEmote>();
            allFavoriteEmotes = new List<string>();

            allEmotesTier0 = new List<UnlockableEmote>();
            allEmotesTier1 = new List<UnlockableEmote>();
            allEmotesTier2 = new List<UnlockableEmote>();
            allEmotesTier3 = new List<UnlockableEmote>();

            var randomEmotePools = new Dictionary<string, List<UnlockableEmote>>();
            var syncEmoteGroups = new Dictionary<string, List<UnlockableEmote>>();

            for (int i = 0; i < Plugin.customAnimationClips.Count; i++)
            {
                AnimationClip clip = Plugin.customAnimationClips[i];
                UnlockableEmote emote = new UnlockableEmote
                {
                    emoteId = i,
                    emoteName = clip.name,
                    displayName = "",
                    animationClip = clip,
                    rarity = 0
                };

                emote.rarity = Plugin.animationClipsTier1.Contains(clip) ? 1 :
                    Plugin.animationClipsTier2.Contains(clip) ? 2 :
                    Plugin.animationClipsTier3.Contains(clip) ? 3 : 0;

                if (emote.emoteName.Contains("_start") && !emote.emoteName.Contains("_start_"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    emote.transitionsToClip = emoteLoop;
                    emote.emoteName = emote.emoteName.Replace("_start", "");
                }
                if (emote.emoteName.Contains("_pose"))
                {
                    emote.emoteName = emote.emoteName.Replace("_pose", "");
                    emote.isPose = true;
                }
                if (emote.emoteName.Contains("_sync"))
                {
                    emote.emoteSyncGroupName = emote.emoteName.Substring(0, emote.emoteName.IndexOf("_sync"));
                    emote.emoteName = emote.emoteName.Replace("_sync", "");

                    int index = emote.emoteSyncGroupName.IndexOf("_");
                    if (index >= 0)
                        emote.emoteSyncGroupName = emote.emoteSyncGroupName.Substring(index + 1, emote.emoteSyncGroupName.Length - index - 1);
                    if (syncEmoteGroups.TryGetValue(emote.emoteSyncGroupName, out var syncGroup))
                    {
                        if (!syncGroup.Contains(emote))
                            syncGroup.Add(emote);
                    }
                    else
                    {
                        syncGroup = new List<UnlockableEmote>() { emote };
                        syncEmoteGroups.Add(emote.emoteSyncGroupName, syncGroup);
                    }
                    emote.emoteSyncGroup = syncGroup;
                }
                if (emote.emoteName.Contains("_random"))
                {
                    emote.randomEmotePoolName = emote.emoteName.Substring(0, emote.emoteName.IndexOf("_random"));
                    if (!randomEmotePools.ContainsKey(emote.randomEmotePoolName))
                        randomEmotePools.Add(emote.randomEmotePoolName, new List<UnlockableEmote> { emote });
                    else
                    {
                        emote.purchasable = false;
                        randomEmotePools[emote.randomEmotePoolName].Add(emote);
                    }
                    emote.randomEmotePool = randomEmotePools[emote.randomEmotePoolName];

                    emote.emoteName = emote.emoteName.Replace("_random", "");
                    emote.displayName = emote.randomEmotePoolName;
                }

                //if (AudioManager.AudioExists(emote.emoteName)) { emote.LoadAudioClip(); }

                if (emote.transitionsToClip != null || emote.animationClip.isLooping || emote.isPose || emote.emoteSyncGroup != null)
                    emote.canSyncEmote = true;

                if (emote.displayName == "")
                    emote.displayName = emote.emoteName;

                emote.displayName = emote.displayName.Replace('_', ' ').Trim(' ');
                emote.displayName = char.ToUpper(emote.displayName[0]) + emote.displayName.Substring(1).ToLower();

                if (!allUnlockableEmotes.Contains(emote))
                {
                    allUnlockableEmotes.Add(emote);
                    allUnlockableEmotesDict.Add(emote.emoteName, emote);
                }

                if (Plugin.complementaryAnimationClips.Contains(clip) && emote.purchasable)
                {
                    emote.complementary = true;
                    complementaryEmotes.Add(emote);
                }
            }

            allUnlockableEmotes = allUnlockableEmotes.OrderBy(item => item.rarity).ThenBy(item => item.emoteName).ToList();

            int id = 0;
            foreach (var emote in allUnlockableEmotes)
            {
                emote.emoteId = id++;
                if (emote.complementary)
                    continue;
                if (emote.rarity == 0)
                    allEmotesTier0.Add(emote);
                else if (emote.rarity == 1)
                    allEmotesTier1.Add(emote);
                else if (emote.rarity == 2)
                    allEmotesTier2.Add(emote);
                else if (emote.rarity == 3)
                    allEmotesTier3.Add(emote);
            }

            SaveManager.LoadFavoritedEmotes();
        }
    }
}