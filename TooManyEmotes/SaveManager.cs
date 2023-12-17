using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using TooManyEmotes.Config;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using System.Collections;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    internal class SaveManager
    {
        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
        [HarmonyPostfix]
        public static void SaveUnlockedEmotes(GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame)
                return;
            if (!StartOfRound.Instance.inShipPhase)
                return;

            try
            {
                string[] unlockedEmoteIds = new string[StartOfRoundPatcher.unlockedEmotes.Count];
                int index = 0;
                foreach (var emote in StartOfRoundPatcher.unlockedEmotes)
                    unlockedEmoteIds[index++] = emote.emoteName;
                ES3.Save("TooManyEmotes.UnlockedEmotes", unlockedEmoteIds, __instance.currentSaveFileName);
                Plugin.Log("Saved " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
            }

            catch (Exception arg)
            {
                Plugin.LogError(string.Format("Error while trying to save TooManyEmotes values when disconnecting as host: {0}", arg));
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        public static void LoadUnlockedEmotes(StartOfRound __instance) {
            if (!GameNetworkManager.Instance.isHostingGame)
                return;

            StartOfRoundPatcher.unlockedEmotes = new List<UnlockableEmote>(StartOfRoundPatcher.complementaryEmotes);
            try
            {
                if (ES3.KeyExists("TooManyEmotes.UnlockedEmotes", GameNetworkManager.Instance.currentSaveFileName))
                {
                    string[] emoteIds = ES3.Load<string[]>("TooManyEmotes.UnlockedEmotes", GameNetworkManager.Instance.currentSaveFileName);
                    for (int i = 0; i < emoteIds.Length; i++)
                    {
                        if (StartOfRoundPatcher.allUnlockableEmotesDict.ContainsKey(emoteIds[i]))
                        {
                            var emote = StartOfRoundPatcher.allUnlockableEmotesDict[emoteIds[i]];
                            if (!StartOfRoundPatcher.unlockedEmotes.Contains(emote))
                                StartOfRoundPatcher.UnlockEmoteLocal(emote);
                        }
                        else
                            Plugin.LogError("Tried to load emote that doesn't exist: " + emoteIds[i]);
                    }
                }
                Plugin.Log("Loaded " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
            }

            catch (Exception arg)
            {
                Plugin.LogError(string.Format("Error while trying to load TooManyEmotes values when disconnecting as host: {0}", arg));
            }
        }

        
        [HarmonyPatch(typeof(GameNetworkManager), "ResetSavedGameValues")]
        [HarmonyPostfix]
        public static void ResetUnlockedEmotesList(GameNetworkManager __instance) {
            if (!__instance.isHostingGame)
                return;
            if (StartOfRound.Instance == null || StartOfRoundPatcher.unlockedEmotes == null)
                return;
            Plugin.Log("Resetting unlockable emotes.");

            ES3.DeleteKey("TooManyEmotes.UnlockedEmotes", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.FreeEmoteCoupon", __instance.currentSaveFileName);

            if (StartOfRoundPatcher.unlockedEmotes != null)
                StartOfRoundPatcher.unlockedEmotes.Clear();
        }
    }
}