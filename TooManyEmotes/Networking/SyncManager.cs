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
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;
using System.Collections;
using ES3Types;

namespace TooManyEmotes.Networking
{
    [HarmonyPatch]
    public static class SyncManager
    {
        public static bool requestedSync = false;
        public static bool isSynced = false;
        public static HashSet<ulong> syncedClients = new HashSet<ulong>();

        /*public static HashSet<int> overrideEmotesComplementary = new HashSet<int>();
        public static HashSet<int> overrideEmotesTier0 = new HashSet<int>();
        public static HashSet<int> overrideEmotesTier1 = new HashSet<int>();
        public static HashSet<int> overrideEmotesTier2 = new HashSet<int>();
        public static HashSet<int> overrideEmotesTier3 = new HashSet<int>();

        public static HashSet<string> overrideEmoteNamesComplementary = new HashSet<string>();
        public static HashSet<string> overrideEmoteNames0 = new HashSet<string>();
        public static HashSet<string> overrideEmoteNames1 = new HashSet<string>();
        public static HashSet<string> overrideEmoteNames2 = new HashSet<string>();
        public static HashSet<string> overrideEmoteNames3 = new HashSet<string>();*/


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        private static void ResetValues()
        {
            isSynced = false;
            requestedSync = false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void Init()
        {
            isSynced = false;
            requestedSync = false;
            if (isServer)
            {
                syncedClients?.Clear();

                /*if (!string.IsNullOrEmpty(ConfigSettings.forceEmotesIntoComplementary.Value) && ConfigSettings.forceEmotesIntoComplementary.Value.Contains(","))
                {
                    string str = ConfigSettings.forceEmotesIntoComplementary.Value.ToLower();
                    while (str.Contains("  "))
                        str = str.Replace("  ", " ");
                    str = str.Replace(", ", ",");
                    var emoteNames = str.Split(',');
                    foreach (var e in emoteNames)
                        overrideEmoteNamesComplementary.Add(e);
                }
                if (!string.IsNullOrEmpty(ConfigSettings.forceEmotesTier0.Value) && ConfigSettings.forceEmotesTier0.Value.Contains(","))
                {
                    string str = ConfigSettings.forceEmotesTier0.Value.ToLower();
                    while (str.Contains("  "))
                        str = str.Replace("  ", " ");
                    str = str.Replace(", ", ",");
                    var emoteNames = str.Split(',');
                    foreach (var e in emoteNames)
                        overrideEmoteNames0.Add(e);
                }
                if (!string.IsNullOrEmpty(ConfigSettings.forceEmotesTier1.Value) && ConfigSettings.forceEmotesTier1.Value.Contains(","))
                {
                    string str = ConfigSettings.forceEmotesTier1.Value.ToLower();
                    while (str.Contains("  "))
                        str = str.Replace("  ", " ");
                    str = str.Replace(", ", ",");
                    var emoteNames = str.Split(',');
                    foreach (var e in emoteNames)
                        overrideEmoteNames1.Add(e);
                }
                if (!string.IsNullOrEmpty(ConfigSettings.forceEmotesTier2.Value) && ConfigSettings.forceEmotesTier2.Value.Contains(","))
                {
                    string str = ConfigSettings.forceEmotesTier2.Value.ToLower();
                    while (str.Contains("  "))
                        str = str.Replace("  ", " ");
                    str = str.Replace(", ", ",");
                    var emoteNames = str.Split(',');
                    foreach (var e in emoteNames)
                        overrideEmoteNames2.Add(e);
                }
                if (!string.IsNullOrEmpty(ConfigSettings.forceEmotesTier3.Value) && ConfigSettings.forceEmotesTier3.Value.Contains(","))
                {
                    string str = ConfigSettings.forceEmotesTier3.Value.ToLower();
                    while (str.Contains("  "))
                        str = str.Replace("  ", " ");
                    str = str.Replace(", ", ",");
                    var emoteNames = str.Split(',');
                    foreach (var e in emoteNames)
                        overrideEmoteNames3.Add(e);
                }

                for (int i = 0; i < Plugin.customAnimationClips.Count; i++)
                {
                    var animationClip = Plugin.customAnimationClips[i];
                    string name = animationClip.name.ToLower();
                    if (overrideEmoteNamesComplementary.Contains(name))
                        overrideEmotesComplementary.Add(i);
                    else if (overrideEmoteNames0.Contains(name))
                        overrideEmotesTier0.Add(i);
                    else if (overrideEmoteNames1.Contains(name))
                        overrideEmotesTier1.Add(i);
                    else if (overrideEmoteNames2.Contains(name))
                        overrideEmotesTier2.Add(i);
                    else if (overrideEmoteNames3.Contains(name))
                        overrideEmotesTier3.Add(i);
                }*/

                OnSynced();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestSyncServerRpc", OnRequestSyncServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnUnlockEmoteServerRpc", OnUnlockEmoteServerRpc);
            }
            else if (isClient)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRequestSyncClientRpc", OnRequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnUnlockEmoteClientRpc", OnUnlockEmoteClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TooManyEmotes.OnRotateEmotesClientRpc", RotateEmoteSelectionClientRpc);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        private static void RequestSyncAfterConfigUpdate(PlayerControllerB __instance)
        {
            if (!isClient || isServer)
                return;

            if (!isSynced && !requestedSync && ConfigSync.isSynced && __instance == localPlayerController)
            {
                requestedSync = true;
                SendSyncRequest();
            }
        }


        public static void SendSyncRequest()
        {
            if (!isClient || isServer)
                return;

            Log("Sending sync request to server.");
            var writer = new FastBufferWriter(0, Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestSyncServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnRequestSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            Log("Receiving sync request from client: " + clientId);
            ServerSendSyncToClient(clientId);

            SyncPerformingEmoteManager.doNotTriggerAudioDict?.Clear(); // Will force the server to send the next doNotTriggerAudio to all clients, for each emote controller.
        }


        public static void ServerSendSyncToClient(ulong clientId)
        {
            if (!isServer || !ConfigSync.syncedClients.Contains(clientId))
                return;

            if (TryGetPlayerByClientId(clientId, out var playerRequestingSync))
            {
                //Log("OnPlayerConnect: " + clientId + " Username: " + playerRequestingSync.playerUsername + " SteamId: " + playerRequestingSync.playerSteamId);
                if (!SessionManager.unlockedEmotesByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    SessionManager.unlockedEmotesByPlayer.Add(playerRequestingSync.playerUsername, new List<UnlockableEmote>());
                if (!TerminalPatcher.currentEmoteCreditsByPlayer.ContainsKey(playerRequestingSync.playerUsername))
                    TerminalPatcher.currentEmoteCreditsByPlayer.Add(playerRequestingSync.playerUsername, ConfigSync.instance.syncStartingEmoteCredits);
            }

            List<UnlockableEmote> unlockedEmotes = SessionManager.unlockedEmotes;
            int emoteCredits = TerminalPatcher.currentEmoteCredits;
            if (!ConfigSync.instance.syncUnlockEverything && !ConfigSync.instance.syncShareEverything)
            {
                if (playerRequestingSync != null)
                {
                    if (SessionManager.unlockedEmotesByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _unlockedEmotes))
                    {
                        Log("Loading " + _unlockedEmotes.Count + " unlocked emotes for player: " + playerRequestingSync.playerUsername);
                        unlockedEmotes = _unlockedEmotes;
                    }
                    if (TerminalPatcher.currentEmoteCreditsByPlayer.TryGetValue(playerRequestingSync.playerUsername, out var _emoteCredits))
                    {
                        Log("Loading " + _emoteCredits + " emote credits for player: " + playerRequestingSync.playerUsername);
                        emoteCredits = _emoteCredits;
                    }
                }
                else
                {
                    LogError("Error loading custom emotes for player. Player with id: " + clientId + " does not exist?");
                    unlockedEmotes = EmotesManager.complementaryEmotes;
                    emoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
                }
            }

            int numEmotes = ConfigSync.instance.syncPersistentUnlocksGlobal ? 0 : unlockedEmotes.Count;
            //int numEmotes = unlockedEmotes.Count;
            int bufferSize = sizeof(int) + sizeof(short) * 2 + sizeof(short) * numEmotes;

            // override emotes tier
            //bufferSize += sizeof(short) * 5 + sizeof(short) * overrideEmotesComplementary.Count + sizeof(short) * overrideEmotesTier0.Count + sizeof(short) * overrideEmotesTier1.Count + sizeof(short) * overrideEmotesTier2.Count + sizeof(short) * overrideEmotesTier3.Count;

            var writer = new FastBufferWriter(bufferSize, Allocator.Temp);

            writer.WriteValue(TerminalPatcher.emoteStoreSeed);
            writer.WriteValue((short)Mathf.Min(emoteCredits, 32767));
            writer.WriteValue((short)numEmotes);
            /*writer.WriteValue((short)overrideEmotesComplementary.Count);
            writer.WriteValue((short)overrideEmotesTier0.Count);
            writer.WriteValue((short)overrideEmotesTier1.Count);
            writer.WriteValue((short)overrideEmotesTier2.Count);
            writer.WriteValue((short)overrideEmotesTier3.Count);*/

            /*
            foreach (var emoteIndex in overrideEmotesComplementary)
                writer.WriteValue((short)emoteIndex);
            foreach (var emoteIndex in overrideEmotesTier0)
                writer.WriteValue((short)emoteIndex);
            foreach (var emoteIndex in overrideEmotesTier1)
                writer.WriteValue((short)emoteIndex);
            foreach (var emoteIndex in overrideEmotesTier2)
                writer.WriteValue((short)emoteIndex);
            foreach (var emoteIndex in overrideEmotesTier3)
                writer.WriteValue((short)emoteIndex);*/

            for (int i = 0; i < numEmotes; i++)
                writer.WriteValue((short)unlockedEmotes[i].emoteId);

            syncedClients.Add(clientId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnRequestSyncClientRpc", clientId, writer);
        }


        private static void OnRequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || !ConfigSync.isSynced)
                return;

            SessionManager.ResetProgressLocal();

            reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
            reader.ReadValue(out short currentEmoteCredits);
            reader.ReadValue(out short numEmotes);
            /*reader.ReadValue(out short numOverrideEmotesComplementary);
            reader.ReadValue(out short numOverrideEmotes0);
            reader.ReadValue(out short numOverrideEmotes1);
            reader.ReadValue(out short numOverrideEmotes2);
            reader.ReadValue(out short numOverrideEmotes3);*/

            TerminalPatcher.currentEmoteCredits = currentEmoteCredits;

            /*overrideEmoteNamesComplementary?.Clear();
            overrideEmoteNames0?.Clear();
            overrideEmoteNames1?.Clear();
            overrideEmoteNames2?.Clear();
            overrideEmoteNames3?.Clear();*/
            
            /*for (int i = 0; i < numOverrideEmotesComplementary; i++)
            {
                if (i >= 0 && i < Plugin.customAnimationClips.Count)
                {
                    string name = Plugin.customAnimationClips[i].name.ToLower();
                    overrideEmoteNamesComplementary.Add(name);
                }
            }
            for (int i = 0; i < numOverrideEmotes0; i++)
            {
                if (i >= 0 && i < Plugin.customAnimationClips.Count)
                {
                    string name = Plugin.customAnimationClips[i].name.ToLower();
                    overrideEmoteNames0.Add(name);
                }
            }
            for (int i = 0; i < numOverrideEmotes1; i++)
            {
                if (i >= 0 && i < Plugin.customAnimationClips.Count)
                {
                    string name = Plugin.customAnimationClips[i].name.ToLower();
                    overrideEmoteNames1.Add(name);
                }
            }
            for (int i = 0; i < numOverrideEmotes2; i++)
            {
                if (i >= 0 && i < Plugin.customAnimationClips.Count)
                {
                    string name = Plugin.customAnimationClips[i].name.ToLower();
                    overrideEmoteNames2.Add(name);
                }
            }
            for (int i = 0; i < numOverrideEmotes3; i++)
            {
                if (i >= 0 && i < Plugin.customAnimationClips.Count)
                {
                    string name = Plugin.customAnimationClips[i].name.ToLower();
                    overrideEmoteNames3.Add(name);
                }
            }*/

            if (numEmotes <= 0)
            {
                if (numEmotes < 0)
                    SessionManager.ResetEmotesLocal();
            }
            else
            {
                if (reader.TryBeginRead(sizeof(short) * numEmotes))
                {
                    for (int i = 0; i < numEmotes; i++)
                    {
                        reader.ReadValue(out short emoteId);
                        SessionManager.UnlockEmoteLocal(emoteId);
                    }
                }
                else
                {
                    LogError("Error receiving emotes sync from server.");
                    OnSynced();
                    return;
                }
            }
            Log("Received sync from server. CurrentEmoteCredits: " + TerminalPatcher.currentEmoteCredits + " EmoteStoreSeed: " + TerminalPatcher.emoteStoreSeed + " NumEmotes: " + numEmotes);
            OnSynced();
        }


        internal static void OnSynced()
        {
            isSynced = true;
            requestedSync = true;
            if (ConfigSync.instance.syncPersistentUnlocksGlobal)
                SessionManager.ResetEmotesLocal();
            SaveManager.LoadLocalPlayerValues();
            StartOfRound.Instance.StartCoroutine(OnSyncedEndOfFrame());
        }


        private static IEnumerator OnSyncedEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            TerminalPatcher.RotateNewEmoteSelection();
        }


        public static void SendOnUnlockEmoteUpdate(int emoteId, int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(short) * 3, Allocator.Temp);
            Log("Sending unlocked emote update to server. Emote id: " + emoteId);
            writer.WriteValue((short)Mathf.Min(newEmoteCredits, 32767));
            writer.WriteValue((short)1); // one emote unlocked
            writer.WriteValue((short)emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        public static void SendOnUnlockEmoteUpdateMulti(int newEmoteCredits = -1)
        {
            var writer = new FastBufferWriter(sizeof(short) * 2 + sizeof(short) * SessionManager.unlockedEmotes.Count, Allocator.Temp);
            Log("Sending all unlocked emotes update to server.");
            writer.WriteValue((short)Mathf.Min(newEmoteCredits, 32767));
            writer.WriteValue((short)SessionManager.unlockedEmotes.Count);
            foreach (var emote in SessionManager.unlockedEmotes)
                writer.WriteValue((short)emote.emoteId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("TooManyEmotes.OnUnlockEmoteServerRpc", NetworkManager.ServerClientId, writer);
        }


        private static void OnUnlockEmoteServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isServer)
                return;

            if (ConfigSync.instance.syncUnlockEverything)
                LogWarning("Received unlocked emote update from client when UnlockEverything is enabled. This will be ignored.");

            reader.ReadValue(out short newEmoteCredits);
            reader.ReadValue(out short numEmotes);

            if (!TryGetPlayerByClientId(clientId, out PlayerControllerB playerWhoUnlocked) && !ConfigSync.instance.syncShareEverything)
            {
                LogError("Failed to receive unlocked emote update from client. Could not find player with client id: " + clientId);
                return;
            }

            if (newEmoteCredits != -1)
            {
                if (!ConfigSync.instance.syncShareEverything && clientId != 0)
                    TerminalPatcher.currentEmoteCreditsByPlayer[playerWhoUnlocked.playerUsername] = newEmoteCredits;
                else
                    TerminalPatcher.currentEmoteCredits = newEmoteCredits;
            }

            Log("Receiving unlocked emote update from client for " + numEmotes + " emotes.");
            var writer = new FastBufferWriter(sizeof(uint) + sizeof(short) * 2 + sizeof(short) * numEmotes, Allocator.Temp);
            writer.WriteValue((uint)clientId);
            writer.WriteValue((short)Mathf.Min(TerminalPatcher.currentEmoteCredits, 32767));
            writer.WriteValue((short)numEmotes);
            if (reader.TryBeginRead(sizeof(short) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    reader.ReadValue(out short emoteId);
                    writer.WriteValue((short)emoteId);
                    if (!ConfigSync.instance.syncShareEverything && clientId != 0)
                        SessionManager.UnlockEmoteLocal(emoteId, purchased: true, playerUsername: playerWhoUnlocked.playerUsername);
                    else
                        SessionManager.UnlockEmoteLocal(emoteId, purchased: true);
                }
                //if (ConfigSync.instance.syncShareEverything)
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnUnlockEmoteClientRpc", writer);
                return;
            }
            LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        private static void OnUnlockEmoteClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out uint clientIdUnlockedEmote);
            reader.ReadValue(out short newEmoteCredits);
            reader.ReadValue(out short numEmotes);

            if (!TryGetPlayerByClientId(clientIdUnlockedEmote, out var playerWhoUnlocked) && !ConfigSync.instance.syncShareEverything)
                return;

            if (newEmoteCredits != -1)
            {
                if (!ConfigSync.instance.syncShareEverything)
                    TerminalPatcher.currentEmoteCreditsByPlayer[playerWhoUnlocked.playerUsername] = newEmoteCredits;
                else
                    TerminalPatcher.currentEmoteCredits = newEmoteCredits;
            }

            if (reader.TryBeginRead(sizeof(short) * numEmotes))
            {
                for (int i = 0; i < numEmotes; i++)
                {
                    reader.ReadValue(out short emoteId);
                    Log("Receiving unlocked emote update from server. Emote id: " + emoteId);
                    if (ConfigSync.instance.syncShareEverything)
                        SessionManager.UnlockEmoteLocal(emoteId, purchased: true);
                    else
                        SessionManager.UnlockEmoteLocal(emoteId, purchased: true, playerUsername: playerWhoUnlocked.playerUsername);
                }
                return;
            }
            LogError("Failed to receive unlocked emote updates from client. Expected updates: " + numEmotes);
        }


        public static void RotateEmoteSelectionServer(int overrideSeed = 0)
        {
            if (!isServer)
                return;

            TerminalPatcher.emoteStoreSeed = overrideSeed == 0 ? UnityEngine.Random.Range(0, 1000000000) : overrideSeed;
            TerminalPatcher.RotateNewEmoteSelection();
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValue(TerminalPatcher.emoteStoreSeed);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("TooManyEmotes.OnRotateEmotesClientRpc", writer);
        }


        private static void RotateEmoteSelectionClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!isClient || isServer)
                return;

            reader.ReadValue(out TerminalPatcher.emoteStoreSeed);
            TerminalPatcher.RotateNewEmoteSelection();
        }
    }
}
