using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;
using MoreCompany.Cosmetics;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using System.Reflection;
using TooManyEmotes.Networking;
using TooManyEmotes.Compatibility;
using TooManyEmotes.UI;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class ThirdPersonEmoteController
    {
        public static Transform localPlayerCameraContainer { get { return localPlayerController?.cameraContainerTransform; } }

        public static GameObject playerHUDHelmetModel;
        public static Camera gameplayCamera;
        //public static Transform gameplayCameraContainer { get { return gameplayCamera?.transform.parent; } }
        public static Camera emoteCamera;
        public static Transform emoteCameraPivot;
        public static int cameraCollideLayerMask = /*1 << LayerMask.NameToLayer("Default") |*/ 1 << LayerMask.NameToLayer("Room") | 1 << LayerMask.NameToLayer("PlaceableShipObject") | 1 << LayerMask.NameToLayer("Terrain") | 1 << LayerMask.NameToLayer("MiscLevelGeometry");

        public static Vector2 clampCameraDistance = new Vector2(1.5f, 5);
        public static float targetCameraDistance = 3f;

        public static int localPlayerBodyLayer = 0;
        public static ShadowCastingMode defaultShadowCastingMode = ShadowCastingMode.On;

        public static string[] emoteControlTipLines = new string[] { "Hold [ALT] : Rotate", "[Mouse Scroll] : Zoom" };

        public static Vector3 firstPersonCameraLocalPosition;
        public static Quaternion firstPersonCameraLocalRotation;
        private static bool isPerformingEmote = false;

        public static bool firstPersonEmotesEnabled { get; internal set; } = false;
        public static bool allowMovingWhileEmoting { get; internal set; } = false;

        //private static bool isMovingWhileEmoting { get { return emoteControllerLocal.IsPerformingCustomEmote() && (ConfigSync.instance.syncEnableMovingWhileEmoting || emoteControllerLocal.performingEmote.canMoveWhileEmoting); } }
        internal static bool isMovingWhileEmoting { get { return emoteControllerLocal.IsPerformingCustomEmote() && (allowMovingWhileEmoting || emoteControllerLocal.performingEmote.canMoveWhileEmoting); } }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitLocalPlayerController(PlayerControllerB __instance)
        {
            gameplayCamera = __instance.gameplayCamera;
            if (!emoteCamera)
            {
                emoteCameraPivot = new GameObject("EmoteCameraPivot").transform;
                emoteCamera = new GameObject("EmoteCamera").AddComponent<Camera>();
                emoteCamera.CopyFrom(gameplayCamera);
            }
            LoadPreferences();
            ResetCamera(); // Calling again in case corporate restructure (or another mod) is enabled and throws an error that blocks the SpawnPlayerAnimation method
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SpawnPlayerAnimation")]
        [HarmonyPostfix]
        public static void OnPlayerSpawn(PlayerControllerB __instance)
        {
            firstPersonCameraLocalPosition = localPlayerCameraContainer.transform.localPosition;
            firstPersonCameraLocalRotation = localPlayerCameraContainer.transform.localRotation;

            ResetCamera();
        }


        internal static void SavePreferences()
        {
            Log("Saving ThirdPersonEmoteController preferences.");
            ES3.Save("TooManyEmotes.EnableFirstPersonEmotes", firstPersonEmotesEnabled);
            ES3.Save("TooManyEmotes.AllowMovingWhileEmoting", allowMovingWhileEmoting);
        }


        internal static void LoadPreferences()
        {
            Log("Loading ThirdPersonEmoteController preferences.");
            firstPersonEmotesEnabled = ES3.Load("TooManyEmotes.EnableFirstPersonEmotes", false);
            allowMovingWhileEmoting = ES3.Load("TooManyEmotes.AllowMovingWhileEmoting", false);
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
            emoteCamera.cullingMask |= (1 << 23);
            //emoteCamera.cullingMask |= 1 << localPlayerBodyLayer;
            emoteCamera.cullingMask &= ~((1 << 5) | (1 << 7) | (1 << 22)); // ui/helmet visor/scan nodes

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
                playerController.GetComponentInChildren<LODGroup>().enabled = false;
                playerController.thisPlayerModelLOD1.gameObject.layer = 5;
                playerController.thisPlayerModelLOD1.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                playerController.thisPlayerModelLOD2.shadowCastingMode = ShadowCastingMode.Off;
                playerController.thisPlayerModelLOD2.enabled = false;
            
                playerController.thisPlayerModel.gameObject.layer = 23;
                playerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On; // defaultShadowCastingMode;
                playerController.thisPlayerModelArms.gameObject.layer = 5;
            }
            catch (Exception e)
            {
                LogError("Error while trying to reset player model for player: " + playerController.name + " Error: " + e);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPrefix]
        public static bool UseFreeCamWhileEmoting(PlayerControllerB __instance)
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
                    //localPlayerCameraContainer.SetPositionAndRotation(localPlayerController.playerGlobalHead.position, localPlayerController.transform.rotation);
                    return false;
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
                    //if (Keybinds.holdingRotatePlayerModifier || Keybinds.toggledRotating || isMovingWhileEmoting)
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

                    //TODO animate the cameracontainer again in the emotecontrollerplayer class

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
            if (localPlayerController == null || !emoteControllerLocal.IsPerformingCustomEmote() || firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (isMovingWhileEmoting && !canZoomModifier)
                return;

            targetCameraDistance = Mathf.Clamp(targetCameraDistance - 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
        }


        internal static void OnZoomOutEmote(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !emoteControllerLocal.IsPerformingCustomEmote() || firstPersonEmotesEnabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (isMovingWhileEmoting && !canZoomModifier)
                return;

            targetCameraDistance = Mathf.Clamp(targetCameraDistance + 0.25f, clampCameraDistance[0], clampCameraDistance[1]);
        }


        /*internal static void UpdateFirstPersonEmoteMode(bool value)
        {
            if (firstPersonEmotesEnabled == value)
                return;

            firstPersonEmotesEnabled = value;
            if (!emoteControllerLocal.IsPerformingCustomEmote())
                return;

            if (firstPersonEmotesEnabled)
                gameplayCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            CallChangeAudioListenerToObject(firstPersonEmotesEnabled ? gameplayCameraContainer.gameObject : emoteCamera.gameObject);
            UpdateControlTip();
        }*/


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
        public static bool PreventSwappingItemsWhileEmoting(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (!emoteCamera || !emoteCamera.enabled)
                return true;

            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return true;

            bool canZoomModifier = ConfigSettings.toggleRotateCharacterInEmote.Value ? Keybinds.toggledRotating : Keybinds.holdingRotatePlayerModifier;
            if (__instance == localPlayerController && context.performed && emoteControllerLocal != null && emoteControllerLocal.IsPerformingCustomEmote() && (!isMovingWhileEmoting || canZoomModifier))
            {
                //if (!EmoteMenuManager.isMenuOpen && !firstPersonEmotesEnabled)
                    //__instance.StartCoroutine(AdjustCameraDistanceEndOfFrame());

                return false; // Prevent swapping items while emoting
            }
            return true;
        }

        /*
        private static IEnumerator AdjustCameraDistanceEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            float value = Keybinds.RawScrollAction.ReadValue<Vector2>().y;
            if (value != 0)
            {
                float direction = value < 0 ? 1 : -1;
                targetCameraDistance = Mathf.Clamp(targetCameraDistance + direction * 0.25f, clampCameraDistance.x, clampCameraDistance.y);
            }
        }
        */

        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPostfix]
        public static void FixedNewHeldItemParent(int slot, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || !emoteControllerLocal.IsPerformingCustomEmote())
                return;

            var heldItem = localPlayerController.ItemSlots[localPlayerController.currentItemSlot];
            if (!heldItem)
                return;

            heldItem.parentObject = localPlayerController.serverItemHolder;
        }


        public static void OnStartCustomEmoteLocal()
        {
            Keybinds.toggledRotating = isMovingWhileEmoting;

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

                localPlayerController.thisPlayerModel.gameObject.layer = 23;
                localPlayerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;

                if (localPlayerController.localItemHolder == localPlayerController.currentlyHeldObjectServer?.parentObject)
                    localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.serverItemHolder;
            }

            HUDManager.Instance.ClearControlTips();
            UpdateControlTip();

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

            localPlayerController.thisPlayerModel.shadowCastingMode = defaultShadowCastingMode;
            if (localPlayerController.currentlyHeldObjectServer != null)
                localPlayerController.currentlyHeldObjectServer.SetControlTipsForItem();
            else
                HUDManager.Instance.ClearControlTips();

            if (localPlayerController.serverItemHolder == localPlayerController.currentlyHeldObjectServer?.parentObject)
                localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.localItemHolder;

            localPlayerController.StartCoroutine(ResetCameraTransformEndOfFrame());
        }


        private static IEnumerator ResetCameraTransformEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!EmoteControllerPlayer.emoteControllerLocal.IsPerformingCustomEmote())
            {
                emoteCameraPivot.eulerAngles = localPlayerCameraContainer.eulerAngles;
                isPerformingEmote = false;
            }
        }


        /*private static void OnUpdateVanillaControlTip()
        {
            if (!emoteControllerLocal.IsPerformingCustomEmote() || firstPersonEmotesEnabled || emoteControlTipLines == null)
                return;

            bool missingZoomTip = true;
            bool missingRotateTip = true;
            int firstEmptyIndex = -1;
            for (int i = 0; i < emoteControlTipLines.Length; i++)
            {
                if (!missingZoomTip && !missingRotateTip)
                    break;
                if (!string.IsNullOrEmpty(emoteControlTipLines[i]))
                {
                    if (emoteControlTipLines[i].StartsWith("Zoom: "))
                        missingZoomTip = false;
                    else if (emoteControlTipLines[i].Contains("Rotate: "))
                        missingRotateTip = false;
                }
                else if (firstEmptyIndex < 0)
                    firstEmptyIndex = i;
            }

            if (missingZoomTip && missingRotateTip && firstEmptyIndex <= controlTipLines.Length - 2)
                UpdateControlTip(firstEmptyIndex);
        }*/


        public static void UpdateControlTip(int appendToIndex = 0)
        {
            if (!emoteControllerLocal.IsPerformingCustomEmote() || firstPersonEmotesEnabled || emoteControlTipLines == null)
                return;

            if (appendToIndex < 0 || appendToIndex >= controlTipLines.Length - 1)
                appendToIndex = 0;
            /*if (allowMovingWhileEmoting)
            {
                HUDManager.Instance.ClearControlTips();
                return; // For now until I can finish updating the control tips while the allow emoting while moving setting is enabled
            }*/

            string zoomInDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomInEmoteAction);
            string zoomOutDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.ZoomOutEmoteAction);
            string rotateDisplayText = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.RotatePlayerEmoteAction);

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

            emoteControlTipLines[index] = "Zoom : ";
            if (allowMovingWhileEmoting)
                emoteControlTipLines[index] += "[" + rotateDisplayText + "] + ";
            emoteControlTipLines[index++] += zoomControlText;

            //if (!ConfigSync.instance.syncEnableMovingWhileEmoting)
            emoteControlTipLines[index++] = string.Format((allowMovingWhileEmoting ? "Freeze" : "Rotate") + " : " + (ConfigSettings.toggleRotateCharacterInEmote.Value ? "Toggle" : "Hold") + " [{0}]", rotateDisplayText);

            for (; index < emoteControlTipLines.Length; index++)
                emoteControlTipLines[index] = "";

            if (emoteCamera.enabled && emoteControllerLocal != null && emoteControllerLocal.IsPerformingCustomEmote())
                HUDManager.Instance.ChangeControlTipMultiple(emoteControlTipLines);
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
