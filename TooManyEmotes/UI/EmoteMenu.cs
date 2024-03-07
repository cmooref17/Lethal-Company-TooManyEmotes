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
using TooManyEmotes.Input;
using UnityEngine.Rendering.HighDefinition;
using System.Linq.Expressions;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Audio;

namespace TooManyEmotes.UI
{
    [HarmonyPatch]
    public static class EmoteMenuManager
    {
        public static QuickMenuManager quickMenuManager { get { return StartOfRound.Instance?.localPlayerController?.quickMenuManager; } }
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static GameObject menuGameObject;
        public static RectTransform menuTransform;
        public static CanvasGroup canvasGroup;
        public static RawImage renderTextureImageUI;
        public static RenderTexture renderTexture;
        public static TextMeshPro swapPageText;
        public static TextMeshPro currentEmoteText;

        public static float hoveredAlpha = 0.75f;
        public static float unhoveredAlpha = 0.75f;
        public static Color defaultUIColor = new Color(0.3f, 0.3f, 0.3f);

        public static List<EmoteUIElement> emoteUIElementsList;
        public static int hoveredEmoteUIIndex = -1;
        public static int hoveredEmoteIndex { get { return hoveredEmoteUIIndex >= 0 ? hoveredEmoteUIIndex + 8 * currentPage : -1; } }
        public static int currentPage = 0;
        public static int numPages { get { return currentLoadoutEmotesList != null ? Mathf.Max((currentLoadoutEmotesList.Count - 1) / emoteUIElementsList.Count, 0) + 1 : 0; } }
        public static UnlockableEmote previewingEmote { get { return currentLoadoutEmotesList != null && hoveredEmoteIndex >= 0 && hoveredEmoteIndex < currentLoadoutEmotesList.Count ? (currentLoadoutEmotesList[hoveredEmoteIndex].inEmoteSyncGroup ? currentLoadoutEmotesList[hoveredEmoteIndex].emoteSyncGroup[0] : currentLoadoutEmotesList[hoveredEmoteIndex]) : null; } }

        public static List<UnlockableEmote> currentLoadoutEmotesList { get { return emoteLoadouts != null && currentLoadoutIndex >= 0 && currentLoadoutIndex < emoteLoadouts.Count ? emoteLoadouts[currentLoadoutIndex] : null; } }

        public static List<EmoteLoadoutUIElement> emoteLoadoutUIElementsList;
        public static List<List<UnlockableEmote>> emoteLoadouts; // = new List<List<string>>();
        public static Color selectedLoadoutUIColor = new Color(0.2f, 0.2f, 1f);
        public static int currentLoadoutIndex = -1;
        public static int numLoadouts { get { return emoteLoadouts.Count; } }
        public static int hoveredLoadoutUIIndex = -1;

        public static Vector2 currentThumbstickPosition = Vector2.zero;
        public static TextMeshProUGUI[] controlTipLines;
        public static bool usingController { get { return StartOfRound.Instance.localPlayerUsingController; } }
        static bool firstTimeOpeningMenu;

        static Toggle muteEmoteToggle;
        static Slider emoteVolumeSlider;
        static bool updatedAudioPreferences = false;


        [HarmonyPatch(typeof(HUDManager), "Start")]
        [HarmonyPostfix]
        public static void InitializeUI(HUDManager __instance)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled || Plugin.radialMenuPrefab == null)
                return;

            AnimationPreviewer.enabled = false;
            firstTimeOpeningMenu = true;
            menuGameObject = GameObject.Instantiate(Plugin.radialMenuPrefab, __instance.HUDContainer.transform.parent);
            menuGameObject.transform.SetAsLastSibling();
            menuGameObject.name = "EmotesRadialMenu";
            menuTransform = menuGameObject.GetComponent<RectTransform>();
            renderTextureImageUI = menuGameObject.GetComponentInChildren<RawImage>();

            renderTexture = new RenderTexture(1024, 1024, 24);
            renderTexture.format = RenderTextureFormat.ARGB32;
            renderTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            renderTexture.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D24_UNorm_S8_UInt;

            Transform emoteUIElementsParent = menuTransform.Find("MenuUI/RadialMenu/RadialElements").transform;
            swapPageText = menuTransform.Find("MenuUI/RadialMenu/RadialBase/SwapPageText").GetComponent<TextMeshPro>();
            currentEmoteText = menuTransform.Find("MenuUI/RadialMenu/RadialBase/CurrentEmoteText").GetComponent<TextMeshPro>();
            currentEmoteText.text = "";
            emoteUIElementsList = new List<EmoteUIElement>();


            controlTipLines = new TextMeshProUGUI[HUDManager.Instance.controlTipLines.Length];

            for (int i = 0; i < HUDManager.Instance.controlTipLines.Length; i++)
            {
                var newControlTipLine = GameObject.Instantiate(HUDManager.Instance.controlTipLines[i], HUDManager.Instance.controlTipLines[0].transform.parent);
                newControlTipLine.transform.localScale = HUDManager.Instance.controlTipLines[0].transform.localScale;
                newControlTipLine.transform.parent = menuTransform;
                newControlTipLine.transform.SetPositionAndRotation(HUDManager.Instance.controlTipLines[i].transform.position, HUDManager.Instance.controlTipLines[i].transform.rotation);
                newControlTipLine.text = "";
                newControlTipLine.overflowMode = TextOverflowModes.Overflow;
                newControlTipLine.enableWordWrapping = false;
                controlTipLines[i] = newControlTipLine;
            }

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

            Transform emoteLoadoutsUIParent = menuTransform.Find("MenuUI/EmoteLoadouts").transform;
            Transform additionalUIParent = menuTransform.Find("MenuUI/AdditionalUI").transform;
            emoteLoadoutsUIParent.gameObject.AddComponent<AdditionalPanelUI>();
            additionalUIParent.gameObject.AddComponent<AdditionalPanelUI>();

            EmoteLoadoutUIElement.uiCount = 0;
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
            emoteLoadoutUIElementsList[2].loadoutName = string.Format("<color={0}>Epic</color>", UnlockableEmote.rarityColorCodes[2]);
            emoteLoadoutUIElementsList[3].loadoutName = string.Format("<color={0}>Rare</color>", UnlockableEmote.rarityColorCodes[1]);
            emoteLoadoutUIElementsList[4].loadoutName = string.Format("<color={0}>Common</color>", UnlockableEmote.rarityColorCodes[0]);
            emoteLoadoutUIElementsList[5].loadoutName = "Complementary";
            emoteLoadoutUIElementsList[6].loadoutName = "All";

            SaveManager.LoadFavoritedEmotes();
            emoteLoadouts = new List<List<UnlockableEmote>>()
            {
                SessionManager.unlockedFavoriteEmotes,
                SessionManager.unlockedEmotesTier3,
                SessionManager.unlockedEmotesTier2,
                SessionManager.unlockedEmotesTier1,
                SessionManager.unlockedEmotesTier0,
                EmotesManager.complementaryEmotes,
                SessionManager.unlockedEmotes
            };

            if (currentLoadoutIndex < 0 || currentLoadoutIndex >= emoteLoadouts.Count)
                currentLoadoutIndex = emoteLoadouts.Count - 1;

            muteEmoteToggle = additionalUIParent.Find("MasterMutePanel")?.GetComponentInChildren<Toggle>();
            muteEmoteToggle.onValueChanged.AddListener(delegate { OnUpdateToggleMuteEmote(muteEmoteToggle); });
            emoteVolumeSlider = additionalUIParent.Find("AudioVolumePanel")?.GetComponentInChildren<Slider>();
            emoteVolumeSlider.onValueChanged.AddListener(delegate { OnUpdateEmoteVolume(emoteVolumeSlider); });

            muteEmoteToggle.isOn = AudioManager.muteEmoteAudio;
            emoteVolumeSlider.value = Mathf.Clamp(AudioManager.emoteVolumeMultiplier, 0, emoteVolumeSlider.maxValue);

            AnimationPreviewer.InitializeAnimationRenderer();
            menuGameObject.SetActive(false);
        }


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void GetInput()
        {
            if (!isMenuOpen || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return;

            if (AdditionalPanelUI.hovered || hoveredLoadoutUIIndex != -1)
            {
                if (hoveredEmoteUIIndex != -1)
                    OnHoveredNewElement(-1);
            }
            else
            {
                Vector2 direction;
                if (!usingController)
                {
                    Vector2 mousePosition = Mouse.current.position.ReadValue();
                    Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
                    direction = mousePosition - screenCenter;
                }
                else
                    direction = currentThumbstickPosition;

                int emoteIndex = -1;
                if ((!usingController && direction.magnitude / Screen.height >= 0.17f) || (usingController && currentThumbstickPosition != Vector2.zero))
                {
                    float angle = Mathf.Atan2(direction.y, -direction.x) * Mathf.Rad2Deg - 67.5f;
                    if (angle < 0) angle += 360;
                    emoteIndex = Mathf.FloorToInt(angle / 45);
                }

                if (emoteIndex != hoveredEmoteUIIndex)
                    OnHoveredNewElement(emoteIndex);
            }
        }


        public static void OnUpdateThumbStickAngle(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled || !context.performed || ConfigSync.instance.syncEnableMovingWhileEmoting || !isMenuOpen)
                return;

            currentThumbstickPosition = context.ReadValue<Vector2>();
            currentThumbstickPosition = currentThumbstickPosition.magnitude > 0.75f ? currentThumbstickPosition : Vector2.zero;
            StartOfRound.Instance.localPlayerUsingController = true;
            if (currentThumbstickPosition == Vector2.zero && previewingEmote != null)
            {
                localPlayerController.PerformEmote(context, -(previewingEmote.emoteId + 1));
                CloseEmoteMenu();
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

            AnimationPreviewer.SetPreviewAnimation(previewingEmote);
            if (previewingEmote != null)
            {
                currentEmoteText.text = previewingEmote.displayNameColorCoded + (EmotesManager.allFavoriteEmotes.Contains(previewingEmote.emoteName) ? " *" : "");
            }
            else
            {
                currentEmoteText.text = "";
            }
        }


        public static void SwapPrevPage()
        {
            hoveredEmoteUIIndex = -1;
            currentPage--;
            currentPage = currentPage < 0 ? numPages - 1 : currentPage;
            UpdateEmoteWheel();
        }


        public static void SwapNextPage()
        {
            hoveredEmoteUIIndex = -1;
            currentPage = (currentPage + 1) % numPages;
            UpdateEmoteWheel();
        }


        public static void UpdateControlTipLines()
        {
            int bindingIndex = usingController ? 1 : 0;

            string prevPageKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.PrevEmotePageAction);
            string nextPageKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmotePageAction);
            string nextEmoteLoadoutUpKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmoteLoadoutUpAction);
            string nextEmoteLoadoutDownKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmoteLoadoutDownAction);
            string favoriteEmoteKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.FavoriteEmoteAction);
            //string performEmoteKeybind = ConfigSettings.GetDisplayName(InputUtilsCompat.Enabled ? Keybinds.PerformSelectedEmoteAction.bindings[bindingIndex].path : Keybinds.PerformSelectedEmoteAction.bindings[bindingIndex].overridePath);

            int index = 0;
            if (!usingController)
                controlTipLines[index++].text = "Swap Page: [Scroll Mouse]";
            else if (prevPageKeybind != "" || nextPageKeybind != "")
                controlTipLines[index++].text = string.Format("Swap Page: [{0}/{1}]", prevPageKeybind, nextPageKeybind);
                
            if (usingController || nextEmoteLoadoutUpKeybind != "" || nextEmoteLoadoutDownKeybind != "")
                controlTipLines[index++].text = string.Format("Swap Loadout: [{0}/{1}]", nextEmoteLoadoutUpKeybind, nextEmoteLoadoutDownKeybind);
            controlTipLines[index++].text = string.Format("Favorite Emote: [{0}]", favoriteEmoteKeybind);
            //if (usingController) controlTipLines[index++].text = string.Format("Perform Emote: [{0}]", performEmoteKeybind);

            for (; index < controlTipLines.Length; index++)
                controlTipLines[index].text = "";
        }


        public static void UpdateEmoteWheel()
        {
            currentPage = Mathf.Clamp(currentPage, 0, numPages - 1);
            swapPageText.text = string.Format("Page [{0} / {1}]", currentPage + 1, numPages);

            for (int i = 0; i < emoteLoadoutUIElementsList.Count; i++)
            {
                var uiElement = emoteLoadoutUIElementsList[i];
                uiElement.OnHover(hoveredLoadoutUIIndex == uiElement.id);
            }
            for (int i = 0; i < emoteLoadouts.Count; i++)
            {
                var emoteList = emoteLoadouts[i];
                var textComponent = emoteLoadoutUIElementsList[i];
                textComponent.textContainer.text = textComponent.loadoutName + " [" + emoteList.Count + "]";
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

            var emote = previewingEmote;
            if (previewingEmote.emoteSyncGroup != null && previewingEmote.emoteSyncGroup.Count > 0)
                emote = previewingEmote.emoteSyncGroup[0];

            string emoteName = emote.emoteName;
            if (EmotesManager.allFavoriteEmotes.Contains(emoteName))
                EmotesManager.allFavoriteEmotes.Remove(emoteName);
            else
                EmotesManager.allFavoriteEmotes.Add(emoteName);
            SessionManager.UpdateUnlockedFavoriteEmotes();
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
            if (firstTimeOpeningMenu)
            {
                SetCurrentEmoteLoadout(emoteLoadouts[0].Count > 0 ? 0 : currentLoadoutIndex);
                firstTimeOpeningMenu = false;
            }
            updatedAudioPreferences = false;
            menuGameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            quickMenuManager.isMenuOpen = true;
            AnimationPreviewer.UpdatePlayerSuit();
            currentThumbstickPosition = Vector2.zero;

            foreach (var controlTipLine in HUDManager.Instance.controlTipLines)
                controlTipLine.enabled = false;

            UpdateControlTipLines();
            UpdateEmoteWheel();
        }


        public static void CloseEmoteMenu()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            localPlayerController.isFreeCamera = false;
            menuGameObject.SetActive(false);
            quickMenuManager.CloseQuickMenu();
            OnHoveredNewLoadoutElement(-1);
            AnimationPreviewer.SetPreviewAnimation(null);
            AdditionalPanelUI.hovered = false;
            
            if (updatedAudioPreferences)
            {
                AudioManager.SavePreferences();
                updatedAudioPreferences = false;
            }

            foreach (var controlTipLine in HUDManager.Instance.controlTipLines)
                controlTipLine.enabled = true;
        }


        public static bool CanOpenEmoteMenu()
        {
            if (quickMenuManager.isMenuOpen && !isMenuOpen)
                return false;
            if (localPlayerController.isPlayerDead || localPlayerController.inTerminalMenu || localPlayerController.isTypingChat || localPlayerController.inSpecialInteractAnimation || localPlayerController.isGrabbingObjectAnimation || localPlayerController.inShockingMinigame || localPlayerController.isClimbingLadder || localPlayerController.isSinking)
                return false;
            if (localPlayerController.inAnimationWithEnemy != null || CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer())
                return false;
            return true;
        }


        public static void OnUpdateToggleMuteEmote(Toggle toggle)
        {
            updatedAudioPreferences = true;
            AudioManager.muteEmoteAudio = toggle.isOn;
            foreach (var emoteAudioSource in EmoteAudioSource.allEmoteAudioSources)
                emoteAudioSource.UpdateVolume();
        }


        public static void OnUpdateEmoteVolume(Slider volumeSlider)
        {
            updatedAudioPreferences = true;
            AudioManager.emoteVolumeMultiplier = Mathf.Clamp(volumeSlider.value, 0, 2);
            foreach (var emoteAudioSource in EmoteAudioSource.allEmoteAudioSources)
                emoteAudioSource.UpdateVolume();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
        [HarmonyPrefix]
        public static bool OnScrollMouse(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (!context.performed || __instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled || !isMenuOpen || usingController)
                return true;

            if (isMenuOpen)
            {
                if (numPages == 0 || (numPages == 1 && currentPage == 0))
                    return false;

                float scrollDirection = context.ReadValue<float>();
                if (scrollDirection == 0)
                    return false;

                if (scrollDirection < 0 == !ConfigSettings.reverseEmoteWheelScrollDirection.Value)
                    SwapPrevPage();
                else
                    SwapNextPage();

                return false;
            }
            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ItemSecondaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemSecondaryUseInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return true;
            return !isMenuOpen;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ItemTertiaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemTertiaryUseInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
                return true;
            return !isMenuOpen;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool PreventInteractInMenu(InputAction.CallbackContext context)
        {
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Patcher.Enabled)
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
            textContainer.text = loadoutName;
        }

        void Start()
        {
            //textContainer.text = loadoutName;
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
    public class AdditionalPanelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

