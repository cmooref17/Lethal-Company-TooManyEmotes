using GameNetcodeStuff;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using UnityEngine.InputSystem;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using System.Reflection;
using TooManyEmotes.Networking;
using TooManyEmotes.Compatibility;
using TooManyEmotes.UI;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using System.Collections.Generic;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class ThirdPersonEmoteController
    {
        internal static Transform localPlayerCameraContainer { get { return localPlayerController?.cameraContainerTransform; } }
        
        internal static GameObject playerHUDHelmetModel;
        internal static GameObject scannedObjectsUI;
        internal static Camera gameplayCamera;
        internal static Camera emoteCamera;
        internal static Transform emoteCameraPivot;
        internal static int cameraCollideLayerMask = 1 << LayerMask.NameToLayer("Room") | 1 << LayerMask.NameToLayer("PlaceableShipObject") | 1 << LayerMask.NameToLayer("Terrain") | 1 << LayerMask.NameToLayer("MiscLevelGeometry");
        
        internal static Vector2 clampCameraDistance = new Vector2(1.5f, 5);
        internal static float targetCameraDistance = 3f;
        
        internal static ShadowCastingMode defaultShadowCastingMode = ShadowCastingMode.On;
        
        internal static RectTransform defaultControlTipLinesParent;
        internal static RectTransform customControlTipLinesParent;
        internal static TextMeshProUGUI[] customControlTipLines;
        private static Vector3 defaultControlTipLinesScale = Vector3.one;

        internal static Vector3 firstPersonCameraLocalPosition;
        internal static Quaternion firstPersonCameraLocalRotation;
        private static bool isPerformingEmote = false;

        public static bool firstPersonEmotesEnabled { get; internal set; } = false;
        public static bool allowMovingWhileEmoting { get; internal set; } = false;

        internal static bool isMovingWhileEmoting { get { return !ConfigSync.instance.syncForceDisableMovingWhileEmoting && emoteControllerLocal.IsPerformingCustomEmote() && (allowMovingWhileEmoting || emoteControllerLocal.performingEmote.canMoveWhileEmoting); } }


        //internal static Dictionary<Renderer, int> registeredLocalChildRendererRenderLayer = new Dictionary<Renderer, int>();


        /*public static void RegisterLocalObjectRendererShowWhileEmoting(Renderer renderer, int overrideRenderLayer = 6)
        {
            if (!renderer || !StartOfRound.Instance.localPlayerController)
                return;

            if (!renderer.transform.IsChildOf(StartOfRound.Instance.localPlayerController.transform))
            {
                LogError("Failed to register local object renderer for dynamic render layer during emotes. Object is not parented to the local player.");
                return;
            }
            registeredLocalChildRendererRenderLayer.Add(renderer, overrideRenderLayer);
        }

        public static void RegisterLocalObjectRendererHideWhileEmoting(Renderer renderer)
        {
            if (!renderer || !StartOfRound.Instance.localPlayerController)
                return;

            if (!renderer.transform.IsChildOf(StartOfRound.Instance.localPlayerController.transform))
            {
                LogError("Failed to register local object renderer for dynamic render layer during emotes. Object is not parented to the local player.");
                return;
            }
            registeredLocalChildRendererRenderLayer.Add(renderer, 23);
        }*/


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void InitLocalPlayerController(PlayerControllerB __instance)
        {
            gameplayCamera = __instance.gameplayCamera;
            if (!emoteCamera)
            {
                emoteCameraPivot = new GameObject("EmoteCameraPivot").transform;
                emoteCamera = new GameObject("EmoteCamera").AddComponent<Camera>();
                emoteCamera.CopyFrom(gameplayCamera);
            }

            scannedObjectsUI = GameObject.Find("Systems/UI/Canvas/ObjectScanner");
            if (!scannedObjectsUI) // Just in case the path changes in a future update or because of another mod?
                scannedObjectsUI = HUDManager.Instance.scanInfoAnimator?.transform.parent.parent.gameObject;

            defaultControlTipLinesParent = HUDManager.Instance.controlTipLines[0].transform.parent.GetComponent<RectTransform>();
            defaultControlTipLinesScale = defaultControlTipLinesParent.localScale;

            customControlTipLinesParent = GameObject.Instantiate(defaultControlTipLinesParent, defaultControlTipLinesParent.parent);
            customControlTipLinesParent.name = "ThirdPersonEmotesControlTips";
            customControlTipLinesParent.SetSiblingIndex(defaultControlTipLinesParent.GetSiblingIndex() + 1);
            customControlTipLinesParent.SetPositionAndRotation(defaultControlTipLinesParent.position, defaultControlTipLinesParent.rotation);
            customControlTipLinesParent.localScale = Vector3.zero;

            customControlTipLines = new TextMeshProUGUI[HUDManager.Instance.controlTipLines.Length];
            int index = 0;
            foreach (var element in customControlTipLinesParent.GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (element != null)
                {
                    if (element.name.ToLower().Contains("controltip"))
                        customControlTipLines[index++] = element;
                    else
                        GameObject.Destroy(element.gameObject);
                }
            }

            LoadPreferences();
            ResetCamera(); // Calling again in case corporate restructure (or another mod) is enabled and throws an error that blocks the SpawnPlayerAnimation method
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SpawnPlayerAnimation")]
        [HarmonyPostfix]
        private static void OnPlayerSpawn(PlayerControllerB __instance)
        {
            firstPersonCameraLocalPosition = localPlayerCameraContainer.transform.localPosition;
            firstPersonCameraLocalRotation = localPlayerCameraContainer.transform.localRotation;

            ResetCamera();
        }


        internal static void SavePreferences()
        {
            try
            {
                Log("Saving ThirdPersonEmoteController preferences.");
                ES3.Save("TooManyEmotes.EnableFirstPersonEmotes", firstPersonEmotesEnabled, SaveManager.TooManyEmotesSaveFileName);
                ES3.Save("TooManyEmotes.AllowMovingWhileEmoting", allowMovingWhileEmoting, SaveManager.TooManyEmotesSaveFileName);
            }
            catch (Exception e) { LogErrorVerbose("Error while trying to save TooManyEmotes ThirdPersonEmoteController preferences.\n" + e); }
        }


        internal static void LoadPreferences()
        {
            Log("Loading ThirdPersonEmoteController preferences.");

            try // I hate this block
            {
                if (ES3.KeyExists("TooManyEmotes.EnableFirstPersonEmotes"))
                    ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes");
                if (ES3.KeyExists("TooManyEmotes.AllowMovingWhileEmoting"))
                    ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting");
            }
            catch
            {
                try
                {
                    ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes");
                    ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting");
                } catch { }
            }

            try
            {
                firstPersonEmotesEnabled = ES3.Load("TooManyEmotes.EnableFirstPersonEmotes", SaveManager.TooManyEmotesSaveFileName, false);
                allowMovingWhileEmoting = ES3.Load("TooManyEmotes.AllowMovingWhileEmoting", SaveManager.TooManyEmotesSaveFileName, false);
            }
            catch (Exception e)
            {
                LogErrorVerbose("Failed to load third person emote preferences. Preferences will be reset.\n" + e);
                firstPersonEmotesEnabled = false;
                allowMovingWhileEmoting = false;
                try
                {
                    ES3.DeleteKey("TooManyEmotes.EnableFirstPersonEmotes", SaveManager.TooManyEmotesSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.AllowMovingWhileEmoting", SaveManager.TooManyEmotesSaveFileName);
                }
                catch { LogErrorVerbose("Failed to reset third person emote preferences. I recommend deleting this file: \"" + SaveManager.TooManyEmotesSaveFileName + "\" located at this path: \"C:\\Users\\YOUR_USER\\AppData\\LocalLow\\ZeekerssRBLX\\Lethal Company\""); }
            }
        }


        public static void ResetCamera()
        {
            if (!gameplayCamera || !emoteCamera || !emoteCameraPivot)
                return;

            emoteCamera.enabled = false;
            Camera activeCamera = (firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled) ? gameplayCamera : StartOfRound.Instance.activeCamera;
            StartOfRound.Instance.SwitchCamera(activeCamera);
            CallChangeAudioListenerToObject(activeCamera.gameObject);

            ReloadPlayerModel(localPlayerController);

            gameplayCamera.cullingMask &= ~(1 << 23);
            //emoteCamera.cullingMask |= (1 << 23);
            emoteCamera.cullingMask = gameplayCamera.cullingMask; // Testing
            emoteCamera.cullingMask &= ~((1 << 5) | (1 << 7)); // ui/helmet visor

            emoteCameraPivot.transform.SetParent(localPlayerController.transform);
            emoteCameraPivot.SetLocalPositionAndRotation(Vector3.up * 1.8f, Quaternion.identity);
            emoteCamera.transform.SetParent(emoteCameraPivot);
            emoteCamera.transform.SetLocalPositionAndRotation(Vector3.back * targetCameraDistance, Quaternion.identity);

            // Fix other player's cameras not seeing local player's body (maybe?)
            foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
            {
                if (playerController != null && playerController != localPlayerController && playerController.gameplayCamera != null)
                    playerController.gameplayCamera.cullingMask |= 1 << 23;
            }

            // Fix ship camera from not seeing local player's body (if I'm remembering this part correctly)
            Camera camera = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera")?.GetComponent<Camera>();
            if (camera)
                camera.cullingMask |= 1 << 23;
        }


        public static void ReloadPlayerModel(PlayerControllerB playerController)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            try
            {
                //playerController.thisPlayerModelArms.gameObject.layer = 5;

                playerController.GetComponentInChildren<LODGroup>().enabled = false;
                playerController.thisPlayerModelLOD1.gameObject.layer = 5;
                playerController.thisPlayerModelLOD1.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                playerController.thisPlayerModelLOD2.shadowCastingMode = ShadowCastingMode.Off;
                playerController.thisPlayerModelLOD2.enabled = false;
                playerController.playerBetaBadgeMesh.gameObject.layer = 5;

                playerController.thisPlayerModel.gameObject.layer = 23;
                playerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On; // defaultShadowCastingMode;
            }
            catch (Exception e)
            {
                LogError("Error while trying to reset player model for player: " + playerController.name + " Error: " + e);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPrefix]
        private static bool UseFreeCamWhileEmoting(PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || emoteControllerLocal == null)
                return true;

            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return true;

            if (emoteControllerLocal.IsPerformingCustomEmote())
            {
                if (firstPersonEmotesEnabled)
                {
                    if (StartOfRound.Instance.activeCamera != gameplayCamera)
                    {
                        StartOfRound.Instance.SwitchCamera(gameplayCamera);
                        CallChangeAudioListenerToObject(gameplayCamera.gameObject);
                        emoteCamera.enabled = false;
                        if (localPlayerController.currentlyHeldObjectServer != null)
                            localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.localItemHolder;
                    }
                    localPlayerCameraContainer.SetPositionAndRotation(localPlayerController.playerGlobalHead.position, localPlayerController.transform.rotation);
                    return isMovingWhileEmoting;
                }

                if (StartOfRound.Instance.activeCamera != emoteCamera)
                {
                    emoteCamera.enabled = true;
                    StartOfRound.Instance.SwitchCamera(emoteCamera);
                    CallChangeAudioListenerToObject(emoteCamera.gameObject);
                    if (localPlayerController.currentlyHeldObjectServer != null)
                        localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.serverItemHolder;
                }

                Vector3 targetPosition = Vector3.back * Mathf.Clamp(targetCameraDistance, clampCameraDistance.x, clampCameraDistance.y);
                emoteCamera.transform.localPosition = Vector3.Lerp(emoteCamera.transform.localPosition, targetPosition, 10 * Time.deltaTime);

                if (!localPlayerController.quickMenuManager.isMenuOpen && !EmoteMenu.isMenuOpen)
                {
                    bool canRotateModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
                    if (canRotateModifier != isMovingWhileEmoting)
                    {
                        if (emoteCameraPivot.localEulerAngles.y != 0)
                        {
                            localPlayerController.transform.localEulerAngles = new Vector3(localPlayerController.transform.localEulerAngles.x, emoteCameraPivot.transform.eulerAngles.y, localPlayerController.transform.localEulerAngles.z);
                            emoteCameraPivot.transform.localEulerAngles = new Vector3(emoteCameraPivot.localEulerAngles.x, 0, emoteCameraPivot.localEulerAngles.z);
                        }
                    }

                    if (!isMovingWhileEmoting || canRotateModifier)
                    {
                        Vector2 vector = localPlayerController.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
                        emoteCameraPivot.Rotate(new Vector3(0f, vector.x, 0f));
                        float cameraPitch = emoteCameraPivot.localEulerAngles.x - vector.y;
                        cameraPitch = (cameraPitch > 180) ? cameraPitch - 360 : cameraPitch;
                        cameraPitch = Mathf.Clamp(cameraPitch, -45, 45);
                        emoteCameraPivot.transform.localEulerAngles = new Vector3(cameraPitch, emoteCameraPivot.localEulerAngles.y, 0f);
                    }
                    else
                        emoteCameraPivot.transform.localEulerAngles = gameplayCamera.transform.localEulerAngles;

                    if (Physics.Raycast(emoteCameraPivot.position, -emoteCameraPivot.forward * targetCameraDistance, out var hit, targetCameraDistance, cameraCollideLayerMask))
                        emoteCamera.transform.localPosition = Vector3.back * Mathf.Clamp(hit.distance - 0.2f, 0, targetCameraDistance);

                    //if (!Keybinds.holdingRotatePlayerModifier && !Keybinds.toggledRotating && !isMovingWhileEmoting)
                    if (!isMovingWhileEmoting || canRotateModifier)
                        return false;
                }
            }
            return true;
        }


        internal static void OnZoomInEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !emoteControllerLocal.IsPerformingCustomEmote() || EmoteMenu.isMenuOpen || quickMenuManager.isMenuOpen || firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (isMovingWhileEmoting && !canZoomModifier)
                return;

            targetCameraDistance = Mathf.Clamp(targetCameraDistance - 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
        }


        internal static void OnZoomOutEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !emoteControllerLocal.IsPerformingCustomEmote() || EmoteMenu.isMenuOpen || quickMenuManager.isMenuOpen || firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (isMovingWhileEmoting && !canZoomModifier)
                return;

            targetCameraDistance = Mathf.Clamp(targetCameraDistance + 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
        }


        public static void OnStartCustomEmoteLocal()
        {
            Keybinds.toggledRotating = false;
            if (!firstPersonEmotesEnabled)
            {
                if (emoteCamera && !emoteCamera.enabled)
                {
                    StartOfRound.Instance.SwitchCamera(emoteCamera);
                    CallChangeAudioListenerToObject(emoteCamera.gameObject);
                    if (!isPerformingEmote)
                        emoteCameraPivot.eulerAngles = gameplayCamera.transform.eulerAngles;
                }

                // Double checking values
                localPlayerController.thisPlayerModelLOD1.gameObject.layer = 5;
                localPlayerController.thisPlayerModelLOD1.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                localPlayerController.thisPlayerModelLOD2.shadowCastingMode = ShadowCastingMode.Off;
                localPlayerController.thisPlayerModelLOD2.enabled = false;
                localPlayerController.playerBetaBadgeMesh.gameObject.layer = 5;

                localPlayerController.thisPlayerModel.gameObject.layer = 3;
                localPlayerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                localPlayerController.thisPlayerModelArms.enabled = false;

                if (scannedObjectsUI)
                    scannedObjectsUI.SetActive(false);

                if (localPlayerController.localItemHolder == localPlayerController.currentlyHeldObjectServer?.parentObject)
                    localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.serverItemHolder;

                if (AdvancedCompany_Compat.Enabled)
                    AdvancedCompany_Compat.ShowLocalCosmetics();
                else if (MoreCompany_Compat.Enabled)
                    MoreCompany_Compat.ShowLocalCosmetics();

                if (LethalVRM_Compat.Enabled)
                    LethalVRM_Compat.DisplayVRMModel();
            }

            //HUDManager.Instance.ClearControlTips();
            UpdateControlTip();
            ShowCustomControlTips(!firstPersonEmotesEnabled);

            isPerformingEmote = true;
        }


        public static void OnStopCustomEmoteLocal()
        {
            localPlayerCameraContainer.SetLocalPositionAndRotation(firstPersonCameraLocalPosition, firstPersonCameraLocalRotation);
            Keybinds.toggledRotating = false;

            if (emoteCamera)
                emoteCamera.enabled = false;

            if (StartOfRound.Instance.activeCamera != gameplayCamera)
                StartOfRound.Instance.SwitchCamera(gameplayCamera);
            if (localPlayerController.activeAudioListener != gameplayCamera.gameObject)
                CallChangeAudioListenerToObject(gameplayCamera.gameObject);

            localPlayerController.thisPlayerModel.gameObject.layer = 23;
            localPlayerController.thisPlayerModel.shadowCastingMode = defaultShadowCastingMode;
            localPlayerController.thisPlayerModelArms.enabled = true;

            if (scannedObjectsUI)
                scannedObjectsUI.SetActive(true);

            if (AdvancedCompany_Compat.Enabled)
                AdvancedCompany_Compat.HideLocalCosmetics();
            else if (MoreCompany_Compat.Enabled)
                MoreCompany_Compat.HideLocalCosmetics();

            if (LethalVRM_Compat.Enabled)
                LethalVRM_Compat.HideVRMModel();

            ShowCustomControlTips(false);

            foreach (var item in localPlayerController.ItemSlots)
            {
                if (item && item.parentObject == localPlayerController.serverItemHolder)
                    item.parentObject = localPlayerController.localItemHolder;
            }

            emoteCameraPivot.eulerAngles = localPlayerCameraContainer.eulerAngles;
            isPerformingEmote = false;
        }


        internal static void UpdateFirstPersonEmoteMode(bool value)
        {
            if (firstPersonEmotesEnabled == value)
                return;

            firstPersonEmotesEnabled = value;

            if (emoteControllerLocal.IsPerformingCustomEmote())
            {
                localPlayerController.thisPlayerModelArms.enabled = firstPersonEmotesEnabled;
                localPlayerController.thisPlayerModel.gameObject.layer = firstPersonEmotesEnabled ? 23 : 3;
                if (firstPersonEmotesEnabled)
                {
                    Keybinds.holdingRotatePlayerModifier = false;
                    Keybinds.toggledRotating = false;
                    if (AdvancedCompany_Compat.Enabled)
                        AdvancedCompany_Compat.HideLocalCosmetics();
                    else if (MoreCompany_Compat.Enabled)
                        MoreCompany_Compat.HideLocalCosmetics();

                    if (LethalVRM_Compat.Enabled)
                        LethalVRM_Compat.HideVRMModel();

                    if (scannedObjectsUI)
                        scannedObjectsUI.SetActive(false);
                }
                else
                {
                    if (AdvancedCompany_Compat.Enabled)
                        AdvancedCompany_Compat.ShowLocalCosmetics();
                    else if (MoreCompany_Compat.Enabled)
                        MoreCompany_Compat.ShowLocalCosmetics();

                    if (LethalVRM_Compat.Enabled)
                        LethalVRM_Compat.DisplayVRMModel();

                    if (scannedObjectsUI)
                        scannedObjectsUI.SetActive(true);
                }
                UpdateControlTip();
                ShowCustomControlTips(!firstPersonEmotesEnabled);
            }
        }


        internal static void SetCanMoveWhileEmoting(bool value)
        {
            if (allowMovingWhileEmoting == value)
                return;

            allowMovingWhileEmoting = value;
            if (!emoteControllerLocal.IsPerformingCustomEmote())
                return;

            if (allowMovingWhileEmoting)
                emoteCameraPivot.localEulerAngles = new Vector3(emoteCameraPivot.localEulerAngles.x, 0, emoteCameraPivot.localEulerAngles.z);

            UpdateControlTip();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
        [HarmonyPrefix]
        private static bool PreventSwappingItemsWhileEmoting(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (!emoteCamera || !emoteCamera.enabled)
                return true;

            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return true;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (__instance == localPlayerController && context.performed && emoteControllerLocal != null && emoteControllerLocal.IsPerformingCustomEmote() && (!isMovingWhileEmoting || canZoomModifier))
                return false; // Prevent swapping items while emoting
            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPostfix]
        private static void FixedNewHeldItemParent(int slot, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController)
                return;

            if (emoteControllerLocal.IsPerformingCustomEmote())
            {
                var heldItem = localPlayerController.ItemSlots[localPlayerController.currentItemSlot];
                if (heldItem)
                {
                    heldItem.parentObject = firstPersonEmotesEnabled ? localPlayerController.localItemHolder : localPlayerController.serverItemHolder;
                    if (EmoteControllerPlayer.emoteControllerLocal.emotingProps.Count > 0)
                        heldItem.EnableItemMeshes(false);
                }
            }
        }


        internal static void ShowCustomControlTips(bool show)
        {
            if (customControlTipLinesParent == null || defaultControlTipLinesParent == null)
                return;

            customControlTipLinesParent.localScale = show ? defaultControlTipLinesScale : Vector3.zero;
            defaultControlTipLinesParent.localScale = show ? Vector3.zero : defaultControlTipLinesScale;
        }


        public static void UpdateControlTip(int appendToIndex = 0)
        {
            if (!emoteControllerLocal.IsPerformingCustomEmote() || customControlTipLines == null)
                return;

            if (appendToIndex < 0 || appendToIndex >= controlTipLines.Length - 1)
                appendToIndex = 0;

            string zoomInDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomInEmoteAction);
            string zoomOutDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomOutEmoteAction);
            string rotateDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.RotatePlayerEmoteAction);
            string performNextInstrumentText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.PerformNextInstrumentAction);

            if (zoomInDisplayText == "")
                zoomInDisplayText = "Unbound";
            if (zoomOutDisplayText == "")
                zoomOutDisplayText = "Unbound";
            if (rotateDisplayText == "")
                rotateDisplayText = "Unbound";

            int index = appendToIndex;
            
            string zoomControlText;
            if ((zoomInDisplayText == "Scroll Up" || zoomInDisplayText == "Scroll Down") && (zoomOutDisplayText == "Scroll Up" || zoomOutDisplayText == "Scroll Down") && zoomInDisplayText != zoomOutDisplayText)
                zoomControlText = "[Scroll Mouse]";
            else if (zoomInDisplayText != "Unbound" || zoomOutDisplayText != "Unbound")
                zoomControlText = zoomInDisplayText + "/" + zoomOutDisplayText;
            else
                zoomControlText = "Unbound";

            customControlTipLines[index].text = "Zoom : ";
            if (isMovingWhileEmoting)
                customControlTipLines[index].text += "[" + rotateDisplayText + "] + ";
            customControlTipLines[index++].text += zoomControlText;

            customControlTipLines[index++].text = string.Format((isMovingWhileEmoting ? "Freeze" : "Rotate") + " : " + (ConfigSettings.toggleRotateCharacterInEmote.Value ? "Toggle" : "Hold") + " [{0}]", rotateDisplayText);

            if (emoteControllerLocal.isPerformingEmote && emoteControllerLocal.performingEmote.inEmoteSyncGroup && emoteControllerLocal.performingEmote.emoteSyncGroup.Count > 1)
                customControlTipLines[index++].text = string.Format("Play Next Instrument: [{0}]", performNextInstrumentText);

            for (; index < customControlTipLines.Length; index++)
                customControlTipLines[index].text = "";
        }


        public static void CallChangeAudioListenerToObject(GameObject gameObject)
        {
            if (firstPersonEmotesEnabled && gameObject != localPlayerController.gameplayCamera)
                return;

            MethodInfo method = localPlayerController.GetType().GetMethod("ChangeAudioListenerToObject", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(localPlayerController, new object[] { gameObject });
        }
    }
}
