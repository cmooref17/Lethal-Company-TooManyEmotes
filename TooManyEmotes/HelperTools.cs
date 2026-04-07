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
        internal enum HeldItemSource
        {
            ItemSlot,
            ItemOnlySlot,
            Invalid
        }

        internal readonly struct HeldItemResult
        {
            public HeldItemSource Source { get; }
            public int Slot { get; }
            public GrabbableObject Item { get; }

            public bool IsValid { get { return Source != HeldItemSource.Invalid; } }
            public bool HasItem { get { return Item != null; } }

            public HeldItemResult(HeldItemSource source, int slot, GrabbableObject item)
            {
                Source = source;
                Slot = slot;
                Item = item;
            }
        }

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


        public static HeldItemResult ResolveHeldItem(this PlayerControllerB playerController)
        {
            if (playerController == null)
                return new HeldItemResult(HeldItemSource.Invalid, -1, null);

            int slot = playerController.currentItemSlot;
            if (slot == 50)
                return new HeldItemResult(HeldItemSource.ItemOnlySlot, slot, playerController.ItemOnlySlot);

            if (playerController.ItemSlots == null || slot < 0 || slot >= playerController.ItemSlots.Length)
                return new HeldItemResult(HeldItemSource.Invalid, slot, null);

            return new HeldItemResult(HeldItemSource.ItemSlot, slot, playerController.ItemSlots[slot]);
        }


        public static GrabbableObject GetHeldGrabbableSafe(this PlayerControllerB playerController)
        {
            return playerController.ResolveHeldItem().Item;
        }


        public static bool HasHeldGrabbable(this PlayerControllerB playerController, GrabbableObject item)
        {
            return item != null && playerController.GetHeldGrabbableSafe() == item;
        }
    }
}
