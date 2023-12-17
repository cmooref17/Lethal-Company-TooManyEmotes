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
        public static List<UnlockableEmote> unlockedEmotes;
        public static List<UnlockableEmote> complementaryEmotes;

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void InitializeEmotes(StartOfRound __instance) {
            __instance.localClientAnimatorController = new AnimatorOverrideController(__instance.localClientAnimatorController);
            __instance.otherClientsAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
            unlockedEmotes = new List<UnlockableEmote>();
            complementaryEmotes = new List<UnlockableEmote>();
            allUnlockableEmotes = new List<UnlockableEmote>();
            allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();

            for (int i = 0; i < Plugin.customAnimationClips.Count; i++)
            {
                AnimationClip clip = Plugin.customAnimationClips[i];
                UnlockableEmote emote = new UnlockableEmote {
                    emoteId = i,
                    emoteName = clip.name,
                    displayName = clip.name,
                    animationClip = clip,
                    price = 100
                };

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
                    emote.price = 0;
                    complementaryEmotes.Add(emote);
                }
                else
                    emote.price = (int)Mathf.Max(emote.price * ConfigSync.syncPriceMultiplierEmotesStore, 0);
            }
            
            for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
                __instance.allPlayerScripts[i].playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        public static void OnServerStart(StartOfRound __instance) {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                foreach (var emote in complementaryEmotes)
                    UnlockEmoteLocal(emote);

                if (StartOfRound.Instance.randomMapSeed == 0)
                {
                    StartOfRound.Instance.ChooseNewRandomMapSeed();
                    StartOfRound.Instance.overrideRandomSeed = true;
                    StartOfRound.Instance.overrideSeedNumber = StartOfRound.Instance.randomMapSeed;
                    TerminalPatcher.terminalInstance.RotateShipDecorSelection();
                }
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        public static void ResetEmotes(StartOfRound __instance)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                unlockedEmotes = new List<UnlockableEmote>(complementaryEmotes);
            else
                unlockedEmotes = new List<UnlockableEmote>();
        }


        /*
        [HarmonyPatch(typeof(StartOfRound), "ChooseNewRandomMapSeed")]
        [HarmonyPostfix]
        public static void OnChooseNewRandomMapSeed(StartOfRound __instance)
        {
            Plugin.Log("SEED: " + __instance.randomMapSeed);
        }
        */


        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        public static void SyncUnlockedEmotesWithClients(StartOfRound __instance) {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;
            if (ConfigSync.syncUnlockEverything)
                return;
            Plugin.Log("Syncing unlocked emotes with clients.");
            SyncUnlockedEmotes.SendOnUnlockEmoteUpdateMulti();
        }


        public static void UnlockEmoteLocal(int emoteId) => UnlockEmoteLocal(emoteId >= 0 && emoteId < allUnlockableEmotes.Count ? allUnlockableEmotes[emoteId] : null);
        public static void UnlockEmoteLocal(UnlockableEmote emote) {
            if (emote == null || unlockedEmotes.Contains(emote))
                return;
            unlockedEmotes.Add(emote);
        }
    }
}