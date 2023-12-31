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
using TooManyEmotes.Networking;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    public static class SaveManager
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
                ES3.Save("TooManyEmotes.CurrentEmoteCredits", TerminalPatcher.currentEmoteCredits, __instance.currentSaveFileName);
                ES3.Save("TooManyEmotes.EmoteStoreSeed", TerminalPatcher.emoteStoreSeed, __instance.currentSaveFileName);

                Plugin.Log("Saved " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
                Plugin.Log("Saved CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Plugin.Log("Saved Seed: " + TerminalPatcher.emoteStoreSeed);
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

            StartOfRoundPatcher.unlockedEmotes.Clear();
            StartOfRoundPatcher.unlockedEmotesTier0.Clear();
            StartOfRoundPatcher.unlockedEmotesTier1.Clear();
            StartOfRoundPatcher.unlockedEmotesTier2.Clear();
            StartOfRoundPatcher.unlockedEmotesTier3.Clear();
            StartOfRoundPatcher.unlockedFavoriteEmotes.Clear();
            StartOfRoundPatcher.UnlockEmotesLocal(StartOfRoundPatcher.complementaryEmotes);
            TerminalPatcher.currentEmoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
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
                TerminalPatcher.currentEmoteCredits = ES3.Load("TooManyEmotes.CurrentEmoteCredits", GameNetworkManager.Instance.currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);
                TerminalPatcher.emoteStoreSeed = ES3.Load("TooManyEmotes.EmoteStoreSeed", GameNetworkManager.Instance.currentSaveFileName, 0);

                Plugin.Log("Loaded " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
                Plugin.Log("Loaded CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Plugin.Log("Loaded Seed: " + TerminalPatcher.emoteStoreSeed);
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
            Plugin.Log("Resetting TooManyEmotes saved game values.");

            ES3.DeleteKey("TooManyEmotes.UnlockedEmotes", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.EmoteStoreSeed", __instance.currentSaveFileName);

            StartOfRoundPatcher.ResetEmotesLocal();
        }


        public static void SaveFavoritedEmotes()
        {
            ES3.Save("TooManyEmotes.FavoriteEmotes", StartOfRoundPatcher.allFavoriteEmotes.ToArray());
        }


        public static void LoadFavoritedEmotes()
        {
            StartOfRoundPatcher.allFavoriteEmotes.Clear();
            StartOfRoundPatcher.allFavoriteEmotes.AddRange(ES3.Load("TooManyEmotes.FavoriteEmotes", new string[0]));
            StartOfRoundPatcher.UpdateUnlockedFavoriteEmotes();
        }
    }
}