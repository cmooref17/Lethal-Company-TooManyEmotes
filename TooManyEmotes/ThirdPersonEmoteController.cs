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
using TooManyEmotes.CompatibilityPatcher;
using MoreCompany.Cosmetics;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using System.Reflection;
using TooManyEmotes.Networking;

namespace TooManyEmotes.Patches {

    [HarmonyPatch]
    public class ThirdPersonEmoteController {

        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static GameObject playerHUDHelmetModel;
        public static Camera gameplayCamera;
        public static Camera emoteCamera;
        public static Transform emoteCameraPivot;
        public static int cameraCollideLayerMask = (1 << LayerMask.NameToLayer("Room")) | 1 << LayerMask.NameToLayer("PlaceableShipObject") | 1 << LayerMask.NameToLayer("Terrain") | 1 << LayerMask.NameToLayer("MiscLevelGeometry");

        public static Vector2 clampCameraDistance = new Vector2(1.5f, 5);
        public static float targetCameraDistance = 3f;

        public static int localPlayerBodyLayer = 0;
        public static ShadowCastingMode defaultShadowCastingMode = ShadowCastingMode.On;

        public static string[] emoteControlTipLines = new string[] { "Hold [ALT] : Rotate" };



        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitLocalPlayerController(PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return;

            gameplayCamera = __instance.gameplayCamera;
            if (emoteCamera == null)
            {
                emoteCameraPivot = new GameObject("EmoteCameraPivot").transform;
                emoteCamera = new GameObject("EmoteCamera").AddComponent<Camera>();
                emoteCamera.CopyFrom(gameplayCamera);
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SpawnPlayerAnimation")]
        [HarmonyPostfix]
        public static void OnPlayerSpawn(PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return;

            emoteCamera.enabled = false;
            StartOfRound.Instance.SwitchCamera(StartOfRound.Instance.activeCamera);

            __instance.GetComponentInChildren<LODGroup>().enabled = false;
            __instance.thisPlayerModelLOD1.gameObject.layer = 5;
            __instance.thisPlayerModelLOD1.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            __instance.thisPlayerModelLOD2.shadowCastingMode = ShadowCastingMode.Off;
            __instance.thisPlayerModelLOD2.enabled = false;

            __instance.thisPlayerModel.gameObject.layer = 23;
            __instance.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On; // defaultShadowCastingMode;

            __instance.thisPlayerModelArms.gameObject.layer = 5;
            gameplayCamera.cullingMask &= ~(1 << 23);
            emoteCamera.cullingMask |= (1 << 23);
            //emoteCamera.cullingMask |= 1 << localPlayerBodyLayer;
            emoteCamera.cullingMask &= ~((1 << 5) | (1 << 7)); // ui/helmet visor

            emoteCameraPivot.transform.parent = __instance.transform;
            emoteCameraPivot.SetLocalPositionAndRotation(Vector3.up * 1.8f, Quaternion.identity);
            emoteCamera.transform.parent = emoteCameraPivot;
            emoteCamera.transform.SetLocalPositionAndRotation(Vector3.back * targetCameraDistance, Quaternion.identity);
        }



        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPrefix]
        public static bool UseFreeCamWhileEmoting(PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return true;

            if (__instance == localPlayerController && PlayerPatcher.playerDataLocal.isPerformingEmote)
            {
                Vector3 targetPosition = Vector3.back * Mathf.Clamp(targetCameraDistance, clampCameraDistance.x, clampCameraDistance.y);
                emoteCamera.transform.localPosition = Vector3.Lerp(emoteCamera.transform.localPosition, targetPosition, 10 * Time.deltaTime);

                if (!localPlayerController.quickMenuManager.isMenuOpen && !EmoteMenuManager.isMenuOpen)
                {
                    if (Keybinds.holdingRotatePlayerModifier || Keybinds.toggledRotating)
                    {
                        if (emoteCameraPivot.localEulerAngles.y != 0)
                        {
                            localPlayerController.transform.localEulerAngles = new Vector3(localPlayerController.transform.localEulerAngles.x, emoteCameraPivot.transform.eulerAngles.y, localPlayerController.transform.localEulerAngles.z);
                            emoteCameraPivot.transform.localEulerAngles = new Vector3(emoteCameraPivot.localEulerAngles.x, 0, emoteCameraPivot.localEulerAngles.z);
                        }
                    }
                    //else
                    //{
                        Vector2 vector = localPlayerController.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
                        emoteCameraPivot.Rotate(new Vector3(0f, vector.x, 0f));
                        float cameraPitch = emoteCameraPivot.localEulerAngles.x - vector.y;
                        cameraPitch = (cameraPitch > 180) ? cameraPitch - 360 : cameraPitch;
                        cameraPitch = Mathf.Clamp(cameraPitch, -45, 45);
                        emoteCameraPivot.transform.eulerAngles = new Vector3(cameraPitch, emoteCameraPivot.eulerAngles.y, 0f);
                    //}

                    if (Physics.Raycast(emoteCameraPivot.position, -emoteCameraPivot.forward * targetCameraDistance, out var hit, targetCameraDistance, cameraCollideLayerMask))
                        emoteCamera.transform.localPosition = Vector3.back * Mathf.Clamp(hit.distance - 0.2f, 0, targetCameraDistance);

                    if (!Keybinds.holdingRotatePlayerModifier && !Keybinds.toggledRotating)
                        return false;
                }
            }
            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPostfix]
        public static void UpdateCameraPitchWhileMoving(PlayerControllerB __instance) {
            //if (ConfigSync.instance.syncEnableMovingWhileEmoting)
                //emoteCameraPivot.rotation = localPlayerController.gameplayCamera.transform.rotation;
        }
        

        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
        [HarmonyPrefix]
        public static bool AdjustCameraDistance(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || !context.performed || __instance != localPlayerController || !PlayerPatcher.playerDataLocal.isPerformingEmote)
                return true;

            if (!EmoteMenuManager.isMenuOpen)
                __instance.StartCoroutine(AdjustCameraDistanceEndOfFrame());

            return false;
        }


        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPrefix]
        public static bool CancelMovingEmote()
        {
            if (PlayerPatcher.playerDataLocal.isPerformingEmote && ConfigSync.instance.syncEnableMovingWhileEmoting)
            {
                localPlayerController.performingEmote = false;
                PlayerPatcher.OnUpdateCustomEmote(-1, localPlayerController);
                localPlayerController.StopPerformingEmoteServerRpc();
                return false;
            }
            return true;
        }


        static IEnumerator AdjustCameraDistanceEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            float value = Keybinds.RawScrollAction.ReadValue<Vector2>().y;
            if (value != 0)
            {
                float direction = value < 0 ? 1 : -1;
                targetCameraDistance = Mathf.Clamp(targetCameraDistance + direction * 0.25f, clampCameraDistance.x, clampCameraDistance.y);
            }
        }


        public static void OnStartCustomEmoteLocal()
        {
            Keybinds.toggledRotating = ConfigSync.instance.syncEnableMovingWhileEmoting;
            if (!emoteCamera.enabled)
            {
                StartOfRound.Instance.SwitchCamera(emoteCamera);
                CallChangeAudioListenerToObject(emoteCamera.gameObject);
                localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
                emoteCameraPivot.eulerAngles = gameplayCamera.transform.eulerAngles + new Vector3(0, 0, 0);
            }
            localPlayerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
            HUDManager.Instance.ClearControlTips();
            if (!ConfigSync.instance.syncEnableMovingWhileEmoting)
                HUDManager.Instance.ChangeControlTipMultiple(emoteControlTipLines);
        }


        public static void OnStopCustomEmoteLocal()
        {
            Keybinds.toggledRotating = false;
            emoteCamera.enabled = false;
            StartOfRound.Instance.SwitchCamera(gameplayCamera);
            CallChangeAudioListenerToObject(gameplayCamera.gameObject);

            localPlayerController.thisPlayerModel.shadowCastingMode = defaultShadowCastingMode;
            if (localPlayerController.currentlyHeldObjectServer != null)
                localPlayerController.currentlyHeldObjectServer.SetControlTipsForItem();
            else
                HUDManager.Instance.ClearControlTips();
        }


        public static void CallChangeAudioListenerToObject(GameObject gameObject)
        {
            MethodInfo method = localPlayerController.GetType().GetMethod("ChangeAudioListenerToObject", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(localPlayerController, new object[] { gameObject });
        }
    }
}
