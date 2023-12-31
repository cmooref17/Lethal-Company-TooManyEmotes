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

namespace TooManyEmotes.Networking {


    [HarmonyPatch]
    public static class EmoteSyncManager {

        public static bool requestedSync = false;
        public static bool isSynced = false;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init() {
            isSynced = NetworkManager.Singleton.IsServer;
            requestedSync = NetworkManager.Singleton.IsServer;
            if (NetworkManager.Singleton.IsServer)
            {
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
            var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
            writer.WriteValueSafe(StartOfRound.Instance.localPlayerController.actualClientId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestSyncServerRpc", NetworkManager.ServerClientId, writer);
        }



        static void OnRequestSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            bool syncWithAll = true;
            ulong syncWithClientId = 0;
            if (reader.TryBeginRead(sizeof(ulong)))
            {
                reader.ReadValue(out syncWithClientId);
                syncWithAll = false;
                Plugin.Log("Receiving sync request from client: " + clientId);
            }
            else
                Plugin.Log("Syncing with all clients.");

            int numEmotes = 0;
            if (!ConfigSync.instance.syncUnlockEverything)
                numEmotes = StartOfRoundPatcher.unlockedEmotes.Count;

            var writer = new FastBufferWriter(sizeof(int) * 3 + sizeof(int) * Mathf.Max(numEmotes, 0), Allocator.Temp);
            writer.WriteValueSafe(TerminalPatcher.currentEmoteCredits);
            writer.WriteValueSafe(TerminalPatcher.emoteStoreSeed);
            writer.WriteValueSafe(numEmotes);
            for (int i = 0; i < numEmotes; i++)
                writer.WriteValueSafe(StartOfRoundPatcher.unlockedEmotes[i].emoteId);

            if (syncWithAll)
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnRequestSyncClientRpc", writer);
            else
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestSyncClientRpc", syncWithClientId, writer);
        }


        static void OnRequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;

            isSynced = true;
            if (reader.TryBeginRead(sizeof(int) * 3))
            {
                reader.ReadValue(out TerminalPatcher.currentEmoteCredits);
                reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
                reader.ReadValue(out int numEmotes);

                StartOfRoundPatcher.ResetEmotesLocal();
                TerminalPatcher.RotateNewEmoteSelection();

                if (numEmotes <= 0)
                {
                    if (numEmotes == -1)
                        StartOfRoundPatcher.ResetEmotesLocal();
                }
                else if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    for (int i = 0; i < numEmotes; i++)
                    {
                        int emoteId;
                        reader.ReadValue(out emoteId);
                        StartOfRoundPatcher.UnlockEmoteLocal(emoteId);
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




        public static void SendOnUnlockEmoteUpdate(int emoteId, int emoteCreditsUsed = -1) {
            var writer = new FastBufferWriter(sizeof(int) * 3, Allocator.Temp);
            Plugin.Log("Sending unlocked emote update to server. Emote id: " + emoteId);
            writer.WriteValue(emoteCreditsUsed);
            writer.WriteValue(1); // one emote unlocked
            writer.WriteValue(emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendOnUnlockEmoteUpdateMulti(int emoteCreditsUsed = -1) {
            var writer = new FastBufferWriter(sizeof(int) * 2 + sizeof(int) * StartOfRoundPatcher.unlockedEmotes.Count, Allocator.Temp);
            Plugin.Log("Sending all unlocked emotes update to server.");
            writer.WriteValue(emoteCreditsUsed);
            writer.WriteValue(StartOfRoundPatcher.unlockedEmotes.Count);
            foreach (var emote in StartOfRoundPatcher.unlockedEmotes)
                writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnUnlockEmoteServerRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (reader.TryBeginRead(sizeof(int) * 2))
            {
                int currentEmoteCredits;
                int numEmotes;
                reader.ReadValue(out currentEmoteCredits);
                reader.ReadValue(out numEmotes);

                if (currentEmoteCredits != -1)
                    TerminalPatcher.currentEmoteCredits = currentEmoteCredits;

                Plugin.Log("Receiving unlocked emote update from client for " + numEmotes + " emotes.");
                var writer = new FastBufferWriter(sizeof(int) * 2 + sizeof(int) * numEmotes, Allocator.Temp);
                writer.WriteValueSafe(TerminalPatcher.currentEmoteCredits);
                writer.WriteValueSafe(numEmotes);
                if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    for (int i = 0; i < numEmotes; i++)
                    {
                        int emoteId;
                        reader.ReadValue(out emoteId);
                        StartOfRoundPatcher.UnlockEmoteLocal(emoteId);
                        writer.WriteValueSafe(emoteId);
                    }
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnUnlockEmoteClientRpc", writer);
                    return;
                }
                Plugin.LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
                return;
            }
            Plugin.LogError("Failed to receive unlocked emote update from client.");
        }


        private static void OnUnlockEmoteClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            if (reader.TryBeginRead(sizeof(int) * 2))
            {
                int currentEmoteCredits;
                int numEmotes;
                reader.ReadValue(out currentEmoteCredits);
                reader.ReadValue(out numEmotes);

                if (currentEmoteCredits != -1)
                    TerminalPatcher.currentEmoteCredits = currentEmoteCredits;

                if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    for (int i = 0; i < numEmotes; i++)
                    {
                        int emoteId;
                        reader.ReadValue(out emoteId);
                        Plugin.Log("Receiving unlocked emote update from server. Emote id: " + emoteId);
                        StartOfRoundPatcher.UnlockEmoteLocal(emoteId);
                    }
                    return;
                }
                Plugin.LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
                return;
            }
            Plugin.LogError("Failed to receive unlocked emote update from client.");
        }



        public static void RotateEmoteSelectionServerRpc(int overrideSeed = 0) {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
                return;

            if (overrideSeed == 0)
                TerminalPatcher.emoteStoreSeed = UnityEngine.Random.Range(0, 1000000000);
            else
                TerminalPatcher.emoteStoreSeed = overrideSeed;

            TerminalPatcher.RotateNewEmoteSelection();
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(TerminalPatcher.emoteStoreSeed);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes-OnRotateEmotesClientRpc", writer);
        }




        static void RotateEmoteSelectionClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                return;

            if (reader.TryBeginRead(sizeof(int)))
            {
                reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
                TerminalPatcher.RotateNewEmoteSelection();
            }
        }
    }
}
