using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public static class SaveManager
    {
        public static string TooManyEmotesSaveFileName = "TooManyEmotes_LocalSaveData";
        private static List<string> globallyUnlockedEmoteNames = new List<string>();


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        private static void CheckIfShouldResetLocalSettings()
        {
            if (ConfigSettings.resetGloballyUnlockedEmotes)
                ResetGloballyUnlockedEmotes();
            if (ConfigSettings.resetFavoriteEmotes)
            {
                ResetFavoritedEmotes();
                //ResetQuickEmotes();
            }

            ConfigSettings.resetGloballyUnlockedEmotes = false;
            ConfigSettings.resetFavoriteEmotes = false;
        }


        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
        [HarmonyPostfix]
        private static void SaveUnlockedEmotes(GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame || !StartOfRound.Instance.inShipPhase)
                return;

            // Don't save values if playing with party pooper mode
            if (ConfigSync.instance.syncUnlockEverything)
                return;

            Log("Saving game values.");

            try
            {
                HashSet<string> usernames;
                try
                {
                    usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", currentSaveFileName, new string[0]));
                }
                catch (Exception e)
                {
                    LogErrorVerbose("Error loading previous users list. Deleting key: TooManyEmotes.UnlockedEmotes.PlayersList from file: " + currentSaveFileName);
                    LogErrorVerbose(e.ToString());
                    ES3.DeleteKey("TooManyEmotes.UnlockedEmotes.PlayersList", currentSaveFileName);
                    usernames = new HashSet<string>();
                }

                foreach (string username in SessionManager.unlockedEmotesByPlayer.Keys)
                    usernames.Add(username);
                ES3.Save("TooManyEmotes.UnlockedEmotes.PlayersList", usernames.ToArray(), currentSaveFileName);

                foreach (string username in usernames)
                {
                    if (!ConfigSync.instance.syncPersistentUnlocksGlobal)
                    {
                        // Only save new values
                        if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                            continue;

                        if (SessionManager.unlockedEmotesByPlayer.TryGetValue(username, out var unlockedEmotes))
                        {
                            Log("Saving " + unlockedEmotes.Count + " unlocked emotes for player: " + username);
                            string[] playerUnlockedEmoteIds = new string[unlockedEmotes.Count];
                            for (int i = 0; i < unlockedEmotes.Count; i++)
                                playerUnlockedEmoteIds[i] = unlockedEmotes[i].emoteName;
                            if (unlockedEmotes == SessionManager.unlockedEmotes)
                                ES3.Save("TooManyEmotes.UnlockedEmotes", playerUnlockedEmoteIds, currentSaveFileName);
                            else
                                ES3.Save("TooManyEmotes.UnlockedEmotes.Player_" + username, playerUnlockedEmoteIds, currentSaveFileName);
                        }
                    }
                    if (TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(username))
                    {
                        Log("Saving " + TerminalPatcher.currentEmoteCreditsByPlayer[username] + " emote credits for player: " + username);
                        string key = "TooManyEmotes.CurrentEmoteCredits" + (ConfigSync.instance.syncPersistentUnlocks ? ".Persistent" : "");
                        if (localPlayerController != null && localPlayerController.playerSteamId != 0 && username == localPlayerController.playerUsername)
                            ES3.Save(key, TerminalPatcher.currentEmoteCredits, __instance.currentSaveFileName);
                        else
                            ES3.Save(key + ".Player_" + username, TerminalPatcher.currentEmoteCreditsByPlayer[username], __instance.currentSaveFileName);
                    }
                }
                //Log("Saved " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
                //Log("Saved CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);

                ES3.Save("TooManyEmotes.EmoteStoreSeed", TerminalPatcher.emoteStoreSeed, __instance.currentSaveFileName);
                Log("Saved Seed: " + TerminalPatcher.emoteStoreSeed);
            }

            catch (Exception e)
            {
                LogError("Error while trying to save TooManyEmotes values when disconnecting as host.");
                LogError(e.ToString());
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        private static void LoadUnlockedEmotes(StartOfRound __instance)
        {
            if (!GameNetworkManager.Instance.isHostingGame)
                return;

            // Don't load values if playing with party pooper mode
            if (ConfigSync.instance.syncUnlockEverything)
                return;

            Log("Loading game values.");

            try
            {
                if (!ConfigSync.instance.syncPersistentUnlocksGlobal)
                {
                    SessionManager.ResetEmotesLocal();
                    string[] emoteNames = ES3.Load("TooManyEmotes.UnlockedEmotes", currentSaveFileName, new string[0]);
                    foreach (string emoteName in emoteNames)
                    {
                        if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                            SessionManager.UnlockEmoteLocal(emote);
                    }
                }
                string key = "TooManyEmotes.CurrentEmoteCredits" + (ConfigSync.instance.syncPersistentUnlocks ? ".Persistent" : "");
                TerminalPatcher.currentEmoteCredits = ES3.Load(key, currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                string[] usernames = ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", currentSaveFileName, new string[0]);
                foreach (string username in usernames)
                {
                    if (localPlayerController != null && localPlayerController.playerSteamId != 0 && username == localPlayerController.playerUsername)
                        continue;

                    if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                        SessionManager.unlockedEmotesByPlayer.Add(username, new List<UnlockableEmote>());
                    key = "TooManyEmotes.UnlockedEmotes.Player_" + username;

                    if (!ConfigSync.instance.syncPersistentUnlocksGlobal)
                    {
                        string[] emoteIds = ES3.Load(key, currentSaveFileName, new string[0]);
                        Log("Loading " + emoteIds.Length + " unlocked emotes for player: " + username);
                        foreach (var emoteId in emoteIds)
                        {
                            if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteId, out var emote))
                                SessionManager.UnlockEmoteLocal(emote, playerUsername: username);
                        }
                    }

                    key = "TooManyEmotes.CurrentEmoteCredits.Player_" + username;
                    if (ConfigSync.instance.syncPersistentUnlocks)
                        key = key.Replace("CurrentEmoteCredits", "CurrentEmoteCredits.Persistent");
                    int emoteCredits = ES3.Load(key, currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                    Log("Loading " + emoteCredits + " emote credits for player: " + username);
                    TerminalPatcher.currentEmoteCreditsByPlayer[username] = emoteCredits;
                }
                //Log("Loaded " + SessionManager.unlockedEmotes.Count + " unlockable emotes.");
                //Log("Loaded CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);

                TerminalPatcher.emoteStoreSeed = ES3.Load("TooManyEmotes.EmoteStoreSeed", currentSaveFileName, 0);
                Log("Loaded Seed: " + TerminalPatcher.emoteStoreSeed);
            }
            catch (Exception e)
            {
                LogError("Error while trying to load TooManyEmotes values: " + e);
            }
        }


        [HarmonyPatch(typeof(GameNetworkManager), "ResetSavedGameValues")]
        [HarmonyPrefix]
        private static void ResetUnlockedEmotesList(GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame || StartOfRound.Instance == null || SessionManager.unlockedEmotes == null)
                return;

            Log("Resetting saved game values.");

            if (!ConfigSync.instance.syncPersistentUnlocks)
            {
                ES3.DeleteKey("TooManyEmotes.UnlockedEmotes", __instance.currentSaveFileName);
                ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits.Persistent", __instance.currentSaveFileName);
            }
            ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.EmoteStoreSeed", __instance.currentSaveFileName);

            HashSet<string> usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", __instance.currentSaveFileName, new string[0]));
            foreach (string username in SessionManager.unlockedEmotesByPlayer.Keys)
                usernames.Add(username);
            foreach (string username in usernames)
            {
                if (!ConfigSync.instance.syncPersistentUnlocks)
                {
                    ES3.DeleteKey("TooManyEmotes.UnlockedEmotes.Player_" + username, __instance.currentSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits.Persistent.Player_" + username, __instance.currentSaveFileName);
                }
                ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits.Player_" + username, __instance.currentSaveFileName); // This is not the persistent key
            }

            SessionManager.ResetProgressLocal();
        }





        [HarmonyPatch(typeof(GameNetworkManager), "SaveLocalPlayerValues")]
        [HarmonyPrefix]
        private static void SaveLocalPlayerValues()
        {
            if (!isClient || !SyncManager.isSynced || ConfigSync.instance == null || !ConfigSync.instance.syncPersistentUnlocksGlobal || globallyUnlockedEmoteNames == null || SessionManager.emotesUnlockedThisSession == null)
                return;

            try
            {
                foreach (var emote in SessionManager.emotesUnlockedThisSession)
                {
                    if (emote != null && !emote.complementary && !emote.requiresHeldProp && !globallyUnlockedEmoteNames.Contains(emote.emoteName))
                        globallyUnlockedEmoteNames.Add(emote.emoteName);
                }
                ES3.Save("UnlockedEmotes", globallyUnlockedEmoteNames.ToArray(), TooManyEmotesSaveFileName);
                Log("Saved " + globallyUnlockedEmoteNames.Count + " globally unlocked emotes for local player.");
            }
            catch (Exception e) { LogErrorVerbose("Error while trying to save TooManyEmotes local player data.\n" + e); }
        }


        internal static void LoadLocalPlayerValues() // Called from SyncManager.OnSynced()
        {
            if (!isClient || !SyncManager.isSynced || !ConfigSync.instance.syncPersistentUnlocksGlobal || globallyUnlockedEmoteNames == null || EmotesManager.allUnlockableEmotesDict == null)
                return;
            
            try
            {
                var loadEmoteNames = ES3.Load("UnlockedEmotes", TooManyEmotesSaveFileName, new string[0]);
                foreach (string emoteName in loadEmoteNames)
                {
                    if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote) && !emote.complementary && !emote.requiresHeldProp)
                        SessionManager.UnlockEmoteLocal(emote);
                }
                globallyUnlockedEmoteNames.Clear();
                globallyUnlockedEmoteNames.AddRange(loadEmoteNames);
                Log("Loaded " + loadEmoteNames.Length + " globally unlocked emotes for local player.");
            }
            catch (Exception e) { LogErrorVerbose("Error while trying to load TooManyEmotes local player data.\n" + e); }
        }


        internal static void ResetGloballyUnlockedEmotes()
        {
            LogWarning("Resetting globally unlocked emotes for local player.");
            try
            {
                globallyUnlockedEmoteNames?.Clear();
                SessionManager.emotesUnlockedThisSession?.Clear();
                ES3.DeleteKey("UnlockedEmotes", TooManyEmotesSaveFileName);
            }
            catch (Exception e) { LogErrorVerbose("Error resetting globally unlocked emotes?\n" + e); }
        }





        public static void SaveFavoritedEmotes()
        {
            if (EmotesManager.allFavoriteEmotes != null)
            {
                try
                {
                    ES3.Save("TooManyEmotes.FavoriteEmotes", EmotesManager.allFavoriteEmotes.ToArray(), TooManyEmotesSaveFileName);
                }
                catch (Exception e) { LogErrorVerbose("Error saving favorited emotes?\n" + e); }
            }
        }


        public static void LoadFavoritedEmotes()
        {
            if (EmotesManager.allFavoriteEmotes == null)
                return;

            EmotesManager.allFavoriteEmotes?.Clear();
            try
            {
                var addFavoritedEmotes = ES3.Load("TooManyEmotes.FavoriteEmotes", TooManyEmotesSaveFileName, new string[0]);
                EmotesManager.allFavoriteEmotes.AddRange(addFavoritedEmotes);
            }
            catch (Exception e)
            {
                LogError("Error while trying to load favorited emotes due to possible save corruption? Your favorited emotes will likely be reset.\n" + e);
                try
                {
                    ES3.DeleteKey("TooManyEmotes.FavoriteEmotes", TooManyEmotesSaveFileName);
                    LogErrorVerbose("Deleted key: \"TooManyEmotes.FavoriteEmotes\" from file: \"" + TooManyEmotesSaveFileName + "\"");
                }
                catch 
                {
                    LogErrorVerbose("Could not delete key: \"TooManyEmotes.FavoriteEmotes\" from file: \"" + TooManyEmotesSaveFileName + "\"");
                }
            }
            SessionManager.UpdateUnlockedFavoriteEmotes();
        }


        internal static void ResetFavoritedEmotes()
        {
            LogWarning("Resetting favorited emotes for local player.");
            try
            {
                ES3.DeleteKey("TooManyEmotes.FavoriteEmotes", TooManyEmotesSaveFileName);
                SessionManager.unlockedFavoriteEmotes?.Clear();
                SessionManager.UpdateUnlockedFavoriteEmotes();
            }
            catch (Exception e) { LogErrorVerbose("Error resetting favorite emotes?\n" + e); }
        }





        public static void SaveQuickEmotes()
        {
            if (EmotesManager.allQuickEmotes != null)
            {
                try
                {
                    for (int i = 0; i < EmotesManager.allQuickEmotes.Count; i++)
                    {
                        string emoteName = EmotesManager.allQuickEmotes[i] ?? "";
                        ES3.Save("TooManyEmotes.QuickEmote" + i, emoteName, TooManyEmotesSaveFileName);
                    }
                }
                catch (Exception e) { LogErrorVerbose("Error saving quick emotes?\n" + e); }
            }
        }


        public static void LoadQuickEmotes()
        {
            if (EmotesManager.allQuickEmotes == null)
                return;

            try
            {
                for (int i = 0; i < EmotesManager.allQuickEmotes.Count; i++)
                {
                    string emoteName = ES3.Load("TooManyEmotes.QuickEmote" + i, TooManyEmotesSaveFileName, "");
                    EmotesManager.allQuickEmotes[i] = emoteName;
                }
            }
            catch (Exception e)
            {
                LogError("Error while trying to load quick emotes due to possible save corruption? Your quick emotes will likely be reset.\n" + e);
                for (int i = 0; i < EmotesManager.allQuickEmotes.Count; i++)
                {
                    string key = "TooManyEmotes.QuickEmote" + i;
                    try
                    {
                        ES3.DeleteKey(key, TooManyEmotesSaveFileName);
                        LogErrorVerbose("Deleted key: \"" + key + "\" from file: \"" + TooManyEmotesSaveFileName + "\"");
                    }
                    catch
                    {
                        LogErrorVerbose("Could not delete key: \"" + key + "\" from file: \"" + TooManyEmotesSaveFileName + "\"");
                    }
                }
            }
        }


        internal static void ResetQuickEmotes()
        {
            LogWarning("Resetting quick emotes.");
            try
            {
                for (int i = 0; i < EmotesManager.allQuickEmotes.Count; i++)
                {
                    string key = "TooManyEmotes.QuickEmote" + i;
                    ES3.DeleteKey(key, TooManyEmotesSaveFileName);
                    EmotesManager.allQuickEmotes[i] = "";
                }
            }
            catch (Exception e) { LogErrorVerbose("Error resetting quick emotes?\n" + e); }
        }
    }
}