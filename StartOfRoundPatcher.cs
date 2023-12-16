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
        public readonly static int emoteLoadoutSize = 10;
        public static HashSet<UnlockableEmote> unlockedEmotes = new HashSet<UnlockableEmote>();
        public static UnlockableEmote[] currentEmoteLoadout = new UnlockableEmote[emoteLoadoutSize];

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void InitializePlayerAnimationControllers(StartOfRound __instance) {
            __instance.localClientAnimatorController = new AnimatorOverrideController(__instance.localClientAnimatorController);
            __instance.otherClientsAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
            unlockedEmotes = new HashSet<UnlockableEmote>();
            currentEmoteLoadout = new UnlockableEmote[emoteLoadoutSize];

            for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
                __instance.allPlayerScripts[i].playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        public static void ResetEmotes(StartOfRound __instance) {
            unlockedEmotes?.Clear();
            currentEmoteLoadout = new UnlockableEmote[emoteLoadoutSize];
        }


        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        public static void SyncUnlockedEmotesWithClients(StartOfRound __instance) {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;
            Plugin.Log("Syncing unlocked emotes with clients.");
            SyncUnlockedEmotes.SendOnUnlockEmoteUpdateMulti();
        }


        public static void UnlockEmoteLocal(int emoteId) => UnlockEmoteLocal(emoteId >= 0 && emoteId < TerminalPatcher.allUnlockableEmotes.Count ? TerminalPatcher.allUnlockableEmotes[emoteId] : null);
        public static void UnlockEmoteLocal(UnlockableEmote emote) {
            if (emote == null)
                return;
            if (!unlockedEmotes.Contains(emote))
                unlockedEmotes.Add(emote);
            int emoteLoadoutIndex = FindEmptyIndexEmoteLoadout(emote);
            if (emoteLoadoutIndex != -1)
                currentEmoteLoadout[emoteLoadoutIndex] = emote;
        }


        public static int FindEmptyIndexEmoteLoadout(UnlockableEmote emote = null) {
            for (int i = 0; i < currentEmoteLoadout.Length; i++)
            {
                if (currentEmoteLoadout[i] == null || currentEmoteLoadout[i] == emote)
                    return i;
            }
            return -1;
        }
    }
}