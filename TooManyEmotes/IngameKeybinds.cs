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
