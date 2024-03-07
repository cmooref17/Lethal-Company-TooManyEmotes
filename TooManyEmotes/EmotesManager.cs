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

                emote.rarity = 
                    Plugin.animationClipsTier1.Contains(clip) ? 1 :
                    Plugin.animationClipsTier2.Contains(clip) ? 2 :
                    Plugin.animationClipsTier3.Contains(clip) ? 3 : 0;

                if (Plugin.complementaryAnimationClips.Contains(clip))
                    emote.complementary = true;


                if (emote.emoteName.Contains("_start") && !emote.emoteName.Contains("_start_"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    if (emoteLoop != null)
                    {
                        emote.transitionsToClip = emoteLoop;
                        emote.emoteName = emote.emoteName.Replace("_start", "");
                        emote.animationClip.name = emote.emoteName + "_start";
                        emote.transitionsToClip.name = emote.emoteName + "_loop";
                    }
                }
                else if (emote.emoteName.Contains("_pose"))
                {
                    emote.isPose = true;
                    emote.emoteName = emote.emoteName.Replace("_pose", "");
                    emote.animationClip.name = emote.emoteName;
                }


                // Set emote sync group (if exists)
                if (emote.emoteName.Contains("."))
                {
                    var args = emote.emoteName.Split('.');
                    if (args.Length > 0 && args[0].Length > 0)
                    {
                        if (args.Length > 3)
                        {
                            Plugin.LogError("Error parsing emote name: " + emote.emoteName + ". Correct format: \"emote_group.optional_arg.emote_name\"");
                            continue;
                        }
                        emote.emoteSyncGroupName = args[0];
                        emote.emoteName = emote.emoteSyncGroupName + "." + args[args.Length - 1];
                        emote.displayName = emote.emoteSyncGroupName;

                        if (emote.transitionsToClip == null)
                            emote.animationClip.name = emote.emoteName;
                        else
                        {
                            emote.animationClip.name = emote.emoteName + "_start";
                            emote.transitionsToClip.name = emote.emoteName + "_loop";

                        }
                        if (!syncEmoteGroups.TryGetValue(emote.emoteSyncGroupName, out emote.emoteSyncGroup))
                        {
                            emote.emoteSyncGroup = new List<UnlockableEmote>();
                            syncEmoteGroups.Add(emote.emoteSyncGroupName, emote.emoteSyncGroup);
                        }

                        // Are the emotes in the group ordered?
                        if (args.Length == 3 && args[1].ToLower().Contains("layer_"))
                        {
                            clip.name = clip.name.Replace("." + args[1], "");
                            if (emote.transitionsToClip != null)
                                emote.transitionsToClip.name = emote.transitionsToClip.name.Replace("." + args[1], "");
                            if (int.TryParse(args[1].Substring(6), out int layerNumber))
                            {
                                emote.purchasable = layerNumber == 0;
                                while (emote.emoteSyncGroup.Count <= layerNumber)
                                    emote.emoteSyncGroup.Add(null);
                                emote.emoteSyncGroup[layerNumber] = emote;
                            }
                            else
                            {
                                Plugin.LogError("Failed to parse emote layer number in arg: " + args[1] + ". Emote will not be added.");
                                continue;
                                //emote.emoteSyncGroup.Add(emote);
                                //emote.purchasable = emote.emoteSyncGroup.Count == 1;
                            }
                        }
                        else
                        {
                            emote.emoteSyncGroup.Add(emote);
                            emote.purchasable = emote.emoteSyncGroup.Count == 1;
                            if (args.Length == 3 && args[1].ToLower() == "random")
                            {
                                emote.randomEmote = true;
                                clip.name = clip.name.Replace("." + args[1], "");
                                if (emote.transitionsToClip != null)
                                    emote.transitionsToClip.name = emote.transitionsToClip.name.Replace("." + args[1], "");
                            }
                        }
                    }
                }

                if (emote.emoteName.Contains("_start") && !emote.emoteName.Contains("_start_"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    if (emoteLoop != null)
                    {
                        emote.transitionsToClip = emoteLoop;
                        emote.emoteName = emote.emoteName.Replace("_start", "");
                        emote.animationClip.name = emote.emoteName + "_start";
                        emote.transitionsToClip.name = emote.emoteName + "_loop";
                    }
                }
                else if (emote.emoteName.Contains("_pose"))
                {
                    emote.isPose = true;
                    emote.emoteName = emote.emoteName.Replace("_pose", "");
                    emote.animationClip.name = emote.emoteName;
                }

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