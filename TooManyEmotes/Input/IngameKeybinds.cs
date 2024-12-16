using BepInEx.Bootstrap;
using LethalCompanyInputUtils.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace TooManyEmotes.Input
{
    internal class IngameKeybinds : LcInputActions
    {
        internal static IngameKeybinds Instance = new IngameKeybinds();
        internal static InputActionAsset GetAsset() => Instance.Asset;

        [InputAction("<Keyboard>/backquote", GamepadPath = "<Gamepad>/leftStickPress", Name = "Open Emote Menu")]
        public InputAction OpenEmoteMenuHotkey { get; set; }

        //[InputAction("<Mouse>/leftButton", GamepadPath = "<Gamepad>/rightStickPress", Name = "Perform Selected Emote")]
        //public InputAction PerformEmoteHotkey { get; set; }

        [InputAction("<Keyboard>/leftAlt", GamepadPath = "", Name = "Rotate Character in Emote")]
        public InputAction RotateCharacterEmoteHotkey { get; set; }

        [InputAction("<Mouse>/scroll/up", GamepadPath = "", Name = "Zoom In While Emoting")]
        public InputAction ZoomInEmoteHotkey { get; set; }

        [InputAction("<Mouse>/scroll/down", GamepadPath = "", Name = "Zoom Out While Emoting")]
        public InputAction ZoomOutEmoteHotkey { get; set; }

        [InputAction("<Mouse>/middleButton", GamepadPath = "<Gamepad>/rightStickPress", Name = "Favorite/Unfavorite Emote")]
        public InputAction FavoriteEmoteHotkey { get; set; }

        [InputAction("", GamepadPath = "<Gamepad>/dpad/left", Name = "Prev Emote Page")]
        public InputAction PrevEmotePageHotkey { get; set; }

        [InputAction("", GamepadPath = "<Gamepad>/dpad/right", Name = "Next Emote Page")]
        public InputAction NextEmotePageHotkey { get; set; }

        [InputAction("", GamepadPath = "<Gamepad>/dpad/up", Name = "Swap Emote Loadout Up")]
        public InputAction NextEmoteLoadoutUpHotkey { get; set; }

        [InputAction("", GamepadPath = "<Gamepad>/dpad/down", Name = "Swap Emote Loadout Down")]
        public InputAction NextEmoteLoadoutDownHotkey { get; set; }

        [InputAction("<Keyboard>/n", GamepadPath = "<Gamepad>/dpad/right", Name = "Play Next Instrument")]
        public InputAction PerformNextInstrumentHotkey { get; set; }

        [InputAction("<Keyboard>/m", GamepadPath = "", Name = "Perform Random Emote")]
        public InputAction PerformRandomEmoteHotkey { get; set; }


        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 1")]
        public InputAction QuickEmoteHotkey1 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 2")]
        public InputAction QuickEmoteHotkey2 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 3")]
        public InputAction QuickEmoteHotkey3 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 4")]
        public InputAction QuickEmoteHotkey4 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 5")]
        public InputAction QuickEmoteHotkey5 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 6")]
        public InputAction QuickEmoteHotkey6 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 7")]
        public InputAction QuickEmoteHotkey7 { get; set; }
        [InputAction("", GamepadPath = "", Name = "Set/Perform Quick Emote 8")]
        public InputAction QuickEmoteHotkey8 { get; set; }
    }
}
