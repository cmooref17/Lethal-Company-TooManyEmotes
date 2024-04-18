using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using TooManyEmotes.Patches;
using TooManyEmotes.Networking;
using TooManyEmotes.UI;

namespace TooManyEmotes.Input
{
    [HarmonyPatch]
    public static class KeybindDisplayNames
    {
        public static bool usingControllerPrevious = false;
        public static bool usingController { get { return StartOfRound.Instance.localPlayerUsingController; } }
        public static string[] keyboardKeywords = new string[] { "keyboard", "mouse" };
        public static string[] controllerKeywords = new string[] { "gamepad", "controller" };


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void CheckForInputSourceUpdate()
        {
            if (usingController != usingControllerPrevious)
            {
                usingControllerPrevious = usingController;
                UpdateControlTipLines();
            }
        }


        [HarmonyPatch(typeof(KepRemapPanel), "OnDisable")]
        [HarmonyPostfix]
        public static void OnCloseRemapPanel()
        {
            UpdateControlTipLines();
        }


        public static void UpdateControlTipLines()
        {
            if (EmoteMenu.isMenuOpen)
                EmoteMenu.UpdateControlTipLines();

            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var line = HUDManager.Instance.controlTipLines[i];
                if (line != null && line.gameObject.activeSelf && line.enabled && line.text.Contains("Emote Radial Menu"))
                {
                    string displayName = GetKeybindDisplayName(Keybinds.OpenEmoteMenuAction);
                    if (displayName != "")
                        HUDManager.Instance.controlTipLines[i].text = string.Format("Open Emote Radial Menu : [{0}]", displayName);
                    break;
                }
            }

            ThirdPersonEmoteController.UpdateControlTip();
        }


        public static string GetKeybindDisplayName(InputAction inputAction)
        {
            if (inputAction == null || !inputAction.enabled)
                return "";

            int bindingIndex = usingController ? 1 : 0;
            string displayName = inputAction.bindings[bindingIndex].effectivePath;

            return GetKeybindDisplayName(displayName);
        }


        public static string GetKeybindDisplayName(string controlPath)
        {
            if (controlPath.Length <= 1)
                return "";

            string displayName = controlPath.ToLower();
            int replaceIndex = displayName.IndexOf(">/");
            displayName = replaceIndex >= 0 ? displayName.Substring(replaceIndex + 2) : displayName;

            if (displayName.Contains("not-bound"))
                return "";

            displayName = displayName.Replace("leftalt", "Alt");
            displayName = displayName.Replace("rightalt", "Alt");
            displayName = displayName.Replace("leftctrl", "Ctrl");
            displayName = displayName.Replace("rightctrl", "Ctrl");
            displayName = displayName.Replace("leftshift", "Shift");
            displayName = displayName.Replace("rightshift", "Shift");
            displayName = displayName.Replace("leftbutton", "LMB");
            displayName = displayName.Replace("rightbutton", "RMB");
            displayName = displayName.Replace("middlebutton", "MMB");
            displayName = displayName.Replace("lefttrigger", "LT");
            displayName = displayName.Replace("righttrigger", "RT");
            displayName = displayName.Replace("leftshoulder", "LB");
            displayName = displayName.Replace("rightshoulder", "RB");
            displayName = displayName.Replace("leftstickpress", "LS");
            displayName = displayName.Replace("rightstickpress", "RS");
            displayName = displayName.Replace("dpad/", "DPad-");
            displayName = displayName.Replace("scroll/up", "Scroll Up");
            displayName = displayName.Replace("scroll/down", "Scroll Down");

            displayName = displayName.Replace("backquote", "`");

            try { displayName = char.ToUpper(displayName[0]) + displayName.Substring(1); }
            catch { }

            return displayName;
        }
    }
}
