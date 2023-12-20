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

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    internal class StartOfRoundPatcher
    {
        public static List<UnlockableEmote> allUnlockableEmotes;
        public static Dictionary<string, UnlockableEmote> allUnlockableEmotesDict;

        public static List<UnlockableEmote> allCommonEmotes;
        public static List<UnlockableEmote> allUncommonEmotes;
        public static List<UnlockableEmote> allRareEmotes;
        public static List<UnlockableEmote> allLegendaryEmotes;

        public static List<UnlockableEmote> unlockedEmotes;
        public static List<UnlockableEmote> complementaryEmotes;

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void InitializeEmotes(StartOfRound __instance) {
            __instance.localClientAnimatorController = new AnimatorOverrideController(__instance.localClientAnimatorController);
            __instance.otherClientsAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);

            allUnlockableEmotes = new List<UnlockableEmote>();
            allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();
            unlockedEmotes = new List<UnlockableEmote>();

            complementaryEmotes = new List<UnlockableEmote>();
            allCommonEmotes = new List<UnlockableEmote>();
            allUncommonEmotes = new List<UnlockableEmote>();
            allRareEmotes = new List<UnlockableEmote>();
            allLegendaryEmotes = new List<UnlockableEmote>();

            for (int i = 0; i < Plugin.customAnimationClips.Count; i++)
            {
                AnimationClip clip = Plugin.customAnimationClips[i];
                UnlockableEmote emote = new UnlockableEmote {
                    emoteId = i,
                    emoteName = clip.name,
                    displayName = clip.name,
                    animationClip = clip,
                    rarity = 0
                };

                if (Plugin.commonAnimationClips.Contains(clip))
                {
                    emote.rarity = 0;
                    allCommonEmotes.Add(emote);
                }
                else if (Plugin.uncommonAnimationClips.Contains(clip))
                {
                    emote.rarity = 1;
                    allUncommonEmotes.Add(emote);
                }
                else if (Plugin.rareAnimationClips.Contains(clip))
                {
                    emote.rarity = 2;
                    allRareEmotes.Add(emote);
                }
                else if (Plugin.legendaryAnimationClips.Contains(clip))
                {
                    emote.rarity = 3;
                    allLegendaryEmotes.Add(emote);
                }

                if (emote.emoteName.Contains("_start"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    emote.transitionsToClip = emoteLoop;
                    emote.displayName = emote.emoteName.Replace("_start", ""); ;
                }
                emote.displayName = emote.displayName.Replace('_', ' ').Trim(' ');
                emote.displayName = char.ToUpper(emote.displayName[0]) + emote.displayName.Substring(1).ToLower();
                allUnlockableEmotes.Add(emote);
                allUnlockableEmotesDict.Add(emote.emoteName, emote);

                if (Plugin.complementaryAnimationClips.Contains(clip))
                {
                    emote.complementary = true;
                    complementaryEmotes.Add(emote);
                }
            }
            
            for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
            {
                if (__instance.allPlayerScripts[i]?.playerBodyAnimator?.runtimeAnimatorController != null)
                {
                    __instance.allPlayerScripts[i].playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
                }
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        public static void OnServerStart(StartOfRound __instance) {
            if (!__instance.IsServer)
                return;

            foreach (var emote in complementaryEmotes)
                UnlockEmoteLocal(emote);

            EmoteSyncManager.RotateEmoteSelectionServerRpc(TerminalPatcher.emoteStoreSeed);
        }


        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPostfix]
        public static void ResetOverrideSeedFlag(StartOfRound __instance)
        {
            __instance.overrideRandomSeed = false;
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        public static void ResetEmotesOnShipReset(StartOfRound __instance)
        {
            ResetEmotesLocal();
        }


        public static void ResetEmotesLocal()
        {
            Plugin.Log("Resetting progression.");
            unlockedEmotes = new List<UnlockableEmote>(complementaryEmotes);
            TerminalPatcher.emoteCreditsUsed = 0;
            TerminalPatcher.emoteStoreSeed = 0;
        }


        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        public static void SyncUnlockedEmotesWithClients(StartOfRound __instance) {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;
            if (ConfigSync.syncUnlockEverything)
                return;
            Plugin.Log("Syncing unlocked emotes with clients.");
            EmoteSyncManager.SendOnUnlockEmoteUpdateMulti(TerminalPatcher.emoteCreditsUsed);
        }


        public static void UnlockEmoteLocal(int emoteId) => UnlockEmoteLocal(emoteId >= 0 && emoteId < allUnlockableEmotes.Count ? allUnlockableEmotes[emoteId] : null);
        public static void UnlockEmoteLocal(UnlockableEmote emote) {
            if (emote == null || unlockedEmotes.Contains(emote))
                return;
            unlockedEmotes.Add(emote);
        }
    }
}