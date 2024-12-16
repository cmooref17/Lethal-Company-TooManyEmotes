using BepInEx.Bootstrap;
using LethalCompanyInputUtils.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using TooManyEmotes.Input;

namespace TooManyEmotes.Compatibility
{
    public static class InputUtils_Compat
    {
        internal static InputActionAsset Asset { get { return IngameKeybinds.GetAsset(); } }
        internal static bool Enabled => Plugin.IsModLoaded("com.rune580.LethalCompanyInputUtils");

        public static InputAction OpenEmoteMenuHotkey => IngameKeybinds.Instance.OpenEmoteMenuHotkey;
        public static InputAction RotateCharacterEmoteHotkey => IngameKeybinds.Instance.RotateCharacterEmoteHotkey;
        public static InputAction ZoomInEmoteHotkey => IngameKeybinds.Instance.ZoomInEmoteHotkey;
        public static InputAction ZoomOutEmoteHotkey => IngameKeybinds.Instance.ZoomOutEmoteHotkey;
        public static InputAction FavoriteEmoteHotkey => IngameKeybinds.Instance.FavoriteEmoteHotkey;

        public static InputAction PrevEmotePageHotkey => IngameKeybinds.Instance.PrevEmotePageHotkey;
        public static InputAction NextEmotePageHotkey => IngameKeybinds.Instance.NextEmotePageHotkey;

        public static InputAction NextEmoteLoadoutUpHotkey => IngameKeybinds.Instance.NextEmoteLoadoutUpHotkey;
        public static InputAction NextEmoteLoadoutDownHotkey => IngameKeybinds.Instance.NextEmoteLoadoutDownHotkey;

        public static InputAction PerformNextInstrumentHotkey => IngameKeybinds.Instance.PerformNextInstrumentHotkey;
        public static InputAction PerformRandomEmoteHotkey => IngameKeybinds.Instance.PerformRandomEmoteHotkey;


        public static InputAction QuickEmote1 => IngameKeybinds.Instance.QuickEmoteHotkey1;
        public static InputAction QuickEmote2 => IngameKeybinds.Instance.QuickEmoteHotkey2;
        public static InputAction QuickEmote3 => IngameKeybinds.Instance.QuickEmoteHotkey3;
        public static InputAction QuickEmote4 => IngameKeybinds.Instance.QuickEmoteHotkey4;
        public static InputAction QuickEmote5 => IngameKeybinds.Instance.QuickEmoteHotkey5;
        public static InputAction QuickEmote6 => IngameKeybinds.Instance.QuickEmoteHotkey6;
        public static InputAction QuickEmote7 => IngameKeybinds.Instance.QuickEmoteHotkey7;
        public static InputAction QuickEmote8 => IngameKeybinds.Instance.QuickEmoteHotkey8;
    }
}
