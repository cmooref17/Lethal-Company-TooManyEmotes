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
using UnityEngine.EventSystems;
using System.Xml.Linq;
using TooManyEmotes.Networking;

namespace TooManyEmotes {

    [HarmonyPatch]
    public static class EmoteMenuManager
    {
        public static QuickMenuManager quickMenuManager { get { return StartOfRound.Instance?.localPlayerController?.quickMenuManager; } }
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static GameObject menuGameObject;
        public static RectTransform menuTransform;
        public static CanvasGroup canvasGroup;
        public static RawImage renderTextureImageUI;
        public static TextMeshPro swapPageText;
        public static TextMeshPro currentEmoteText;

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

        public static List<EmoteUIElement> emoteUIElementsList;
        public static int hoveredEmoteUIIndex = -1;
        public static int hoveredEmoteIndex { get { return hoveredEmoteUIIndex >= 0 ? hoveredEmoteUIIndex + 8 * currentPage : -1; } }
        public static int currentPage = 0;
        public static int numPages { get { return currentLoadoutEmotesList != null ? Mathf.Max((currentLoadoutEmotesList.Count - 1) / emoteUIElementsList.Count, 0) + 1 : 0; } }
        public static UnlockableEmote previewingEmote { get { return currentLoadoutEmotesList != null && hoveredEmoteIndex >= 0 && hoveredEmoteIndex < currentLoadoutEmotesList.Count ? currentLoadoutEmotesList[hoveredEmoteIndex] : null; } }

        public static List<UnlockableEmote> currentLoadoutEmotesList { get { return emoteLoadouts != null && currentLoadoutIndex >= 0 && currentLoadoutIndex < emoteLoadouts.Count ? emoteLoadouts[currentLoadoutIndex] : null; } }

        public static List<EmoteLoadoutUIElement> emoteLoadoutUIElementsList;
        public static List<List<UnlockableEmote>> emoteLoadouts; // = new List<List<string>>();
        public static Color selectedLoadoutUIColor = new Color(0.2f, 0.2f, 1f);
        public static int currentLoadoutIndex = -1;
        public static int numLoadouts = 3;

        public static int hoveredLoadoutUIIndex = -1;




        [HarmonyPatch(typeof(HUDManager), "Start")]
        [HarmonyPostfix]
        public static void InitializeUI(HUDManager __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || Plugin.radialMenuPrefab == null)
                return;

            menuGameObject = GameObject.Instantiate(Plugin.radialMenuPrefab, __instance.HUDContainer.transform.parent);
            menuGameObject.transform.SetAsLastSibling();
            menuGameObject.name = "EmotesRadialMenu";
            menuTransform = menuGameObject.GetComponent<RectTransform>();
            renderTextureImageUI = menuGameObject.GetComponentInChildren<RawImage>();
            Transform emoteUIElementsParent = menuTransform.Find("RadialMenuUI/RadialElements").transform;
            swapPageText = menuTransform.Find("RadialMenuUI/RadialBase/SwapPageText").GetComponent<TextMeshPro>();
            currentEmoteText = menuTransform.Find("RadialMenuUI/RadialBase/CurrentEmoteText").GetComponent<TextMeshPro>();
            currentEmoteText.text = "";
            emoteUIElementsList = new List<EmoteUIElement>();

            currentPage = 0;
            hoveredEmoteUIIndex = -1;

            for (int i = 0; i < emoteUIElementsParent.childCount; i++)
            {
                Transform uiObject = emoteUIElementsParent.GetChild(i);
                EmoteUIElement uiElement = new EmoteUIElement {
                    uiGameObject = uiObject.gameObject,
                    id = i,
                    backgroundImage = uiObject.GetComponentInChildren<Image>(),
                    textContainer = uiObject.GetComponentInChildren<TextMeshPro>()
                };
                //emoteUIElement.backgroundImage.color = colorUnhovered;
                emoteUIElementsList.Add(uiElement);

            }

            EmoteLoadoutUIElement.uiCount = 0;
            Transform emoteLoadoutsUIParent = menuTransform.Find("RadialMenuUI/EmoteLoadouts").transform;
            emoteLoadoutsUIParent.gameObject.AddComponent<EmoteLoadoutUIContainer>();
            emoteLoadoutUIElementsList = new List<EmoteLoadoutUIElement>();
            emoteLoadoutUIElementsList.Add(emoteLoadoutsUIParent.GetChild(0).gameObject.AddComponent<EmoteLoadoutUIElement>());
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));
            emoteLoadoutUIElementsList.Add(GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent));

            for (int i = 0; i < emoteLoadoutUIElementsList.Count; i++)
                emoteLoadoutUIElementsList[i].name = "EmoteLoadout_" + i;

            emoteLoadoutUIElementsList[0].loadoutName = "Favorites";
            emoteLoadoutUIElementsList[1].loadoutName = string.Format("<color={0}>Legendary</color>", UnlockableEmote.rarityColorCodes[3]);
            emoteLoadoutUIElementsList[2].loadoutName = string.Format("<color={0}>Rare</color>", UnlockableEmote.rarityColorCodes[2]);
            emoteLoadoutUIElementsList[3].loadoutName = string.Format("<color={0}>Uncommon</color>", UnlockableEmote.rarityColorCodes[1]);
            emoteLoadoutUIElementsList[4].loadoutName = string.Format("<color={0}>Common</color>", UnlockableEmote.rarityColorCodes[0]);
            emoteLoadoutUIElementsList[5].loadoutName = "Complementary";
            emoteLoadoutUIElementsList[6].loadoutName = "All";

            SaveManager.LoadFavoritedEmotes();
            emoteLoadouts = new List<List<UnlockableEmote>>()
            {
                StartOfRoundPatcher.unlockedFavoriteEmotes,
                StartOfRoundPatcher.unlockedEmotesTier3,
                StartOfRoundPatcher.unlockedEmotesTier2,
                StartOfRoundPatcher.unlockedEmotesTier1,
                StartOfRoundPatcher.unlockedEmotesTier0,
                StartOfRoundPatcher.complementaryEmotes,
                StartOfRoundPatcher.unlockedEmotes
            };

            if (currentLoadoutIndex < 0 || currentLoadoutIndex >= emoteLoadouts.Count)
                currentLoadoutIndex = emoteLoadouts.Count - 1;

            InitializeAnimationRenderer();
            menuGameObject.SetActive(false);
        }


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void GetInput()
        {
            if (ConfigSettings.disableEmotesForSelf.Value || previewPlayerAnimatorController == null || !isMenuOpen)
                return;

            if (EmoteLoadoutUIContainer.hovered || hoveredLoadoutUIIndex != -1)
            {
                if (hoveredEmoteUIIndex != -1)
                    OnHoveredNewElement(-1);
            }
            else
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
                Vector2 direction = mousePosition - screenCenter;
                int emoteIndex = -1;
                if (direction.magnitude / Screen.height >= 0.17f)
                {
                    float angle = Mathf.Atan2(direction.y, -direction.x) * Mathf.Rad2Deg - 67.5f;
                    if (angle < 0) angle += 360;
                    emoteIndex = Mathf.FloorToInt(angle / 45);
                }

                if (emoteIndex != hoveredEmoteUIIndex)
                    OnHoveredNewElement(emoteIndex);
            }
        }


        public static void OnHoveredNewLoadoutElement(int index)
        {
            if (hoveredLoadoutUIIndex == index)
                return;

            hoveredLoadoutUIIndex = index;
            foreach (var loadoutUIElement in emoteLoadoutUIElementsList)
                loadoutUIElement.OnHover(loadoutUIElement.id == index);
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
            swapPageText.text = string.Format("[{0} / {1}]\n[Mouse Scroll]", currentPage + 1, numPages);

            for (int i = 0; i < emoteLoadoutUIElementsList.Count; i++)
            {
                var uiElement = emoteLoadoutUIElementsList[i];
                uiElement.OnHover(hoveredLoadoutUIIndex == uiElement.id);
            }

            for (int i = 0; i < emoteLoadouts.Count; i++)
            {
                var emoteList = emoteLoadouts[i];
                var textComponent = emoteLoadoutUIElementsList[i];
                string text = textComponent.textContainer.text;
                int indexOfSpace = text.IndexOf(" ");
                indexOfSpace = indexOfSpace == -1 ? text.Length : indexOfSpace;
                text = text.Substring(0, indexOfSpace) + " [" + emoteList.Count + "]";
                textComponent.textContainer.text = text;
            }

            for (int i = 0; i < emoteUIElementsList.Count; i++)
            {
                var emoteUI = emoteUIElementsList[i];
                int emoteIndex = i + 8 * currentPage;
                emoteUI.textContainer.text = "";
                emoteUI.emote = null;
                Color color = defaultUIColor;
                if (emoteIndex < currentLoadoutEmotesList.Count)
                {
                    UnlockableEmote emote = currentLoadoutEmotesList[emoteIndex];
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
            if (emoteIndex >= 0 && emoteIndex < currentLoadoutEmotesList.Count && currentLoadoutEmotesList[emoteIndex] != null)
            {
                UnlockableEmote emote = currentLoadoutEmotesList[emoteIndex];
                previewPlayerObject.SetActive(true);
                renderingCamera.enabled = true;

                previewPlayerAnimatorController["EmoteStart"] = emote.animationClip;
                previewPlayerAnimatorController["EmoteLoop"] = emote.transitionsToClip != null ? emote.transitionsToClip : null;
                previewPlayerAnimator.SetBool("hasTransition", emote.transitionsToClip != null);
                previewPlayerAnimator.Play("EmoteStart", 0, 0);

                currentEmoteText.text = "[" + emote.displayNameColorCoded + "]\n[MMB] " + (StartOfRoundPatcher.allFavoriteEmotes.Contains(emote.emoteName) ? "Unfavorite" : "Favorite");
            }
            else
            {
                previewPlayerAnimatorController["EmoteStart"] = null;
                previewPlayerAnimatorController["EmoteLoop"] = null;
                previewPlayerObject.SetActive(false);
                currentEmoteText.text = "";
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


        public static void SetCurrentEmoteLoadout(int loadoutIndex)
        {
            if (currentLoadoutIndex == loadoutIndex)
                return;
            currentPage = 0;
            currentLoadoutIndex = loadoutIndex;
            UpdateEmoteWheel();
        }


        public static void ToggleFavoriteHoveredEmote()
        {
            if (!isMenuOpen || previewingEmote == null)
                return;

            string emoteName = previewingEmote.emoteName;
            if (StartOfRoundPatcher.allFavoriteEmotes.Contains(emoteName))
                StartOfRoundPatcher.allFavoriteEmotes.Remove(emoteName);
            else
                StartOfRoundPatcher.allFavoriteEmotes.Add(emoteName);
            StartOfRoundPatcher.UpdateUnlockedFavoriteEmotes();
            SaveManager.SaveFavoritedEmotes();
            UpdateEmoteWheel();
        }


        public static bool isMenuOpen { get { return menuGameObject != null && menuGameObject.activeSelf; } }


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
            if (localPlayerController.isPlayerDead || localPlayerController.inTerminalMenu || localPlayerController.isTypingChat || localPlayerController.isPlayerDead || localPlayerController.inSpecialInteractAnimation || localPlayerController.isGrabbingObjectAnimation || localPlayerController.inShockingMinigame || localPlayerController.isClimbingLadder || localPlayerController.isSinking || localPlayerController.inAnimationWithEnemy != null)
                return false;
            return true;
        }


        static void InitializeAnimationRenderer()
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
        public static void InitializePlayerCloneRenderObject(PlayerControllerB __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return;

            IEnumerator InitPlayerCloneAfterSpawnAnimation()
            {
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
            if (ConfigSettings.disableEmotesForSelf.Value || __instance != localPlayerController || !isMenuOpen || !context.performed)
                return true;

            if (numPages == 0 || (numPages == 1 && currentPage == 0))
                return false;

            if (context.ReadValue<float>() < 0 && !ConfigSettings.reverseEmoteWheelScrollDirection.Value)
                SwapPrevPage();
            else
                SwapNextPage();

            return false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ItemSecondaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemSecondaryUseInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return true;
            return !isMenuOpen;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ItemTertiaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemTertiaryUseInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return true;
            return !isMenuOpen;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool PreventInteractInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value)
                return true;
            return !isMenuOpen;
        }


        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPrefix]
        public static bool OnOpenQuickMenu()
        {
            if (!isMenuOpen)
                return true;
            CloseEmoteMenu();
            return false;
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
            if (isMenuOpen && __instance == localPlayerController && __instance.isPlayerDead)
                CloseEmoteMenu();
        }
    }


    public class EmoteUIElement
    {
        public GameObject uiGameObject;
        public int id;
        public Image backgroundImage;
        public TextMeshPro textContainer;
        public Color baseColor;
        public UnlockableEmote emote;

        public void OnHover(bool hovered = true)
        {
            Color newColor = baseColor * (hovered ? 1f : 0.5f);
            newColor.a = hovered ? EmoteMenuManager.hoveredAlpha : EmoteMenuManager.unhoveredAlpha;
            backgroundImage.color = newColor;
        }
    }


    public class EmoteLoadoutUIElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static int uiCount = 0;
        public int id;
        public string loadoutName;
        public Image backgroundImage;
        public TextMeshPro textContainer;


        void Awake()
        {
            id = uiCount++;
            backgroundImage = GetComponentInChildren<Image>();
            textContainer = GetComponentInChildren<TextMeshPro>();
        }

        void Start()
        {
            textContainer.text = loadoutName;
        }

        public void OnHover(bool hovered = true)
        {
            Color newColor = (EmoteMenuManager.currentLoadoutIndex == id ? EmoteMenuManager.selectedLoadoutUIColor : EmoteMenuManager.defaultUIColor) * (hovered ? 1f : 0.5f);
            newColor.a = hovered ? EmoteMenuManager.hoveredAlpha : EmoteMenuManager.unhoveredAlpha;
            backgroundImage.color = newColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EmoteMenuManager.OnHoveredNewLoadoutElement(id);
            OnHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EmoteMenuManager.OnHoveredNewLoadoutElement(-1);
            OnHover(false);
        }
    }


    // Prevent selecting emote ui elements when mouse is between loadout ui elements (temporary)
    public class EmoteLoadoutUIContainer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static bool hovered = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }
    }
}

