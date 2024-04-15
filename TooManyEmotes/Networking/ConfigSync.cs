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
using UnityEngine;
using TooManyEmotes.Audio;
using TooManyEmotes.Props;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;
using TooManyEmotes.Compatibility;

namespace TooManyEmotes.Networking
{
    [Serializable]
    [HarmonyPatch]
    public class ConfigSync
    {
        public static bool isSynced = false;
        public static ConfigSync defaultConfig;
        public static ConfigSync instance;

        public bool syncUnlockEverything;
        public bool syncShareEverything;
        public bool syncPersistentUnlocks;
        public bool syncPersistentEmoteCredits;
        public bool syncSyncUnsharedEmotes;
        //public bool syncEnableMovingWhileEmoting;
        public bool syncDisableRaritySystem;
        public bool syncEnableGrabbableEmoteProps;

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

        public bool syncEnableMaskedEnemiesEmoting;
        public float syncMaskedEnemiesEmoteChanceOnEncounter;
        public bool syncMaskedEnemiesAlwaysEmoteOnFirstEncounter;
        public bool syncOverrideStopAndStareDuration;
        public float syncMaskedEnemyEmoteRandomDelayMin;
        public float syncMaskedEnemyEmoteRandomDelayMax;
        public float syncMaskedEnemyEmoteRandomDurationMin;
        public float syncMaskedEnemyEmoteRandomDurationMax;

        public bool syncDisableAudioShipSpeaker;

        public static Vector2 syncMaskedEnemyEmoteRandomDelay;
        public static Vector2 syncMaskedEnemyEmoteRandomDuration;


        public static HashSet<ulong> syncedClients;


        public ConfigSync()
        {
            syncUnlockEverything = ConfigSettings.unlockEverything.Value;
            syncShareEverything = syncUnlockEverything || ConfigSettings.shareEverything.Value;
            syncPersistentUnlocks = ConfigSettings.persistentUnlocks.Value;
            syncPersistentEmoteCredits = syncPersistentUnlocks && ConfigSettings.persistentEmoteCredits.Value;
            syncSyncUnsharedEmotes = ConfigSettings.syncUnsharedEmotes.Value;
            //syncEnableMovingWhileEmoting = ConfigSettings.enableMovingWhileEmoting.Value;
            syncDisableRaritySystem = ConfigSettings.disableRaritySystem.Value;

            syncEnableGrabbableEmoteProps = ConfigSettings.enableGrabbableEmoteProps.Value;

            syncStartingEmoteCredits = ConfigSettings.startingEmoteCredits.Value;
            syncAddEmoteCreditsMultiplier = ConfigSettings.addEmoteCreditsMultiplier.Value;
            syncPurchaseEmotesWithDefaultCurrency = ConfigSettings.purchaseEmotesWithDefaultCurrency.Value;

            syncPriceMultiplierEmotesStore = ConfigSettings.priceMultiplierEmotesStore.Value;

            syncNumEmotesStoreRotation = ConfigSettings.numEmotesStoreRotation.Value;
            syncRotationChanceEmoteTier0 = ConfigSettings.rotationChanceEmoteTier0.Value;
            syncRotationChanceEmoteTier1 = ConfigSettings.rotationChanceEmoteTier1.Value;
            syncRotationChanceEmoteTier2 = ConfigSettings.rotationChanceEmoteTier2.Value;
            syncRotationChanceEmoteTier3 = ConfigSettings.rotationChanceEmoteTier3.Value;

            syncEnableMaskedEnemiesEmoting = ConfigSettings.enableMaskedEnemiesEmoting.Value;
            syncMaskedEnemiesEmoteChanceOnEncounter = ConfigSettings.maskedEnemiesEmoteChanceOnEncounter.Value;
            syncMaskedEnemiesAlwaysEmoteOnFirstEncounter = ConfigSettings.maskedEnemiesAlwaysEmoteOnFirstEncounter.Value;
            syncOverrideStopAndStareDuration = ConfigSettings.overrideStopAndStareDuration.Value;

            syncMaskedEnemyEmoteRandomDelay = ParseVector2FromString(ConfigSettings.maskedEnemyEmoteRandomDelay.Value);
            syncMaskedEnemyEmoteRandomDelayMin = syncMaskedEnemyEmoteRandomDelay.x;
            syncMaskedEnemyEmoteRandomDelayMax = syncMaskedEnemyEmoteRandomDelay.y;

            syncMaskedEnemyEmoteRandomDuration = ParseVector2FromString(ConfigSettings.maskedEnemyEmoteRandomDuration.Value);
            syncMaskedEnemyEmoteRandomDurationMin = syncMaskedEnemyEmoteRandomDuration.x;
            syncMaskedEnemyEmoteRandomDurationMax = syncMaskedEnemyEmoteRandomDuration.y;

            syncDisableAudioShipSpeaker = ConfigSettings.disableAudioShipSpeaker.Value;

            if (syncUnlockEverything)
            {
                syncShareEverything = true;
                syncPersistentUnlocks = false;
            }
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


        public Vector2 ParseVector2FromString(string str)
        {
            Vector2 vector = Vector2.zero;
            try
            {
                string[] values = str.Split(',');
                if (float.TryParse(values[0].Trim(' '), out float x) && float.TryParse(values[1].Trim(' '), out float y))
                    vector = new Vector2(Mathf.Min(Mathf.Abs(x), Mathf.Abs(y)), Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)));
                return vector;
            } catch { }
            return Vector2.zero;
        }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void ResetValues()
        {
            isSynced = false;
            BuildDefaultConfigSync();
        }


        public static void BuildDefaultConfigSync()
        {
            defaultConfig = new ConfigSync();
            instance = new ConfigSync();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance)
        {
            if (isSynced)
                return;

            isSynced = isServer;
            SyncManager.isSynced = false;
            SyncManager.requestedSync = false;
            if (isServer)
            {
                syncedClients = new HashSet<ulong>();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestConfigSyncServerRpc", OnRequestConfigSyncServerRpc);
                if (instance.syncUnlockEverything)
                {
                    foreach (var emote in EmotesManager.allUnlockableEmotes)
                        SessionManager.UnlockEmoteLocal(emote);
                }
                OnSynced();
            }
            else
            {
                // Unlock all emotes until synced with host
                foreach (var emote in EmotesManager.allUnlockableEmotes)
                    SessionManager.UnlockEmoteLocal(emote);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestConfigSyncClientRpc", OnRequestConfigSyncClientRpc);
                RequestConfigSync();
            }
        }


        public static void RequestConfigSync()
        {
            if (NetworkManager.Singleton.IsClient)
            {
                Log("Requesting config sync from server");
                var writer = new FastBufferWriter(0, Allocator.Temp);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestConfigSyncServerRpc", NetworkManager.ServerClientId, writer);
                return;
            }
            LogError("Failed to send unlocked emote update to server.");
        }


        private static void OnRequestConfigSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            Log("Receiving config sync request from client: " + clientId);
            syncedClients.Add(clientId);
            SyncManager.syncedClients.Remove(clientId);
            byte[] bytes = SerializeConfigToByteArray(instance);
            var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;

            int dataLength;
            reader.ReadValueSafe(out dataLength);
            if (reader.TryBeginRead(dataLength))
            {
                Log("Receiving config sync from server.");
                byte[] bytes = new byte[dataLength];
                reader.ReadBytesSafe(ref bytes, dataLength);
                instance = DeserializeFromByteArray(bytes);
                syncMaskedEnemyEmoteRandomDelay = new Vector2(instance.syncMaskedEnemyEmoteRandomDelayMin, instance.syncMaskedEnemyEmoteRandomDelayMax);
                syncMaskedEnemyEmoteRandomDuration = new Vector2(instance.syncMaskedEnemyEmoteRandomDurationMin, instance.syncMaskedEnemyEmoteRandomDurationMax);

                OnSynced();

                if (EmotesManager.allUnlockableEmotes != null && SessionManager.unlockedEmotes != null)
                {
                    SessionManager.ResetProgressLocal(true);
                    if (instance.syncUnlockEverything)
                        SessionManager.UnlockEmotesLocal(EmotesManager.allUnlockableEmotes);
                    else
                        SessionManager.UnlockEmotesLocal(EmotesManager.complementaryEmotes);
                    SessionManager.UpdateUnlockedFavoriteEmotes();
                }
                isSynced = true;
                return;
            }
            LogError("Error receiving sync from server.");
        }


        private static void OnSynced()
        {
            isSynced = true;
            if (!instance.syncDisableAudioShipSpeaker)
                EmoteAudioPlayerManager.InitializeShipSpeakerAudioPlayer();
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
