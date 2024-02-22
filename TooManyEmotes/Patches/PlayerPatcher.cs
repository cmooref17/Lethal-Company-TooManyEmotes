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
using UnityEngine.Rendering;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using System.Reflection;
using Unity.Netcode;
using MoreCompany.Cosmetics;
using UnityEditor.Animations;
using TMPro;
using UnityEngine.Scripting;
using UnityEditor.PackageManager;
using TooManyEmotes.Input;
using TooManyEmotes.Compatibility;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class PlayerPatcher
    {
        //public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        //public static Dictionary<GameObject, EmoteController> allEmoteControllers { get { return EmoteController.allEmoteControllers; } }
        //public static Dictionary<PlayerControllerB, EmoteControllerPlayer> allPlayerEmoteControllers { get { return EmoteControllerPlayer.allPlayerEmoteControllers; } }
        public static EmoteControllerPlayer emoteControllerLocal { get { return EmoteControllerPlayer.emoteControllerLocal; } }
        //public static int emoteStateHash { get { return localPlayerController != null ? Animator.StringToHash(localPlayerController.playerBodyAnimator.GetLayerName(1) + ".Dance1") : -1; } }


        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        public static void InitializeEmoteController(PlayerControllerB __instance)
        {
            __instance.gameObject.AddComponent<EmoteControllerPlayer>();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void OnLocalClientReady(PlayerControllerB __instance)
        {
            Plugin.Log("Initializing local player.");
            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var textComponent = HUDManager.Instance.controlTipLines[i];
                if (textComponent.text == "")
                {
                    HUDManager.Instance.controlTipLines[i].text = string.Format("[{0}]: Open Emote Radial Menu", KeybindDisplayNames.GetKeybindDisplayName(Keybinds.OpenEmoteMenuAction));
                    break;
                }
            }
            KeybindDisplayNames.UpdateControlTipLines();
        }


        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPrefix]
        public static void OnPlayerDC(int playerObjectNumber, ulong clientId, StartOfRound __instance)
        {
            PlayerControllerB playerController = __instance.allPlayerObjects[playerObjectNumber].GetComponent<PlayerControllerB>();
            if (playerController == null)
                return;
            if (playerController != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController) && emoteController.IsPerformingCustomEmote())
                emoteController.StopPerformingEmote();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPrefix]
        public static void OnPlayerDeath(Vector3 bodyVelocity, PlayerControllerB __instance)
        {
            Plugin.LogWarning("Player died while emoting. What a loser... I mean, I hope this handles smoothly.");
            if (__instance!= null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
                emoteController.StopPerformingEmote();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "StopPerformingEmoteClientRpc")]
        [HarmonyPrefix]
        public static void OnStopPerformingEmote(PlayerControllerB __instance)
        {
            if (__instance != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
                emoteController.StopPerformingEmote();
        }


        // Let's not remove this, unless ModelReplacementAPI updates their patch for this mod to reference the UnlockableEmotePlayer class.
        public static UnlockableEmote GetCurrentlyPlayingEmote(PlayerControllerB playerController)
        {
            if (EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController))
                return emoteController.performingEmote;
            return null;
        }


        /*
        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static bool PerformCustomEmoteLocalPrefix(InputAction.CallbackContext context, int emoteID, PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || __instance != localPlayerController)
                return true;

            var syncWithPlayerData = lookingAtPlayerSyncableEmote != null ? allPlayerData[lookingAtPlayerSyncableEmote] : null;

            if (emoteID > 2)
            {
                //if (performingCustomEmoteLocal != null)
                return !playerDataLocal.isPerformingEmote;
            }
            
            if (emoteID < 0)
            {
                // Prevent the emote if performing an emote from another mod, such as MoreEmotes
                if (localPlayerController.playerBodyAnimator.GetInteger("emoteNumber") > 2)
                    return false;

                if (CallCheckConditionsForEmote(localPlayerController))
                {
                    localPlayerController.performingEmote = true;
                    localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                    emoteID = Mathf.Abs(emoteID) - 1;
                    var emote = StartOfRoundPatcher.allUnlockableEmotes[emoteID];
                    if (emote != null)
                    {
                        if (emote.randomEmotePool != null && emote.randomEmotePool.Count >= 1)
                        {
                            int randomIndex = UnityEngine.Random.Range(0, emote.randomEmotePool.Count);
                            var randomEmote = emote.randomEmotePool[randomIndex];
                            if (randomEmote != null)
                                emote = randomEmote;
                        }
                        PlayerControllerB syncWithPlayer = null;
                        //if (syncingEmoteWithPlayer && lookingAtPlayerSyncableEmote != null && performingEmotes.TryGetValue(lookingAtPlayerSyncableEmote, out var syncEmote) && syncEmote.canSyncEmote)
                        if (syncingEmoteWithPlayer && syncWithPlayerData?.performingEmote != null && syncWithPlayerData.performingEmote.canSyncEmote)
                        {
                            emote = syncWithPlayerData.performingEmote;
                            syncWithPlayer = lookingAtPlayerSyncableEmote;
                        }
                        OnUpdateCustomEmote(emote.emoteId, localPlayerController, syncWithPlayer);
                        ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                        ForceSendAnimationUpdateLocal(emote.emoteId);
                    }
                    return false;
                }
            }
            if (playerDataLocal.isPerformingEmote)
            {
                OnUpdateCustomEmote(-1, localPlayerController);
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
                ForceSendAnimationUpdateLocal(-1);
                return false;
            }

            return true;
        }
        */


        [HarmonyPatch(typeof(PlayerControllerB), "PerformEmote")]
        [HarmonyPrefix]
        public static void StopCustomEmoteOnDefaultEmote(InputAction.CallbackContext context, int emoteID)
        {
            if (context.performed && emoteControllerLocal.IsPerformingCustomEmote())
            {
                //Plugin.LogWarning("OnPerformEmoteLocalPlayer. Stopping custom emote.");
                emoteControllerLocal.StopPerformingEmote();
            }
        }


        /*
        [HarmonyPatch(typeof(PlayerControllerB), "UpdatePlayerAnimationClientRpc")]
        [HarmonyPrefix]
        public static void UpdatePlayerAnimationClientRpcPrefix(int animationState, ref float animationSpeed, PlayerControllerB __instance)
        {
            if (localPlayerController == null || __instance == localPlayerController)
            {
                Plugin.LogWarning("Return A");
                return;
            }

            if ((NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost) && (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() != 2)
            {
                Plugin.LogWarning("Return B");
                return;
            }

            if (animationState != emoteStateHash)
            {
                Plugin.LogWarning("Return C: " + animationState);
                return;
            }

            // Let's do some fun logic. Why do I do this?
            if (!allEmoteControllers.TryGetValue(__instance.gameObject, out var emoteController))
            {
                Debug.Assert(false);
                return;
            }

            int clientId = Mathf.RoundToInt(animationSpeed * 10000 % 1 * 100) - 1;
            if (clientId >= 0 && SessionManager.TryGetPlayerByClientId((ulong)clientId, out var syncWithPlayer) && allEmoteControllers.TryGetValue(syncWithPlayer.gameObject, out var syncWithEmoteController))
            {
                Plugin.Log("Player syncing emote with another player. Player id syncing emote: " + __instance.actualClientId + " Syncing with player id: " + clientId);
                emoteController.SyncWithEmoteController(syncWithEmoteController);
                return;
            }

            int emoteId = Mathf.RoundToInt(animationSpeed * 10 % 1 * 1000) - 1;
            animationSpeed = 1;
            if (emoteId >= 0 && emoteId < EmotesManager.allUnlockableEmotes.Count)
            {
                Plugin.LogWarning("Update Perfroming emoteId: " + emoteId);
                var emote = EmotesManager.allUnlockableEmotes[emoteId];
                if (emote == null)
                    Plugin.LogWarning("EMOTE NULL");
                else
                    Plugin.LogWarning("Emote not null");
                emoteController.PerformEmote(emote);
            }
            else
            {
                Plugin.LogWarning("OnUpdatePlayerAnimation. Stopping emote.");
                allEmoteControllers[__instance.gameObject].StopPerformingEmote();
            }
        }
        */
    }
}