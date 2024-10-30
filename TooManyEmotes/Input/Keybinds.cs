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
using System;
using System.Linq;
using System.Reflection;

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    internal static class Keybinds
    {
        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        public static InputAction OpenEmoteMenuAction;
        public static InputAction RotatePlayerEmoteAction;
        public static InputAction ZoomOutEmoteAction;
        public static InputAction ZoomInEmoteAction;
        public static InputAction FavoriteEmoteAction;
        public static InputAction PrevEmotePageAction;
        public static InputAction NextEmotePageAction;
        public static InputAction NextEmoteLoadoutUpAction;
        public static InputAction NextEmoteLoadoutDownAction;
        public static InputAction PerformNextInstrumentAction;
        public static InputAction PerformRandomEmoteAction;

        /*
        public static InputAction QuickEmote1Action;
        public static InputAction QuickEmote2Action;
        public static InputAction QuickEmote3Action;
        public static InputAction QuickEmote4Action;
        public static InputAction QuickEmote5Action;
        public static InputAction QuickEmote6Action;
        public static InputAction QuickEmote7Action;
        public static InputAction QuickEmote8Action;
        */

        public static InputAction PerformSelectedEmoteAction;
        public static InputAction ThumbStickAction;
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

                PerformRandomEmoteAction = InputUtils_Compat.PerformRandomEmoteHotkey;

                /*
                QuickEmote1Action = InputUtils_Compat.QuickEmote1;
                QuickEmote2Action = InputUtils_Compat.QuickEmote2;
                QuickEmote3Action = InputUtils_Compat.QuickEmote3;
                QuickEmote4Action = InputUtils_Compat.QuickEmote4;
                QuickEmote5Action = InputUtils_Compat.QuickEmote5;
                QuickEmote6Action = InputUtils_Compat.QuickEmote6;
                QuickEmote7Action = InputUtils_Compat.QuickEmote7;
                QuickEmote8Action = InputUtils_Compat.QuickEmote8;
                */

                PerformSelectedEmoteAction = new InputAction("TooManyEmotes.PerformSelectedEmote", binding: "<Mouse>/leftButton", interactions: "Press");
                ThumbStickAction = new InputAction("TooManyEmotes.ThumbStick", binding: "<Gamepad>/rightStick");
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

                PrevEmotePageAction = ActionMap.AddAction("TooManyEmotes.PrevEmotePage", binding: "<Keyboard>/ ", interactions: "Press");
                PrevEmotePageAction.AddBinding("<Gamepad>/dpad/left");
                NextEmotePageAction = ActionMap.AddAction("TooManyEmotes.NextEmotePage", binding: "<Keyboard>/ ", interactions: "Press");
                NextEmotePageAction.AddBinding("<Gamepad>/dpad/right");

                NextEmoteLoadoutUpAction = ActionMap.AddAction("TooManyEmotes.EmoteLoadoutUp", binding: "<Keyboard>/ ", interactions: "Press");
                NextEmoteLoadoutUpAction.AddBinding("<Gamepad>/dpad/up");
                NextEmoteLoadoutDownAction = ActionMap.AddAction("TooManyEmotes.EmoteLoadoutDown", binding: "<Keyboard>/ ", interactions: "Press");
                NextEmoteLoadoutDownAction.AddBinding("<Gamepad>/dpad/down");

                RotatePlayerEmoteAction = ActionMap.AddAction("TooManyEmotes.RotatePlayerEmote", binding: "<Keyboard>/leftAlt", interactions: "Press");
                RotatePlayerEmoteAction.AddBinding("<Gamepad>/ ");
                ZoomInEmoteAction = ActionMap.AddAction("TooManyEmotes.ZoomInEmote", binding: "<Mouse>/scroll/up", interactions: "Press");
                ZoomInEmoteAction.AddBinding("<Gamepad>/ ");
                ZoomOutEmoteAction = ActionMap.AddAction("TooManyEmotes.ZoomOutEmote", binding: "<Mouse>/scroll/down", interactions: "Press");
                ZoomOutEmoteAction.AddBinding("<Gamepad>/ ");
                
                PerformNextInstrumentAction = ActionMap.AddAction("TooManyEmotes.PlayNextInstrument", binding: "<Keyboard>/n", interactions: "Press");
                PerformNextInstrumentAction.AddBinding("<Gamepad>/dpad/right");

                PerformRandomEmoteAction = ActionMap.AddAction("TooManyEmotes.PerformRandomEmote", binding: "<Keyboard>/m", interactions: "Press");
                PerformRandomEmoteAction.AddBinding("<Gamepad>/ ");

                PerformSelectedEmoteAction = new InputAction("TooManyEmotes.PerformSelectedEmote", binding: "<Mouse>/leftButton", interactions: "Press");
                ThumbStickAction = new InputAction("TooManyEmotes.ThumbStick", binding: "<Gamepad>/rightStick");
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

            PrevEmotePageAction.performed += OnPrevEmotePage;
            NextEmotePageAction.performed += OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed += OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed += OnEmoteLoadoutDown;

            RotatePlayerEmoteAction.performed += OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled += OnUpdateRotatePlayerEmoteModifier;
            ZoomInEmoteAction.performed += ThirdPersonEmoteController.OnZoomInEmote;
            ZoomOutEmoteAction.performed += ThirdPersonEmoteController.OnZoomOutEmote;

            PerformNextInstrumentAction.performed += PlayNextInstrument;
            PerformRandomEmoteAction.performed += PerformRandomEmote;

            // For now
            if (InputUtils_Compat.Enabled)
            {
                /*
                QuickEmote1Action.performed += OnQuickEmote1;
                QuickEmote2Action.performed += OnQuickEmote2;
                QuickEmote3Action.performed += OnQuickEmote3;
                QuickEmote4Action.performed += OnQuickEmote4;
                QuickEmote5Action.performed += OnQuickEmote5;
                QuickEmote6Action.performed += OnQuickEmote6;
                QuickEmote7Action.performed += OnQuickEmote7;
                QuickEmote8Action.performed += OnQuickEmote8;
                */
            }

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

            PrevEmotePageAction.performed -= OnPrevEmotePage;
            NextEmotePageAction.performed -= OnNextEmotePage;
            NextEmoteLoadoutUpAction.performed -= OnEmoteLoadoutUp;
            NextEmoteLoadoutDownAction.performed -= OnEmoteLoadoutDown;

            RotatePlayerEmoteAction.performed -= OnUpdateRotatePlayerEmoteModifier;
            RotatePlayerEmoteAction.canceled -= OnUpdateRotatePlayerEmoteModifier;
            ZoomInEmoteAction.performed -= ThirdPersonEmoteController.OnZoomInEmote;
            ZoomOutEmoteAction.performed -= ThirdPersonEmoteController.OnZoomOutEmote;

            PerformNextInstrumentAction.performed -= PlayNextInstrument;
            PerformRandomEmoteAction.performed -= PerformRandomEmote;

            // For now
            if (InputUtils_Compat.Enabled)
            {
                /*
                QuickEmote1Action.performed -= OnQuickEmote1;
                QuickEmote2Action.performed -= OnQuickEmote2;
                QuickEmote3Action.performed -= OnQuickEmote3;
                QuickEmote4Action.performed -= OnQuickEmote4;
                QuickEmote5Action.performed -= OnQuickEmote5;
                QuickEmote6Action.performed -= OnQuickEmote6;
                QuickEmote7Action.performed -= OnQuickEmote7;
                QuickEmote8Action.performed -= OnQuickEmote8;
                */
            }

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


        private static void OnQuickEmote1(InputAction.CallbackContext context) => OnQuickEmote(context, 0);
        private static void OnQuickEmote2(InputAction.CallbackContext context) => OnQuickEmote(context, 1);
        private static void OnQuickEmote3(InputAction.CallbackContext context) => OnQuickEmote(context, 2);
        private static void OnQuickEmote4(InputAction.CallbackContext context) => OnQuickEmote(context, 3);
        private static void OnQuickEmote5(InputAction.CallbackContext context) => OnQuickEmote(context, 4);
        private static void OnQuickEmote6(InputAction.CallbackContext context) => OnQuickEmote(context, 5);
        private static void OnQuickEmote7(InputAction.CallbackContext context) => OnQuickEmote(context, 6);
        private static void OnQuickEmote8(InputAction.CallbackContext context) => OnQuickEmote(context, 7);

        public static void OnQuickEmote(InputAction.CallbackContext context, int quickEmoteIndex)
        {
            if (localPlayerController == null || !context.performed)
                return;
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (EmoteMenu.isMenuOpen)
            {
                // Try to assign emote to a quick emote slot
                try
                {
                    string previousQuickEmoteName = "";
                    if (EmotesManager.allQuickEmotes != null && quickEmoteIndex >= 0 && quickEmoteIndex < EmotesManager.allQuickEmotes.Count())
                        previousQuickEmoteName = EmotesManager.allQuickEmotes[quickEmoteIndex];

                    if (EmoteMenu.hoveredEmoteIndex >= 0 && EmoteMenu.hoveredEmoteIndex < EmoteMenu.currentLoadoutEmotesList.Count)
                    {
                        UnlockableEmote quickEmote = EmoteMenu.currentLoadoutEmotesList[EmoteMenu.hoveredEmoteIndex];
                        
                        if (quickEmote != null)
                        {
                            if (quickEmote.emoteName != previousQuickEmoteName)
                            {
                                while (EmotesManager.allQuickEmotes.Contains(quickEmote.emoteName))
                                    EmotesManager.allQuickEmotes[EmotesManager.allQuickEmotes.IndexOf(quickEmote.emoteName)] = "";

                                EmotesManager.allQuickEmotes[quickEmoteIndex] = quickEmote.emoteName;
                                EmoteMenu.UpdateEmoteWheel(); // Update UI
                                SaveManager.SaveQuickEmotes();

                                LogVerbose("Assigned quick emote " + (quickEmoteIndex + 1) + ". Emote: " + quickEmote.emoteName + ((!string.IsNullOrEmpty(previousQuickEmoteName)) ? ". Previous emote: " + previousQuickEmoteName : ""));
                            }
                            else
                            {
                                // Unassign quick emote
                                EmotesManager.allQuickEmotes[quickEmoteIndex] = "";
                                EmoteMenu.UpdateEmoteWheel(); // Update UI
                                SaveManager.SaveQuickEmotes();
                                LogVerbose("Unassigned quick emote " + (quickEmoteIndex + 1) + ". Emote: " + previousQuickEmoteName);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LogErrorVerbose("Could not assign quick emote " + (quickEmoteIndex + 1) + "\n" + e);
                }
            }
            else
            {
                // Try performing quick emote
                try
                {
                    string emoteName = EmotesManager.allQuickEmotes[quickEmoteIndex];
                    if (string.IsNullOrEmpty(emoteName))
                        LogWarningVerbose("Could not perform quick emote " + (quickEmoteIndex + 1) + ". Emote has not been assigned.");
                    else if (!EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                        LogWarningVerbose("Could not perform quick emote " + (quickEmoteIndex + 1) + ". Emote is invalid: " + emoteName);
                    else
                    {
                        if (!SessionManager.unlockedEmotes.Contains(emote))
                            LogWarningVerbose("Could not perform quick emote " + (quickEmoteIndex + 1) + ". Emote is not unlocked: " + emoteName);
                        else
                        {
                            LogVerbose("Attempting to perform quick emote " + (quickEmoteIndex + 1) + ". Emote: " + emote);
                            emoteControllerLocal.TryPerformingEmoteLocal(emote);
                        }
                    }

                }
                catch (Exception e)
                {
                    LogErrorVerbose("Could not perform quick emote " + (quickEmoteIndex + 1) + "\n" + e);
                }
            }
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


        public static void PerformRandomEmote(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;
            if (EmoteMenu.isMenuOpen || SessionManager.unlockedEmotes == null || SessionManager.unlockedEmotes.Count <= 0)
                return;

            int emoteIndex = UnityEngine.Random.Range(0, SessionManager.unlockedEmotes.Count);
            var emote = SessionManager.unlockedEmotes[emoteIndex];

            bool success = emoteControllerLocal.TryPerformingEmoteLocal(emote);
            if (success && !ConfigSettings.disableChatLogRandomEmote.Value)
            {
                MethodInfo method = HUDManager.Instance.GetType().GetMethod("AddChatMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(HUDManager.Instance, new object[] { string.Format("<size=80%><align=\"center\">Perform Random Emote\n<line-height=80%>[{0}]</line-height></align><size=100%>", emoteControllerLocal.performingEmote.displayNameColorCoded), "" });
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
