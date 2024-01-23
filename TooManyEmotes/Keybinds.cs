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
using TooManyEmotes.Networking;

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    public static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        public static InputAction OpenEmoteMenuAction;
        public static InputAction RotatePlayerEmoteAction;
        public static InputAction FavoriteEmoteAction;
        public static InputAction QuickEmoteFavorite1Action;
        public static InputAction QuickEmoteFavorite2Action;
        public static InputAction QuickEmoteFavorite3Action;
        public static InputAction QuickEmoteFavorite4Action;
        public static InputAction QuickEmoteFavorite5Action;
        public static InputAction QuickEmoteFavorite6Action;
        public static InputAction QuickEmoteFavorite7Action;
        public static InputAction QuickEmoteFavorite8Action;

        public static InputAction SelectEmoteUIAction;
        public static InputAction RawScrollAction;

        public static bool holdingRotatePlayerModifier = false;
        public static bool toggledRotating = false;

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

                /*
                QuickEmoteFavorite1Action = InputUtilsCompat.QuickEmoteFavorite1;
                QuickEmoteFavorite2Action = InputUtilsCompat.QuickEmoteFavorite2;
                QuickEmoteFavorite3Action = InputUtilsCompat.QuickEmoteFavorite3;
                QuickEmoteFavorite4Action = InputUtilsCompat.QuickEmoteFavorite4;
                QuickEmoteFavorite5Action = InputUtilsCompat.QuickEmoteFavorite5;
                QuickEmoteFavorite6Action = InputUtilsCompat.QuickEmoteFavorite6;
                QuickEmoteFavorite7Action = InputUtilsCompat.QuickEmoteFavorite7;
                QuickEmoteFavorite8Action = InputUtilsCompat.QuickEmoteFavorite8;
                */

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

                /*
                QuickEmoteFavorite1Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite1Keybind.Value, interactions: "Press");
                QuickEmoteFavorite2Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite2Keybind.Value, interactions: "Press");
                QuickEmoteFavorite3Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite3Keybind.Value, interactions: "Press");
                QuickEmoteFavorite4Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite4Keybind.Value, interactions: "Press");
                QuickEmoteFavorite5Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite5Keybind.Value, interactions: "Press");
                QuickEmoteFavorite6Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite6Keybind.Value, interactions: "Press");
                QuickEmoteFavorite7Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite7Keybind.Value, interactions: "Press");
                QuickEmoteFavorite8Action = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: ConfigSettings.quickEmoteFavorite8Keybind.Value, interactions: "Press");
                */

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

            //QuickEmoteFavorite1Action.performed +=

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


        public static void PerformEmoteLocal(InputAction.CallbackContext context)
        {
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

        /*
        public static void OnQuickEmote1(InputAction.CallbackContext context) => OnQuickEmote(context, 0);
        public static void OnQuickEmote2(InputAction.CallbackContext context) => OnQuickEmote(context, 1);
        public static void OnQuickEmote3(InputAction.CallbackContext context) => OnQuickEmote(context, 2);
        public static void OnQuickEmote4(InputAction.CallbackContext context) => OnQuickEmote(context, 3);
        public static void OnQuickEmote5(InputAction.CallbackContext context) => OnQuickEmote(context, 4);
        public static void OnQuickEmote6(InputAction.CallbackContext context) => OnQuickEmote(context, 5);
        public static void OnQuickEmote7(InputAction.CallbackContext context) => OnQuickEmote(context, 6);
        public static void OnQuickEmote8(InputAction.CallbackContext context) => OnQuickEmote(context, 7);
        */


        public static void OnQuickEmote(InputAction.CallbackContext context, int quickEmoteNumber)
        {
            return;
            if (!context.performed || quickEmoteNumber >= StartOfRoundPatcher.unlockedFavoriteEmotes.Count)
                return;

            if (EmoteMenuManager.isMenuOpen)
            {
                if (EmoteMenuManager.hoveredEmoteIndex >= 0 && EmoteMenuManager.hoveredEmoteIndex != quickEmoteNumber && EmoteMenuManager.previewingEmote != null)
                {
                    var emote = EmoteMenuManager.previewingEmote;
                    // Favorite an emote
                    int indexInAllFavoritedEmotes = StartOfRoundPatcher.allFavoriteEmotes.IndexOf(emote.emoteName);
                    if (indexInAllFavoritedEmotes >= 0) 
                    {

                    }
                }
            }
            else
            {
                // Perform favorited emote
                var emote = StartOfRoundPatcher.unlockedFavoriteEmotes[quickEmoteNumber];
                if (emote != null)
                    localPlayerController.PerformEmote(context, -(emote.emoteId + 1));
            }
        }

        public static void OnUpdateRotatePlayerEmoteModifier(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || ConfigSync.instance.syncEnableMovingWhileEmoting)
                return;
            
            if (context.performed)
            {
                if (ConfigSettings.toggleRotateCharacterInEmote.Value)
                    toggledRotating = !toggledRotating;
                else
                    holdingRotatePlayerModifier = true;
            }
            else if (context.canceled && !ConfigSettings.toggleRotateCharacterInEmote.Value)
                holdingRotatePlayerModifier = false;
        }
    }
}
