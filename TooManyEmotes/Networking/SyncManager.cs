using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using TooManyEmotes.Patches;
using TooManyEmotes.Config;
using UnityEngine;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Networking
{
    [HarmonyPatch]
    public static class SyncManager
    {
        public static bool requestedSync = false;
        public static bool isSynced = false;
        public static HashSet<ulong> syncedClients;

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        private static void ResetValues()
        {
            isSynced = false;
            requestedSync = false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void Init()
        {
            isSynced = isServer;
            requestedSync = isServer;
            if (isServer)
            {
                syncedClients = new HashSet<ulong>();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestSyncServerRpc", OnRequestSyncServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnUnlockEmoteServerRpc", OnUnlockEmoteServerRpc);
            }
            else if (isClient)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestSyncClientRpc", OnRequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnUnlockEmoteClientRpc", OnUnlockEmoteClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRotateEmotesClientRpc", RotateEmoteSelectionClientRpc);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void RequestSyncAfterConfigUpdate(PlayerControllerB __instance)
        {
            if (!isSynced && !requestedSync && ConfigSync.isSynced && __instance == localPlayerController)
            {
                SendSyncRequest();
                requestedSync = true;
            }
        }


        public static void SendSyncRequest()
        {
            if (!isClient)
                return;

            Log("Sending sync request to server.");
            var writer = new FastBufferWriter(0, Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestSyncServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnRequestSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            Log("Receiving sync request from client: " + clientId);
            ServerSendSyncToClient(clientId);

            SyncPerformingEmoteManager.doNotTriggerAudioDict?.Clear(); // Will force the server to send the next doNotTriggerAudio to all clients, for each emote controller.
        }


        public static void ServerSendSyncToClient(ulong clientId)
        {
            if (!isServer)
                return;
            if (!ConfigSync.syncedClients.Contains(clientId))
                return;

            if (TryGetPlayerByClientId(clientId, out var playerRequestingSync))
            {
                //Log("OnPlayerConnect: " + clientId + " Username: " + playerRequestingSync.playerUsername + " SteamId: " + playerRequestingSync.playerSteamId);
                if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    SessionManager.unlockedEmotesByPlayer.Add(playerRequestingSync.playerUsername, new List<UnlockableEmote>());
                if (!TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    TerminalPatcher.currentEmoteCreditsByPlayer.Add(playerRequestingSync.playerUsername, ConfigSync.instance.syncStartingEmoteCredits);
            }

            List<UnlockableEmote> unlockedEmotes = SessionManager.unlockedEmotes;
            int emoteCredits = TerminalPatcher.currentEmoteCredits;
            if (!ConfigSync.instance.syncUnlockEverything && !ConfigSync.instance.syncShareEverything)
            {
                if (playerRequestingSync != null)
                {
                    if (SessionManager.unlockedEmotesByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _unlockedEmotes))
                    {
                        Log("Loading " + _unlockedEmotes.Count + " unlocked emotes for player: " + playerRequestingSync.playerUsername);
                        unlockedEmotes = _unlockedEmotes;
                    }
                    if (TerminalPatcher.currentEmoteCreditsByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _emoteCredits))
                    {
                        Log("Loading " + _emoteCredits + " emote credits for player: " + playerRequestingSync.playerUsername);
                        emoteCredits = _emoteCredits;
                    }
                }
                else
                {
                    LogError("Error loading custom emotes for player. Player with id: " + clientId + " does not exist?");
                    unlockedEmotes = EmotesManager.complementaryEmotes;
                    emoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
                }
            }

            var writer = new FastBufferWriter(sizeof(int) * 3 + sizeof(int) * unlockedEmotes.Count, Allocator.Temp);
            writer.WriteValueSafe(emoteCredits);
            writer.WriteValueSafe(TerminalPatcher.emoteStoreSeed);
            writer.WriteValueSafe(unlockedEmotes.Count);
            for (int i = 0; i < unlockedEmotes.Count; i++)
                writer.WriteValueSafe(unlockedEmotes[i].emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestSyncClientRpc", clientId, writer);
        }


        private static void OnRequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient)
                return;
            if (!ConfigSync.isSynced)
                return;

            isSynced = true;
            SessionManager.ResetProgressLocal();

            reader.ReadValue(out TerminalPatcher.currentEmoteCredits);
            reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
            reader.ReadValue(out int numEmotes);

            TerminalPatcher.RotateNewEmoteSelection();
            if (numEmotes <= 0)
            {
                if (numEmotes == -1)
                    SessionManager.ResetEmotesLocal();
            }
            else if (reader.TryBeginRead(sizeof(int) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    reader.ReadValue(out int emoteId);
                    SessionManager.UnlockEmoteLocal(emoteId);
                }
            }
            else
            {
                LogError("Error receiving emotes sync from server.");
                return;
            }
            Log("Received sync from server. CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits + " EmoteStoreSeed: " + TerminalPatcher.emoteStoreSeed + " NumEmotes: " + numEmotes);
            isSynced = true;
        }


        public static void SendOnUnlockEmoteUpdate(int emoteId, int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(int) * 3, Allocator.Temp);
            Log("Sending unlocked emote update to server. Emote id: " + emoteId);
            writer.WriteValue(newEmoteCredits);
            writer.WriteValue(1); // one emote unlocked
            writer.WriteValue(emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendOnUnlockEmoteUpdateMulti(int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(int) * 2 + sizeof(int) * SessionManager.unlockedEmotes.Count, Allocator.Temp);
            Log("Sending all unlocked emotes update to server.");
            writer.WriteValue(newEmoteCredits);
            writer.WriteValue(SessionManager.unlockedEmotes.Count);
            foreach (var emote in SessionManager.unlockedEmotes)
                writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnUnlockEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            reader.ReadValue(out int newEmoteCredits);
            reader.ReadValue(out int numEmotes);

            PlayerControllerB playerWhoUnlocked = null;
            TryGetPlayerByClientId(clientId, out playerWhoUnlocked);

            if (newEmoteCredits != -1)
            {
                if (!ConfigSync.instance.syncShareEverything && clientId != 0)
                {
                    if (playerWhoUnlocked != null)
                        TerminalPatcher.currentEmoteCreditsByPlayer[playerWhoUnlocked.playerUsername] = newEmoteCredits;
                }
                else
                    TerminalPatcher.currentEmoteCredits = newEmoteCredits;
            }

            Log("Receiving unlocked emote update from client for " + numEmotes + " emotes.");
            var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) * 2 + sizeof(int) * numEmotes, Allocator.Temp);
            writer.WriteValue(clientId);
            writer.WriteValue(TerminalPatcher.currentEmoteCredits);
            writer.WriteValue(numEmotes);
            if (reader.TryBeginRead(sizeof(int) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    reader.ReadValue(out int emoteId);
                    writer.WriteValue(emoteId);
                    if (!ConfigSync.instance.syncShareEverything && clientId != 0)
                    {
                        if (playerWhoUnlocked != null)
                            SessionManager.UnlockEmoteLocal(emoteId, playerWhoUnlocked.playerUsername);
                    }
                    else
                        SessionManager.UnlockEmoteLocal(emoteId);
                }
                //if (ConfigSync.instance.syncShareEverything)
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnUnlockEmoteClientRpc", writer);
                return;
            }
            LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        private static void OnUnlockEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out ulong clientIdUnlockedEmote);
            reader.ReadValue(out int newEmoteCredits);
            reader.ReadValue(out int numEmotes);

            TryGetPlayerByClientId(clientIdUnlockedEmote, out var playerWhoUnlocked);
            if (!ConfigSync.instance.syncShareEverything && playerWhoUnlocked == null)
                return;

            if (newEmoteCredits != -1)
            {
                if (!ConfigSync.instance.syncShareEverything)
                    TerminalPatcher.currentEmoteCreditsByPlayer[playerWhoUnlocked.playerUsername] = newEmoteCredits;
                else
                    TerminalPatcher.currentEmoteCredits = newEmoteCredits;
            }

            if (reader.TryBeginRead(sizeof(int) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    reader.ReadValue(out int emoteId);
                    Log("Receiving unlocked emote update from server. Emote id: " + emoteId);
                    if (ConfigSync.instance.syncShareEverything)
                        SessionManager.UnlockEmoteLocal(emoteId);
                    else
                        SessionManager.UnlockEmoteLocal(emoteId, playerWhoUnlocked.playerUsername);
                }
                return;
            }
            LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        public static void RotateEmoteSelectionServerRpc(int overrideSeed = 0)
        {
            if (!isServer && !isHost)
                return;

            TerminalPatcher.emoteStoreSeed = overrideSeed == 0 ? UnityEngine.Random.Range(0, 1000000000) : overrideSeed;
            TerminalPatcher.RotateNewEmoteSelection();
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValue(TerminalPatcher.emoteStoreSeed);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnRotateEmotesClientRpc", writer);
        }


        private static void RotateEmoteSelectionClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
            TerminalPatcher.RotateNewEmoteSelection();
        }
    }
}
