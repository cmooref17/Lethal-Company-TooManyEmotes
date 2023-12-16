using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using Unity.Collections;
using Unity.Netcode;

namespace TooManyEmotes.Networking {

    [HarmonyPatch]
    public static class ConfigSync {

        public static bool isSynced = false;

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
                    isSynced = true;
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncServerRpc", OnRequestConfigSyncServerRpc);
                }
                else
                {
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncClientRpc", OnRequestConfigSyncClientRpc);
                    RequestConfigSync();
                }
            }
        }


        public static void BuildDefaultConfigSync() {
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
            var writer = new FastBufferWriter(sizeof(float) + sizeof(int) * 3, Allocator.Temp);
            writer.WriteValueSafe(syncPriceMultiplierEmotesStore);
            writer.WriteValueSafe(syncNumEmotesStoreRotation);
            writer.WriteValueSafe(syncNumMysteryEmotesStoreRotation);
            writer.WriteValueSafe(syncNumFreeEmoteCoupons);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient)
                return;

            if (reader.TryBeginRead(sizeof(float) + sizeof(int) * 3))
            {
                Plugin.Log("Receiving Config sync from server.");
                float syncPriceMultiplierEmotesStoreUpdate;
                int syncNumEmotesStoreRotationUpdate;
                int syncNumMysteryEmotesStoreRotationUpdate;
                int syncNumFreeEmoteCouponsUpdate;

                reader.ReadValue(out syncPriceMultiplierEmotesStoreUpdate);
                reader.ReadValue(out syncNumEmotesStoreRotationUpdate);
                reader.ReadValue(out syncNumMysteryEmotesStoreRotationUpdate);
                reader.ReadValue(out syncNumFreeEmoteCouponsUpdate)
                    ;
                syncPriceMultiplierEmotesStore = syncPriceMultiplierEmotesStoreUpdate;
                syncNumEmotesStoreRotation = syncNumEmotesStoreRotationUpdate;
                syncNumMysteryEmotesStoreRotation = syncNumMysteryEmotesStoreRotationUpdate;
                syncNumFreeEmoteCoupons = syncNumFreeEmoteCouponsUpdate;
                isSynced = true;
                return;
            }
            Plugin.LogError("Failed to receive config sync from server.");
        }
    }
}
