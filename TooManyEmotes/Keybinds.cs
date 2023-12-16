using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;

namespace TooManyEmotes
{
    [HarmonyPatch]
    internal class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        static InputAction OpenEmoteMenuAction;
        static InputAction SelectEmoteUIAction;

        //static int maxEmotes = 10;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializeHotkeys(PlayerControllerB __instance)
        {
            Plugin.Log("Initializing custom emote hotkeys.");

            OpenEmoteMenuAction = new InputAction(binding: ConfigSettings.openEmoteMenuKeybind.Value, interactions: "Press");
            SelectEmoteUIAction = new InputAction(binding: "<Mouse>/leftButton", interactions: "Press");

            if (__instance.gameObject.activeSelf)
                SubscribeToEvents();
        }


        static void SubscribeToEvents()
        {
            Plugin.Log("Subscribing to OnPressCustomEmoteKey events");

            OpenEmoteMenuAction.performed += OnPressOpenEmoteMenu;
            OpenEmoteMenuAction.Enable();

            SelectEmoteUIAction.performed += OnSelectEmoteUI;
            SelectEmoteUIAction.Enable();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController) return;
            SubscribeToEvents();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "OnDisable")]
        [HarmonyPostfix]
        public static void OnDisable(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController) return;
            Plugin.Log("Unsubscribing from OnPressCustomEmoteKey events.");

            OpenEmoteMenuAction.performed -= OnPressOpenEmoteMenu;
            OpenEmoteMenuAction.Disable();

            SelectEmoteUIAction.performed -= OnSelectEmoteUI;
            SelectEmoteUIAction.Disable();
        }


        static void OnPressOpenEmoteMenu(InputAction.CallbackContext context)
        {
            Plugin.Log("Starting opening emote menu...");
            if (localPlayerController == null || !context.performed)
                return;

            if (EmoteMenuManager.CanOpenEmoteMenu())
                EmoteMenuManager.ToggleEmoteMenu();
        }


        static void OnSelectEmoteUI(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;

            if (EmoteMenuManager.isMenuOpen && EmoteMenuManager.hoveredEmoteIndex >= 0 && EmoteMenuManager.hoveredEmoteIndex < StartOfRoundPatcher.currentEmoteLoadout.Length)
            {
                UnlockableEmote emote = StartOfRoundPatcher.currentEmoteLoadout[EmoteMenuManager.hoveredEmoteIndex];
                if (emote != null)
                    localPlayerController.PerformEmote(context, -(EmoteMenuManager.hoveredEmoteIndex + 1));
                EmoteMenuManager.CloseEmoteMenu();
            }
        }
    }
}
