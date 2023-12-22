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
using TooManyEmotes.Config;

namespace TooManyEmotes {

    [HarmonyPatch]
    public class EmoteMenuManager {

        public static QuickMenuManager quickMenuManager { get { return StartOfRound.Instance?.localPlayerController?.quickMenuManager; } }
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static GameObject menuGameObject;
        public static RectTransform menuTransform;
        public static CanvasGroup canvasGroup;
        public static RawImage renderTextureImageUI;
        public static TextMeshPro swapPageText;

        public static RenderTexture renderTexture;
        public static Camera renderingCamera;

        public static GameObject previewPlayerObject;
        public static SkinnedMeshRenderer previewPlayerMesh;
        public static Animator previewPlayerAnimator;
        public static AnimatorOverrideController previewPlayerAnimatorController;
        public static int playerLayer = LayerMask.NameToLayer("Player");
        public static int playerLayerMask { get { return 1 << playerLayer; } }

        public static float hoveredAlpha = 0.75f;
        public static float unhoveredAlpha = 0.75f;
        public static Color defaultUIColor = new Color(0.3f, 0.3f, 0.3f);
        //public static Color colorUnhovered = new Color(0.05f, 0.05f, 0.05f, 0.5f);
        //public static Color colorHovered = new Color(0.3f, 0.3f, 0.3f, 0.6f);

        public static List<EmoteUIElement> emoteUIElementsList;
        public static int hoveredEmoteUIIndex = 0;
        public static int hoveredEmoteIndex { get { return hoveredEmoteUIIndex >= 0 ? hoveredEmoteUIIndex + 8 * currentPage : -1; } }
        public static int currentPage = 0;
        public static int numPages { get { return Mathf.Max((StartOfRoundPatcher.unlockedEmotes.Count - 1) / emoteUIElementsList.Count, 0) + 1; } }
        public static UnlockableEmote previewingEmote { get { return hoveredEmoteUIIndex >= 0 && hoveredEmoteUIIndex < StartOfRoundPatcher.unlockedEmotes.Count ? StartOfRoundPatcher.unlockedEmotes[hoveredEmoteUIIndex] : null; } }




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
            swapPageText = menuTransform.Find("RadialBase/SwapPageText").GetComponent<TextMeshPro>();
            emoteUIElementsList = new List<EmoteUIElement>();

            currentPage = 0;
            hoveredEmoteUIIndex = -1;

            for (int i = 0; i < emoteUIElementsParent.childCount; i++)
            {
                Transform emoteUIObject = emoteUIElementsParent.GetChild(i);
                EmoteUIElement emoteUIElement = new EmoteUIElement {
                    uiGameObject = emoteUIObject.gameObject,
                    id = i,
                    backgroundImage = emoteUIObject.GetComponentInChildren<Image>(),
                    textContainer = emoteUIObject.GetComponentInChildren<TextMeshPro>()
                };
                //emoteUIElement.backgroundImage.color = colorUnhovered;
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

            if (emoteIndex != hoveredEmoteUIIndex)
                OnHoveredNewElement(emoteIndex);
        }


        public static void OnHoveredNewElement(int index)
        {
            if (hoveredEmoteUIIndex != -1 && hoveredEmoteUIIndex != index)
                emoteUIElementsList[hoveredEmoteUIIndex].OnHover(false);
            if (index != -1)
                emoteUIElementsList[index].OnHover(true);
            hoveredEmoteUIIndex = index;
            SetPreviewAnimation(hoveredEmoteIndex);
        }


        public static void SwapPrevPage()
        {
            currentPage--;
            if (currentPage < 0)
                currentPage = numPages - 1;
            UpdateEmoteWheel();
        }


        public static void SwapNextPage()
        {
            currentPage++;
            if (currentPage >= numPages)
                currentPage = 0;
            UpdateEmoteWheel();
        }


        public static void UpdateEmoteWheel()
        {
            currentPage = Mathf.Clamp(currentPage, 0, numPages - 1);
            swapPageText.text = string.Format("[{0} / {1}]\nChange page\n[Mouse Scroll]", currentPage + 1, numPages);
            for (int i = 0; i < emoteUIElementsList.Count; i++)
            {
                var emoteUI = emoteUIElementsList[i];
                int emoteIndex = i + 8 * currentPage;
                emoteUI.textContainer.text = "";
                emoteUI.emote = null;
                Color color = defaultUIColor;
                if (emoteIndex < StartOfRoundPatcher.unlockedEmotes.Count)
                {
                    UnlockableEmote emote = StartOfRoundPatcher.unlockedEmotes[emoteIndex];
                    if (emote != null)
                    {
                        emoteUI.emote = emote;
                        emoteUI.textContainer.text = emote.displayName;

                        //if (ColorUtility.TryParseHtmlString(UnlockableEmote.rarityColorCodes[emote.rarity], out var emoteColor))
                            //color = emoteColor;
                    }
                }
                emoteUI.baseColor = color;
                emoteUI.OnHover(false);
            }
            if (hoveredEmoteUIIndex >= 0 && hoveredEmoteUIIndex < 8)
                OnHoveredNewElement(hoveredEmoteUIIndex);
        }


        public static void SetPreviewAnimation(int emoteIndex)
        {
            if (emoteIndex >= 0 && emoteIndex < StartOfRoundPatcher.unlockedEmotes.Count && StartOfRoundPatcher.unlockedEmotes[emoteIndex] != null)
            {
                UnlockableEmote emote = StartOfRoundPatcher.unlockedEmotes[emoteIndex];
                previewPlayerObject.SetActive(true);
                renderingCamera.enabled = true;

                previewPlayerAnimatorController["EmoteStart"] = emote.animationClip;
                previewPlayerAnimatorController["EmoteLoop"] = emote.transitionsToClip != null ? emote.transitionsToClip : null;
                previewPlayerAnimator.SetBool("hasTransition", emote.transitionsToClip != null);
                previewPlayerAnimator.Play("EmoteStart", 0, 0);
            }
            else
            {
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
            previewPlayerMesh.material = localPlayerController.thisPlayerModel.material;
            UpdateEmoteWheel();
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
            if ((quickMenuManager.isMenuOpen && !isMenuOpen) || previewPlayerObject == null)
                return false;
            if (localPlayerController.isPlayerDead || localPlayerController.inTerminalMenu || localPlayerController.isTypingChat || localPlayerController.isPlayerDead || localPlayerController.inSpecialInteractAnimation || localPlayerController.inShockingMinigame || localPlayerController.isClimbingLadder || localPlayerController.isSinking)
                return false;
            return true;
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
            renderingCamera.transform.position = Vector3.down * 1000;
            renderTextureImageUI.texture = renderTexture;

            Light spotlight = new GameObject("Spotlight").AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.transform.position = renderingCamera.transform.position;
            spotlight.transform.parent = renderingCamera.transform;
            spotlight.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spotlight.intensity = 50;
            spotlight.range = 40;
            spotlight.innerSpotAngle = 100;
            spotlight.spotAngle = 120;
            spotlight.gameObject.layer = playerLayer;

            DisableRenderCameraNextFrame();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitializePlayerCloneRenderObject(PlayerControllerB __instance) {

            IEnumerator InitPlayerCloneAfterSpawnAnimation() {
                yield return new WaitForSeconds(2);
                previewPlayerObject = GameObject.Instantiate(__instance.gameObject, renderingCamera.transform);
                previewPlayerObject.name = "PreviewPlayerAnimationObject";
                GameObject modelGameObject = previewPlayerObject.transform.Find("ScavengerModel").gameObject;
                GameObject metarigGameObject = modelGameObject.transform.Find("metarig").gameObject;
                PlayerControllerB copyPlayerController = previewPlayerObject.GetComponentInChildren<PlayerControllerB>();
                copyPlayerController.thisPlayerModel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                previewPlayerMesh = copyPlayerController.thisPlayerModel;
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

                previewPlayerObject.transform.position = renderingCamera.transform.position + renderingCamera.transform.forward * 2.8f + Vector3.down * 1.35f;
                previewPlayerObject.transform.LookAt(new Vector3(renderingCamera.transform.position.x, previewPlayerObject.transform.position.y, renderingCamera.transform.position.z));
                SetObjectLayerRecursive(previewPlayerObject, playerLayer);

                foreach (MonoBehaviour script in previewPlayerObject.GetComponents<MonoBehaviour>())
                    GameObject.Destroy(script);
            }

            if (Plugin.radialMenuPrefab == null)
                return;

            __instance.StartCoroutine(InitPlayerCloneAfterSpawnAnimation());
        }


        static void SetObjectLayerRecursive(GameObject obj, int layer) {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
                SetObjectLayerRecursive(obj.transform.GetChild(i)?.gameObject, layer);
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
        [HarmonyPrefix]
        public static bool OnScrollMouse(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (!isMenuOpen || __instance != localPlayerController || !context.performed)
                return true;

            if (context.ReadValue<float>() < 0 && !ConfigSettings.reverseEmoteWheelScrollDirection.Value)
                SwapNextPage();
            else
                SwapPrevPage();

            return false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ItemSecondaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemSecondaryUseInMenu(InputAction.CallbackContext context) => !isMenuOpen;

        [HarmonyPatch(typeof(PlayerControllerB), "ItemTertiaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemTertiaryUseInMenu(InputAction.CallbackContext context) => !isMenuOpen;

        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool PreventInteractInMenu(InputAction.CallbackContext context) => !isMenuOpen;


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
        public Color baseColor;
        public UnlockableEmote emote;
        //public static string favoriteText = "[F] Favorite";

        public void OnHover(bool hovered = true)
        {
            Color newColor = baseColor * (hovered ? 1f : 0.5f);
            newColor.a = hovered ? EmoteMenuManager.hoveredAlpha : EmoteMenuManager.unhoveredAlpha;
            backgroundImage.color = newColor;
        }
    }
}

