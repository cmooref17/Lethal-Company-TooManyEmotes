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
        public static void ResetValues()
        {
            isSynced = false;
            requestedSync = false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init() {
            isSynced = NetworkManager.Singleton.IsServer;
            requestedSync = NetworkManager.Singleton.IsServer;
            if (NetworkManager.Singleton.IsServer)
            {
                syncedClients = new HashSet<ulong>();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestSyncServerRpc", OnRequestSyncServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnUnlockEmoteServerRpc", OnUnlockEmoteServerRpc);
            }
            else
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestSyncClientRpc", OnRequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnUnlockEmoteClientRpc", OnUnlockEmoteClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRotateEmotesClientRpc", RotateEmoteSelectionClientRpc);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void RequestSyncAfterConfigUpdate(PlayerControllerB __instance)
        {
            if (!isSynced && !requestedSync && ConfigSync.isSynced && __instance == StartOfRound.Instance.localPlayerController)
            {
                SendSyncRequest();
                requestedSync = true;
            }
        }


        public static void SendSyncRequest()
        {
            if (!NetworkManager.Singleton.IsClient)
                return;
            Plugin.Log("Sending sync request to server.");
            var writer = new FastBufferWriter(0, Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestSyncServerRpc", NetworkManager.ServerClientId, writer);
        }


        static void OnRequestSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            Plugin.Log("Receiving sync request from client: " + clientId);
            ServerSendSyncToClient(clientId);
        }


        public static void ServerSendSyncToClient(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;
            if (!ConfigSync.syncedClients.Contains(clientId))
                return;

            PlayerControllerB playerRequestingSync = null;
            SessionManager.TryGetPlayerByClientId(clientId, out playerRequestingSync);
            if (playerRequestingSync != null)
            {
                //Plugin.Log("OnPlayerConnect: " + clientId + " Username: " + playerRequestingSync.playerUsername + " SteamId: " + playerRequestingSync.playerSteamId);
                if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    SessionManager.unlockedEmotesByPlayer.Add(playerRequestingSync.playerUsername, new List<UnlockableEmote>());
                if (!TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    TerminalPatcher.currentEmoteCreditsByPlayer.Add(playerRequestingSync.playerUsername, ConfigSync.instance.syncStartingEmoteCredits);
            }

            List<UnlockableEmote> unlockedEmotes = SessionManager.unlockedEmotes;
            int emoteCredits = TerminalPatcher.currentEmoteCredits;
            if (!ConfigSync.instance.syncUnlockEverything)
            {
                if (!ConfigSync.instance.syncShareEverything)
                {
                    if (playerRequestingSync != null)
                    {
                        if (SessionManager.unlockedEmotesByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _unlockedEmotes))
                        {
                            Plugin.Log("Loading " + _unlockedEmotes.Count + " unlocked emotes for player: " + playerRequestingSync.playerUsername);
                            unlockedEmotes = _unlockedEmotes;
                        }
                        if (TerminalPatcher.currentEmoteCreditsByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _emoteCredits))
                        {
                            Plugin.Log("Loading " + _emoteCredits + " emote credits for player: " + playerRequestingSync.playerUsername);
                            emoteCredits = _emoteCredits;
                        }
                    }
                    else
                    {
                        Plugin.LogError("Error loading custom emotes for player. Player with id: " + clientId + " does not exist?");
                        unlockedEmotes = EmotesManager.complementaryEmotes;
                        emoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
                    }
                }
            }

            var writer = new FastBufferWriter(sizeof(int) * 3 + sizeof(int) * unlockedEmotes.Count, Allocator.Temp);
            writer.WriteValueSafe(emoteCredits);
            writer.WriteValueSafe(TerminalPatcher.emoteStoreSeed);
            writer.WriteValueSafe(unlockedEmotes.Count);
            for (int i = 0; i < unlockedEmotes.Count; i++)
                writer.WriteValueSafe(unlockedEmotes[i].emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestSyncClientRpc", clientId, writer);
        }


        static void OnRequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;
            if (!ConfigSync.isSynced)
                return;

            isSynced = true;
            if (reader.TryBeginRead(sizeof(int) * 3))
            {
                SessionManager.ResetProgressLocal();

                reader.ReadValue(out TerminalPatcher.currentEmoteCredits);
                reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
                reader.ReadValue(out int numEmotes);

                TerminalPatcher.RotateNewEmoteSelection();
                Plugin.Log("Receiving sync from server. CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits + " EmoteStoreSeed: " + TerminalPatcher.emoteStoreSeed + " NumEmotes: " + numEmotes);
                if (numEmotes <= 0)
                {
                    if (numEmotes == -1)
                        SessionManager.ResetEmotesLocal();
                }
                else if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    for (int i = 0; i < numEmotes; i++)
                    {
                        int emoteId;
                        reader.ReadValue(out emoteId);
                        SessionManager.UnlockEmoteLocal(emoteId);
                    }
                }
                else
                {
                    Plugin.LogError("Error receiving emotes sync from server.");
                    return;
                }
                isSynced = true;
                Plugin.Log("Received sync from server.");
                return;
            }
            Plugin.LogError("Error receiving sync from server.");
        }


        public static void SendOnUnlockEmoteUpdate(int emoteId, int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(int) * 3, Allocator.Temp);
            Plugin.Log("Sending unlocked emote update to server. Emote id: " + emoteId);
            writer.WriteValue(newEmoteCredits);
            writer.WriteValue(1); // one emote unlocked
            writer.WriteValue(emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendOnUnlockEmoteUpdateMulti(int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(int) * 2 + sizeof(int) * SessionManager.unlockedEmotes.Count, Allocator.Temp);
            Plugin.Log("Sending all unlocked emotes update to server.");
            writer.WriteValue(newEmoteCredits);
            writer.WriteValue(SessionManager.unlockedEmotes.Count);
            foreach (var emote in SessionManager.unlockedEmotes)
                writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnUnlockEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            int newEmoteCredits;
            int numEmotes;
            reader.ReadValue(out newEmoteCredits);
            reader.ReadValue(out numEmotes);

            PlayerControllerB playerWhoUnlocked = null;
            SessionManager.TryGetPlayerByClientId(clientId, out playerWhoUnlocked);

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

            Plugin.Log("Receiving unlocked emote update from client for " + numEmotes + " emotes.");
            var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) * 2 + sizeof(int) * numEmotes, Allocator.Temp);
            writer.WriteValueSafe(clientId);
            writer.WriteValueSafe(TerminalPatcher.currentEmoteCredits);
            writer.WriteValueSafe(numEmotes);
            if (reader.TryBeginRead(sizeof(int) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    int emoteId;
                    reader.ReadValue(out emoteId);
                    writer.WriteValueSafe(emoteId);
                    if (!ConfigSync.instance.syncShareEverything && clientId != 0)
                    {
                        if (playerWhoUnlocked != null)
                            SessionManager.UnlockEmoteLocal(emoteId, playerWhoUnlocked.playerUsername);
                    }
                    else
                        SessionManager.UnlockEmoteLocal(emoteId);
                }
                //if (ConfigSync.instance.syncShareEverything)
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnUnlockEmoteClientRpc", writer);
                return;
            }
            Plugin.LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        private static void OnUnlockEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            ulong clientIdUnlockedEmote;
            int newEmoteCredits;
            int numEmotes;
            reader.ReadValue(out clientIdUnlockedEmote);
            reader.ReadValue(out newEmoteCredits);
            reader.ReadValue(out numEmotes);

            SessionManager.TryGetPlayerByClientId(clientIdUnlockedEmote, out var playerWhoUnlocked);
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
                    int emoteId;
                    reader.ReadValue(out emoteId);
                    Plugin.Log("Receiving unlocked emote update from server. Emote id: " + emoteId);
                    if (ConfigSync.instance.syncShareEverything)
                        SessionManager.UnlockEmoteLocal(emoteId);
                    else
                        SessionManager.UnlockEmoteLocal(emoteId, playerWhoUnlocked.playerUsername);
                }
                return;
            }
            Plugin.LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        public static void RotateEmoteSelectionServerRpc(int overrideSeed = 0)
        {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;

            TerminalPatcher.emoteStoreSeed = overrideSeed == 0 ? UnityEngine.Random.Range(0, 1000000000) : overrideSeed;
            TerminalPatcher.RotateNewEmoteSelection();
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValue(TerminalPatcher.emoteStoreSeed);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnRotateEmotesClientRpc", writer);
        }


        static void RotateEmoteSelectionClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            if (reader.TryBeginRead(sizeof(int)))
            {
                reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
                TerminalPatcher.RotateNewEmoteSelection();
            }
        }


        
    }
}
