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

namespace TooManyEmotes.Networking {


    [HarmonyPatch]
    internal class SyncUnlockedEmotes {

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance) {
            if (NetworkManager.Singleton.IsServer)
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnUnlockEmoteServerRpc", OnUnlockEmoteServerRpc);
            else
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnUnlockEmoteClientRpc", OnUnlockEmoteClientRpc);
        }


        public static void SendOnUnlockEmoteUpdate(int emoteId) {
            var writer = new FastBufferWriter(sizeof(int) * 2, Allocator.Temp);
            Plugin.Log("Sending unlocked emote update to server. Emote id: " + emoteId);
            writer.WriteValue(1);
            writer.WriteValue(emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendOnUnlockEmoteUpdateMulti() {
            var writer = new FastBufferWriter(sizeof(int) * (StartOfRoundPatcher.unlockedEmotes.Count + 1), Allocator.Temp);
            Plugin.Log("Sending all unlocked emotes update to server.");
            writer.WriteValue(StartOfRoundPatcher.unlockedEmotes.Count);
            foreach (var emote in StartOfRoundPatcher.unlockedEmotes)
                writer.WriteValue(emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnUnlockEmoteServerRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (reader.TryBeginRead(sizeof(int)))
            {
                int numEmotes;
                reader.ReadValue(out numEmotes);

                if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    int[] emoteIds = new int[numEmotes];
                    for (int i = 0; i < numEmotes; i++)
                    {
                        reader.ReadValue(out emoteIds[i]);
                        int emoteId = emoteIds[i];
                        Plugin.Log("Receiving unlocked emote update from client. Emote id: " + emoteId);
                        if (emoteId < StartOfRoundPatcher.allUnlockableEmotes.Count)
                            StartOfRoundPatcher.UnlockEmoteLocal(emoteId);
                        else
                            Plugin.LogError("Error while syncing unlocked emote from client: Emote id is invalid! Emote id: " + emoteId);
                    }

                    var writer = new FastBufferWriter(sizeof(int) * (emoteIds.Length + 1), Allocator.Temp);
                    writer.WriteValueSafe(emoteIds.Length);
                    for (int i = 0; i < emoteIds.Length; i++)
                        writer.WriteValueSafe(emoteIds[i]);
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

            if (reader.TryBeginRead(sizeof(int)))
            {
                int numEmotes;
                reader.ReadValue(out numEmotes);

                if (reader.TryBeginRead(sizeof(int) * numEmotes))
                {
                    int[] emoteIds = new int[numEmotes];
                    for (int i = 0; i < numEmotes; i++)
                    {
                        reader.ReadValue(out emoteIds[i]);
                        int emoteId = emoteIds[i];
                        Plugin.Log("Receiving unlocked emote update from server. Emote id: " + emoteId);
                        if (emoteId < StartOfRoundPatcher.allUnlockableEmotes.Count)
                            StartOfRoundPatcher.UnlockEmoteLocal(emoteId);
                        else
                            Plugin.LogError("Error while syncing unlocked emote from server: Emote id is invalid! Emote id: " + emoteId);
                    }
                    return;
                }
                Plugin.LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
                return;
            }
            Plugin.LogError("Failed to receive unlocked emote update from client.");
        }
    }
}
