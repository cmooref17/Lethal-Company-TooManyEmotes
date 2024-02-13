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

        [InputAction("", GamepadPath = "", Name = "Force Reload Local Player Model")]
        public InputAction ForceReloadPlayerModelHotkey { get; set; }
        /*
        public InputAction QuickEmoteFavorite1 { get; set; }
        public InputAction QuickEmoteFavorite2 { get; set; }
        public InputAction QuickEmoteFavorite3 { get; set; }
        public InputAction QuickEmoteFavorite4 { get; set; }
        public InputAction QuickEmoteFavorite5 { get; set; }
        public InputAction QuickEmoteFavorite6 { get; set; }
        public InputAction QuickEmoteFavorite7 { get; set; }
        public InputAction QuickEmoteFavorite8 { get; set; }
        */
    }
}
