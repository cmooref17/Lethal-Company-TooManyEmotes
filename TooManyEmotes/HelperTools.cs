using System.Collections.Generic;
using GameNetcodeStuff;
using TMPro;
using TooManyEmotes.Patches;
using Unity.Netcode;
using UnityEngine;

namespace TooManyEmotes;

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

	public bool IsValid => Source != HeldItemSource.Invalid;

	public bool HasItem => Item != null;

	public HeldItemResult(HeldItemSource source, int slot, GrabbableObject item)
	{
		Source = source;
		Slot = slot;
		Item = item;
	}
}

internal static class HelperTools
{
	public static NetworkManager networkManager => NetworkManager.Singleton;

	public static bool isClient => networkManager.IsClient;

	public static bool isServer => networkManager.IsServer;

	public static bool isHost => networkManager.IsHost;

	public static PlayerControllerB localPlayerController => StartOfRound.Instance?.localPlayerController;

	public static QuickMenuManager quickMenuManager => localPlayerController?.quickMenuManager;

	public static TextMeshProUGUI[] controlTipLines => HUDManager.Instance?.controlTipLines;

	public static List<Item> allItems => StartOfRound.Instance?.allItemsList?.itemsList;

	public static SelectableLevel[] selectableLevels => StartOfRound.Instance?.levels;

	public static string currentSaveFileName => GameNetworkManager.Instance?.currentSaveFileName;

	public static int groupCredits => (TerminalPatcher.terminalInstance != null) ? groupCredits : (-1);

	public static int currentEmoteCredits => TerminalPatcher.currentEmoteCredits;

	public static EmoteControllerPlayer emoteControllerLocal => EmoteControllerPlayer.emoteControllerLocal;

	public static bool TryGetPlayerByClientId(ulong clientId, out PlayerControllerB playerController)
	{
		playerController = null;
		PlayerControllerB[] allPlayerScripts = StartOfRound.Instance.allPlayerScripts;
		foreach (PlayerControllerB player in allPlayerScripts)
		{
			if (player.actualClientId == clientId)
			{
				playerController = player;
				break;
			}
		}
		return playerController != null;
	}

	public static bool TryGetPlayerByUsername(string username, out PlayerControllerB playerController)
	{
		playerController = null;
		PlayerControllerB[] allPlayerScripts = StartOfRound.Instance.allPlayerScripts;
		foreach (PlayerControllerB player in allPlayerScripts)
		{
			if (player.playerUsername == username)
			{
				playerController = player;
				break;
			}
		}
		return playerController != null;
	}

	public static EmoteController GetEmoteControllerById(ulong id)
	{
		foreach (EmoteController value in EmoteController.allEmoteControllers.Values)
		{
			if (value.emoteControllerId == id)
			{
				return value;
			}
		}
		return null;
	}

	public static HeldItemResult ResolveHeldItem(this PlayerControllerB playerController)
	{
		if (playerController == null)
		{
			return new HeldItemResult(HeldItemSource.Invalid, -1, null);
		}
		int currentItemSlot = playerController.currentItemSlot;
		if (currentItemSlot == 50)
		{
			return new HeldItemResult(HeldItemSource.ItemOnlySlot, currentItemSlot, playerController.ItemOnlySlot);
		}
		if (playerController.ItemSlots == null || (uint)currentItemSlot >= (uint)playerController.ItemSlots.Length)
		{
			return new HeldItemResult(HeldItemSource.Invalid, currentItemSlot, null);
		}
		return new HeldItemResult(HeldItemSource.ItemSlot, currentItemSlot, playerController.ItemSlots[currentItemSlot]);
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
