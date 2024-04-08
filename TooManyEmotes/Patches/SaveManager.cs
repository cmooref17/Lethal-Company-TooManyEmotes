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

            Plugin.Log("[SaveManager] Saving game values.");

            try
            {
                HashSet<string> usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", GameNetworkManager.Instance.currentSaveFileName, new string[0]));
                foreach (string username in SessionManager.unlockedEmotesByPlayer.Keys)
                    usernames.Add(username);
                ES3.Save("TooManyEmotes.UnlockedEmotes.PlayersList", usernames.ToArray(), GameNetworkManager.Instance.currentSaveFileName);

                foreach (string username in usernames)
                {
                    // Only save new values
                    if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                        continue;

                    if (SessionManager.unlockedEmotesByPlayer.TryGetValue(username, out var unlockedEmotes))
                    {
                        Plugin.Log("Saving " + unlockedEmotes.Count + " emotes for player: " + username);
                        string[] playerUnlockedEmoteIds = new string[unlockedEmotes.Count];
                        for (int i = 0; i < unlockedEmotes.Count; i++)
                            playerUnlockedEmoteIds[i] = unlockedEmotes[i].emoteName;
                        if (unlockedEmotes == SessionManager.unlockedEmotes)
                            ES3.Save("TooManyEmotes.UnlockedEmotes", playerUnlockedEmoteIds, GameNetworkManager.Instance.currentSaveFileName);
                        else
                            ES3.Save("TooManyEmotes.UnlockedEmotes.Player_" + username, playerUnlockedEmoteIds, GameNetworkManager.Instance.currentSaveFileName);
                    }
                    if (TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(username))
                    {
                        Plugin.Log("Saving " + TerminalPatcher.currentEmoteCreditsByPlayer[username] + " emote credits for player: " + username);
                        if (StartOfRound.Instance.localPlayerController != null && StartOfRound.Instance.localPlayerController.playerSteamId != 0 && username == StartOfRound.Instance.localPlayerController.playerUsername)
                            ES3.Save("TooManyEmotes.CurrentEmoteCredits", TerminalPatcher.currentEmoteCredits, __instance.currentSaveFileName);
                        else
                            ES3.Save("TooManyEmotes.CurrentEmoteCredits.Player_" + username, TerminalPatcher.currentEmoteCreditsByPlayer[username], __instance.currentSaveFileName);
                    }
                }

                ES3.Save("TooManyEmotes.EmoteStoreSeed", TerminalPatcher.emoteStoreSeed, __instance.currentSaveFileName);

                //Plugin.Log("Saved " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
                //Plugin.Log("Saved CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Plugin.Log("Saved Seed: " + TerminalPatcher.emoteStoreSeed);
            }

            catch (Exception arg)
            {
                Plugin.LogError(string.Format("Error while trying to save TooManyEmotes values when disconnecting as host: {0}", arg));
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        public static void LoadUnlockedEmotes(StartOfRound __instance)
        {
            if (!GameNetworkManager.Instance.isHostingGame)
                return;

            Plugin.Log("[SaveManager] Loading game values.");
            SessionManager.ResetEmotesLocal();

            try
            {
                string[] emoteNames = ES3.Load("TooManyEmotes.UnlockedEmotes", GameNetworkManager.Instance.currentSaveFileName, new string[0]);
                foreach (string emoteName in emoteNames)
                {
                    if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                        SessionManager.UnlockEmoteLocal(emote);
                }
                TerminalPatcher.currentEmoteCredits = ES3.Load("TooManyEmotes.CurrentEmoteCredits", GameNetworkManager.Instance.currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                string[] usernames = ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", GameNetworkManager.Instance.currentSaveFileName, new string[0]);
                foreach (string username in usernames)
                {
                    if (StartOfRound.Instance.localPlayerController != null && StartOfRound.Instance.localPlayerController.playerSteamId != 0 && username == StartOfRound.Instance.localPlayerController.playerUsername)
                        continue;


                    if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                        SessionManager.unlockedEmotesByPlayer.Add(username, new List<UnlockableEmote>());
                    string key = "TooManyEmotes.UnlockedEmotes.Player_" + username;
                        
                    string[] emoteIds = ES3.Load(key, GameNetworkManager.Instance.currentSaveFileName, new string[0]);
                    Plugin.Log("Loading " + emoteIds.Length + " emotes for player: " + username);
                    foreach (var emoteId in emoteIds)
                    {
                        if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteId, out var emote))
                            SessionManager.UnlockEmoteLocal(emote, username);
                    }

                    key = "TooManyEmotes.CurrentEmoteCredits.Player_" + username;
                    int emoteCredits = ES3.Load(key, GameNetworkManager.Instance.currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                    Plugin.Log("Loading " + emoteCredits + " emote credits for player: " + username);
                    if (!TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(username))
                        TerminalPatcher.currentEmoteCreditsByPlayer.Add(username, emoteCredits);
                    else
                        TerminalPatcher.currentEmoteCreditsByPlayer[username] = emoteCredits;
                }
                
                TerminalPatcher.emoteStoreSeed = ES3.Load("TooManyEmotes.EmoteStoreSeed", GameNetworkManager.Instance.currentSaveFileName, 0);

                Plugin.Log("Loaded " + SessionManager.unlockedEmotes.Count + " unlockable emotes.");
                Plugin.Log("Loaded CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Plugin.Log("Loaded Seed: " + TerminalPatcher.emoteStoreSeed);
            }
            catch (Exception arg)
            {
                Plugin.LogError("Error while trying to load TooManyEmotes values: " + arg);
            }
        }

        
        [HarmonyPatch(typeof(GameNetworkManager), "ResetSavedGameValues")]
        [HarmonyPostfix]
        public static void ResetUnlockedEmotesList(GameNetworkManager __instance) {
            if (!__instance.isHostingGame)
                return;
            if (StartOfRound.Instance == null || SessionManager.unlockedEmotes == null)
                return;

            Plugin.Log("[SaveManager] Resetting game values.");

            ES3.DeleteKey("TooManyEmotes.UnlockedEmotes", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.EmoteStoreSeed", __instance.currentSaveFileName);

            HashSet<string> usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", GameNetworkManager.Instance.currentSaveFileName, new string[0]));
            foreach (string username in SessionManager.unlockedEmotesByPlayer.Keys)
                usernames.Add(username);
            foreach (string username in usernames)
            {
                ES3.DeleteKey("TooManyEmotes.UnlockedEmotes.Player_" + username, __instance.currentSaveFileName);
                ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits.Player_" + username, __instance.currentSaveFileName);
            }

            SessionManager.ResetProgressLocal();
        }


        public static void SaveFavoritedEmotes()
        {
            ES3.Save("TooManyEmotes.FavoriteEmotes", EmotesManager.allFavoriteEmotes.ToArray());
        }


        public static void LoadFavoritedEmotes()
        {
            EmotesManager.allFavoriteEmotes.Clear();
            EmotesManager.allFavoriteEmotes.AddRange(ES3.Load("TooManyEmotes.FavoriteEmotes", new string[0]));
            //EmotesManager.allFavoriteEmotes.RemoveAll(emoteName => !EmotesManager.allUnlockableEmotesDict.ContainsKey(emoteName));
            SessionManager.UpdateUnlockedFavoriteEmotes();
        }
    }
}