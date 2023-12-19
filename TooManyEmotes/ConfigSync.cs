using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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
        public static bool syncDisableRaritySystem;

        public static float syncPriceMultiplierEmotesStore;
        public static int syncBasePriceCommonEmote;
        public static int syncBasePriceUncommonEmote;
        public static int syncBasePriceRareEmote;
        public static int syncBasePriceLegendaryEmote;

        public static int syncNumEmotesStoreRotation;
        public static float syncRotationChanceCommonEmote;
        public static float syncRotationChanceUncommonEmote;
        public static float syncRotationChanceRareEmote;
        public static float syncRotationChanceLegendaryEmote;

        public static int syncNumMysteryEmotesStoreRotation;
        public static int syncNumFreeEmoteCredits;


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
                    // Unlock all emotes until synced with host
                    foreach (var emote in StartOfRoundPatcher.allUnlockableEmotes)
                        StartOfRoundPatcher.UnlockEmoteLocal(emote);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncClientRpc", OnRequestConfigSyncClientRpc);
                    RequestConfigSync();
                }
            }
        }


        public static void BuildDefaultConfigSync() {
            syncUnlockEverything = ConfigSettings.unlockEverything.Value;
            syncDisableRaritySystem = ConfigSettings.disableRaritySystem.Value;

            syncPriceMultiplierEmotesStore = ConfigSettings.priceMultiplierEmotesStore.Value;

            syncNumEmotesStoreRotation = ConfigSettings.numEmotesStoreRotation.Value;
            syncRotationChanceCommonEmote = ConfigSettings.rotationChanceCommonEmote.Value;
            syncRotationChanceUncommonEmote = ConfigSettings.rotationChanceUncommonEmote.Value;
            syncRotationChanceRareEmote = ConfigSettings.rotationChanceRareEmote.Value;
            syncRotationChanceLegendaryEmote = ConfigSettings.rotationChanceLegendaryEmote.Value;

            syncNumMysteryEmotesStoreRotation = ConfigSettings.numMysteryEmotesStoreRotation.Value;
            syncNumFreeEmoteCredits = ConfigSettings.numFreeEmoteCredits.Value;

            if (ConfigSettings.disableRaritySystem.Value)
            {
                syncBasePriceCommonEmote = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceUncommonEmote = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceRareEmote = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceLegendaryEmote = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
            }
            else
            {
                syncBasePriceCommonEmote = ConfigSettings.basePriceCommonEmote.Value;
                syncBasePriceUncommonEmote = ConfigSettings.basePriceUncommonEmote.Value;
                syncBasePriceRareEmote = ConfigSettings.basePriceRareEmote.Value;
                syncBasePriceLegendaryEmote = ConfigSettings.basePriceLegendaryEmote.Value;
            }
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
            var writer = new FastBufferWriter(sizeof(bool) * 2 + sizeof(float) * 5 + sizeof(int) * 7, Allocator.Temp);
            writer.WriteValueSafe(syncUnlockEverything);
            writer.WriteValueSafe(syncDisableRaritySystem);

            writer.WriteValueSafe(syncPriceMultiplierEmotesStore);
            writer.WriteValueSafe(syncBasePriceCommonEmote);
            writer.WriteValueSafe(syncBasePriceUncommonEmote);
            writer.WriteValueSafe(syncBasePriceRareEmote);
            writer.WriteValueSafe(syncBasePriceLegendaryEmote);

            writer.WriteValueSafe(syncNumEmotesStoreRotation);
            writer.WriteValueSafe(syncRotationChanceCommonEmote);
            writer.WriteValueSafe(syncRotationChanceUncommonEmote);
            writer.WriteValueSafe(syncRotationChanceRareEmote);
            writer.WriteValueSafe(syncRotationChanceLegendaryEmote);

            writer.WriteValueSafe(syncNumMysteryEmotesStoreRotation);
            writer.WriteValueSafe(syncNumFreeEmoteCredits);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient)
                return;

            if (reader.TryBeginRead(sizeof(bool) + sizeof(float) * 5 + sizeof(int) * 7))
            {
                Plugin.Log("Receiving Config sync from server.");
                bool syncUnlockEverythingUpdate;
                bool syncDisableRaritySystemUpdate;

                float syncPriceMultiplierEmotesStoreUpdate;
                int syncBasePriceCommonEmoteUpdate;
                int syncBasePriceUncommonEmoteUpdate;
                int syncBasePriceRareEmoteUpdate;
                int syncBasePriceLegendaryEmoteUpdate;

                int syncNumEmotesStoreRotationUpdate;
                float syncRotationChanceCommonEmoteUpdate;
                float syncRotationChanceUncommonEmoteUpdate;
                float syncRotationChanceRareEmoteUpdate;
                float syncRotationChanceLegendaryEmoteUpdate;

                int syncNumMysteryEmotesStoreRotationUpdate;
                int syncNumFreeEmoteCouponsUpdate;


                reader.ReadValue(out syncUnlockEverythingUpdate);
                reader.ReadValue(out syncDisableRaritySystemUpdate);

                reader.ReadValue(out syncPriceMultiplierEmotesStoreUpdate);
                reader.ReadValue(out syncBasePriceCommonEmoteUpdate);
                reader.ReadValue(out syncBasePriceUncommonEmoteUpdate);
                reader.ReadValue(out syncBasePriceRareEmoteUpdate);
                reader.ReadValue(out syncBasePriceLegendaryEmoteUpdate);

                reader.ReadValue(out syncNumEmotesStoreRotationUpdate);
                reader.ReadValue(out syncRotationChanceCommonEmoteUpdate);
                reader.ReadValue(out syncRotationChanceUncommonEmoteUpdate);
                reader.ReadValue(out syncRotationChanceRareEmoteUpdate);
                reader.ReadValue(out syncRotationChanceLegendaryEmoteUpdate);

                reader.ReadValue(out syncNumMysteryEmotesStoreRotationUpdate);
                reader.ReadValue(out syncNumFreeEmoteCouponsUpdate);


                syncUnlockEverything = syncUnlockEverythingUpdate;
                syncDisableRaritySystem = syncDisableRaritySystemUpdate;

                syncPriceMultiplierEmotesStore = syncPriceMultiplierEmotesStoreUpdate;
                syncBasePriceCommonEmote = syncBasePriceCommonEmoteUpdate;
                syncBasePriceUncommonEmote = syncBasePriceUncommonEmoteUpdate;
                syncBasePriceRareEmote = syncBasePriceRareEmoteUpdate;
                syncBasePriceLegendaryEmote = syncBasePriceLegendaryEmoteUpdate;

                syncNumEmotesStoreRotation = syncNumEmotesStoreRotationUpdate;
                syncRotationChanceCommonEmote = syncRotationChanceCommonEmoteUpdate;
                syncRotationChanceUncommonEmote = syncRotationChanceUncommonEmoteUpdate;
                syncRotationChanceRareEmote = syncRotationChanceRareEmoteUpdate;
                syncRotationChanceLegendaryEmote = syncRotationChanceLegendaryEmoteUpdate;

                syncNumMysteryEmotesStoreRotation = syncNumMysteryEmotesStoreRotationUpdate;
                syncNumFreeEmoteCredits = syncNumFreeEmoteCouponsUpdate;

                isSynced = true;


                if (StartOfRoundPatcher.allUnlockableEmotes != null && StartOfRoundPatcher.unlockedEmotes != null)
                {
                    if (syncUnlockEverything)
                    {
                        foreach (var emote in StartOfRoundPatcher.allUnlockableEmotes)
                            StartOfRoundPatcher.UnlockEmoteLocal(emote);
                    }
                    else
                        StartOfRoundPatcher.unlockedEmotes = new List<UnlockableEmote>(StartOfRoundPatcher.complementaryEmotes);
                }
                return;
            }
            Plugin.LogError("Failed to receive config sync from server.");
        }
    }
}
