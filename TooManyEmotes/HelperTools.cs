using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TooManyEmotes.Patches;

namespace TooManyEmotes
{
    internal static class HelperTools
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static QuickMenuManager quickMenuManager { get { return localPlayerController?.quickMenuManager; } }
        public static TextMeshProUGUI[] controlTipLines { get { return HUDManager.Instance?.controlTipLines; } }
        public static List<Item> allItems { get { return StartOfRound.Instance?.allItemsList?.itemsList; } }
        public static SelectableLevel[] selectableLevels { get { return StartOfRound.Instance?.levels; } }
        public static string currentSaveFileName { get { return GameNetworkManager.Instance?.currentSaveFileName; } }
        public static int groupCredits { get { return TerminalPatcher.terminalInstance != null ? groupCredits : -1; } }
        public static int currentEmoteCredits { get { return TerminalPatcher.currentEmoteCredits; } }
        public static EmoteControllerPlayer emoteControllerLocal { get { return EmoteControllerPlayer.emoteControllerLocal; } }
    }
}
