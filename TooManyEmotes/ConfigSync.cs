using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections;
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

    [Serializable]
    [HarmonyPatch]
    public class ConfigSync {

        public static bool isSynced = false;
        public static ConfigSync defaultConfig;
        public static ConfigSync instance;

        public bool syncUnlockEverything;
        public bool syncShareEverything;
        public bool syncSyncUnsharedEmotes;
        public bool syncDisableRaritySystem;

        public int syncStartingEmoteCredits;
        public float syncAddEmoteCreditsMultiplier;
        public bool syncPurchaseEmotesWithDefaultCurrency;

        public float syncPriceMultiplierEmotesStore;
        public int syncBasePriceEmoteTier0;
        public int syncBasePriceEmoteTier1;
        public int syncBasePriceEmoteTier2;
        public int syncBasePriceEmoteTier3;

        public int syncNumEmotesStoreRotation;
        public float syncRotationChanceEmoteTier0;
        public float syncRotationChanceEmoteTier1;
        public float syncRotationChanceEmoteTier2;
        public float syncRotationChanceEmoteTier3;

        //public static int syncNumMysteryEmotesStoreRotation;

        public static HashSet<ulong> syncedClients;


        public ConfigSync()
        {
            syncUnlockEverything = ConfigSettings.unlockEverything.Value;
            syncShareEverything = ConfigSettings.shareEverything.Value;
            syncSyncUnsharedEmotes = ConfigSettings.syncUnsharedEmotes.Value;
            syncDisableRaritySystem = ConfigSettings.disableRaritySystem.Value;

            syncStartingEmoteCredits = ConfigSettings.startingEmoteCredits.Value;
            syncAddEmoteCreditsMultiplier = ConfigSettings.addEmoteCreditsMultiplier.Value;
            syncPurchaseEmotesWithDefaultCurrency = ConfigSettings.purchaseEmotesWithDefaultCurrency.Value;

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


        public static void BuildDefaultConfigSync()
        {
            defaultConfig = new ConfigSync();
            instance = new ConfigSync();
        }



        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void ResetValues()
        {
            isSynced = false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance)
        {
            if (isSynced)
                return;
            isSynced = NetworkManager.Singleton.IsServer;
            EmoteSyncManager.isSynced = false;
            EmoteSyncManager.requestedSync = false;
            if (NetworkManager.Singleton.IsServer)
            {
                syncedClients = new HashSet<ulong>();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes-OnRequestConfigSyncServerRpc", OnRequestConfigSyncServerRpc);
                if (instance.syncUnlockEverything)
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
            syncedClients.Add(clientId);
            EmoteSyncManager.syncedClients.Remove(clientId);
            byte[] bytes = SerializeConfigToByteArray(instance);
            var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes-OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader) {
            if (!NetworkManager.Singleton.IsClient)
                return;

            int dataLength;
            if (reader.TryBeginRead(sizeof(int)))
            {
                reader.ReadValueSafe(out dataLength);
                if (reader.TryBeginRead(dataLength))
                {
                    Plugin.Log("Receiving config sync from server.");
                    byte[] bytes = new byte[dataLength];
                    reader.ReadBytesSafe(ref bytes, dataLength);
                    instance = DeserializeFromByteArray(bytes);
                    isSynced = true;

                    if (StartOfRoundPatcher.allUnlockableEmotes != null && StartOfRoundPatcher.unlockedEmotes != null)
                    {
                        StartOfRoundPatcher.ResetProgressLocal();
                        if (instance.syncUnlockEverything)
                            StartOfRoundPatcher.UnlockEmotesLocal(StartOfRoundPatcher.allUnlockableEmotes);
                        else
                            StartOfRoundPatcher.UnlockEmotesLocal(StartOfRoundPatcher.complementaryEmotes);
                        StartOfRoundPatcher.UpdateUnlockedFavoriteEmotes();
                    }
                    return;
                }
                Plugin.LogError("Error receiving sync from server.");
                return;
            }
            Plugin.LogError("Error receiving bytes length.");
        }



        public static byte[] SerializeConfigToByteArray(ConfigSync config)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            binaryFormatter.Serialize(memoryStream, config);
            return memoryStream.ToArray();
        }

        public static ConfigSync DeserializeFromByteArray(byte[] data)
        {
            MemoryStream s = new MemoryStream(data);
            BinaryFormatter b = new BinaryFormatter();
            return (ConfigSync)b.Deserialize(s);
        }
    }
}
