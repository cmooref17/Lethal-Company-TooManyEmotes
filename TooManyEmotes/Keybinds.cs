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

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    public static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;
        static InputAction OpenEmoteMenuAction;
        static InputAction RotatePlayerEmoteAction;
        static InputAction FavoriteEmoteAction;

        static InputAction SelectEmoteUIAction;
        static InputAction NextEmotePageAction;
        static InputAction PrevEmotePageAction;
        public static InputAction RawScrollAction;

        public static bool holdingRotatePlayerModifier = false;

        public static void InitKeybinds()
        {
            Plugin.Log("Initializing custom emote hotkeys.");

            bool inputUtilsLoaded = InputUtilsCompat.Enabled;
            Plugin.Log("InputUtilsLoaded: " + inputUtilsLoaded);
            if (InputUtilsCompat.Enabled)
            {
                Asset = InputUtilsCompat.Asset;
                ActionMap = Asset.actionMaps[0];

                OpenEmoteMenuAction = InputUtilsCompat.OpenEmoteMenuHotkey;
                RotatePlayerEmoteAction = InputUtilsCompat.RotateCharacterEmoteHotkey;
                FavoriteEmoteAction = InputUtilsCompat.FavoriteEmoteHotkey;

                SelectEmoteUIAction = new InputAction("TooManyEmotes.SelectEmote", binding: "<Mouse>/leftButton", interactions: "Press");
                RawScrollAction = new InputAction("TooManyEmotes.ScrollEmoteMenu", binding: "<Mouse>/scroll");
            }
            else
            {
                Asset = new InputActionAsset();
                ActionMap = new InputActionMap("TooManyEmotes");
                Asset.AddActionMap(ActionMap);

                OpenEmoteMenuAction = ActionMap.AddAction("TooManyEmotes.OpenEmoteMenu", binding: ConfigSettings.openEmoteMenuKeybind.Value, interactions: "Press");
                RotatePlayerEmoteAction = ActionMap.AddAction("TooManyEmotes.RotatePlayerEmote", binding: ConfigSettings.rotateCharacterInEmoteKeybind.Value, interactions: "Press");
                FavoriteEmoteAction = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: "<Mouse>/middleButton", interactions: "Press");
                SelectEmoteUIAction = new InputAction("TooManyEmotes.SelectEmote", binding: "<Mouse>/leftButton", interactions: "Press");
                RawScrollAction = new InputAction("TooManyEmotes.ScrollEmoteMenu", binding: "<Mouse>/scroll");
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable()
        {
            Asset.Enable();
            OpenEmoteMenuAction.performed += OnPressOpenEmoteMenu;
            OpenEmoteMenuAction.canceled += OnPressOpenEmoteMenu;
            FavoriteEmoteAction.performed += OnFavoriteEmote;
            RotatePlayerEmoteAction.performed += OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled += OnUpdateRotatePlayerEmoteModifier;

            SelectEmoteUIAction.Enable();
            SelectEmoteUIAction.performed += OnSelectEmoteUI;
        }


        [HarmonyPatch(typeof(StartOfRound), "OnDisable")]
        [HarmonyPostfix]
        public static void OnDisable()
        {
            Asset.Disable();
            OpenEmoteMenuAction.performed -= OnPressOpenEmoteMenu;
            OpenEmoteMenuAction.canceled -= OnPressOpenEmoteMenu;
            FavoriteEmoteAction.performed -= OnFavoriteEmote;
            RotatePlayerEmoteAction.performed -= OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled -= OnUpdateRotatePlayerEmoteModifier;

            SelectEmoteUIAction.Disable();
            SelectEmoteUIAction.performed -= OnSelectEmoteUI;
        }


        static void OnPressOpenEmoteMenu(InputAction.CallbackContext context)
        {
            //Plugin.Log("Starting opening emote menu...");
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value)
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
            if (EmoteMenuManager.hoveredEmoteIndex < 0 || EmoteMenuManager.hoveredEmoteIndex >= EmoteMenuManager.currentLoadoutEmotesList.Count || ConfigSettings.disableEmotesForSelf.Value)
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


        public static void OnUpdateRotatePlayerEmoteModifier(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value)
                return;
            
            if (context.performed)
                holdingRotatePlayerModifier = true;
            else if (context.canceled)
                holdingRotatePlayerModifier = false;
        }
    }
}
