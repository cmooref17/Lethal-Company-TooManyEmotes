using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Networking
{
    [HarmonyPatch]
    public static class SyncPerformingEmoteManager
    {
        internal static Dictionary<EmoteController, bool> doNotTriggerAudioDict = new Dictionary<EmoteController, bool>();
        private static HashSet<PlayerControllerB> sentLastAudioUpdateToPlayers = new HashSet<PlayerControllerB>();


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init()
        {
            doNotTriggerAudioDict.Clear();
            sentLastAudioUpdateToPlayers.Clear();
            if (isServer)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.PerformEmoteServerRpc", PerformEmoteServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.SyncEmoteServerRpc", SyncEmoteServerRpc);
            }
            else if (isClient)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.PerformEmoteClientRpc", PerformEmoteClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.SyncEmoteClientRpc", SyncEmoteClientRpc);
            }
        }

        
        // Client
        public static void SendPerformingEmoteUpdateToServer(UnlockableEmote emote, bool doNotTriggerAudio = false)
        {
            if (!isClient || emote == null)
                return;

            if (!doNotTriggerAudioDict.ContainsKey(emoteControllerLocal))
                doNotTriggerAudioDict[emoteControllerLocal] = !doNotTriggerAudio;

            if (isServer)
            {
                ServerSendPerformingEmoteUpdateToClients(emoteControllerLocal, emote, doNotTriggerAudio);
                return;
            }

            bool sendTriggerAudioUpdate = doNotTriggerAudioDict[emoteControllerLocal] != doNotTriggerAudio;

            int bufferSize = sizeof(short) + (sendTriggerAudioUpdate ? sizeof(bool) : 0);
            var writer = new FastBufferWriter(bufferSize, Allocator.Temp);
            writer.WriteValue((short)emote.emoteId);

            Log("Sending performing emote update to server. Emote: " + emote.emoteName + " EmoteId: " + emote.emoteId);
            if (sendTriggerAudioUpdate)
            {
                writer.WriteValue(doNotTriggerAudio);
                doNotTriggerAudioDict[emoteControllerLocal] = doNotTriggerAudio;
            }

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.PerformEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        // ServerRpc
        private static void PerformEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            if (!TryGetPlayerByClientId(clientId, out var playerController) || !EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController))
            {
                LogWarning("Could not handle performing emote request. Could not find emote controller for player with id: " + clientId);
                return;
            }

            reader.ReadValue(out short emoteId);
            if (emoteId < 0 || emoteId >= EmotesManager.allUnlockableEmotes.Count)
            {
                LogWarning("Could not handle performing emote request from client with id: " + clientId + ". Invalid emote id: " + emoteId + " AllUnlockableEmoteListSize: " + EmotesManager.allUnlockableEmotes.Count);
                return;
            }

            bool doNotTriggerAudio = false;
            if (reader.TryBeginRead(sizeof(bool)))
                reader.ReadValue(out doNotTriggerAudio);
            else if (doNotTriggerAudioDict.ContainsKey(emoteController))
                doNotTriggerAudio = doNotTriggerAudioDict[emoteController];

            var emote = EmotesManager.allUnlockableEmotes[emoteId];
            int overrideEmoteId = -1;
            if (emote.emoteSyncGroup != null)
                overrideEmoteId = emote.emoteSyncGroup.IndexOf(emote);

            Log("Receiving performing emote update from client: " + clientId + " Emote: " + emote.emoteName);
            if (isClient && !emoteController.isLocalPlayer)
                emoteController.PerformEmote(emote, overrideEmoteId: overrideEmoteId, doNotTriggerAudio: doNotTriggerAudio);
            ServerSendPerformingEmoteUpdateToClients(emoteController, emote, doNotTriggerAudio);
        }


        // Server
        public static void ServerSendPerformingEmoteUpdateToClients(EmoteController emoteController, UnlockableEmote emote, bool doNotTriggerAudio = false)
        {
            if (!isServer)
            {
                LogWarning("[ServerSendPerformingEmoteUpdateToClients] Only the server can call this method!");
                return;
            }

            if (emoteController == null || emote == null)
                return;

            if (!doNotTriggerAudioDict.ContainsKey(emoteController))
                doNotTriggerAudioDict[emoteController] = !doNotTriggerAudio;

            bool sendTriggerAudioUpdate = doNotTriggerAudioDict[emoteController] != doNotTriggerAudio;

            int bufferSize = sizeof(ushort) + sizeof(short) + (sendTriggerAudioUpdate ? sizeof(bool) : 0);
            var writer = new FastBufferWriter(bufferSize, Allocator.Temp);
            writer.WriteValue((ushort)emoteController.emoteControllerId);
            writer.WriteValue((short)emote.emoteId);

            if (sendTriggerAudioUpdate)
            {
                writer.WriteValue(doNotTriggerAudio);
                doNotTriggerAudioDict[emoteController] = doNotTriggerAudio;
            }
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.PerformEmoteClientRpc", writer);
        }


        // ClientRpc
        private static void PerformEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out ushort emoteControllerId);

            // Do not update player's local emote controller
            if (emoteControllerLocal != null && emoteControllerId == emoteControllerLocal.emoteControllerId)
                return;

            var emoteController = GetEmoteControllerById(emoteControllerId);
            if (emoteController == null)
            {
                LogWarning("Could not handle performing emote request from server. Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            reader.ReadValue(out short emoteId);
            if (emoteId < 0 || emoteId >= EmotesManager.allUnlockableEmotes.Count)
            {
                LogWarning("Could not handle performing emote request from server for emote controller with id: " + emoteControllerId + ". Invalid emote id: " + emoteId + " AllUnlockableEmoteListSize: " + EmotesManager.allUnlockableEmotes.Count);
                return;
            }

            bool doNotTriggerAudio = false;
            if (reader.TryBeginRead(sizeof(bool)))
                reader.ReadValue(out doNotTriggerAudio);
            else if (doNotTriggerAudioDict.ContainsKey(emoteController))
                doNotTriggerAudio = doNotTriggerAudioDict[emoteController];

            doNotTriggerAudioDict[emoteController] = doNotTriggerAudio;

            var emote = EmotesManager.allUnlockableEmotes[emoteId];
            int overrideEmoteId = -1;
            if (emote.emoteSyncGroup != null)
                overrideEmoteId = emote.emoteSyncGroup.IndexOf(emote);
            Log("Receiving performing emote update from server for emote controller with id: " + emoteControllerId + " Emote: " + emote.emoteName);
            emoteController.PerformEmote(emote, overrideEmoteId: overrideEmoteId, doNotTriggerAudio: doNotTriggerAudio);
        }








        // Client
        public static void SendSyncEmoteUpdateToServer(EmoteController emoteController, int overrideEmoteId = -1)
        {
            if (!isClient || emoteController == null)
                return;

            if (isServer)
            {
                ServerSendSyncEmoteUpdateToClients(emoteControllerLocal, emoteController, overrideEmoteId);
                return;
            }

            Log("Sending sync emote update to server. Sync with emote controller id: " + emoteController);
            var writer = new FastBufferWriter(sizeof(ushort) + sizeof(short), Allocator.Temp);
            writer.WriteValue((ushort)emoteController.emoteControllerId);
            writer.WriteValue((short)overrideEmoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.SyncEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        // ServerRpc
        private static void SyncEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            if (!TryGetPlayerByClientId(clientId, out var playerController) || !EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController))
            {
                LogWarning("Could not handle sync emote request. Could not find emote controller for player with id: " + clientId);
                return;
            }

            reader.ReadValue(out ushort emoteControllerId);
            reader.ReadValue(out short overrideEmoteId);

            var syncWithEmoteController = GetEmoteControllerById(emoteControllerId);
            if (syncWithEmoteController == null)
            {
                LogWarning("Could not handle sync emote request from client with id: " + clientId + ". Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            if (syncWithEmoteController.performingEmote == null)
            {
                LogWarning("Could not handle sync emote request from client with id: " + clientId + ". Emote controller is not performing any emote.");
                return;
            }

            Log("Receiving sync emote update from client with id: " + clientId + " Sync with emote controller id: " + emoteControllerId);
            if (isClient && !emoteController.isLocalPlayer)
                emoteController.SyncWithEmoteController(syncWithEmoteController, overrideEmoteId);
            ServerSendSyncEmoteUpdateToClients(emoteController, syncWithEmoteController, overrideEmoteId);
        }


        // Server
        public static void ServerSendSyncEmoteUpdateToClients(EmoteController emoteController, EmoteController syncWithEmoteController, int overrideEmoteId = -1)
        {
            if (!isServer)
            {
                LogWarning("[ServerSendSyncEmoteUpdateToClients] Only the server can call this method!");
                return;
            }

            if (emoteController == null || syncWithEmoteController == null)
                return;

            var writer = new FastBufferWriter(sizeof(ushort) * 2 + sizeof(short), Allocator.Temp);
            writer.WriteValue((ushort)emoteController.emoteControllerId);
            writer.WriteValue((ushort)syncWithEmoteController.emoteControllerId);
            writer.WriteValue((short)overrideEmoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.SyncEmoteClientRpc", writer);
        }


        // ClientRpc
        private static void SyncEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out ushort emoteControllerId);

            // Do not update player's local emote controller
            if (emoteControllerLocal != null && emoteControllerId == emoteControllerLocal.emoteControllerId)
                return;

            var emoteController = GetEmoteControllerById(emoteControllerId);
            if (emoteController == null)
            {
                LogWarning("Could not handle sync emote request from server. Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            reader.ReadValue(out ushort syncWithEmoteControllerId);
            reader.ReadValue(out short overrideEmoteId);

            var syncWithEmoteController = GetEmoteControllerById(syncWithEmoteControllerId);
            if (syncWithEmoteController == null)
            {
                LogWarning("Could not handle sync emote request from server for emote controller with id: " + emoteControllerId + ". Failed to find emote controller with id: " + emoteControllerId + " to sync with.");
                return;
            }

            if (syncWithEmoteController.performingEmote == null)
            {
                LogWarning("Could not handle sync emote request from server for emote controller with id: " + clientId + ". Emote controller is not performing any emote.");
                return;
            }

            Log("Receiving sync emote update from server for emote controller with id: " + emoteControllerId + " SyncWithEmoteControllerId: " + syncWithEmoteControllerId);
            emoteController.SyncWithEmoteController(syncWithEmoteController, overrideEmoteId);
        }
    }
}