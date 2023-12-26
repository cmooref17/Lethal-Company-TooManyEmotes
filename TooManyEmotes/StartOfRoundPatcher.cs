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
using static UnityEditor.Progress;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    public static class StartOfRoundPatcher
    {
        public static List<UnlockableEmote> allUnlockableEmotes;
        public static Dictionary<string, UnlockableEmote> allUnlockableEmotesDict;
        public static List<UnlockableEmote> complementaryEmotes;
        public static List<UnlockableEmote> unlockedFavoriteEmotes;
        public static List<string> allFavoriteEmotes;

        public static List<UnlockableEmote> allEmotesTier0;
        public static List<UnlockableEmote> allEmotesTier1;
        public static List<UnlockableEmote> allEmotesTier2;
        public static List<UnlockableEmote> allEmotesTier3;

        public static List<UnlockableEmote> unlockedEmotes;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void CreateEmoteLists()
        {

        }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void InitializeEmotes(StartOfRound __instance) {
            __instance.localClientAnimatorController = new AnimatorOverrideController(__instance.localClientAnimatorController);
            __instance.otherClientsAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);

            allUnlockableEmotes = new List<UnlockableEmote>();
            allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();
            unlockedEmotes = new List<UnlockableEmote>();
            complementaryEmotes = new List<UnlockableEmote>();
            unlockedFavoriteEmotes = new List<UnlockableEmote>();
            allFavoriteEmotes = new List<string>();

            allEmotesTier0 = new List<UnlockableEmote>();
            allEmotesTier1 = new List<UnlockableEmote>();
            allEmotesTier2 = new List<UnlockableEmote>();
            allEmotesTier3 = new List<UnlockableEmote>();

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

                emote.rarity = Plugin.animationClipsTier1.Contains(clip) ? 1 :
                    Plugin.animationClipsTier2.Contains(clip) ? 2 :
                    Plugin.animationClipsTier3.Contains(clip) ? 3 : 0;

                if (emote.emoteName.Contains("_start"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    emote.transitionsToClip = emoteLoop;
                    emote.emoteName = emote.emoteName.Replace("_start", "");
                }
                emote.displayName = emote.emoteName.Replace('_', ' ').Trim(' ');
                emote.displayName = char.ToUpper(emote.displayName[0]) + emote.displayName.Substring(1).ToLower();
                allUnlockableEmotes.Add(emote);
                allUnlockableEmotesDict.Add(emote.emoteName, emote);

                if (Plugin.complementaryAnimationClips.Contains(clip))
                {
                    emote.complementary = true;
                    complementaryEmotes.Add(emote);
                }
            }

            allUnlockableEmotes = allUnlockableEmotes.OrderBy(item => item.rarity).ThenBy(item => item.emoteName).ToList();

            int id = 0;
            foreach (var emote in allUnlockableEmotes)
            {
                emote.emoteId = id;
                if (emote.rarity == 0)
                    allEmotesTier0.Add(emote);
                else if (emote.rarity == 1)
                    allEmotesTier1.Add(emote);
                else if (emote.rarity == 2)
                    allEmotesTier2.Add(emote);
                else if (emote.rarity == 3)
                    allEmotesTier3.Add(emote);
                id++;
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
            unlockedEmotes.Clear();
            unlockedEmotes.AddRange(complementaryEmotes);
            TerminalPatcher.currentEmoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
            TerminalPatcher.emoteStoreSeed = 0;
        }


        public static void UpdateUnlockedFavoriteEmotes()
        {
            unlockedFavoriteEmotes.Clear();
            foreach (string emoteName in allFavoriteEmotes)
            {
                var emote = allUnlockableEmotesDict[emoteName];
                if (emote != null && unlockedEmotes.Contains(emote))
                    unlockedFavoriteEmotes.Add(emote);
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        public static void SyncUnlockedEmotesWithClients(StartOfRound __instance) {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;
            if (ConfigSync.instance.syncUnlockEverything)
                return;
            Plugin.Log("Syncing unlocked emotes with clients.");
            EmoteSyncManager.SendOnUnlockEmoteUpdateMulti(TerminalPatcher.currentEmoteCredits);
        }


        public static void UnlockEmoteLocal(int emoteId) => UnlockEmoteLocal(emoteId >= 0 && emoteId < allUnlockableEmotes.Count ? allUnlockableEmotes[emoteId] : null);
        public static void UnlockEmoteLocal(UnlockableEmote emote) {
            if (emote == null || unlockedEmotes.Contains(emote))
                return;
            unlockedEmotes.Add(emote);
        }


        public static void SortUnlockedEmotes()
        {
            unlockedEmotes = unlockedEmotes.OrderBy(item => item.rarity).ThenBy(item => item.emoteName).ToList();
        }
    }
}