using GameNetcodeStuff;
using HarmonyLib;
using System.Diagnostics.Eventing.Reader;
using UnityEngine;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

/*namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    internal static class ScanNodePatcher
    {
        private static Vector3 originalCameraPosition;
        private static Quaternion originalCameraRotation;

        [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
        [HarmonyPrefix]
        private static void OnUpdateScanNodesPrefix(PlayerControllerB playerScript)
        {
            if (playerScript == localPlayerController && emoteControllerLocal.IsPerformingCustomEmote() && !ThirdPersonEmoteController.firstPersonEmotesEnabled && playerScript.gameplayCamera != null)
            {
                originalCameraPosition = playerScript.gameplayCamera.transform.position;
                originalCameraRotation = playerScript.gameplayCamera.transform.rotation;
                playerScript.gameplayCamera.transform.position = ThirdPersonEmoteController.emoteCamera.transform.position;
                playerScript.gameplayCamera.transform.rotation = ThirdPersonEmoteController.emoteCamera.transform.rotation;
            }
        }


        [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
        [HarmonyPostfix]
        private static void OnUpdateScanNodesPostfix(PlayerControllerB playerScript)
        {
            if (playerScript == localPlayerController && emoteControllerLocal.IsPerformingCustomEmote() && !ThirdPersonEmoteController.firstPersonEmotesEnabled && playerScript.gameplayCamera != null)
            {
                playerScript.gameplayCamera.transform.position = originalCameraPosition;
                playerScript.gameplayCamera.transform.rotation = originalCameraRotation;
            }
        }
    }
}*/