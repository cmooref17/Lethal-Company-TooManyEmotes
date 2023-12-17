using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using Unity.Collections;
using Unity.Netcode;

namespace TooManyEmotes.Networking {

    [HarmonyPatch]
    public static class ConfigSync {

        public static bool isSynced = false;

        public static bool syncUnlockEverything;
        public static float syncPriceMultiplierEmotesStore;
        public static int syncNumEmotesStoreRotation;
        public static int syncNumMysteryEmotesStoreRotation;
        public static int syncNumFreeEmoteCoupons;


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance) {
            if (GameNetworkManager.Instance.localPlayerController == __instance)
            {
                isSynced = false;
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncServerRpc", OnRequestConfigSyncServerRpc);
                    isSynced = true;
                    if (syncUnlockEverything)
                    {
                        foreach (var emote in StartOfRoundPatcher.allUnlockableEmotes)
                            StartOfRoundPatcher.UnlockEmoteLocal(emote);
                    }
                }
                else
                {
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncClientRpc", OnRequestConfigSyncClientRpc);
                    RequestConfigSync();
                }
            }
        }


        public static void BuildDefaultConfigSync() {
            syncUnlockEverything = ConfigSettings.unlockEverything.Value;
            syncPriceMultiplierEmotesStore = ConfigSettings.priceMultiplierEmotesStore.Value;
            syncNumEmotesStoreRotation = ConfigSettings.numEmotesStoreRotation.Value;
            syncNumMysteryEmotesStoreRotation = ConfigSettings.numMysteryEmotesStoreRotation.Value;
            syncNumFreeEmoteCoupons = ConfigSettings.numFreeEmoteCoupons.Value;
        }


        public static void RequestConfigSync() {
            if (NetworkManager.Singleton.IsClient)
            {
                Plugin.Log("Requesting config sync from server");
                var writer = new FastBufferWriter(0, Allocator.Temp);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncServerRpc", NetworkManager.ServerClientId, writer);
                return;
            }
            Plugin.LogError("Failed to send unlocked emote update to server.");
        }


        private static void OnRequestConfigSyncServerRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsServer)
                return;
            Plugin.Log("Receiving config sync request from client: " + clientId);
            var writer = new FastBufferWriter(sizeof(bool) + sizeof(float) + sizeof(int) * 3, Allocator.Temp);
            writer.WriteValueSafe(syncUnlockEverything);
            writer.WriteValueSafe(syncPriceMultiplierEmotesStore);
            writer.WriteValueSafe(syncNumEmotesStoreRotation);
            writer.WriteValueSafe(syncNumMysteryEmotesStoreRotation);
            writer.WriteValueSafe(syncNumFreeEmoteCoupons);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient)
                return;

            if (reader.TryBeginRead(sizeof(bool) + sizeof(float) + sizeof(int) * 3))
            {
                Plugin.Log("Receiving Config sync from server.");
                bool syncUnlockEverythingUpdate;
                float syncPriceMultiplierEmotesStoreUpdate;
                int syncNumEmotesStoreRotationUpdate;
                int syncNumMysteryEmotesStoreRotationUpdate;
                int syncNumFreeEmoteCouponsUpdate;

                reader.ReadValue(out syncUnlockEverythingUpdate);
                reader.ReadValue(out syncPriceMultiplierEmotesStoreUpdate);
                reader.ReadValue(out syncNumEmotesStoreRotationUpdate);
                reader.ReadValue(out syncNumMysteryEmotesStoreRotationUpdate);
                reader.ReadValue(out syncNumFreeEmoteCouponsUpdate);

                syncUnlockEverything = syncUnlockEverythingUpdate;
                syncPriceMultiplierEmotesStore = syncPriceMultiplierEmotesStoreUpdate;
                syncNumEmotesStoreRotation = syncNumEmotesStoreRotationUpdate;
                syncNumMysteryEmotesStoreRotation = syncNumMysteryEmotesStoreRotationUpdate;
                syncNumFreeEmoteCoupons = syncNumFreeEmoteCouponsUpdate;
                isSynced = true;

                if (syncUnlockEverything && StartOfRoundPatcher.allUnlockableEmotes != null && StartOfRoundPatcher.unlockedEmotes != null)
                {
                    foreach (var emote in StartOfRoundPatcher.allUnlockableEmotes)
                        StartOfRoundPatcher.UnlockEmoteLocal(emote);
                }
                return;
            }
            Plugin.LogError("Failed to receive config sync from server.");
        }
    }
}
