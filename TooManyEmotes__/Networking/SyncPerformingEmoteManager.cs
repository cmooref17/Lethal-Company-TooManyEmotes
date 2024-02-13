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
using UnityEditor.PackageManager;

namespace TooManyEmotes.Networking
{
    [HarmonyPatch]
    public static class SyncPerformingEmoteManager
    {
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.PerformEmoteServerRpc", PerformEmoteServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.SyncEmoteServerRpc", SyncEmoteServerRpc);
            }
            else
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.PerformEmoteClientRpc", PerformEmoteClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.SyncEmoteClientRpc", SyncEmoteClientRpc);
            }
        }


        public static void SendPerformingEmoteUpdateToServer(UnlockableEmote emote)
        {
            if (!NetworkManager.Singleton.IsClient || emote == null)
                return;

            Plugin.Log("Sending performing emote update to server. Emote: " + emote.emoteName + " EmoteId: " + emote.emoteId);
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.PerformEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendSyncEmoteUpdateToServer(EmoteController emoteController)
        {
            if (!NetworkManager.Singleton.IsClient || emoteController == null)
                return;

            Plugin.Log("Sending sync emote update to server. Sync with emote controller id: " + emoteController);
            var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
            writer.WriteValue(emoteController.GetEmoteControllerId());
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.SyncEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        static void PerformEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (!SessionManager.TryGetPlayerByClientId(clientId, out var playerController) || !EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController))
            {
                Plugin.LogWarning("Could not handle performing emote request. Could not find emote controller for player with id: " + clientId);
                return;
            }

            int emoteId;
            reader.ReadValue(out emoteId);

            if (emoteId < 0 || emoteId >= EmotesManager.allUnlockableEmotes.Count)
            {
                Plugin.LogWarning("Could not handle performing emote request from client with id: " + clientId + ". Invalid emote id: " + emoteId + " AllUnlockableEmoteListSize: " + EmotesManager.allUnlockableEmotes.Count);
                return;
            }

            var emote = EmotesManager.allUnlockableEmotes[emoteId];
            Plugin.Log("Receiving performing emote update from client: " + clientId + " Emote: " + emote.emoteName);
            if (NetworkManager.Singleton.IsClient)
                emoteController.PerformEmote(emote);
            ServerSendPerformingEmoteUpdateToClients(emoteController, emote);
        }


        // SYNC EMOTE
        static void SyncEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (!SessionManager.TryGetPlayerByClientId(clientId, out var playerController) || !EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController))
            {
                Plugin.LogWarning("Could not handle sync emote request. Could not find emote controller for player with id: " + clientId);
                return;
            }

            ulong emoteControllerId;
            reader.ReadValue(out emoteControllerId);

            var syncWithEmoteController = GetEmoteControllerById(emoteControllerId);
            if (syncWithEmoteController == null)
            {
                Plugin.LogWarning("Could not handle sync emote request from client with id: " + clientId + ". Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            if (syncWithEmoteController.performingEmote == null)
            {
                Plugin.LogWarning("Could not handle sync emote request from client with id: " + clientId + ". Emote controller is not performing any emote.");
                return;
            }
            
            Plugin.Log("Receiving sync emote update from client with id: " + clientId + " Sync with emote controller id: " + emoteControllerId);
            if (NetworkManager.Singleton.IsClient)
                emoteController.SyncWithEmoteController(syncWithEmoteController);
            ServerSendSyncEmoteUpdateToClients(emoteController, syncWithEmoteController);
        }


        public static void ServerSendPerformingEmoteUpdateToClients(EmoteController emoteController, UnlockableEmote emote)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Plugin.LogWarning("[ServerSendPerformingEmoteUpdateToClients] Only the server can call this method!");
                return;
            }

            if (emoteController == null || emote == null)
                return;

            var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int), Allocator.Temp);
            writer.WriteValue(emoteController.GetEmoteControllerId());
            writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.PerformEmoteClientRpc", writer);
        }

        // SYNC EMOTE
        public static void ServerSendSyncEmoteUpdateToClients(EmoteController emoteController, EmoteController syncWithEmoteController)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Plugin.LogWarning("[ServerSendSyncEmoteUpdateToClients] Only the server can call this method!");
                return;
            }

            if (emoteController == null || syncWithEmoteController == null)
                return;

            var writer = new FastBufferWriter(sizeof(ulong) * 2, Allocator.Temp);
            writer.WriteValue(emoteController.GetEmoteControllerId());
            writer.WriteValue(syncWithEmoteController.GetEmoteControllerId());
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.SyncEmoteClientRpc", writer);
        }


        static void PerformEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            ulong emoteControllerId;
            reader.ReadValue(out emoteControllerId);

            // Do not update player's local emote controller
            if (EmoteControllerPlayer.emoteControllerLocal != null && emoteControllerId == EmoteControllerPlayer.emoteControllerLocal.GetEmoteControllerId())
                return;

            var emoteController = GetEmoteControllerById(emoteControllerId);
            if (emoteController == null)
            {
                Plugin.LogWarning("Could not handle performing emote request from server. Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            int emoteId;
            reader.ReadValue(out emoteId);

            if (emoteId < 0 || emoteId >= EmotesManager.allUnlockableEmotes.Count)
            {
                Plugin.LogWarning("Could not handle performing emote request from server for emote controller with id: " + emoteControllerId + ". Invalid emote id: " + emoteId + " AllUnlockableEmoteListSize: " + EmotesManager.allUnlockableEmotes.Count);
                return;
            }
            
            var emote = EmotesManager.allUnlockableEmotes[emoteId];
            Plugin.Log("Receiving performing emote update from server for emote controller with id: " + emoteControllerId + " Emote: " + emote.emoteName);
            emoteController.PerformEmote(emote);
        }


        // SYNC EMOTE
        static void SyncEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;


            ulong emoteControllerId;
            reader.ReadValue(out emoteControllerId);

            if (EmoteControllerPlayer.emoteControllerLocal != null && emoteControllerId == EmoteControllerPlayer.emoteControllerLocal.GetEmoteControllerId())
                return;

            var emoteController = GetEmoteControllerById(emoteControllerId);
            if (emoteController == null)
            {
                Plugin.LogWarning("Could not handle sync emote request from server. Failed to find emote controller with id: " + emoteControllerId);
                return;
            }

            ulong syncWithEmoteControllerId;
            reader.ReadValue(out syncWithEmoteControllerId);
            var syncWithEmoteController = GetEmoteControllerById(syncWithEmoteControllerId);

            if (syncWithEmoteController == null)
            {
                Plugin.LogWarning("Could not handle sync emote request from server for emote controller with id: " + emoteControllerId + ". Failed to find emote controller with id: " + emoteControllerId + " to sync with.");
                return;
            }

            if (syncWithEmoteController.performingEmote == null)
            {
                Plugin.LogWarning("Could not handle sync emote request from server for emote controller with id: " + clientId + ". Emote controller is not performing any emote.");
                return;
            }

            Plugin.Log("Receiving sync emote update from server for emote controller with id: " + emoteControllerId + " SyncWithEmoteControllerId: " + syncWithEmoteControllerId);
            emoteController.SyncWithEmoteController(syncWithEmoteController);
        }


        public static EmoteController GetEmoteControllerById(ulong id)
        {
            foreach (var emoteController in EmoteController.allEmoteControllers.Values)
            {
                if (emoteController.GetEmoteControllerId() == id)
                    return emoteController;
            }
            return null;
        }
    }
}
