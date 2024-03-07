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
using TooManyEmotes.Compatibility;
using TooManyEmotes.UI;

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    public static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        public static InputAction OpenEmoteMenuAction;
        public static InputAction PerformSelectedEmoteAction;
        public static InputAction RotatePlayerEmoteAction;
        public static InputAction FavoriteEmoteAction;
        public static InputAction ThumbStickAction;
        public static InputAction PrevEmotePageAction;
        public static InputAction NextEmotePageAction;
        public static InputAction NextEmoteLoadoutUpAction;
        public static InputAction NextEmoteLoadoutDownAction;
        public static InputAction ForceReloadPlayerModelAction;

        /*
        public static InputAction QuickEmoteFavorite1Action;
        public static InputAction QuickEmoteFavorite2Action;
        public static InputAction QuickEmoteFavorite3Action;
        public static InputAction QuickEmoteFavorite4Action;
        public static InputAction QuickEmoteFavorite5Action;
        public static InputAction QuickEmoteFavorite6Action;
        public static InputAction QuickEmoteFavorite7Action;
        public static InputAction QuickEmoteFavorite8Action;
        */

        public static InputAction RawScrollAction;

        public static bool holdingRotatePlayerModifier = false;
        public static bool toggledRotating = false;


        public static void InitKeybinds()
        {
            Plugin.Log("Initializing custom emote hotkeys.");

            if (InputUtils_Compat.Enabled)
            {
                Asset = InputUtils_Compat.Asset;
                ActionMap = Asset.actionMaps[0];

                OpenEmoteMenuAction = InputUtils_Compat.OpenEmoteMenuHotkey;
                RotatePlayerEmoteAction = InputUtils_Compat.RotateCharacterEmoteHotkey;
                FavoriteEmoteAction = InputUtils_Compat.FavoriteEmoteHotkey;

                PrevEmotePageAction = InputUtils_Compat.PrevEmotePageHotkey;
                NextEmotePageAction = InputUtils_Compat.NextEmotePageHotkey;

                NextEmoteLoadoutUpAction = InputUtils_Compat.NextEmoteLoadoutUpHotkey;
                NextEmoteLoadoutDownAction = InputUtils_Compat.NextEmoteLoadoutDownHotkey;

                ForceReloadPlayerModelAction = InputUtils_Compat.ForceReloadPlayerModelHotkey;

                ThumbStickAction = new InputAction("TooManyEmotes.ThumbStick", binding: "<Gamepad>/rightStick");
                PerformSelectedEmoteAction = new InputAction("TooManyEmotes.PerformSelectedEmote", binding: "<Mouse>/leftButton", interactions: "Press");
                RawScrollAction = new InputAction("TooManyEmotes.ScrollEmoteMenu", binding: "<Mouse>/scroll");
            }
            else
            {
                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                ActionMap = new InputActionMap("TooManyEmotes");
                Asset.AddActionMap(ActionMap);

                OpenEmoteMenuAction = ActionMap.AddAction("TooManyEmotes.OpenEmoteMenu", binding: ConfigSettings.openEmoteMenuKeybind.Value, interactions: "Press");
                OpenEmoteMenuAction.AddBinding("<Gamepad>/leftStickPress");
                RotatePlayerEmoteAction = ActionMap.AddAction("TooManyEmotes.RotatePlayerEmote", binding: ConfigSettings.rotateCharacterInEmoteKeybind.Value, interactions: "Press");
                RotatePlayerEmoteAction.AddBinding("<Gamepad>/");
                FavoriteEmoteAction = ActionMap.AddAction("TooManyEmotes.FavoriteEmote", binding: "<Mouse>/middleButton", interactions: "Press");
                FavoriteEmoteAction.AddBinding("<Gamepad>/rightStickPress");

                PrevEmotePageAction = ActionMap.AddAction("TooManyEmotes.PrevEmotePage", binding: "<Keyboard>/", interactions: "Press");
                PrevEmotePageAction.AddBinding("<Gamepad>/dpad/left");
                NextEmotePageAction = ActionMap.AddAction("TooManyEmotes.NextEmotePage", binding: "<Keyboard>/", interactions: "Press");
                NextEmotePageAction.AddBinding("<Gamepad>/dpad/right");

                NextEmoteLoadoutUpAction = ActionMap.AddAction("TooManyEmotes.EmoteLoadoutUp", binding: "<Keyboard>/", interactions: "Press");
                NextEmoteLoadoutUpAction.AddBinding("<Gamepad>/dpad/up");
                NextEmoteLoadoutDownAction = ActionMap.AddAction("TooManyEmotes.EmoteLoadoutDown", binding: "<Keyboard>/", interactions: "Press");
                NextEmoteLoadoutDownAction.AddBinding("<Gamepad>/dpad/down");

                ForceReloadPlayerModelAction = ActionMap.AddAction("TooManyEmotes.ForceReloadPlayerModel", binding: "<Keyboard>/", interactions: "Press");
                ForceReloadPlayerModelAction.AddBinding("<Gamepad>/");

                ThumbStickAction = new InputAction("TooManyEmotes.ThumbStick", binding: "<Gamepad>/rightStick");
                PerformSelectedEmoteAction = new InputAction("TooManyEmotes.PerformSelectedEmote", binding: "<Mouse>/leftButton", interactions: "Press");
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

            PrevEmotePageAction.Enable();
            NextEmotePageAction.Enable();
            NextEmoteLoadoutUpAction.Enable();
            NextEmoteLoadoutDownAction.Enable();

            PrevEmotePageAction.performed += OnPrevEmotePage;
            NextEmotePageAction.performed += OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed += OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed += OnEmoteLoadoutDown;

            ForceReloadPlayerModelAction.performed += OnReloadPlayerModel;

            PerformSelectedEmoteAction.Enable();
            PerformSelectedEmoteAction.performed += OnSelectEmoteUI;

            ThumbStickAction.Enable();
            ThumbStickAction.performed += EmoteMenuManager.OnUpdateThumbStickAngle;
            RawScrollAction.Enable();
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

            PrevEmotePageAction.Disable();
            NextEmotePageAction.Disable();
            NextEmoteLoadoutUpAction.Disable();
            NextEmoteLoadoutDownAction.Disable();

            PrevEmotePageAction.performed -= OnPrevEmotePage;
            NextEmotePageAction.performed -= OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed -= OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed -= OnEmoteLoadoutDown;

            ForceReloadPlayerModelAction.performed -= OnReloadPlayerModel;

            PerformSelectedEmoteAction.Disable();
            PerformSelectedEmoteAction.performed -= OnSelectEmoteUI;

            ThumbStickAction.Disable();
            ThumbStickAction.performed -= EmoteMenuManager.OnUpdateThumbStickAngle;
            RawScrollAction.Disable();
        }


        public static void OnPrevEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenuManager.isMenuOpen || !context.performed)
                return;
            EmoteMenuManager.SwapPrevPage();
        }


        public static void OnNextEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenuManager.isMenuOpen || !context.performed)
                return;
            EmoteMenuManager.SwapNextPage();
        }

        public static void OnEmoteLoadoutUp(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenuManager.isMenuOpen || !context.performed)
                return;

            EmoteMenuManager.SetCurrentEmoteLoadout(EmoteMenuManager.currentLoadoutIndex != 0 ? EmoteMenuManager.currentLoadoutIndex - 1 : EmoteMenuManager.numLoadouts - 1);
        }

        public static void OnEmoteLoadoutDown(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenuManager.isMenuOpen || !context.performed)
                return;

            EmoteMenuManager.SetCurrentEmoteLoadout((EmoteMenuManager.currentLoadoutIndex + 1) % EmoteMenuManager.numLoadouts);
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
                if (ConfigSettings.toggleEmoteMenu.Value || StartOfRound.Instance.localPlayerUsingController)
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
            if (EmoteMenuManager.hoveredEmoteIndex < 0 || EmoteMenuManager.hoveredEmoteIndex >= EmoteMenuManager.currentLoadoutEmotesList.Count || ConfigSettings.disableEmotesForSelf.Value || EmoteControllerPlayer.emoteControllerLocal == null)
                return;
            UnlockableEmote emote = EmoteMenuManager.currentLoadoutEmotesList[EmoteMenuManager.hoveredEmoteIndex];
            if (emote != null)
                EmoteControllerPlayer.emoteControllerLocal.TryPerformingEmoteLocal(emote);
        }


        public static void OnFavoriteEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenuManager.isMenuOpen || EmoteMenuManager.hoveredEmoteUIIndex == -1 || EmoteMenuManager.previewingEmote == null)
                return;

            EmoteMenuManager.ToggleFavoriteHoveredEmote();
        }


        public static void OnQuickEmote(InputAction.CallbackContext context, int quickEmoteNumber)
        {
            /*
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
            */
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


        public static void OnReloadPlayerModel(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !context.performed)
                return;
            Plugin.Log("Reloading local player model.");
            ThirdPersonEmoteController.ReloadPlayerModel(localPlayerController);
        }
    }
}
