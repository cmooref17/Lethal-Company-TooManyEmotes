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
            if (playerController != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController) && emoteController.IsPerformingCustomEmote())
                emoteController.StopPerformingEmoteImmediately();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPrefix]
        public static void OnPlayerDeath(Vector3 bodyVelocity, PlayerControllerB __instance)
        {
            if (__instance != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
            {
                Plugin.LogWarning("Player died while emoting. Heh... I mean, I hope this handles smoothly.");
                emoteController.StopPerformingEmoteImmediately();
            }
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
    }
}