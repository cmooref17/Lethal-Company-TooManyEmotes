using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using Unity.Netcode;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public static class SessionManager
    {
        public static string localPlayerUsername { get { return GameNetworkManager.Instance?.username; } }

        public static List<UnlockableEmote> unlockedEmotes = new List<UnlockableEmote>();
        public static List<UnlockableEmote> unlockedEmotesTier0 = new List<UnlockableEmote>();
        public static List<UnlockableEmote> unlockedEmotesTier1 = new List<UnlockableEmote>();
        public static List<UnlockableEmote> unlockedEmotesTier2 = new List<UnlockableEmote>();
        public static List<UnlockableEmote> unlockedEmotesTier3 = new List<UnlockableEmote>();

        internal static List<UnlockableEmote> emotesUnlockedThisSession = new List<UnlockableEmote>();
        
        public static Dictionary<string, List<UnlockableEmote>> unlockedEmotesByPlayer = new Dictionary<string, List<UnlockableEmote>>();
        public static List<UnlockableEmote> unlockedFavoriteEmotes = new List<UnlockableEmote>();


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void ResetGameValues()
        {
            EmoteController.allEmoteControllers?.Clear();
            EmoteControllerPlayer.allPlayerEmoteControllers?.Clear();
            EmoteControllerMaskedEnemy.allMaskedEnemyEmoteControllers?.Clear();
            EmoteAudioSource.allEmoteAudioSources?.Clear();
            EmotesManager.complementaryEmotes = new List<UnlockableEmote>(EmotesManager.complementaryEmotesDefault);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void OnHostConnected(PlayerControllerB __instance)
        {
            if (!isServer)
                return;

            if (!unlockedEmotesByPlayer.ContainsKey(localPlayerController.playerUsername))
            {
                unlockedEmotesByPlayer.Add(localPlayerController.playerUsername, unlockedEmotes);
                TerminalPatcher.currentEmoteCreditsByPlayer.Add(localPlayerController.playerUsername, TerminalPatcher.currentEmoteCredits);
            }
            else
            {
                foreach (var emote in unlockedEmotesByPlayer[localPlayerController.playerUsername])
                {
                    if (!IsEmoteUnlocked(emote))
                        unlockedEmotes.Add(emote);
                }
                unlockedEmotesByPlayer[localPlayerController.playerUsername] = unlockedEmotes;
                TerminalPatcher.currentEmoteCreditsByPlayer[localPlayerController.playerUsername] = TerminalPatcher.currentEmoteCredits;
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        private static void OnServerStart(StartOfRound __instance)
        {
            if (isServer)
            {
                //SyncManager.OnSynced();
            }
            //UnlockEmotesLocal(ConfigSync.instance.syncUnlockEverything ? EmotesManager.allUnlockableEmotes : EmotesManager.complementaryEmotes);
            SyncManager.RotateEmoteSelectionServer(TerminalPatcher.emoteStoreSeed);
        }


        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPostfix]
        private static void ResetOverrideSeedFlag(StartOfRound __instance)
        {
            __instance.overrideRandomSeed = false;
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        private static void ResetEmotesOnShipReset(StartOfRound __instance)
        {
            if (!ConfigSync.instance.syncUnlockEverything)
                ResetProgressLocal();
            if (isServer)
                SyncManager.RotateEmoteSelectionServer();
        }


        public static void ResetProgressLocal(bool forceResetAll = false)
        {
            Log("Resetting progress.");
            if (!ConfigSync.instance.syncPersistentUnlocks || forceResetAll)
                ResetEmotesLocal();
            if (!ConfigSync.instance.syncPersistentEmoteCredits || forceResetAll)
            {
                TerminalPatcher.currentEmoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
                var usernames = new List<string>(TerminalPatcher.currentEmoteCreditsByPlayer.Keys);
                foreach (string username in usernames)
                    TerminalPatcher.currentEmoteCreditsByPlayer[username] = ConfigSync.instance.syncStartingEmoteCredits;
            }
            TerminalPatcher.emoteStoreSeed = 0;
        }


        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        private static void SyncUnlockedEmotesWithClients(StartOfRound __instance)
        {
            if (!isServer)
                return;
            //if (ConfigSync.instance.syncUnlockEverything)
            if (ConfigSync.instance.syncUnlockEverything || ConfigSync.instance.syncPersistentUnlocksGlobal)
                return;

            if (ConfigSync.instance.syncShareEverything)
            {
                Log("Syncing unlocked emotes with clients.");
                SyncManager.SendOnUnlockEmoteUpdateMulti(TerminalPatcher.currentEmoteCredits);
            }
            else
            {
                HashSet<ulong> syncWithClients = new HashSet<ulong>();
                foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
                {
                    if (playerController.actualClientId != 0 && playerController.playerSteamId != 0)
                        syncWithClients.Add(playerController.actualClientId); // Prevent duplicates
                }
                foreach (var clientId in syncWithClients)
                    SyncManager.ServerSendSyncToClient(clientId);
            }
        }


        public static bool IsEmoteUnlocked(string emoteName, string playerUsername = "")
        {
            if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                return IsEmoteUnlocked(emote, playerUsername: playerUsername);
            return false;
        }


        public static bool IsEmoteUnlocked(UnlockableEmote emote, string playerUsername = "")
        {
            if (emote == null)
                return false;

            var emotes = unlockedEmotes;
            if (playerUsername != "" && !unlockedEmotesByPlayer.TryGetValue(playerUsername, out emotes))
                return false;

            if (emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 0)
            {
                foreach (var otherEmote in emote.emoteSyncGroup)
                {
                    if (emotes.Contains(otherEmote))
                        return true;
                }
            }
            return emotes.Contains(emote);
        }


        public static List<UnlockableEmote> GetUnlockedEmotes(PlayerControllerB playerController) => GetUnlockedEmotes(playerController != null ? playerController.playerUsername : "");
        public static List<UnlockableEmote> GetUnlockedEmotes(string playerUsername)
        {
            if (unlockedEmotesByPlayer.TryGetValue(playerUsername, out var unlockedEmotes))
                return unlockedEmotes;
            return null;
        }


        public static void UnlockEmotesLocal(IEnumerable<UnlockableEmote> emotes, bool purchased = false, string playerUsername = "")
        {
            foreach (var emote in emotes)
                UnlockEmoteLocal(emote, purchased: purchased, playerUsername: playerUsername);
        }
        public static void UnlockEmoteLocal(int emoteId, bool purchased = false, string playerUsername = "") => UnlockEmoteLocal(emoteId >= 0 && emoteId < EmotesManager.allUnlockableEmotes.Count ? EmotesManager.allUnlockableEmotes[emoteId] : null, purchased: purchased, playerUsername: playerUsername);
        public static void UnlockEmoteLocal(UnlockableEmote emote, bool purchased = false, string playerUsername = "")
        {
            if (emote == null)
                return;

            if (emote.requiresHeldProp)
            {
                if (ConfigSync.instance.syncRemoveGrabbableEmotesPartyPooperMode)
                    return;
            }

            var _unlockedEmotes = unlockedEmotes;

            if (playerUsername != "" && playerUsername != localPlayerUsername)
            {
                if (!unlockedEmotesByPlayer.TryGetValue(playerUsername, out var emotes) && !ConfigSync.instance.syncShareEverything)
                    return;
                if (emotes != null)
                    _unlockedEmotes = emotes;
            }

            //if (playerUsername != "" && playerUsername != localPlayerUsername && !ConfigSync.instance.syncShareEverything && !unlockedEmotesByPlayer.TryGetValue(playerUsername, out _unlockedEmotes))
                //return;

            // Check if one of the emotes in the sync group is already unlocked
            if (IsEmoteUnlocked(emote, playerUsername))
                return;

            if (emote.emoteSyncGroup != null)
            {
                foreach (var syncEmote in emote.emoteSyncGroup)
                {
                    if (_unlockedEmotes.Contains(syncEmote))
                        return;
                }
            }
            if (!_unlockedEmotes.Contains(emote))
                _unlockedEmotes.Add(emote);

            if (_unlockedEmotes == unlockedEmotes)
            {
                if (!emote.complementary)
                {
                    if (emote.rarity == 3 && !unlockedEmotesTier3.Contains(emote))
                        unlockedEmotesTier3.Add(emote);
                    else if (emote.rarity == 2 && !unlockedEmotesTier2.Contains(emote))
                        unlockedEmotesTier2.Add(emote);
                    else if (emote.rarity == 1 && !unlockedEmotesTier1.Contains(emote))
                        unlockedEmotesTier1.Add(emote);
                    else if (emote.rarity == 0 && !unlockedEmotesTier0.Contains(emote))
                        unlockedEmotesTier0.Add(emote);
                }

                if (EmotesManager.allFavoriteEmotes.Contains(emote.emoteName) && !unlockedFavoriteEmotes.Contains(emote))
                    unlockedFavoriteEmotes.Add(emote);
                if (ConfigSync.instance.syncPersistentUnlocksGlobal && purchased && !emotesUnlockedThisSession.Contains(emote))
                    emotesUnlockedThisSession.Add(emote);
            }
        }


        public static void RemoveEmoteLocal(UnlockableEmote emote)
        {
            unlockedEmotes.Remove(emote);
            unlockedEmotesTier0.Remove(emote);
            unlockedEmotesTier1.Remove(emote);
            unlockedEmotesTier2.Remove(emote);
            unlockedEmotesTier3.Remove(emote);
            unlockedFavoriteEmotes.Remove(emote);
            emotesUnlockedThisSession.Remove(emote);
            foreach (var playerEmotes in unlockedEmotesByPlayer.Values)
                playerEmotes.Remove(emote);
        }


        public static void ResetEmotesLocal()
        {
            Log("Resetting unlocked emotes.");
            unlockedEmotes.Clear();
            unlockedEmotesTier0.Clear();
            unlockedEmotesTier1.Clear();
            unlockedEmotesTier2.Clear();
            unlockedEmotesTier3.Clear();
            emotesUnlockedThisSession.Clear();

            unlockedEmotesByPlayer.Clear();
            foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (playerController.playerSteamId != 0)
                    unlockedEmotesByPlayer.Add(playerController.playerUsername, (playerController == localPlayerController || ConfigSync.instance.syncShareEverything) ? unlockedEmotes : new List<UnlockableEmote>());
            }
            UnlockEmotesLocal(ConfigSync.instance.syncUnlockEverything ? EmotesManager.allUnlockableEmotes : EmotesManager.complementaryEmotes);
            UpdateUnlockedFavoriteEmotes();
        }


        public static void UpdateUnlockedFavoriteEmotes()
        {
            unlockedFavoriteEmotes?.Clear();
            if (EmotesManager.allFavoriteEmotes == null || EmotesManager.allUnlockableEmotesDict == null || unlockedFavoriteEmotes == null)
                return;

            foreach (string emoteName in EmotesManager.allFavoriteEmotes)
            {
                if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                {
                    if (emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 0)
                        emote = emote.emoteSyncGroup[0];
                    if (IsEmoteUnlocked(emote))
                        unlockedFavoriteEmotes.Add(emote);
                }
            }
        }
    }
}
