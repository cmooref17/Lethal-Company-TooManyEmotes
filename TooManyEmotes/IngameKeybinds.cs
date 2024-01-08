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

        [InputAction("<Keyboard>/backquote", Name = "[TooManyEmotes]\nOpen Emote Menu")]
        public InputAction OpenEmoteMenuHotkey { get; set; }
        [InputAction("<Keyboard>/leftAlt", Name = "[TooManyEmotes]\nRotate Character in Emote")]
        public InputAction RotateCharacterEmoteHotkey { get; set; }
        [InputAction("<Mouse>/middleButton", Name = "[TooManyEmotes]\nFavorite Emote")]
        public InputAction FavoriteEmoteHotkey { get; set; }
    }

    
    internal class InputUtilsCompat
    {
        internal static InputActionAsset Asset { get { return IngameKeybinds.GetAsset(); } }
        internal static bool Enabled => Plugin.IsModLoaded("com.rune580.LethalCompanyInputUtils");

        public static InputAction OpenEmoteMenuHotkey => IngameKeybinds.Instance.OpenEmoteMenuHotkey;
        public static InputAction RotateCharacterEmoteHotkey => IngameKeybinds.Instance.RotateCharacterEmoteHotkey;
        public static InputAction FavoriteEmoteHotkey => IngameKeybinds.Instance.FavoriteEmoteHotkey;
    }
}
