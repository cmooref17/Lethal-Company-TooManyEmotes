using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using System.Collections;
using Unity.Netcode;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using Unity.Collections;
//using static UnityEditor.Progress;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    public static class StartOfRoundPatcher
    {
        public static List<UnlockableEmote> allUnlockableEmotes;
        public static Dictionary<string, UnlockableEmote> allUnlockableEmotesDict;
        public static List<UnlockableEmote> complementaryEmotes;
        public static List<UnlockableEmote> unlockedFavoriteEmotes;
        public static List<string> allFavoriteEmotes;

        public static List<UnlockableEmote> allEmotesTier0;
        public static List<UnlockableEmote> allEmotesTier1;
        public static List<UnlockableEmote> allEmotesTier2;
        public static List<UnlockableEmote> allEmotesTier3;

        public static List<UnlockableEmote> unlockedEmotes;
        public static List<UnlockableEmote> unlockedEmotesTier0;
        public static List<UnlockableEmote> unlockedEmotesTier1;
        public static List<UnlockableEmote> unlockedEmotesTier2;
        public static List<UnlockableEmote> unlockedEmotesTier3;

        public static Dictionary<string, List<UnlockableEmote>> unlockedEmotesByPlayer;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void CreateEmoteLists()
        {

        }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void InitializeEmotes(StartOfRound __instance) {
            __instance.localClientAnimatorController = new AnimatorOverrideController(__instance.localClientAnimatorController);
            __instance.otherClientsAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);

            allUnlockableEmotes = new List<UnlockableEmote>();
            allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();

            unlockedEmotes = new List<UnlockableEmote>();
            unlockedEmotesTier0 = new List<UnlockableEmote>();
            unlockedEmotesTier1 = new List<UnlockableEmote>();
            unlockedEmotesTier2 = new List<UnlockableEmote>();
            unlockedEmotesTier3 = new List<UnlockableEmote>();

            unlockedEmotesByPlayer = new Dictionary<string, List<UnlockableEmote>>();

            complementaryEmotes = new List<UnlockableEmote>();
            unlockedFavoriteEmotes = new List<UnlockableEmote>();
            allFavoriteEmotes = new List<string>();

            allEmotesTier0 = new List<UnlockableEmote>();
            allEmotesTier1 = new List<UnlockableEmote>();
            allEmotesTier2 = new List<UnlockableEmote>();
            allEmotesTier3 = new List<UnlockableEmote>();

            var randomEmotePools = new Dictionary<string, List<UnlockableEmote>>();

            for (int i = 0; i < Plugin.customAnimationClips.Count; i++)
            {
                AnimationClip clip = Plugin.customAnimationClips[i];
                UnlockableEmote emote = new UnlockableEmote {
                    emoteId = i,
                    emoteName = clip.name,
                    displayName = "",
                    animationClip = clip,
                    rarity = 0
                };

                emote.rarity = Plugin.animationClipsTier1.Contains(clip) ? 1 :
                    Plugin.animationClipsTier2.Contains(clip) ? 2 :
                    Plugin.animationClipsTier3.Contains(clip) ? 3 : 0;

                if (emote.emoteName.Contains("_start"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    emote.transitionsToClip = emoteLoop;
                    emote.emoteName = emote.emoteName.Replace("_start", "");
                }
                if (emote.emoteName.Contains("_pose"))
                {
                    emote.emoteName = emote.emoteName.Replace("_pose", "");
                    emote.isPose = true;
                }
                if (emote.emoteName.Contains("_random"))
                {
                    emote.randomEmotePoolName = emote.emoteName.Substring(0, emote.emoteName.IndexOf("_random"));
                    if (!randomEmotePools.ContainsKey(emote.randomEmotePoolName))
                        randomEmotePools.Add(emote.randomEmotePoolName, new List<UnlockableEmote> { emote });
                    else
                    {
                        emote.purchasable = false;
                        randomEmotePools[emote.randomEmotePoolName].Add(emote);
                    }
                    emote.randomEmotePool = randomEmotePools[emote.randomEmotePoolName];

                    emote.emoteName = emote.emoteName.Replace("_random", "");
                    emote.displayName = emote.randomEmotePoolName;
                }

                if (emote.transitionsToClip != null || emote.animationClip.isLooping || emote.isPose)
                    emote.canSyncEmote = true;

                if (emote.displayName == "")
                    emote.displayName = emote.emoteName;

                emote.displayName = emote.displayName.Replace('_', ' ').Trim(' ');
                emote.displayName = char.ToUpper(emote.displayName[0]) + emote.displayName.Substring(1).ToLower();

                if (!allUnlockableEmotes.Contains(emote))
                {
                    allUnlockableEmotes.Add(emote);
                    allUnlockableEmotesDict.Add(emote.emoteName, emote);
                }

                if (Plugin.complementaryAnimationClips.Contains(clip) && emote.purchasable)
                {
                    emote.complementary = true;
                    complementaryEmotes.Add(emote);
                }
            }

            allUnlockableEmotes = allUnlockableEmotes.OrderBy(item => item.rarity).ThenBy(item => item.emoteName).ToList();

            int id = 0;
            foreach (var emote in allUnlockableEmotes)
            {
                emote.emoteId = id++;
                if (emote.rarity == 0)
                    allEmotesTier0.Add(emote);
                else if (emote.rarity == 1)
                    allEmotesTier1.Add(emote);
                else if (emote.rarity == 2)
                    allEmotesTier2.Add(emote);
                else if (emote.rarity == 3)
                    allEmotesTier3.Add(emote);
            }

            SaveManager.LoadFavoritedEmotes();

            for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
            {
                if (__instance.allPlayerScripts[i]?.playerBodyAnimator?.runtimeAnimatorController != null)
                {
                    __instance.allPlayerScripts[i].playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(__instance.otherClientsAnimatorController);
                }
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnHostConnected(PlayerControllerB __instance)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;
            if (!unlockedEmotesByPlayer.ContainsKey(StartOfRound.Instance.localPlayerController.playerUsername))
            {
                unlockedEmotesByPlayer.Add(StartOfRound.Instance.localPlayerController.playerUsername, unlockedEmotes);
                TerminalPatcher.currentEmoteCreditsByPlayer.Add(StartOfRound.Instance.localPlayerController.playerUsername, TerminalPatcher.currentEmoteCredits);
            }
            else
            {
                unlockedEmotesByPlayer[StartOfRound.Instance.localPlayerController.playerUsername] = unlockedEmotes;
                TerminalPatcher.currentEmoteCreditsByPlayer[StartOfRound.Instance.localPlayerController.playerUsername] = TerminalPatcher.currentEmoteCredits;
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        public static void OnServerStart(StartOfRound __instance) {
            if (!__instance.IsServer)
                return;

            UnlockEmotesLocal(ConfigSync.instance.syncUnlockEverything ? allUnlockableEmotes : complementaryEmotes);
            EmoteSyncManager.RotateEmoteSelectionServerRpc(TerminalPatcher.emoteStoreSeed);
        }


        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPostfix]
        public static void ResetOverrideSeedFlag(StartOfRound __instance)
        {
            __instance.overrideRandomSeed = false;
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        public static void ResetEmotesOnShipReset(StartOfRound __instance)
        {
            if (!ConfigSync.instance.syncUnlockEverything)
                ResetProgressLocal();
            if (NetworkManager.Singleton.IsServer)
                EmoteSyncManager.RotateEmoteSelectionServerRpc();
        }


        public static void ResetProgressLocal()
        {
            Plugin.Log("Resetting progression.");
            ResetEmotesLocal();
            TerminalPatcher.currentEmoteCredits = ConfigSync.instance.syncStartingEmoteCredits;
            foreach (string username in TerminalPatcher.currentEmoteCreditsByPlayer.Keys)
                TerminalPatcher.currentEmoteCreditsByPlayer[username] = ConfigSync.instance.syncStartingEmoteCredits;
            TerminalPatcher.emoteStoreSeed = 0;
        }


        public static void ResetEmotesLocal()
        {
            Plugin.Log("Resetting unlocked emotes.");
            unlockedEmotes.Clear();
            unlockedEmotesTier0.Clear();
            unlockedEmotesTier1.Clear();
            unlockedEmotesTier2.Clear();
            unlockedEmotesTier3.Clear();

            unlockedEmotesByPlayer.Clear();
            foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (playerController.playerSteamId != 0)
                    unlockedEmotesByPlayer.Add(playerController.playerUsername, playerController == StartOfRound.Instance.localPlayerController ? unlockedEmotes : new List<UnlockableEmote>());
            }
            UnlockEmotesLocal(ConfigSync.instance.syncUnlockEverything ? allUnlockableEmotes : complementaryEmotes);
            UpdateUnlockedFavoriteEmotes();
        }


        public static void UpdateUnlockedFavoriteEmotes()
        {
            unlockedFavoriteEmotes.Clear();
            foreach (string emoteName in allFavoriteEmotes)
            {
                if (allUnlockableEmotesDict.ContainsKey(emoteName))
                {
                    var emote = allUnlockableEmotesDict[emoteName];
                    if (emote != null && unlockedEmotes.Contains(emote))
                        unlockedFavoriteEmotes.Add(emote);
                }
                else
                    Plugin.LogWarning("Error loading favorited emote. Emote does not exist. The emote has likely been temporarily removed in this update.");
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyPostfix]
        public static void SyncUnlockedEmotesWithClients(StartOfRound __instance) {
            if (!NetworkManager.Singleton.IsServer)
                return;
            if (ConfigSync.instance.syncUnlockEverything)
                return;

            if (ConfigSync.instance.syncShareEverything)
            {
                Plugin.Log("Syncing unlocked emotes with clients.");
                EmoteSyncManager.SendOnUnlockEmoteUpdateMulti(TerminalPatcher.currentEmoteCredits);
            }
            else
            {
                HashSet<ulong> syncWithClients = new HashSet<ulong>();
                foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
                {
                    if (playerController.actualClientId != 0 && playerController.playerSteamId != 0)
                        syncWithClients.Add(playerController.actualClientId); // Prevent duplicates
                }
                foreach (var clientId in syncWithClients)
                    EmoteSyncManager.ServerSendSyncToClient(clientId);
            }
        }


        public static void UnlockEmotesLocal(IEnumerable<UnlockableEmote> emotes, string playerUsername = "")
        {
            foreach (var emote in emotes)
                UnlockEmoteLocal(emote, playerUsername);
        }
        public static void UnlockEmoteLocal(int emoteId, string playerUsername = "") => UnlockEmoteLocal(emoteId >= 0 && emoteId < allUnlockableEmotes.Count ? allUnlockableEmotes[emoteId] : null, playerUsername);
        public static void UnlockEmoteLocal(UnlockableEmote emote, string playerUsername = "") {
            if (emote == null)
                return;

            var _unlockedEmotes = unlockedEmotes;
            if (playerUsername != "" && !ConfigSync.instance.syncShareEverything && !unlockedEmotesByPlayer.TryGetValue(playerUsername, out _unlockedEmotes))
                return;

            if (emote.randomEmotePool != null)
            {
                foreach (var rEmote in emote.randomEmotePool)
                {
                    if (_unlockedEmotes.Contains(rEmote))
                        return;
                }
            }
            if (!_unlockedEmotes.Contains(emote))
                _unlockedEmotes.Add(emote);

            if (playerUsername == "" || ConfigSync.instance.syncShareEverything)
            {
                if (emote.rarity == 3 && !unlockedEmotesTier3.Contains(emote))
                    unlockedEmotesTier3.Add(emote);
                else if (emote.rarity == 2 && !unlockedEmotesTier2.Contains(emote))
                    unlockedEmotesTier2.Add(emote);
                else if (emote.rarity == 1 && !unlockedEmotesTier1.Contains(emote))
                    unlockedEmotesTier1.Add(emote);
                else if (!unlockedEmotesTier0.Contains(emote))
                    unlockedEmotesTier0.Add(emote);

                if (allFavoriteEmotes.Contains(emote.emoteName) && !unlockedFavoriteEmotes.Contains(emote))
                    unlockedFavoriteEmotes.Add(emote);
            }
        }


        public static void SortUnlockedEmotes()
        {
            unlockedEmotes = unlockedEmotes.OrderBy(item => item.rarity).ThenBy(item => item.emoteName).ToList();
        }


        public static bool TryGetPlayerByClientId(ulong clientId, out PlayerControllerB playerController) {
            playerController = null;
            foreach (var _playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (_playerController.actualClientId == clientId)
                {
                    playerController = _playerController;
                    break;
                }
            }
            return playerController != null;
        }


        public static bool TryGetPlayerByUsername(string username, out PlayerControllerB playerController) {
            playerController = null;
            foreach (var _playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (_playerController.playerUsername == username)
                {
                    playerController = _playerController;
                    break;
                }
            }
            return playerController != null;
        }
    }
}