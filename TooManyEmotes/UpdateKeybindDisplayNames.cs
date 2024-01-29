using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using TooManyEmotes.Patches;
using TooManyEmotes.Networking;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public static class UpdateKeybindDisplayNames
    {
        public static bool usingControllerPrevious = false;
        public static bool usingController { get { return StartOfRound.Instance.localPlayerUsingController; } }


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
            if (EmoteMenuManager.isMenuOpen)
                EmoteMenuManager.UpdateControlTipLines();

            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var line = HUDManager.Instance.controlTipLines[i];
                if (line != null && line.gameObject.activeSelf && line.enabled && line.text.Contains("Emote Radial Menu"))
                {
                    int bindingIndex = usingController ? 1 : 0;
                    string displayName = ConfigSettings.GetDisplayName(InputUtilsCompat.Enabled ? Keybinds.OpenEmoteMenuAction.bindings[bindingIndex].path : Keybinds.OpenEmoteMenuAction.bindings[bindingIndex].path);
                    if (displayName != "")
                        HUDManager.Instance.controlTipLines[i].text = string.Format("[{0}]: Open Emote Radial Menu", displayName);
                    break;
                }
            }

            ThirdPersonEmoteController.UpdateControlTip();
        }
    }
}
