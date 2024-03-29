﻿using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TooManyEmotes.Config;
using TooManyEmotes.Compatibility;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;
using TMPro;
using Dissonance.Integrations.Unity_NFGO;
using UnityEngine.Rendering.HighDefinition;
using Unity.Netcode;

namespace TooManyEmotes.UI
{
    [HarmonyPatch]
    public static class AnimationPreviewer
    {
        public static bool enabled = false;
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static Camera renderingCamera;
        public static GameObject previewPlayerObject;
        public static SkinnedMeshRenderer previewPlayerMesh;

        public static EmoteController simpleEmoteController;

        public static int playerLayer = LayerMask.NameToLayer("Player");
        public static int playerLayerMask { get { return 1 << playerLayer; } }

        public static GameObject previewBoombox;


        public static void UpdatePlayerSuit()
        {
            if (previewPlayerMesh != null)
                previewPlayerMesh.material  = localPlayerController.thisPlayerModel.material;
        }


        public static void SetPreviewAnimation(UnlockableEmote emote)
        {
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
                        emoteProp.SetPropLayer(3);
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


        public static void DisableRenderCameraNextFrame()
        {
            IEnumerator DisableRenderCameraNextFrameCoroutine()
            {
                yield return null;
                renderingCamera.enabled = false;
            }

            HUDManager.Instance.StartCoroutine(DisableRenderCameraNextFrameCoroutine());
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializePlayerCloneRenderObject(PlayerControllerB __instance)
        {
            if (!enabled || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return;

            IEnumerator InitPlayerCloneAfterSpawnAnimation()
            {
                yield return new WaitForSeconds(2);
                previewPlayerObject = GameObject.Instantiate(__instance.gameObject, renderingCamera.transform);
                previewPlayerObject.name = "PreviewPlayerAnimationObject";
                previewPlayerObject.transform.localPosition = new Vector3(0, -1.25f, 3);
                previewPlayerObject.transform.localEulerAngles = new Vector3(0, 180, 0);

                GameObject modelGameObject = previewPlayerObject.transform.Find("ScavengerModel").gameObject;
                GameObject metarigGameObject = modelGameObject.transform.Find("metarig").gameObject;
                PlayerControllerB copyPlayerController = previewPlayerObject.GetComponentInChildren<PlayerControllerB>();
                copyPlayerController.thisPlayerModel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                previewPlayerMesh = copyPlayerController.thisPlayerModel;
                GameObject.DestroyImmediate(modelGameObject.GetComponentInChildren<LODGroup>());
                GameObject.DestroyImmediate(metarigGameObject.GetComponentInChildren<RigBuilder>());
                GameObject.DestroyImmediate(metarigGameObject.GetComponentInChildren<GraphicRaycaster>());
                GameObject.DestroyImmediate(metarigGameObject.GetComponentInChildren<TMP_Text>());
                GameObject.DestroyImmediate(copyPlayerController.playerBodyAnimator);
                GameObject.DestroyImmediate(previewPlayerObject.GetComponent<NfgoPlayer>());

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

                List<Component> destroyComponents = new List<Component>(previewPlayerObject.GetComponentsInChildren<HDAdditionalLightData>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<HDAdditionalCameraData>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<AudioReverbFilter>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<OccludeAudio>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<AudioLowPassFilter>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<AudioHighPassFilter>());
                destroyComponents.AddRange(previewPlayerObject.GetComponentsInChildren<AudioChorusFilter>());
                foreach (var component in destroyComponents)
                    GameObject.DestroyImmediate(component);

                foreach (Component component in previewPlayerObject.GetComponentsInChildren<Component>())
                {
                    if (component is Transform || component is SkinnedMeshRenderer || component is MeshFilter || component is Animator)
                        continue;
                    try { GameObject.DestroyImmediate(component); }
                    catch { Plugin.LogError("Failed to destroy component of type: " + component.GetType().ToString() + " on animation previewer object."); }
                }

                simpleEmoteController = previewPlayerObject.AddComponent<EmoteController>();
                simpleEmoteController.Initialize();
                simpleEmoteController.CreateBoneMap(EmoteControllerPlayer.sourceBoneNames);

                GameObject boomboxPrefab = null;
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    if (item.itemName == "Boombox")
                    {
                        boomboxPrefab = item.spawnPrefab;
                        break;
                    }
                }

                if (boomboxPrefab)
                {
                    previewBoombox = GameObject.Instantiate(boomboxPrefab, previewPlayerObject.transform);
                    previewBoombox.name = "BoomboxAudioIndicator";
                    previewBoombox.transform.localPosition = new Vector3(-1, 0.2f, -0.5f);
                    previewBoombox.transform.localEulerAngles = new Vector3(0, 40, 90);

                    GameObject.DestroyImmediate(previewBoombox.GetComponentInChildren<GrabbableObject>());
                    GameObject.DestroyImmediate(previewBoombox.GetComponentInChildren<NetworkObject>());
                    GameObject.DestroyImmediate(previewBoombox.GetComponentInChildren<OccludeAudio>());
                    foreach (var component in previewBoombox.GetComponentsInChildren<Component>())
                    {
                        if (!(component is Transform || component is Renderer || component is MeshFilter))
                            GameObject.DestroyImmediate(component);
                    }
                }
                
                SetObjectLayerRecursive(previewPlayerObject, playerLayer);
            }

            if (Plugin.radialMenuPrefab == null)
                return;

            __instance.StartCoroutine(InitPlayerCloneAfterSpawnAnimation());
        }


        static void SetObjectLayerRecursive(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
                SetObjectLayerRecursive(obj.transform.GetChild(i)?.gameObject, layer);
        }


        public static void InitializeAnimationRenderer()
        {
            renderingCamera = new GameObject("AnimationRenderingCamera").AddComponent<Camera>();
            GameObject.Destroy(renderingCamera.GetComponent<AudioListener>());
            renderingCamera.cullingMask = playerLayerMask;
            renderingCamera.clearFlags = CameraClearFlags.SolidColor;
            renderingCamera.cameraType = CameraType.Preview;
            renderingCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0);
            // Most of this was to try and get transparency working, but it was being stubborn. Still keeping it though
            renderingCamera.allowHDR = false;
            renderingCamera.allowMSAA = false;
            renderingCamera.farClipPlane = 5;
            renderingCamera.targetTexture = EmoteMenuManager.renderTexture;
            renderingCamera.transform.position = Vector3.down * 1000;
            EmoteMenuManager.renderTextureImageUI.texture = EmoteMenuManager.renderTexture;

            Light spotlight = new GameObject("Spotlight").AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.transform.parent = renderingCamera.transform;
            spotlight.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spotlight.intensity = 50;
            spotlight.range = 40;
            spotlight.innerSpotAngle = 100;
            spotlight.spotAngle = 120;
            spotlight.gameObject.layer = playerLayer;

            DisableRenderCameraNextFrame();
            enabled = true;
        }
    }
}
