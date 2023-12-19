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

namespace TooManyEmotes.Patches {

    [HarmonyPatch]
    internal class ThirdPersonEmoteController {

        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static GameObject playerHUDHelmetModel;
        public static Camera gameplayCamera;
        public static Camera emoteCamera;
        public static Transform emoteCameraPivot;
        public static float thirdPersonCameraDistance = 3f;

        static int cameraCollideLayerMask;

        public static ShadowCastingMode defaultShadowCasterMode;
        public static bool defaultShowHelmetHud;
        public static bool defaultShowArms;
        public static bool defaultShowHud;


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializeThirdPersonController(PlayerControllerB __instance) {
            gameplayCamera = __instance.gameplayCamera;
            if (emoteCamera == null)
            {
                emoteCameraPivot = new GameObject("EmoteCameraPivot").transform;
                emoteCamera = new GameObject("EmoteCamera").AddComponent<Camera>();
                emoteCamera.CopyFrom(gameplayCamera);
            }

            emoteCameraPivot.transform.parent = __instance.transform;
            emoteCameraPivot.SetLocalPositionAndRotation(Vector3.up * 1.8f, Quaternion.identity);
            emoteCamera.transform.parent = emoteCameraPivot;
            emoteCamera.transform.SetLocalPositionAndRotation(Vector3.back * thirdPersonCameraDistance, Quaternion.identity);

            //emoteCamera.cullingMask |= 1 << 23;
            //gameplayCamera.cullingMask &= ~(1 << 23);

            emoteCamera.enabled = false;
            StartOfRound.Instance.SwitchCamera(StartOfRound.Instance.activeCamera);

            cameraCollideLayerMask = (1 << LayerMask.NameToLayer("Room")) | 1 << LayerMask.NameToLayer("PlaceableShipObject") | 1 << LayerMask.NameToLayer("Terrain") | 1 << LayerMask.NameToLayer("MiscLevelGeometry");

            playerHUDHelmetModel = GameObject.Find("PlayerHUDHelmetModel");
            defaultShowHelmetHud = playerHUDHelmetModel.activeSelf;
            defaultShadowCasterMode = __instance.thisPlayerModel.shadowCastingMode;
            defaultShowArms = __instance.thisPlayerModelArms.gameObject.activeSelf && __instance.thisPlayerModelArms.enabled;
            defaultShowHud = __instance.playerHudUIContainer.gameObject.activeSelf;
        }





        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPrefix]
        public static bool UseFreeCamWhileEmoting(PlayerControllerB __instance) {
            if (__instance == localPlayerController && PlayerPatcher.performingCustomEmoteLocal && !localPlayerController.quickMenuManager.isMenuOpen)
            {
                Vector2 vector = localPlayerController.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
                emoteCameraPivot.Rotate(new Vector3(0f, vector.x, 0f));
                float cameraPitch = emoteCameraPivot.localEulerAngles.x - vector.y;
                cameraPitch = (cameraPitch > 180) ? cameraPitch - 360 : cameraPitch;
                cameraPitch = Mathf.Clamp(cameraPitch, -45, 45);
                emoteCameraPivot.transform.eulerAngles = new Vector3(cameraPitch, emoteCameraPivot.eulerAngles.y, 0f);

                RaycastHit hit;
                if (Physics.Raycast(emoteCameraPivot.position, -emoteCameraPivot.forward * thirdPersonCameraDistance, out hit, thirdPersonCameraDistance, cameraCollideLayerMask))
                    emoteCamera.transform.localPosition = Vector3.back * Mathf.Clamp(hit.distance - 0.1f, 0, thirdPersonCameraDistance);
                else
                    emoteCamera.transform.localPosition = Vector3.back * thirdPersonCameraDistance;

                return false;
            }
            return true;
        }


        public static void OnStartCustomEmoteLocal() {
            StartOfRound.Instance.SwitchCamera(emoteCamera);
            localPlayerController.playerBodyAnimator.SetInteger("emoteNumber", 1);
            localPlayerController.playerModelArmsMetarig.transform.parent.gameObject.SetActive(false);
            localPlayerController.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
            playerHUDHelmetModel.SetActive(false);
            emoteCameraPivot.eulerAngles = gameplayCamera.transform.eulerAngles + new Vector3(0, 0, 0);
            HUDManager.Instance.HideHUD(true);
            MoreCompanyPatcher.ShowCosmetics(true);
        }


        public static void OnStopCustomEmoteLocal() {
            emoteCamera.enabled = false;
            StartOfRound.Instance.SwitchCamera(gameplayCamera);
            localPlayerController.playerModelArmsMetarig.transform.parent.gameObject.SetActive(defaultShowArms);
            localPlayerController.thisPlayerModel.shadowCastingMode = defaultShadowCasterMode;
            playerHUDHelmetModel.SetActive(defaultShowHelmetHud);
            HUDManager.Instance.HideHUD(false);
            MoreCompanyPatcher.ShowCosmetics(false);
        }
    }
}
