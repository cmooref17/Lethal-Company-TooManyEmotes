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
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class PlayerPatcher
    {
        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        private static void InitializeEmoteController(PlayerControllerB __instance)
        {
            __instance.gameObject.AddComponent<EmoteControllerPlayer>();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void OnLocalClientReady(PlayerControllerB __instance)
        {
            Log("Initializing local player.");
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
        private static void OnPlayerDC(int playerObjectNumber, ulong clientId, StartOfRound __instance)
        {
            PlayerControllerB playerController = __instance.allPlayerObjects[playerObjectNumber].GetComponent<PlayerControllerB>();
            if (playerController != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerController, out var emoteController) && emoteController.IsPerformingCustomEmote())
                emoteController.StopPerformingEmote();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPrefix]
        private static void OnPlayerDeath(Vector3 bodyVelocity, PlayerControllerB __instance)
        {
            if (__instance != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
            {
                LogWarning("Player died while emoting. Heh... I mean, I hope this handles smoothly.");
                emoteController.StopPerformingEmote();
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "StopPerformingEmoteClientRpc")]
        [HarmonyPrefix]
        private static void OnStopPerformingEmote(PlayerControllerB __instance)
        {
            if (__instance == localPlayerController)
                return;
            if (EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
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
        private static void OnPerformDefaultEmote(InputAction.CallbackContext context, int emoteID)
        {
            if (context.performed && emoteControllerLocal.IsPerformingCustomEmote())
            {
                LogWarningVerbose("[PerformEmote] On perform vanilla emote on local player. Stopping custom emote.");
                emoteControllerLocal.StopPerformingEmote();
            }
        }
    }
}