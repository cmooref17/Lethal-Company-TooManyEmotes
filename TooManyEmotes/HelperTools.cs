using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TooManyEmotes.Patches;
using Unity.Netcode;

namespace TooManyEmotes
{
    internal static class HelperTools
    {
        public static NetworkManager networkManager { get { return NetworkManager.Singleton; } }
        public static bool isClient { get { return networkManager.IsClient; } }
        public static bool isServer { get { return networkManager.IsServer; } }
        public static bool isHost { get { return networkManager.IsHost; } }
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static QuickMenuManager quickMenuManager { get { return localPlayerController?.quickMenuManager; } }
        public static TextMeshProUGUI[] controlTipLines { get { return HUDManager.Instance?.controlTipLines; } }
        public static List<Item> allItems { get { return StartOfRound.Instance?.allItemsList?.itemsList; } }
        public static SelectableLevel[] selectableLevels { get { return StartOfRound.Instance?.levels; } }
        public static string currentSaveFileName { get { return GameNetworkManager.Instance?.currentSaveFileName; } }
        public static int groupCredits { get { return TerminalPatcher.terminalInstance != null ? groupCredits : -1; } }
        public static int currentEmoteCredits { get { return TerminalPatcher.currentEmoteCredits; } }
        public static EmoteControllerPlayer emoteControllerLocal { get { return EmoteControllerPlayer.emoteControllerLocal; } }


        public static bool TryGetPlayerByClientId(ulong clientId, out PlayerControllerB playerController)
        {
            playerController = null;
            foreach (var _playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (_playerController.actualClientId == clientId)
                {
                    playerController = _playerController;
                    break;
                }
            }
            return playerController != null;
        }


        public static bool TryGetPlayerByUsername(string username, out PlayerControllerB playerController)
        {
            playerController = null;
            foreach (var _playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (_playerController.playerUsername == username)
                {
                    playerController = _playerController;
                    break;
                }
            }
            return playerController != null;
        }


        public static EmoteController GetEmoteControllerById(ulong id)
        {
            foreach (var emoteController in EmoteController.allEmoteControllers.Values)
            {
                if (emoteController.emoteControllerId == id)
                    return emoteController;
            }
            return null;
        }
    }
}
