using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Animations.Rigging;
using Unity.Mathematics;
using UnityEngine.Windows;
using TooManyEmotes.Patches;
using System.Collections;
using Dissonance.Integrations.Unity_NFGO;
using System.Runtime.CompilerServices;

namespace TooManyEmotes {

    [HarmonyPatch]
    public class EmoteMenuManager {

        public static QuickMenuManager quickMenuManager { get { return StartOfRound.Instance?.localPlayerController?.quickMenuManager; } }
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static GameObject menuGameObject;
        public static RectTransform menuTransform;
        public static CanvasGroup canvasGroup;
        public static RawImage renderTextureImageUI;

        public static RenderTexture renderTexture;
        public static Camera renderingCamera;

        public static GameObject previewPlayerObject;
        public static Animator previewPlayerAnimator;
        public static AnimatorOverrideController previewPlayerAnimatorController;
        public static int playerLayer = LayerMask.NameToLayer("Player");
        public static int playerLayerMask { get { return 1 << playerLayer; } }

        public static Color colorUnhovered = new Color(0.05f, 0.05f, 0.05f, 0.5f);
        public static Color colorHovered = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        public static List<EmoteUIElement> emoteUIElementsList;
        public static int hoveredEmoteIndex = 0;
        public static UnlockableEmote previewingEmote { get { return hoveredEmoteIndex >= 0 && hoveredEmoteIndex < StartOfRoundPatcher.currentEmoteLoadout.Length ? StartOfRoundPatcher.currentEmoteLoadout[hoveredEmoteIndex] : null; } }


        [HarmonyPatch(typeof(HUDManager), "Awake")]
        [HarmonyPostfix]
        public static void InitializeUI(HUDManager __instance) {
            if (Plugin.radialMenuPrefab == null)
                return;

            menuGameObject = GameObject.Instantiate(Plugin.radialMenuPrefab, __instance.HUDContainer.transform.parent);
            menuGameObject.transform.SetAsLastSibling();
            menuGameObject.name = "EmotesRadialMenu";
            menuTransform = menuGameObject.GetComponent<RectTransform>();
            renderTextureImageUI = menuGameObject.GetComponentInChildren<RawImage>();
            Transform emoteUIElementsParent = menuTransform.Find("RadialElements").transform;
            emoteUIElementsList = new List<EmoteUIElement>();

            int i = 0;
            for (i = 0; i < emoteUIElementsParent.childCount; i++)
            {
                Transform emoteUIObject = emoteUIElementsParent.GetChild(i);
                EmoteUIElement emoteUIElement = new EmoteUIElement {
                    uiGameObject = emoteUIObject.gameObject,
                    id = i,
                    backgroundImage = emoteUIObject.GetComponentInChildren<Image>(),
                    textContainer = emoteUIObject.GetComponentInChildren<TextMeshPro>()
                };
                emoteUIElement.backgroundImage.color = colorUnhovered;
                emoteUIElementsList.Add(emoteUIElement);
            }

            InitializeAnimationRenderer();
            menuGameObject.SetActive(false);
        }


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void GetInput()
        {
            if (previewPlayerAnimatorController == null || !isMenuOpen)
                return;


            Vector2 mousePosition = Mouse.current.position.ReadValue();

            Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
            Vector2 direction = mousePosition - screenCenter;
            int emoteIndex = -1;
            if (direction.magnitude / Screen.height >= 0.18f)
            {
                float angle = Mathf.Atan2(direction.y, -direction.x) * Mathf.Rad2Deg - 67.5f;
                if (angle < 0) angle += 360;
                emoteIndex = Mathf.FloorToInt(angle / 45);
            }

            if (emoteIndex != hoveredEmoteIndex)
                OnHoveredNewElement(emoteIndex);
        }


        public static void OnHoveredNewElement(int index)
        {
            Plugin.Log("OnHoveredNewIndex: " + index);
            if (hoveredEmoteIndex != -1)
                emoteUIElementsList[hoveredEmoteIndex].OnHover(false);
            if (index != -1)
                emoteUIElementsList[index].OnHover(true);
            hoveredEmoteIndex = index;
            SetPreviewAnimation(hoveredEmoteIndex);
        }


        public static void SetPreviewAnimation(int index)
        {
            if (index >= 0 && index < StartOfRoundPatcher.currentEmoteLoadout.Length && StartOfRoundPatcher.currentEmoteLoadout[index] != null)
            {
                UnlockableEmote emote = StartOfRoundPatcher.currentEmoteLoadout[index];
                Plugin.Log("Setting preview emote to: " + emote.emoteName);
                previewPlayerObject.SetActive(true);
                renderingCamera.enabled = true;

                previewPlayerAnimatorController["EmoteStart"] = emote.animationClip;
                previewPlayerAnimatorController["EmoteLoop"] = emote.transitionsToClip != null ? emote.transitionsToClip : null;
                previewPlayerAnimator.SetBool("hasTransition", emote.transitionsToClip != null);
                previewPlayerAnimator.Play("EmoteStart", 0, 0);
            }
            else
            {
                Plugin.Log("Stopping preview emote");
                previewPlayerAnimatorController["EmoteStart"] = null;
                previewPlayerAnimatorController["EmoteLoop"] = null;
                previewPlayerObject.SetActive(false);
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


        public static bool isMenuOpen { get { return menuGameObject.activeSelf; } }
        public static void ToggleEmoteMenu()
        {
            if (!isMenuOpen)
                OpenEmoteMenu();
            else
                CloseEmoteMenu();
        }
        public static void OpenEmoteMenu()
        {
            menuGameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            quickMenuManager.isMenuOpen = true;
            for (int i = 0; i < emoteUIElementsList.Count; i++)
            {
                var emoteUI = emoteUIElementsList[i];
                emoteUI.textContainer.text = "";
                if (i < StartOfRoundPatcher.currentEmoteLoadout.Length)
                {
                    UnlockableEmote emote = StartOfRoundPatcher.currentEmoteLoadout[i];
                    if (emote != null)
                        emoteUI.textContainer.text = emote.displayName;
                }
            }
        }
        public static void CloseEmoteMenu()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            localPlayerController.isFreeCamera = false;
            menuGameObject.SetActive(false);
            quickMenuManager.CloseQuickMenu();
        }


        public static bool CanOpenEmoteMenu()
        {
            if ((quickMenuManager.isMenuOpen && !isMenuOpen) || localPlayerController.inTerminalMenu || localPlayerController.isTypingChat || localPlayerController.isPlayerDead || localPlayerController.inSpecialInteractAnimation || localPlayerController.inShockingMinigame || localPlayerController.isClimbingLadder || localPlayerController.isSinking)
                return false;
            return true;
        }





        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializePlayerCloneRenderObject(PlayerControllerB __instance) {
            if (Plugin.radialMenuPrefab == null)
                return;

            previewPlayerObject = GameObject.Instantiate(__instance.gameObject);
            GameObject modelGameObject = previewPlayerObject.transform.Find("ScavengerModel").gameObject;
            GameObject metarigGameObject = modelGameObject.transform.Find("metarig").gameObject;
            PlayerControllerB copyPlayerController = previewPlayerObject.GetComponentInChildren<PlayerControllerB>();
            copyPlayerController.thisPlayerModel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Material copyPlayerObjectMaterial = new Material(copyPlayerController.thisPlayerModel.material);
            copyPlayerObjectMaterial.shader = Shader.Find("Unlit/Texture");
            copyPlayerController.thisPlayerModel.material = copyPlayerObjectMaterial;

            GameObject.Destroy(modelGameObject.GetComponentInChildren<LODGroup>());
            GameObject.DestroyImmediate(metarigGameObject.GetComponentInChildren<RigBuilder>());
            GameObject.DestroyImmediate(copyPlayerController.playerBodyAnimator);

            previewPlayerAnimator = metarigGameObject.AddComponent<Animator>();
            previewPlayerAnimatorController = new AnimatorOverrideController(Plugin.previewAnimatorController);
            previewPlayerAnimator.runtimeAnimatorController = previewPlayerAnimatorController;

            previewPlayerAnimator.Play("EmoteStart", 0, 0);

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

            copyPlayerController.thisPlayerModel.gameObject.layer = playerLayer;

            foreach (MonoBehaviour script in previewPlayerObject.GetComponents<MonoBehaviour>())
                GameObject.Destroy(script);

            previewPlayerObject.transform.position = renderingCamera.transform.position + renderingCamera.transform.forward * 2.8f + Vector3.down * 1.35f;
        }


        static void InitializeAnimationRenderer() {
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
            renderTexture = new RenderTexture(1024, 1024, 24);
            renderTexture.format = RenderTextureFormat.ARGB32;
            renderTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            renderTexture.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D24_UNorm_S8_UInt;
            renderingCamera.targetTexture = renderTexture;
            renderingCamera.transform.position = Vector3.up;
            renderTextureImageUI.texture = renderTexture;

            DisableRenderCameraNextFrame();
        }




        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPrefix]
        public static bool OnOpenQuickMenu()
        {
            if (isMenuOpen)
            {
                CloseEmoteMenu();
                return false;
            }
            return true;
        }


        [HarmonyPatch(typeof(QuickMenuManager), "CloseQuickMenu")]
        [HarmonyPostfix]
        public static void OnCloseQuickMenu()
        {
            if (isMenuOpen)
                CloseEmoteMenu();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPostfix]
        public static void OnLocalPlayerDeath(Vector3 bodyVelocity, PlayerControllerB __instance)
        {
            if (__instance == localPlayerController && isMenuOpen && __instance.isPlayerDead)
                CloseEmoteMenu();
        }
    }



    public class EmoteUIElement {

        public GameObject uiGameObject;
        public int id;
        public Image backgroundImage;
        public TextMeshPro textContainer;


        public void OnHover(bool hovered = true)
        {
            backgroundImage.color = hovered ? EmoteMenuManager.colorHovered : EmoteMenuManager.colorUnhovered;
        }


    }
}

