using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TooManyEmotes.Networking;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class SaveManager
    {
        public static string TooManyEmotesSaveFileName = "TooManyEmotes_LocalSaveData";
        //private static List<string> globallyUnlockedEmoteNames = new List<string>();


        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
        [HarmonyPostfix]
        public static void SaveUnlockedEmotes(GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame || !StartOfRound.Instance.inShipPhase)
                return;

            Log("[SaveManager] Saving game values.");

            try
            {
                //if (!ConfigSync.instance.syncPersistentUnlocksGlobal)
                //{
                HashSet<string> usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", currentSaveFileName, new string[0]));
                foreach (string username in SessionManager.unlockedEmotesByPlayer.Keys)
                    usernames.Add(username);
                ES3.Save("TooManyEmotes.UnlockedEmotes.PlayersList", usernames.ToArray(), currentSaveFileName);

                foreach (string username in usernames)
                {
                    // Only save new values
                    if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                        continue;

                    if (SessionManager.unlockedEmotesByPlayer.TryGetValue(username, out var unlockedEmotes))
                    {
                        Log("Saving " + unlockedEmotes.Count + " emotes for player: " + username);
                        string[] playerUnlockedEmoteIds = new string[unlockedEmotes.Count];
                        for (int i = 0; i < unlockedEmotes.Count; i++)
                            playerUnlockedEmoteIds[i] = unlockedEmotes[i].emoteName;
                        if (unlockedEmotes == SessionManager.unlockedEmotes)
                            ES3.Save("TooManyEmotes.UnlockedEmotes", playerUnlockedEmoteIds, currentSaveFileName);
                        else
                            ES3.Save("TooManyEmotes.UnlockedEmotes.Player_" + username, playerUnlockedEmoteIds, currentSaveFileName);
                    }
                    if (TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(username))
                    {
                        Log("Saving " + TerminalPatcher.currentEmoteCreditsByPlayer[username] + " emote credits for player: " + username);
                        if (localPlayerController != null && localPlayerController.playerSteamId != 0 && username == localPlayerController.playerUsername)
                            ES3.Save("TooManyEmotes.CurrentEmoteCredits", TerminalPatcher.currentEmoteCredits, __instance.currentSaveFileName);
                        else
                            ES3.Save("TooManyEmotes.CurrentEmoteCredits.Player_" + username, TerminalPatcher.currentEmoteCreditsByPlayer[username], __instance.currentSaveFileName);
                    }
                }
                //}

                ES3.Save("TooManyEmotes.EmoteStoreSeed", TerminalPatcher.emoteStoreSeed, __instance.currentSaveFileName);

                //Log("Saved " + StartOfRoundPatcher.unlockedEmotes.Count + " unlockable emotes.");
                //Log("Saved CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Log("Saved Seed: " + TerminalPatcher.emoteStoreSeed);
            }

            catch (Exception e)
            {
                LogError("Error while trying to save TooManyEmotes values when disconnecting as host: " + e);
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        public static void LoadUnlockedEmotes(StartOfRound __instance)
        {
            if (!GameNetworkManager.Instance.isHostingGame)
                return;

            Log("[SaveManager] Loading game values.");
            SessionManager.ResetEmotesLocal();

            try
            {
                string[] emoteNames = ES3.Load("TooManyEmotes.UnlockedEmotes", currentSaveFileName, new string[0]);
                foreach (string emoteName in emoteNames)
                {
                    if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                        SessionManager.UnlockEmoteLocal(emote);
                }
                TerminalPatcher.currentEmoteCredits = ES3.Load("TooManyEmotes.CurrentEmoteCredits", currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                string[] usernames = ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", currentSaveFileName, new string[0]);
                foreach (string username in usernames)
                {
                    if (localPlayerController != null && localPlayerController.playerSteamId != 0 && username == localPlayerController.playerUsername)
                        continue;


                    if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(username))
                        SessionManager.unlockedEmotesByPlayer.Add(username, new List<UnlockableEmote>());
                    string key = "TooManyEmotes.UnlockedEmotes.Player_" + username;
                        
                    string[] emoteIds = ES3.Load(key, currentSaveFileName, new string[0]);
                    Log("Loading " + emoteIds.Length + " emotes for player: " + username);
                    foreach (var emoteId in emoteIds)
                    {
                        if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteId, out var emote))
                            SessionManager.UnlockEmoteLocal(emote, username);
                    }

                    key = "TooManyEmotes.CurrentEmoteCredits.Player_" + username;
                    int emoteCredits = ES3.Load(key, currentSaveFileName, ConfigSync.instance.syncStartingEmoteCredits);

                    Log("Loading " + emoteCredits + " emote credits for player: " + username);
                    if (!TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(username))
                        TerminalPatcher.currentEmoteCreditsByPlayer.Add(username, emoteCredits);
                    else
                        TerminalPatcher.currentEmoteCreditsByPlayer[username] = emoteCredits;
                }
                
                TerminalPatcher.emoteStoreSeed = ES3.Load("TooManyEmotes.EmoteStoreSeed", currentSaveFileName, 0);

                Log("Loaded " + SessionManager.unlockedEmotes.Count + " unlockable emotes.");
                Log("Loaded CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits);
                Log("Loaded Seed: " + TerminalPatcher.emoteStoreSeed);
            }
            catch (Exception e)
            {
                LogError("Error while trying to load TooManyEmotes values: " + e);
            }
        }





        /*[HarmonyPatch(typeof(GameNetworkManager), "SaveLocalPlayerValues")]
        [HarmonyPostfix]
        private static void SaveLocalPlayerValues()
        {
            if (!SyncManager.isSynced || !ConfigSync.instance.syncPersistentUnlocksGlobal || SessionManager.unlockedEmotes == null)
                return;

            //Log("Saving local player data.");
            try
            {
                var saveEmoteNames = new List<string>(globallyUnlockedEmoteNames);
                foreach (var emote in SessionManager.unlockedEmotes)
                {
                    if (!emote.complementary && !emote.requiresHeldProp && !saveEmoteNames.Contains(emote.emoteName))
                        saveEmoteNames.Add(emote.emoteName);
                }
                ES3.Save("UnlockedEmotes", saveEmoteNames.ToArray(), TooManyEmotesSaveFileName);
                Log("Saved " + saveEmoteNames.Count + " globally unlocked emotes for local player.");

            }
            catch (Exception e)
            {
                LogError("Error while trying to save TooManyEmotes local player data: " + e);
            }
        }


        internal static void LoadLocalPlayerValues() // Called from SyncManager.OnSynced()
        {
            if (!SyncManager.isSynced || !ConfigSync.instance.syncPersistentUnlocksGlobal)
                return;
            
            //Log("Loading local saved data.");
            try
            {
                string[] loadEmoteNames = ES3.Load("UnlockedEmotes", TooManyEmotesSaveFileName, new string[0]);
                foreach (string emoteName in loadEmoteNames)
                {
                    if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                        SessionManager.UnlockEmoteLocal(emote);
                }
                globallyUnlockedEmoteNames.Clear();
                globallyUnlockedEmoteNames.AddRange(loadEmoteNames);
                Log("Loaded " + loadEmoteNames.Length + " globally unlocked emotes for local player.");
            }
            catch (Exception e)
            {
                LogError("Error while trying to load TooManyEmotes local player data: " + e);
            }
        }


        internal static void ResetGloballyUnlockedEmotes()
        {
            Log("Resetting globally unlocked emotes for local player.");
            ES3.DeleteKey("UnlockedEmotes", TooManyEmotesSaveFileName);
            globallyUnlockedEmoteNames?.Clear();
        }*/





        [HarmonyPatch(typeof(GameNetworkManager), "ResetSavedGameValues")]
        [HarmonyPostfix]
        public static void ResetUnlockedEmotesList(GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame || StartOfRound.Instance == null || SessionManager.unlockedEmotes == null)
                return;

            Log("Resetting saved game values.");

            ES3.DeleteKey("TooManyEmotes.UnlockedEmotes", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.CurrentEmoteCredits", __instance.currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.EmoteStoreSeed", __instance.currentSaveFileName);

            HashSet<string> usernames = new HashSet<string>(ES3.Load("TooManyEmotes.UnlockedEmotes.PlayersList", __instance.currentSaveFileName, new string[0]));
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
            ES3.Save("TooManyEmotes.FavoriteEmotes", EmotesManager.allFavoriteEmotes.ToArray(), TooManyEmotesSaveFileName);
        }


        public static void LoadFavoritedEmotes()
        {
            EmotesManager.allFavoriteEmotes.Clear();
            try
            {
                var addFavoritedEmotes = ES3.Load("TooManyEmotes.FavoriteEmotes", TooManyEmotesSaveFileName, new string[0]);
                EmotesManager.allFavoriteEmotes.AddRange(addFavoritedEmotes);
                ES3.DeleteKey("TooManyEmotes.FavoriteEmotes"); // Old save location
            }
            catch (Exception e)
            {
                LogError("Error while trying to load favorited emotes due to possible save corruption? Your favorited emotes will likely be reset.\n" + e);
                ES3.DeleteKey("TooManyEmotes.FavoriteEmotes", TooManyEmotesSaveFileName);
            }
            SessionManager.UpdateUnlockedFavoriteEmotes();
        }


        internal static void ResetFavoritedEmotes()
        {
            Log("Resetting favorited emotes for local player.");
            ES3.DeleteKey("TooManyEmotes.FavoriteEmotes");
            SessionManager.unlockedFavoriteEmotes.Clear();
            SessionManager.UpdateUnlockedFavoriteEmotes();
        }
    }
}