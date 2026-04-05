using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using TMPro;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using TooManyEmotes.Networking;
using TooManyEmotes.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TooManyEmotes.Patches;

[HarmonyPatch]
public static class ThirdPersonEmoteController
{
	internal static GameObject playerHUDHelmetModel;

	internal static GameObject scannedObjectsUI;

	internal static Camera gameplayCamera;

	internal static Camera emoteCamera;

	internal static Transform emoteCameraPivot;

	internal static int cameraCollideLayerMask = (1 << LayerMask.NameToLayer("Room")) | (1 << LayerMask.NameToLayer("PlaceableShipObject")) | (1 << LayerMask.NameToLayer("Terrain")) | (1 << LayerMask.NameToLayer("MiscLevelGeometry"));

	internal static Vector2 clampCameraDistance = new Vector2(1.5f, 5f);

	internal static float targetCameraDistance = 3f;

	internal static ShadowCastingMode defaultShadowCastingMode = (ShadowCastingMode)1;

	internal static RectTransform defaultControlTipLinesParent;

	internal static RectTransform customControlTipLinesParent;

	internal static TextMeshProUGUI[] customControlTipLines;

	private static Vector3 defaultControlTipLinesScale = Vector3.one;

	internal static Vector3 firstPersonCameraLocalPosition;

	internal static Quaternion firstPersonCameraLocalRotation;

	private static bool isPerformingEmote = false;

	internal static Transform localPlayerCameraContainer => HelperTools.localPlayerController?.cameraContainerTransform;

	public static bool firstPersonEmotesEnabled { get; internal set; } = false;

	public static bool allowMovingWhileEmoting { get; internal set; } = false;

	internal static bool isMovingWhileEmoting => !ConfigSync.instance.syncForceDisableMovingWhileEmoting && HelperTools.emoteControllerLocal.IsPerformingCustomEmote() && (allowMovingWhileEmoting || HelperTools.emoteControllerLocal.performingEmote.canMoveWhileEmoting);

	[HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
	[HarmonyPostfix]
	private static void InitLocalPlayerController(PlayerControllerB __instance)
	{
		gameplayCamera = __instance.gameplayCamera;
		if (emoteCamera == null)
		{
			emoteCameraPivot = new GameObject("EmoteCameraPivot").transform;
			emoteCamera = new GameObject("EmoteCamera").AddComponent<Camera>();
			emoteCamera.CopyFrom(gameplayCamera);
		}
		scannedObjectsUI = GameObject.Find("Systems/UI/Canvas/ObjectScanner");
		if (scannedObjectsUI == null)
		{
			Animator scanInfoAnimator = HUDManager.Instance.scanInfoAnimator;
			scannedObjectsUI = ((scanInfoAnimator != null) ? scanInfoAnimator.transform.parent.parent.gameObject : null);
		}
		defaultControlTipLinesParent = HUDManager.Instance.controlTipLines[0].transform.parent.GetComponent<RectTransform>();
		defaultControlTipLinesScale = defaultControlTipLinesParent.localScale;
		customControlTipLinesParent = Object.Instantiate(defaultControlTipLinesParent, defaultControlTipLinesParent.parent);
		customControlTipLinesParent.name = "ThirdPersonEmotesControlTips";
		customControlTipLinesParent.SetSiblingIndex(defaultControlTipLinesParent.GetSiblingIndex() + 1);
		customControlTipLinesParent.SetPositionAndRotation(defaultControlTipLinesParent.position, defaultControlTipLinesParent.rotation);
		customControlTipLinesParent.localScale = Vector3.zero;
		customControlTipLines = new TextMeshProUGUI[HUDManager.Instance.controlTipLines.Length];
		int num = 0;
		TextMeshProUGUI[] componentsInChildren = customControlTipLinesParent.GetComponentsInChildren<TextMeshProUGUI>();
		foreach (TextMeshProUGUI controlTipLine in componentsInChildren)
		{
			if (controlTipLine != null)
			{
				if (controlTipLine.name.ToLower().Contains("controltip"))
				{
					customControlTipLines[num++] = controlTipLine;
				}
				else
				{
					Object.Destroy(controlTipLine.gameObject);
				}
			}
		}
		LoadPreferences();
		ResetCamera();
	}

	[HarmonyPatch(typeof(PlayerControllerB), "SpawnPlayerAnimation")]
	[HarmonyPostfix]
	private static void OnPlayerSpawn(PlayerControllerB __instance)
	{
		firstPersonCameraLocalPosition = localPlayerCameraContainer.localPosition;
		firstPersonCameraLocalRotation = localPlayerCameraContainer.localRotation;
		ResetCamera();
	}

	internal static void SavePreferences()
	{
		try
		{
			CustomLogging.Log("Saving ThirdPersonEmoteController preferences.");
			ES3.Save<bool>("TooManyEmotes.EnableFirstPersonEmotes", firstPersonEmotesEnabled, SaveManager.TooManyEmotesSaveFileName);
			ES3.Save<bool>("TooManyEmotes.AllowMovingWhileEmoting", allowMovingWhileEmoting, SaveManager.TooManyEmotesSaveFileName);
		}
		catch (Exception ex)
		{
			CustomLogging.LogErrorVerbose("Error while trying to save TooManyEmotes ThirdPersonEmoteController preferences.\n" + ex);
		}
	}

	internal static void LoadPreferences()
	{
		CustomLogging.Log("Loading ThirdPersonEmoteController preferences.");
		try
		{
			if (ES3.KeyExists("TooManyEmotes.EnableFirstPersonEmotes"))
			{
				ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes");
			}
			if (ES3.KeyExists("TooManyEmotes.AllowMovingWhileEmoting"))
			{
				ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting");
			}
		}
		catch
		{
			try
			{
				ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes");
				ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting");
			}
			catch
			{
			}
		}
		try
		{
			firstPersonEmotesEnabled = ES3.Load<bool>("TooManyEmotes.EnableFirstPersonEmotes", SaveManager.TooManyEmotesSaveFileName, false);
			allowMovingWhileEmoting = ES3.Load<bool>("TooManyEmotes.AllowMovingWhileEmoting", SaveManager.TooManyEmotesSaveFileName, false);
		}
		catch (Exception ex)
		{
			CustomLogging.LogErrorVerbose("Failed to load third person emote preferences. Preferences will be reset.\n" + ex);
			firstPersonEmotesEnabled = false;
			allowMovingWhileEmoting = false;
			try
			{
				ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes", SaveManager.TooManyEmotesSaveFileName);
				ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting", SaveManager.TooManyEmotesSaveFileName);
			}
			catch
			{
				CustomLogging.LogErrorVerbose("Failed to reset third person emote preferences. I recommend deleting this file: \"" + SaveManager.TooManyEmotesSaveFileName + "\" located at this path: \"C:\\Users\\YOUR_USER\\AppData\\LocalLow\\ZeekerssRBLX\\Lethal Company\"");
			}
		}
	}

	public static void ResetCamera()
	{
		if (gameplayCamera == null || emoteCamera == null || emoteCameraPivot == null)
		{
			return;
		}
		emoteCamera.enabled = false;
		Camera activeCamera = ((firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled) ? gameplayCamera : StartOfRound.Instance.activeCamera);
		StartOfRound.Instance.SwitchCamera(activeCamera);
		CallChangeAudioListenerToObject(activeCamera.gameObject);
		ReloadPlayerModel(HelperTools.localPlayerController);
		gameplayCamera.cullingMask &= -8388609;
		emoteCamera.cullingMask = gameplayCamera.cullingMask;
		emoteCamera.cullingMask &= -161;
		emoteCameraPivot.SetParent(HelperTools.localPlayerController.transform);
		emoteCameraPivot.SetLocalPositionAndRotation(Vector3.up * 1.8f, Quaternion.identity);
		emoteCamera.transform.SetParent(emoteCameraPivot);
		emoteCamera.transform.SetLocalPositionAndRotation(Vector3.back * targetCameraDistance, Quaternion.identity);
		PlayerControllerB[] allPlayerScripts = StartOfRound.Instance.allPlayerScripts;
		foreach (PlayerControllerB player in allPlayerScripts)
		{
			if (player != null && player != HelperTools.localPlayerController && player.gameplayCamera != null)
			{
				player.gameplayCamera.cullingMask |= 0x800000;
			}
		}
		GameObject shipCameraObject = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera");
		Camera shipCamera = ((shipCameraObject != null) ? shipCameraObject.GetComponent<Camera>() : null);
		if (shipCamera != null)
		{
			shipCamera.cullingMask |= 0x800000;
		}
	}

	public static void ReloadPlayerModel(PlayerControllerB playerController)
	{
		if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return;
		}
		try
		{
			playerController.GetComponentInChildren<LODGroup>().enabled = false;
			playerController.thisPlayerModelLOD1.gameObject.layer = 5;
			playerController.thisPlayerModelLOD1.shadowCastingMode = (ShadowCastingMode)3;
			playerController.thisPlayerModelLOD2.shadowCastingMode = (ShadowCastingMode)0;
			playerController.thisPlayerModelLOD2.enabled = false;
			playerController.playerBetaBadgeMesh.gameObject.layer = 5;
			playerController.thisPlayerModel.gameObject.layer = 23;
			playerController.thisPlayerModel.shadowCastingMode = (ShadowCastingMode)1;
		}
		catch (Exception ex)
		{
			CustomLogging.LogError("Error while trying to reset player model for player: " + playerController.name + " Error: " + ex);
		}
	}

	[HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
	[HarmonyPrefix]
	private static bool UseFreeCamWhileEmoting(PlayerControllerB __instance)
	{
		if (__instance != HelperTools.localPlayerController || HelperTools.emoteControllerLocal == null)
		{
			return true;
		}
		if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return true;
		}
		if (HelperTools.emoteControllerLocal.IsPerformingCustomEmote())
		{
			if (firstPersonEmotesEnabled)
			{
				if (StartOfRound.Instance.activeCamera != gameplayCamera)
				{
					StartOfRound.Instance.SwitchCamera(gameplayCamera);
					CallChangeAudioListenerToObject(gameplayCamera.gameObject);
					emoteCamera.enabled = false;
					if (HelperTools.localPlayerController.currentlyHeldObjectServer != null)
					{
						HelperTools.localPlayerController.currentlyHeldObjectServer.parentObject = HelperTools.localPlayerController.localItemHolder;
					}
				}
				localPlayerCameraContainer.SetPositionAndRotation(HelperTools.localPlayerController.playerGlobalHead.position, HelperTools.localPlayerController.transform.rotation);
				return isMovingWhileEmoting;
			}
			if (StartOfRound.Instance.activeCamera != emoteCamera)
			{
				emoteCamera.enabled = true;
				StartOfRound.Instance.SwitchCamera(emoteCamera);
				CallChangeAudioListenerToObject(emoteCamera.gameObject);
				if (HelperTools.localPlayerController.currentlyHeldObjectServer != null)
				{
					HelperTools.localPlayerController.currentlyHeldObjectServer.parentObject = HelperTools.localPlayerController.serverItemHolder;
				}
			}
			Vector3 cameraOffset = Vector3.back * Mathf.Clamp(targetCameraDistance, clampCameraDistance.x, clampCameraDistance.y);
			emoteCamera.transform.localPosition = Vector3.Lerp(emoteCamera.transform.localPosition, cameraOffset, 10f * Time.deltaTime);
			if (!HelperTools.localPlayerController.quickMenuManager.isMenuOpen && !EmoteMenu.isMenuOpen)
			{
				bool rotateCharacter = (ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier);
				if (rotateCharacter != isMovingWhileEmoting && emoteCameraPivot.localEulerAngles.y != 0f)
				{
					HelperTools.localPlayerController.transform.localEulerAngles = new Vector3(HelperTools.localPlayerController.transform.localEulerAngles.x, emoteCameraPivot.transform.eulerAngles.y, HelperTools.localPlayerController.transform.localEulerAngles.z);
					emoteCameraPivot.transform.localEulerAngles = new Vector3(emoteCameraPivot.localEulerAngles.x, 0f, emoteCameraPivot.localEulerAngles.z);
				}
				if (!isMovingWhileEmoting || rotateCharacter)
				{
					Vector2 lookInput = HelperTools.localPlayerController.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * (float)IngamePlayerSettings.Instance.settings.lookSensitivity;
					emoteCameraPivot.Rotate(new Vector3(0f, lookInput.x, 0f));
					float cameraPitch = emoteCameraPivot.localEulerAngles.x - lookInput.y;
					cameraPitch = ((cameraPitch > 180f) ? (cameraPitch - 360f) : cameraPitch);
					cameraPitch = Mathf.Clamp(cameraPitch, -45f, 45f);
					emoteCameraPivot.transform.localEulerAngles = new Vector3(cameraPitch, emoteCameraPivot.localEulerAngles.y, 0f);
				}
				else
				{
					emoteCameraPivot.transform.localEulerAngles = gameplayCamera.transform.localEulerAngles;
				}
				RaycastHit hit = default(RaycastHit);
				if (Physics.Raycast(emoteCameraPivot.position, -emoteCameraPivot.forward * targetCameraDistance, ref hit, targetCameraDistance, cameraCollideLayerMask))
				{
					emoteCamera.transform.localPosition = Vector3.back * Mathf.Clamp(hit.distance - 0.2f, 0f, targetCameraDistance);
				}
				if (!isMovingWhileEmoting || rotateCharacter)
				{
					return false;
				}
			}
		}
		return true;
	}

	internal static void OnZoomInEmote(CallbackContext context)
	{
		if (HelperTools.localPlayerController != null && HelperTools.emoteControllerLocal.IsPerformingCustomEmote() && !EmoteMenu.isMenuOpen && !HelperTools.quickMenuManager.isMenuOpen && !firstPersonEmotesEnabled && !ConfigSettings.disableEmotesForSelf.Value && !LCVR_Compat.LoadedAndEnabled)
		{
			bool rotateCharacter = (ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier);
			if (!isMovingWhileEmoting || rotateCharacter)
			{
				targetCameraDistance = Mathf.Clamp(targetCameraDistance - 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
			}
		}
	}

	internal static void OnZoomOutEmote(CallbackContext context)
	{
		if (HelperTools.localPlayerController != null && HelperTools.emoteControllerLocal.IsPerformingCustomEmote() && !EmoteMenu.isMenuOpen && !HelperTools.quickMenuManager.isMenuOpen && !firstPersonEmotesEnabled && !ConfigSettings.disableEmotesForSelf.Value && !LCVR_Compat.LoadedAndEnabled)
		{
			bool rotateCharacter = (ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier);
			if (!isMovingWhileEmoting || rotateCharacter)
			{
				targetCameraDistance = Mathf.Clamp(targetCameraDistance + 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
			}
		}
	}

	public static void OnStartCustomEmoteLocal()
	{
		Keybinds.toggledRotating = false;
		if (!firstPersonEmotesEnabled)
		{
			if (emoteCamera != null && !emoteCamera.enabled)
			{
				StartOfRound.Instance.SwitchCamera(emoteCamera);
				CallChangeAudioListenerToObject(emoteCamera.gameObject);
				if (!isPerformingEmote)
				{
					emoteCameraPivot.eulerAngles = ((Component)gameplayCamera).transform.eulerAngles;
				}
			}
			HelperTools.localPlayerController.thisPlayerModelLOD1.gameObject.layer = 5;
			HelperTools.localPlayerController.thisPlayerModelLOD1.shadowCastingMode = (ShadowCastingMode)3;
			HelperTools.localPlayerController.thisPlayerModelLOD2.shadowCastingMode = (ShadowCastingMode)0;
			HelperTools.localPlayerController.thisPlayerModelLOD2.enabled = false;
			HelperTools.localPlayerController.playerBetaBadgeMesh.gameObject.layer = 5;
			HelperTools.localPlayerController.thisPlayerModel.gameObject.layer = 3;
			HelperTools.localPlayerController.thisPlayerModel.shadowCastingMode = (ShadowCastingMode)1;
			HelperTools.localPlayerController.thisPlayerModelArms.enabled = false;
			if (scannedObjectsUI != null)
			{
				scannedObjectsUI.SetActive(false);
			}
			if (HelperTools.localPlayerController.localItemHolder == HelperTools.localPlayerController.currentlyHeldObjectServer?.parentObject)
			{
				HelperTools.localPlayerController.currentlyHeldObjectServer.parentObject = HelperTools.localPlayerController.serverItemHolder;
			}
			if (AdvancedCompany_Compat.Enabled)
			{
				AdvancedCompany_Compat.ShowLocalCosmetics();
			}
			else if (MoreCompany_Compat.Enabled)
			{
				MoreCompany_Compat.ShowLocalCosmetics();
			}
			if (LethalVRM_Compat.Enabled)
			{
				LethalVRM_Compat.DisplayVRMModel();
			}
		}
		UpdateControlTip();
		ShowCustomControlTips(!firstPersonEmotesEnabled);
		isPerformingEmote = true;
	}

	public static void OnStopCustomEmoteLocal()
	{
		localPlayerCameraContainer.SetLocalPositionAndRotation(firstPersonCameraLocalPosition, firstPersonCameraLocalRotation);
		Keybinds.toggledRotating = false;
		if (emoteCamera != null)
		{
			emoteCamera.enabled = false;
		}
		if (StartOfRound.Instance.activeCamera != gameplayCamera)
		{
			StartOfRound.Instance.SwitchCamera(gameplayCamera);
		}
		if (HelperTools.localPlayerController.activeAudioListener != gameplayCamera.gameObject)
		{
			CallChangeAudioListenerToObject(gameplayCamera.gameObject);
		}
		HelperTools.localPlayerController.thisPlayerModel.gameObject.layer = 23;
		HelperTools.localPlayerController.thisPlayerModel.shadowCastingMode = defaultShadowCastingMode;
		HelperTools.localPlayerController.thisPlayerModelArms.enabled = true;
		if (scannedObjectsUI != null)
		{
			scannedObjectsUI.SetActive(true);
		}
		if (AdvancedCompany_Compat.Enabled)
		{
			AdvancedCompany_Compat.HideLocalCosmetics();
		}
		else if (MoreCompany_Compat.Enabled)
		{
			MoreCompany_Compat.HideLocalCosmetics();
		}
		if (LethalVRM_Compat.Enabled)
		{
			LethalVRM_Compat.HideVRMModel();
		}
		ShowCustomControlTips(show: false);
		GrabbableObject[] itemSlots = HelperTools.localPlayerController.ItemSlots;
		foreach (GrabbableObject heldItem in itemSlots)
		{
			if (heldItem != null && heldItem.parentObject == HelperTools.localPlayerController.serverItemHolder)
			{
				heldItem.parentObject = HelperTools.localPlayerController.localItemHolder;
			}
		}
		emoteCameraPivot.eulerAngles = localPlayerCameraContainer.eulerAngles;
		isPerformingEmote = false;
	}

	internal static void UpdateFirstPersonEmoteMode(bool value)
	{
		if (firstPersonEmotesEnabled == value)
		{
			return;
		}
		firstPersonEmotesEnabled = value;
		if (!HelperTools.emoteControllerLocal.IsPerformingCustomEmote())
		{
			return;
		}
		HelperTools.localPlayerController.thisPlayerModelArms.enabled = firstPersonEmotesEnabled;
		((Component)HelperTools.localPlayerController.thisPlayerModel).gameObject.layer = (firstPersonEmotesEnabled ? 23 : 3);
		if (firstPersonEmotesEnabled)
		{
			Keybinds.holdingRotatePlayerModifier = false;
			Keybinds.toggledRotating = false;
			if (AdvancedCompany_Compat.Enabled)
			{
				AdvancedCompany_Compat.HideLocalCosmetics();
			}
			else if (MoreCompany_Compat.Enabled)
			{
				MoreCompany_Compat.HideLocalCosmetics();
			}
			if (LethalVRM_Compat.Enabled)
			{
				LethalVRM_Compat.HideVRMModel();
			}
			if (Object.op_Implicit((Object)(object)scannedObjectsUI))
			{
				scannedObjectsUI.SetActive(false);
			}
		}
		else
		{
			if (AdvancedCompany_Compat.Enabled)
			{
				AdvancedCompany_Compat.ShowLocalCosmetics();
			}
			else if (MoreCompany_Compat.Enabled)
			{
				MoreCompany_Compat.ShowLocalCosmetics();
			}
			if (LethalVRM_Compat.Enabled)
			{
				LethalVRM_Compat.DisplayVRMModel();
			}
			if (Object.op_Implicit((Object)(object)scannedObjectsUI))
			{
				scannedObjectsUI.SetActive(true);
			}
		}
		UpdateControlTip();
		ShowCustomControlTips(!firstPersonEmotesEnabled);
	}

	internal static void SetCanMoveWhileEmoting(bool value)
	{
		if (allowMovingWhileEmoting == value)
		{
			return;
		}
		allowMovingWhileEmoting = value;
		if (HelperTools.emoteControllerLocal.IsPerformingCustomEmote())
		{
			if (allowMovingWhileEmoting)
			{
				emoteCameraPivot.localEulerAngles = new Vector3(emoteCameraPivot.localEulerAngles.x, 0f, emoteCameraPivot.localEulerAngles.z);
			}
			UpdateControlTip();
		}
	}

	[HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
	[HarmonyPrefix]
	private static bool PreventSwappingItemsWhileEmoting(CallbackContext context, PlayerControllerB __instance)
	{
		if (emoteCamera == null || !emoteCamera.enabled)
		{
			return true;
		}
		if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return true;
		}
		bool flag = (ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier);
		if (__instance == HelperTools.localPlayerController && context.performed && HelperTools.emoteControllerLocal != null && HelperTools.emoteControllerLocal.IsPerformingCustomEmote() && (!isMovingWhileEmoting || flag))
		{
			return false;
		}
		return true;
	}

	[HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
	[HarmonyPostfix]
	private static void FixedNewHeldItemParent(int slot, PlayerControllerB __instance)
	{
		if (__instance != HelperTools.localPlayerController || !HelperTools.emoteControllerLocal.IsPerformingCustomEmote())
		{
			return;
		}
		HeldItemResult heldItem = HelperTools.localPlayerController.ResolveHeldItem();
		GrabbableObject heldItemObject = heldItem.Item;
		if (heldItem.IsValid && heldItemObject != null)
		{
			heldItemObject.parentObject = (firstPersonEmotesEnabled ? HelperTools.localPlayerController.localItemHolder : HelperTools.localPlayerController.serverItemHolder);
			if (EmoteControllerPlayer.emoteControllerLocal.emotingProps.Count > 0)
			{
				heldItemObject.EnableItemMeshes(false);
			}
		}
	}

	internal static void ShowCustomControlTips(bool show)
	{
		if (customControlTipLinesParent != null && defaultControlTipLinesParent != null)
		{
			customControlTipLinesParent.localScale = (show ? defaultControlTipLinesScale : Vector3.zero);
			defaultControlTipLinesParent.localScale = (show ? Vector3.zero : defaultControlTipLinesScale);
		}
	}

	public static void UpdateControlTip(int appendToIndex = 0)
	{
		if (HelperTools.emoteControllerLocal.IsPerformingCustomEmote() && HelperTools.controlTipLines != null && customControlTipLines != null && customControlTipLines.Length >= 4)
		{
			if (appendToIndex < 0 || appendToIndex >= HelperTools.controlTipLines.Length - 1)
			{
				appendToIndex = 0;
			}
			string text = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomInEmoteAction);
			string text2 = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomOutEmoteAction);
			string text3 = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.RotatePlayerEmoteAction);
			string keybindDisplayName = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.PerformNextInstrumentAction);
			if (text == "")
			{
				text = "Unbound";
			}
			if (text2 == "")
			{
				text2 = "Unbound";
			}
			if (text3 == "")
			{
				text3 = "Unbound";
			}
			int i = appendToIndex;
			string text4 = (((text == "Scroll Up" || text == "Scroll Down") && (text2 == "Scroll Up" || text2 == "Scroll Down") && text != text2) ? "[Scroll Mouse]" : ((!(text != "Unbound") && !(text2 != "Unbound")) ? "Unbound" : (text + "/" + text2)));
			customControlTipLines[i].text = "Zoom : ";
			if (isMovingWhileEmoting)
			{
				TextMeshProUGUI controlTipLine = customControlTipLines[i];
				controlTipLine.text = controlTipLine.text + "[" + text3 + "] + ";
			}
			TextMeshProUGUI zoomControlTip = customControlTipLines[i++];
			zoomControlTip.text = zoomControlTip.text + text4;
			customControlTipLines[i++].text = string.Format((isMovingWhileEmoting ? "Freeze" : "Rotate") + " : " + (ConfigSettings.toggleRotateCharacterInEmote.Value ? "Toggle" : "Hold") + " [{0}]", text3);
			if (HelperTools.emoteControllerLocal.isPerformingEmote && HelperTools.emoteControllerLocal.performingEmote.inEmoteSyncGroup && HelperTools.emoteControllerLocal.performingEmote.emoteSyncGroup.Count > 1)
			{
				customControlTipLines[i++].text = $"Play Next Instrument: [{keybindDisplayName}]";
			}
			for (; i < customControlTipLines.Length; i++)
			{
				customControlTipLines[i].text = "";
			}
		}
	}

	public static void CallChangeAudioListenerToObject(GameObject gameObject)
	{
		if (!firstPersonEmotesEnabled || !(gameObject != HelperTools.localPlayerController.gameplayCamera))
		{
			MethodInfo method = HelperTools.localPlayerController.GetType().GetMethod("ChangeAudioListenerToObject", BindingFlags.Instance | BindingFlags.NonPublic);
			method.Invoke(HelperTools.localPlayerController, new object[1] { gameObject });
		}
	}
}
