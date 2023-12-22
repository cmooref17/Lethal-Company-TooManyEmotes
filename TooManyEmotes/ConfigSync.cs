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

        public static int syncStartingEmoteCredits;
        public static float syncAddEmoteCreditsMultiplier;
        public static float syncPriceMultiplierEmotesStore;

        public static int syncBasePriceEmoteTier0;
        public static int syncBasePriceEmoteTier1;
        public static int syncBasePriceEmoteTier2;
        public static int syncBasePriceEmoteTier3;

        public static int syncNumEmotesStoreRotation;
        public static float syncRotationChanceEmoteTier0;
        public static float syncRotationChanceEmoteTier1;
        public static float syncRotationChanceEmoteTier2;
        public static float syncRotationChanceEmoteTier3;

        //public static int syncNumMysteryEmotesStoreRotation;


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

            syncStartingEmoteCredits = ConfigSettings.startingEmoteCredits.Value;
            syncAddEmoteCreditsMultiplier = ConfigSettings.addEmoteCreditsMultiplier.Value;
            syncPriceMultiplierEmotesStore = ConfigSettings.priceMultiplierEmotesStore.Value;

            syncNumEmotesStoreRotation = ConfigSettings.numEmotesStoreRotation.Value;
            syncRotationChanceEmoteTier0 = ConfigSettings.rotationChanceEmoteTier0.Value;
            syncRotationChanceEmoteTier1 = ConfigSettings.rotationChanceEmoteTier1.Value;
            syncRotationChanceEmoteTier2 = ConfigSettings.rotationChanceEmoteTier2.Value;
            syncRotationChanceEmoteTier3 = ConfigSettings.rotationChanceEmoteTier3.Value;

            //syncNumMysteryEmotesStoreRotation = ConfigSettings.numMysteryEmotesStoreRotation.Value;

            if (ConfigSettings.disableRaritySystem.Value)
            {
                syncBasePriceEmoteTier0 = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceEmoteTier1 = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceEmoteTier2 = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
                syncBasePriceEmoteTier3 = ConfigSettings.basePriceEmoteRaritySystemDisabled.Value;
            }
            else
            {
                syncBasePriceEmoteTier0 = ConfigSettings.basePriceEmoteTier0.Value;
                syncBasePriceEmoteTier1 = ConfigSettings.basePriceEmoteTier1.Value;
                syncBasePriceEmoteTier2 = ConfigSettings.basePriceEmoteTier2.Value;
                syncBasePriceEmoteTier3 = ConfigSettings.basePriceEmoteTier3.Value;
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
            var writer = new FastBufferWriter(sizeof(bool) * 2 + sizeof(float) * 6 + sizeof(int) * 6, Allocator.Temp);
            writer.WriteValueSafe(syncUnlockEverything);
            writer.WriteValueSafe(syncDisableRaritySystem);

            writer.WriteValueSafe(syncStartingEmoteCredits);
            writer.WriteValueSafe(syncAddEmoteCreditsMultiplier);
            writer.WriteValueSafe(syncPriceMultiplierEmotesStore);

            writer.WriteValueSafe(syncBasePriceEmoteTier0);
            writer.WriteValueSafe(syncBasePriceEmoteTier1);
            writer.WriteValueSafe(syncBasePriceEmoteTier2);
            writer.WriteValueSafe(syncBasePriceEmoteTier3);

            writer.WriteValueSafe(syncNumEmotesStoreRotation);
            writer.WriteValueSafe(syncRotationChanceEmoteTier0);
            writer.WriteValueSafe(syncRotationChanceEmoteTier1);
            writer.WriteValueSafe(syncRotationChanceEmoteTier2);
            writer.WriteValueSafe(syncRotationChanceEmoteTier3);

            //writer.WriteValueSafe(syncNumMysteryEmotesStoreRotation);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient)
                return;

            if (reader.TryBeginRead(sizeof(bool) + sizeof(float) * 6 + sizeof(int) * 6))
            {
                Plugin.Log("Receiving Config sync from server.");
                
                reader.ReadValue(out syncUnlockEverything);
                reader.ReadValue(out syncDisableRaritySystem);

                reader.ReadValue(out syncStartingEmoteCredits);
                reader.ReadValue(out syncAddEmoteCreditsMultiplier);
                reader.ReadValue(out syncPriceMultiplierEmotesStore);

                reader.ReadValue(out syncBasePriceEmoteTier0);
                reader.ReadValue(out syncBasePriceEmoteTier1);
                reader.ReadValue(out syncBasePriceEmoteTier2);
                reader.ReadValue(out syncBasePriceEmoteTier3);

                reader.ReadValue(out syncNumEmotesStoreRotation);
                reader.ReadValue(out syncRotationChanceEmoteTier0);
                reader.ReadValue(out syncRotationChanceEmoteTier1);
                reader.ReadValue(out syncRotationChanceEmoteTier2);
                reader.ReadValue(out syncRotationChanceEmoteTier3);

                //reader.ReadValue(out syncNumMysteryEmotesStoreRotation);

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
