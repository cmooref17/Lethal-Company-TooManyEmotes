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
    public static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        static InputAction OpenEmoteMenuAction;
        static InputAction SelectEmoteUIAction;
        static InputAction NextEmotePageAction;
        static InputAction PrevEmotePageAction;
        static InputAction FavoriteEmoteAction;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializeHotkeys(PlayerControllerB __instance)
        {
            Plugin.Log("Initializing custom emote hotkeys.");

            OpenEmoteMenuAction = new InputAction(binding: ConfigSettings.openEmoteMenuKeybind.Value, interactions: "Press");
            SelectEmoteUIAction = new InputAction(binding: "<Mouse>/leftButton", interactions: "Press");
            PrevEmotePageAction = new InputAction(binding: "<Keyboard>/q", interactions: "Press");
            NextEmotePageAction = new InputAction(binding: "<Keyboard>/e", interactions: "Press");
            FavoriteEmoteAction = new InputAction(binding: "<Mouse>/middleButton", interactions: "Press");

            if (__instance.gameObject.activeSelf)
                SubscribeToEvents();
        }


        static void SubscribeToEvents()
        {
            Plugin.Log("Subscribing to OnPressCustomEmoteKey events");

            OpenEmoteMenuAction.performed += OnPressOpenEmoteMenu;
            OpenEmoteMenuAction.canceled += OnPressOpenEmoteMenu;
            SelectEmoteUIAction.performed += OnSelectEmoteUI;
            PrevEmotePageAction.performed += OnSwapPrevEmotePage;
            NextEmotePageAction.performed += OnSwapNextEmotePage;
            FavoriteEmoteAction.performed += OnFavoriteEmote;

            OpenEmoteMenuAction.Enable();
            SelectEmoteUIAction.Enable();
            PrevEmotePageAction.Enable();
            NextEmotePageAction.Enable();
            FavoriteEmoteAction.Enable();
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
            OpenEmoteMenuAction.canceled -= OnPressOpenEmoteMenu;
            SelectEmoteUIAction.performed -= OnSelectEmoteUI;
            PrevEmotePageAction.performed -= OnSwapPrevEmotePage;
            NextEmotePageAction.performed -= OnSwapNextEmotePage;
            FavoriteEmoteAction.performed -= OnFavoriteEmote;

            OpenEmoteMenuAction.Disable();
            SelectEmoteUIAction.Disable();
            PrevEmotePageAction.Disable();
            NextEmotePageAction.Disable();
            FavoriteEmoteAction.Disable();
        }


        static void OnPressOpenEmoteMenu(InputAction.CallbackContext context)
        {
            //Plugin.Log("Starting opening emote menu...");
            if (localPlayerController == null)
                return;

            if (!EmoteMenuManager.isMenuOpen)
            {
                if (context.performed && EmoteMenuManager.CanOpenEmoteMenu())
                    EmoteMenuManager.OpenEmoteMenu();
            }
            else
            {
                if (ConfigSettings.toggleEmoteMenu.Value)
                {
                    if (context.performed)
                        EmoteMenuManager.CloseEmoteMenu();
                }
                else if (context.canceled)
                {
                    if (EmoteMenuManager.hoveredEmoteIndex != -1)
                        PerformEmoteLocal(context);
                    EmoteMenuManager.CloseEmoteMenu();
                }
            }
        }


        static void OnSelectEmoteUI(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenuManager.isMenuOpen)
                return;

            if (EmoteMenuManager.hoveredLoadoutUIIndex != -1 && EmoteMenuManager.hoveredLoadoutUIIndex != EmoteMenuManager.currentLoadoutIndex)
                EmoteMenuManager.SetCurrentEmoteLoadout(EmoteMenuManager.hoveredLoadoutUIIndex);
            else if (EmoteMenuManager.hoveredEmoteIndex >= 0 && EmoteMenuManager.hoveredEmoteIndex < EmoteMenuManager.currentLoadoutEmotesList.Count)
            {
                PerformEmoteLocal(context);
                EmoteMenuManager.CloseEmoteMenu();
            }
        }


        static void OnSwapPrevEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenuManager.isMenuOpen || EmoteMenuManager.numPages <= 1)
                return;

            EmoteMenuManager.SwapPrevPage();
        }


        static void OnSwapNextEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenuManager.isMenuOpen || EmoteMenuManager.numPages <= 1)
                return;

            EmoteMenuManager.SwapNextPage();
        }


        public static void PerformEmoteLocal(InputAction.CallbackContext context) {
            if (EmoteMenuManager.hoveredEmoteIndex < 0 || EmoteMenuManager.hoveredEmoteIndex >= EmoteMenuManager.currentLoadoutEmotesList.Count)
                return;
            UnlockableEmote emote = EmoteMenuManager.currentLoadoutEmotesList[EmoteMenuManager.hoveredEmoteIndex];
            if (emote != null)
                localPlayerController.PerformEmote(context, -(emote.emoteId + 1));
        }


        public static void OnFavoriteEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenuManager.isMenuOpen || EmoteMenuManager.hoveredEmoteUIIndex == -1 || EmoteMenuManager.previewingEmote == null)
                return;

            EmoteMenuManager.ToggleFavoriteHoveredEmote();
        }
    }
}
