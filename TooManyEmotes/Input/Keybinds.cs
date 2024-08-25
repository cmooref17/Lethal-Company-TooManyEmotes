using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using TooManyEmotes.Networking;
using TooManyEmotes.Compatibility;
using TooManyEmotes.UI;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    internal static class Keybinds
    {
        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        public static InputAction OpenEmoteMenuAction;
        public static InputAction PerformSelectedEmoteAction;
        public static InputAction RotatePlayerEmoteAction;
        public static InputAction ZoomOutEmoteAction;
        public static InputAction ZoomInEmoteAction;
        public static InputAction FavoriteEmoteAction;
        public static InputAction ThumbStickAction;
        public static InputAction PrevEmotePageAction;
        public static InputAction NextEmotePageAction;
        public static InputAction NextEmoteLoadoutUpAction;
        public static InputAction NextEmoteLoadoutDownAction;
        public static InputAction PerformNextInstrumentAction;
        //public static InputAction ForceReloadPlayerModelAction;

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
            Log("Initializing custom emote hotkeys.");

            if (InputUtils_Compat.Enabled)
            {
                Asset = InputUtils_Compat.Asset;
                ActionMap = Asset.actionMaps[0];

                OpenEmoteMenuAction = InputUtils_Compat.OpenEmoteMenuHotkey;
                FavoriteEmoteAction = InputUtils_Compat.FavoriteEmoteHotkey;

                PrevEmotePageAction = InputUtils_Compat.PrevEmotePageHotkey;
                NextEmotePageAction = InputUtils_Compat.NextEmotePageHotkey;

                NextEmoteLoadoutUpAction = InputUtils_Compat.NextEmoteLoadoutUpHotkey;
                NextEmoteLoadoutDownAction = InputUtils_Compat.NextEmoteLoadoutDownHotkey;

                RotatePlayerEmoteAction = InputUtils_Compat.RotateCharacterEmoteHotkey;
                ZoomInEmoteAction = InputUtils_Compat.ZoomInEmoteHotkey;
                ZoomOutEmoteAction = InputUtils_Compat.ZoomOutEmoteHotkey;
                
                PerformNextInstrumentAction = InputUtils_Compat.PerformNextInstrumentHotkey;

                //ForceReloadPlayerModelAction = InputUtils_Compat.ForceReloadPlayerModelHotkey;

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

                RotatePlayerEmoteAction = ActionMap.AddAction("TooManyEmotes.RotatePlayerEmote", binding: "<Keyboard>/leftAlt", interactions: "Press");
                RotatePlayerEmoteAction.AddBinding("<Gamepad>/");
                ZoomInEmoteAction = ActionMap.AddAction("TooManyEmotes.ZoomInEmote", binding: "<Mouse>/scroll/up", interactions: "Press");
                ZoomInEmoteAction.AddBinding("<Gamepad>/");
                ZoomOutEmoteAction = ActionMap.AddAction("TooManyEmotes.ZoomOutEmote", binding: "<Mouse>/scroll/down", interactions: "Press");
                ZoomOutEmoteAction.AddBinding("<Gamepad>/");
                
                PerformNextInstrumentAction = ActionMap.AddAction("TooManyEmotes.PlayNextInstrument", binding: "<Keyboard>/n", interactions: "Press");
                PerformNextInstrumentAction.AddBinding("<Gamepad>/dpad/right");

                //ForceReloadPlayerModelAction = ActionMap.AddAction("TooManyEmotes.ForceReloadPlayerModel", binding: "<Keyboard>/", interactions: "Press");
                //ForceReloadPlayerModelAction.AddBinding("<Gamepad>/");

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

            PrevEmotePageAction.Enable();
            NextEmotePageAction.Enable();
            NextEmoteLoadoutUpAction.Enable();
            NextEmoteLoadoutDownAction.Enable();

            PrevEmotePageAction.performed += OnPrevEmotePage;
            NextEmotePageAction.performed += OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed += OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed += OnEmoteLoadoutDown;

            RotatePlayerEmoteAction.performed += OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled += OnUpdateRotatePlayerEmoteModifier;
            ZoomInEmoteAction.performed += ThirdPersonEmoteController.OnZoomInEmote;
            ZoomOutEmoteAction.performed += ThirdPersonEmoteController.OnZoomOutEmote;

            PerformNextInstrumentAction.performed += PlayNextInstrument;
            //ForceReloadPlayerModelAction.performed += OnReloadPlayerModel;

            PerformSelectedEmoteAction.Enable();
            PerformSelectedEmoteAction.performed += OnSelectEmoteUI;

            ThumbStickAction.Enable();
            ThumbStickAction.performed += EmoteMenu.OnUpdateThumbStickAngle;
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

            PrevEmotePageAction.Disable();
            NextEmotePageAction.Disable();
            NextEmoteLoadoutUpAction.Disable();
            NextEmoteLoadoutDownAction.Disable();

            PrevEmotePageAction.performed -= OnPrevEmotePage;
            NextEmotePageAction.performed -= OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed -= OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed -= OnEmoteLoadoutDown;

            RotatePlayerEmoteAction.performed -= OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled -= OnUpdateRotatePlayerEmoteModifier;
            ZoomInEmoteAction.performed -= ThirdPersonEmoteController.OnZoomInEmote;
            ZoomOutEmoteAction.performed -= ThirdPersonEmoteController.OnZoomOutEmote;

            PerformNextInstrumentAction.performed -= PlayNextInstrument;
            //ForceReloadPlayerModelAction.performed -= OnReloadPlayerModel;

            PerformSelectedEmoteAction.Disable();
            PerformSelectedEmoteAction.performed -= OnSelectEmoteUI;

            ThumbStickAction.Disable();
            ThumbStickAction.performed -= EmoteMenu.OnUpdateThumbStickAngle;
            RawScrollAction.Disable();
        }


        public static void OnPrevEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenu.isMenuOpen || !context.performed)
                return;
            EmoteMenu.SwapPrevPage();
        }


        public static void OnNextEmotePage(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenu.isMenuOpen || !context.performed)
                return;
            EmoteMenu.SwapNextPage();
        }

        public static void OnEmoteLoadoutUp(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenu.isMenuOpen || !context.performed)
                return;

            EmoteMenu.SetCurrentEmoteLoadout(EmoteMenu.currentLoadoutIndex != 0 ? EmoteMenu.currentLoadoutIndex - 1 : EmoteMenu.numLoadouts - 1);
        }

        public static void OnEmoteLoadoutDown(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || !EmoteMenu.isMenuOpen || !context.performed)
                return;

            EmoteMenu.SetCurrentEmoteLoadout((EmoteMenu.currentLoadoutIndex + 1) % EmoteMenu.numLoadouts);
        }


        static void OnPressOpenEmoteMenu(InputAction.CallbackContext context)
        {
            //Log("Starting opening emote menu...");
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (!EmoteMenu.isMenuOpen)
            {
                if (context.performed && EmoteMenu.CanOpenEmoteMenu())
                    EmoteMenu.OpenEmoteMenu();
            }
            else
            {
                if (ConfigSettings.toggleEmoteMenu.Value || StartOfRound.Instance.localPlayerUsingController)
                {
                    if (context.performed)
                        EmoteMenu.CloseEmoteMenu();
                }
                else if (context.canceled)
                {
                    if (EmoteMenu.hoveredEmoteIndex != -1)
                        PerformEmoteLocal(context);
                    EmoteMenu.CloseEmoteMenu();
                }
            }
        }


        static void OnSelectEmoteUI(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenu.isMenuOpen)
                return;

            if (EmoteMenu.hoveredLoadoutUIIndex != -1 && EmoteMenu.hoveredLoadoutUIIndex != EmoteMenu.currentLoadoutIndex)
                EmoteMenu.SetCurrentEmoteLoadout(EmoteMenu.hoveredLoadoutUIIndex);
            else if (EmoteMenu.hoveredEmoteIndex >= 0 && EmoteMenu.hoveredEmoteIndex < EmoteMenu.currentLoadoutEmotesList.Count)
            {
                PerformEmoteLocal(context);
                EmoteMenu.CloseEmoteMenu();
            }
        }


        public static void PerformEmoteLocal(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;
            if (EmoteMenu.hoveredEmoteIndex < 0 || EmoteMenu.hoveredEmoteIndex >= EmoteMenu.currentLoadoutEmotesList.Count || EmoteControllerPlayer.emoteControllerLocal == null)
                return;

            UnlockableEmote emote = EmoteMenu.currentLoadoutEmotesList[EmoteMenu.hoveredEmoteIndex];
            if (emote != null)
            {
                /*if (emoteControllerLocal.isPerformingEmote && emoteControllerLocal.performingEmote.inEmoteSyncGroup && emoteControllerLocal.performingEmote.IsEmoteInEmoteGroup(emote))
                    emoteControllerLocal.TrySyncingEmoteWithEmoteController(emoteControllerLocal);
                else*/
                emoteControllerLocal.TryPerformingEmoteLocal(emote);
            }
        }


        public static void OnFavoriteEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (!EmoteMenu.isMenuOpen || EmoteMenu.hoveredEmoteUIIndex == -1 || EmoteMenu.previewingEmote == null)
                return;

            EmoteMenu.ToggleFavoriteHoveredEmote();
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
            //if (localPlayerController == null || ConfigSync.instance.syncEnableMovingWhileEmoting || !emoteControllerLocal.IsPerformingCustomEmote())
            if (localPlayerController == null || !emoteControllerLocal.IsPerformingCustomEmote() || ThirdPersonEmoteController.firstPersonEmotesEnabled)
                return;
            
            if (context.performed)
            {
                if (ConfigSettings.toggleRotateCharacterInEmote.Value)
                {
                    toggledRotating = !toggledRotating;
                    holdingRotatePlayerModifier = false;
                }
                else
                {
                    holdingRotatePlayerModifier = true;
                    toggledRotating = false;
                }
            }
            else if (context.canceled && !ConfigSettings.toggleRotateCharacterInEmote.Value)
                holdingRotatePlayerModifier = false;
        }

        public static void PlayNextInstrument(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (emoteControllerLocal.isPerformingEmote && emoteControllerLocal.performingEmote.inEmoteSyncGroup && emoteControllerLocal.performingEmote.emoteSyncGroup.Count > 1)
            {
                emoteControllerLocal.TryPerformingEmoteLocal(emoteControllerLocal.performingEmote);
                //emoteControllerLocal.TrySyncingEmoteWithEmoteController(emoteControllerLocal);
            }
        }

        /*public static void OnReloadPlayerModel(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !context.performed || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            Log("Reloading local player model.");
            ThirdPersonEmoteController.ReloadPlayerModel(localPlayerController);
        }*/
    }
}
