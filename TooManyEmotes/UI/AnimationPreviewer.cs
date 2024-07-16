using GameNetcodeStuff;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TooManyEmotes.Config;
using TooManyEmotes.Compatibility;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;
using TMPro;
using Dissonance.Integrations.Unity_NFGO;
using Unity.Netcode;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using UnityEngine.Rendering.HighDefinition;

namespace TooManyEmotes.UI
{
    [HarmonyPatch]
    public static class AnimationPreviewer
    {
        public static bool enabled = false;

        public static Camera renderingCamera;
        public static GameObject previewPlayerObject;
        public static SkinnedMeshRenderer previewPlayerMesh;

        public static EmoteController simpleEmoteController;

        public static int renderLayer = 23; // EnemiesNotRendered layer
        //public static int propLayer = 6; // Prop layer
        public static int renderLayerMask { get { return (1 << renderLayer)/* | (1 << propLayer)*/; } }

        public static GameObject previewBoombox;


        public static void InitializeAnimationRenderer()
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            Log("Initializing animation renderer");
            renderingCamera = new GameObject("AnimationRenderingCamera").AddComponent<Camera>();
            GameObject.Destroy(renderingCamera.GetComponent<AudioListener>());
            renderingCamera.cullingMask = renderLayerMask;
            renderingCamera.clearFlags = CameraClearFlags.SolidColor;
            renderingCamera.cameraType = CameraType.Preview;
            renderingCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0);
            // Most of this was to try and get transparency working, but it was being stubborn. Still keeping it though
            renderingCamera.allowHDR = false;
            renderingCamera.allowMSAA = false;
            renderingCamera.farClipPlane = 5;
            renderingCamera.targetTexture = EmoteMenu.renderTexture;
            renderingCamera.transform.position = Vector3.down * 1000;
            EmoteMenu.renderTextureImageUI.texture = EmoteMenu.renderTexture;

            Light spotlight = new GameObject("Spotlight").AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.transform.SetParent(renderingCamera.transform);
            spotlight.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spotlight.intensity = 50;
            spotlight.range = 40;
            spotlight.innerSpotAngle = 100;
            spotlight.spotAngle = 120;
            spotlight.gameObject.layer = renderLayer;

            DisableRenderCameraNextFrame();
            enabled = true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializePlayerCloneRenderObject(PlayerControllerB __instance)
        {
            if (!enabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            IEnumerator InitPlayerCloneAfterSpawnAnimation()
            {
                yield return new WaitForSeconds(2);

                Assert(renderingCamera != null, "Render camera null!");

                previewPlayerObject = GameObject.Instantiate(__instance.gameObject, renderingCamera.transform);
                previewPlayerObject.name = "PreviewPlayerAnimationObject";
                previewPlayerObject.transform.localPosition = new Vector3(0, -1.25f, 3);
                previewPlayerObject.transform.localEulerAngles = new Vector3(0, 180, 0);

                GameObject modelGameObject = previewPlayerObject.transform.Find("ScavengerModel").gameObject;
                GameObject metarigGameObject = modelGameObject.transform.Find("metarig").gameObject;
                PlayerControllerB copyPlayerController = previewPlayerObject.GetComponentInChildren<PlayerControllerB>();
                copyPlayerController.thisPlayerModel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                previewPlayerMesh = copyPlayerController.thisPlayerModel;
                GameObject.Destroy(modelGameObject.GetComponentInChildren<LODGroup>());
                GameObject.Destroy(metarigGameObject.GetComponentInChildren<RigBuilder>());
                GameObject.Destroy(metarigGameObject.GetComponentInChildren<GraphicRaycaster>());
                GameObject.Destroy(metarigGameObject.GetComponentInChildren<TMP_Text>());
                GameObject.Destroy(copyPlayerController.playerBodyAnimator);
                GameObject.Destroy(previewPlayerObject.GetComponent<NfgoPlayer>());

                // It's brute force, but w/e
                foreach (Transform child in previewPlayerObject.transform)
                    if (child.name != "ScavengerModel")
                        GameObject.Destroy(child.gameObject);

                foreach (Transform child in modelGameObject.transform)
                    if (child.name != "LOD1" && child.name != "metarig")
                        GameObject.Destroy(child.gameObject);

                foreach (Transform child in metarigGameObject.transform)
                    if (child.name != "spine")
                        GameObject.Destroy(child.gameObject);

                // Temp fix for a few errors
                foreach (var additionalLightData in previewPlayerObject.GetComponentsInChildren<HDAdditionalLightData>())
                    GameObject.Destroy(additionalLightData);

                foreach (var component in previewPlayerObject.GetComponentsInChildren<Component>())
                {
                    if (component is Transform || component is SkinnedMeshRenderer || component is MeshFilter || component is Animator)
                        continue;

                    GameObject.Destroy(component);
                }

                simpleEmoteController = previewPlayerObject.AddComponent<EmoteController>();
                simpleEmoteController.Initialize();
                simpleEmoteController.CreateBoneMap(EmoteControllerPlayer.sourceBoneNames);
                simpleEmoteController.smoothTransitionToEmote = false;

                GameObject boomboxPrefab = null;
                if (allItems == null)
                    LogError("AllItemsList null!");
                else
                {
                    foreach (var item in allItems)
                    {
                        if (item.itemName.ToLower() == "boombox")
                        {
                            boomboxPrefab = item.spawnPrefab;
                            break;
                        }
                    }
                }

                if (boomboxPrefab)
                {
                    previewBoombox = GameObject.Instantiate(boomboxPrefab, previewPlayerObject.transform);
                    previewBoombox.name = "BoomboxAudioIndicator";
                    previewBoombox.transform.localPosition = new Vector3(-1, 0.2f, -0.5f);
                    previewBoombox.transform.localEulerAngles = new Vector3(0, 40, 90);

                    GameObject.Destroy(previewBoombox.GetComponentInChildren<GrabbableObject>());
                    GameObject.Destroy(previewBoombox.GetComponentInChildren<NetworkObject>());
                    GameObject.Destroy(previewBoombox.GetComponentInChildren<OccludeAudio>());
                    foreach (var component in previewBoombox.GetComponentsInChildren<Component>())
                    {
                        if (!(component is Transform || component is Renderer || component is MeshFilter))
                            GameObject.Destroy(component);
                    }
                }

                yield return null;

                if (MoreCompany_Patcher.Enabled)
                    MoreCompany_Patcher.ShowLocalCosmetics(metarigGameObject.transform);

                SetObjectLayerRecursive(previewPlayerObject, renderLayer);
            }

            if (!Plugin.radialMenuPrefab)
                return;

            __instance.StartCoroutine(InitPlayerCloneAfterSpawnAnimation());
        }


        public static void UpdatePlayerSuit()
        {
            if (previewPlayerMesh != null && localPlayerController?.thisPlayerModel != null)
                previewPlayerMesh.material = localPlayerController.thisPlayerModel.material;
        }


        public static void SetPreviewAnimation(UnlockableEmote emote)
        {
            if (!enabled || !previewPlayerObject || !simpleEmoteController)
                return;

            if (enabled && emote != null)
            {
                previewPlayerObject.SetActive(true);
                renderingCamera.enabled = true;
                if (previewBoombox)
                    previewBoombox.SetActive(emote.hasAudio && emote.isBoomboxAudio);

                simpleEmoteController.PerformEmote(emote);
                if (simpleEmoteController.emotingProps != null)
                {
                    foreach (var emoteProp in simpleEmoteController.emotingProps)
                        emoteProp.SetPropLayer(renderLayer);
                }
            }
            else
            {
                simpleEmoteController.StopPerformingEmote();
                previewPlayerObject.SetActive(false);
                if (previewBoombox)
                    previewBoombox.SetActive(false);
                DisableRenderCameraNextFrame();
            }
        }


        private static void DisableRenderCameraNextFrame()
        {
            IEnumerator DisableRenderCameraNextFrameCoroutine()
            {
                yield return null;
                renderingCamera.enabled = false;
            }

            HUDManager.Instance.StartCoroutine(DisableRenderCameraNextFrameCoroutine());
        }


        private static void SetObjectLayerRecursive(GameObject obj, int layer)
        {
            if (!obj) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
                SetObjectLayerRecursive(obj.transform.GetChild(i)?.gameObject, layer);
        }
    }
}