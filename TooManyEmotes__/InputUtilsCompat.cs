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
    internal class InputUtilsCompat
    {
        internal static InputActionAsset Asset { get { return IngameKeybinds.GetAsset(); } }
        internal static bool Enabled => Plugin.IsModLoaded("com.rune580.LethalCompanyInputUtils");

        public static InputAction OpenEmoteMenuHotkey => IngameKeybinds.Instance.OpenEmoteMenuHotkey;
        //public static InputAction PerformEmoteHotkey => IngameKeybinds.Instance.PerformEmoteHotkey;
        public static InputAction RotateCharacterEmoteHotkey => IngameKeybinds.Instance.RotateCharacterEmoteHotkey;
        public static InputAction FavoriteEmoteHotkey => IngameKeybinds.Instance.FavoriteEmoteHotkey;

        public static InputAction PrevEmotePageHotkey => IngameKeybinds.Instance.PrevEmotePageHotkey;
        public static InputAction NextEmotePageHotkey => IngameKeybinds.Instance.NextEmotePageHotkey;

        public static InputAction NextEmoteLoadoutUpHotkey => IngameKeybinds.Instance.NextEmoteLoadoutUpHotkey;
        public static InputAction NextEmoteLoadoutDownHotkey => IngameKeybinds.Instance.NextEmoteLoadoutDownHotkey;
        /*
        public static InputAction QuickEmoteFavorite1 => IngameKeybinds.Instance.QuickEmoteFavorite1;
        public static InputAction QuickEmoteFavorite2 => IngameKeybinds.Instance.QuickEmoteFavorite2;
        public static InputAction QuickEmoteFavorite3 => IngameKeybinds.Instance.QuickEmoteFavorite3;
        public static InputAction QuickEmoteFavorite4 => IngameKeybinds.Instance.QuickEmoteFavorite4;
        public static InputAction QuickEmoteFavorite5 => IngameKeybinds.Instance.QuickEmoteFavorite5;
        public static InputAction QuickEmoteFavorite6 => IngameKeybinds.Instance.QuickEmoteFavorite6;
        public static InputAction QuickEmoteFavorite7 => IngameKeybinds.Instance.QuickEmoteFavorite7;
        public static InputAction QuickEmoteFavorite8 => IngameKeybinds.Instance.QuickEmoteFavorite8;
        */
    }
}
