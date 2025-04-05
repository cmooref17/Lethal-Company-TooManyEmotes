 using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using TooManyEmotes.Patches;
using TooManyEmotes.Config;
using UnityEngine.EventSystems;
using TooManyEmotes.Input;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Audio;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using System.Linq;
using TooManyEmotes.Networking;
using System.Xml.Linq;
using System.Collections;
using System;


namespace TooManyEmotes.UI
{
    [HarmonyPatch]
    public static class EmoteMenu
    {
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

        public static List<EmoteLoadoutUIElement> emoteLoadoutUIElementsList = new List<EmoteLoadoutUIElement>();
        public static List<List<UnlockableEmote>> emoteLoadouts = new List<List<UnlockableEmote>>();
        public static Color selectedLoadoutUIColor = new Color(0.2f, 0.2f, 1f);
        public static int currentLoadoutIndex = -1;
        public static int numLoadouts { get { return emoteLoadouts.Count; } }
        public static int hoveredLoadoutUIIndex = -1;

        public static Vector2 currentThumbstickPosition = Vector2.zero;
        private static Transform customControlTipLinesParent;
        public static TextMeshProUGUI[] customControlTipLines;
        public static bool usingController { get { return StartOfRound.Instance.localPlayerUsingController; } }
        private static bool firstTimeOpeningMenu;
        
        // AudioManager preferences
        private static Slider emoteVolumeSlider;
        private static Toggle muteEmoteToggle;
        private static Toggle emoteOnlyModeToggle;
        private static Toggle enableDmcaFreeToggle;

        // ThirdPersonEmoteController preferences
        private static GameObject allowMovingWhileEmotingGameObject;
        private static Toggle enableFirstPersonEmoteToggle;
        private static Toggle allowMovingWhileEmotingToggle;
        
        private static float currentVolumeSetting = 1;
        private static bool currentMuteSetting = false;
        private static bool currentEmoteOnlyMode = false;
        private static bool currentDmcaFreeSetting = false;

        private static bool currentFirstPersonEmotes = false;
        private static bool currentAllowMovingWhileEmoting = false;
        
        private static List<UnlockableEmote> allUnlockedEmotesFiltered = new List<UnlockableEmote>();
        
        private static GameObject togglePanelComplementary;
        private static GameObject togglePanel0;
        private static GameObject togglePanel1;
        private static GameObject togglePanel2;
        private static GameObject togglePanel3;
        
        private static Toggle hideEmotesComplementaryToggle;
        private static Toggle hideEmotes0Toggle;
        private static Toggle hideEmotes1Toggle;
        private static Toggle hideEmotes2Toggle;
        private static Toggle hideEmotes3Toggle;


        [HarmonyPatch(typeof(HUDManager), "Awake")]
        [HarmonyPostfix]
        public static void InitializeUI(HUDManager __instance)
        {
            if (!Assert(Plugin.radialMenuPrefab != null, "Radial menu prefab is null??"))
                return;
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            Log("Initializing emote menu");
            AnimationPreviewer.enabled = false;
            firstTimeOpeningMenu = true;
            // quickMenuManager, HUDManager.Instance?.controlTipLines
            hoveredEmoteUIIndex = -1;
            currentPage = 0;
            currentLoadoutIndex = -1;
            hoveredLoadoutUIIndex = -1;
            currentThumbstickPosition = Vector2.zero;

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


            var defaultControlTipLinesParent = HUDManager.Instance.controlTipLines[0].transform.parent.GetComponent<RectTransform>();

            customControlTipLinesParent = GameObject.Instantiate(defaultControlTipLinesParent, defaultControlTipLinesParent.parent);
            customControlTipLinesParent.name = "EmoteMenuControlTips";
            customControlTipLinesParent.SetSiblingIndex(defaultControlTipLinesParent.GetSiblingIndex() + 1);
            customControlTipLinesParent.SetPositionAndRotation(defaultControlTipLinesParent.position, defaultControlTipLinesParent.rotation);
            customControlTipLinesParent.localScale = defaultControlTipLinesParent.localScale;

            customControlTipLines = new TextMeshProUGUI[HUDManager.Instance.controlTipLines.Length];
            int index = 0;
            foreach (var element in customControlTipLinesParent.GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (element != null)
                {
                    if (element.name.ToLower().Contains("controltip"))
                        customControlTipLines[index++] = element;
                    else
                        GameObject.Destroy(element.gameObject);
                }
            }
            customControlTipLinesParent.SetParent(menuTransform, true);
            customControlTipLinesParent.localPosition = new Vector3(customControlTipLinesParent.localPosition.x, customControlTipLinesParent.localPosition.y, 0);
            customControlTipLinesParent.SetSiblingIndex(1);

            currentPage = 0;
            hoveredEmoteUIIndex = -1;

            for (int i = 0; i < emoteUIElementsParent.childCount; i++)
            {
                Transform uiObject = emoteUIElementsParent.GetChild(i);
                EmoteUIElement uiElement = new EmoteUIElement
                {
                    uiGameObject = uiObject.gameObject,
                    uiRectTransform = uiObject as RectTransform,
                    id = i,
                    backgroundImage = uiObject.GetComponentInChildren<Image>(),
                    textContainer = uiObject.GetComponentInChildren<TextMeshPro>()
                };
                //emoteUIElement.backgroundImage.color = colorUnhovered;
                emoteUIElementsList.Add(uiElement);
            }

            Transform emoteLoadoutsUIParent = menuTransform.Find("MenuUI/EmoteLoadouts").transform;
            emoteLoadoutsUIParent.gameObject.AddComponent<AdditionalPanelUI>();

            EmoteLoadoutUIElement.uiCount = 0;
            emoteLoadoutUIElementsList.Clear();
            emoteLoadoutUIElementsList.Add(emoteLoadoutsUIParent.GetChild(0).gameObject.AddComponent<EmoteLoadoutUIElement>());
            emoteLoadoutUIElementsList.AddRange(new EmoteLoadoutUIElement[]
            {
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent),
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent),
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent),
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent),
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent),
                GameObject.Instantiate(emoteLoadoutUIElementsList[0], emoteLoadoutsUIParent)
            });

            for (int i = 0; i < emoteLoadoutUIElementsList.Count; i++)
                emoteLoadoutUIElementsList[i].name = "EmoteLoadout_" + i;

            emoteLoadoutUIElementsList[0].loadoutName = "Favorites";
            emoteLoadoutUIElementsList[1].loadoutName = string.Format("<color={0}>Legendary</color>", UnlockableEmote.rarityColorCodes[3]);
            emoteLoadoutUIElementsList[2].loadoutName = string.Format("<color={0}>Epic</color>", UnlockableEmote.rarityColorCodes[2]);
            emoteLoadoutUIElementsList[3].loadoutName = string.Format("<color={0}>Rare</color>", UnlockableEmote.rarityColorCodes[1]);
            emoteLoadoutUIElementsList[4].loadoutName = string.Format("<color={0}>Common</color>", UnlockableEmote.rarityColorCodes[0]);
            emoteLoadoutUIElementsList[5].loadoutName = "Complementary";
            emoteLoadoutUIElementsList[6].loadoutName = "All";

            emoteLoadouts.Clear();
            emoteLoadouts.AddRange(new List<UnlockableEmote>[]
            {
                SessionManager.unlockedFavoriteEmotes,
                SessionManager.unlockedEmotesTier3,
                SessionManager.unlockedEmotesTier2,
                SessionManager.unlockedEmotesTier1,
                SessionManager.unlockedEmotesTier0,
                EmotesManager.complementaryEmotes,
                allUnlockedEmotesFiltered
            });

            if (currentLoadoutIndex < 0 || currentLoadoutIndex >= emoteLoadouts.Count)
                currentLoadoutIndex = emoteLoadouts.Count - 1;

            Transform additionalUIParent = menuTransform.Find("MenuUI/AdditionalUI");
            additionalUIParent.gameObject.AddComponent<AdditionalPanelUI>();

            AudioManager.LoadPreferences();
            ThirdPersonEmoteController.LoadPreferences();

            emoteVolumeSlider = additionalUIParent.Find("AudioVolumePanel")?.GetComponentInChildren<Slider>();
            emoteVolumeSlider.value = Mathf.Clamp(AudioManager.emoteVolumeMultiplier, 0, emoteVolumeSlider.maxValue);
            emoteVolumeSlider.onValueChanged.AddListener(delegate { OnUpdateEmoteVolume(emoteVolumeSlider); });
            currentVolumeSetting = emoteVolumeSlider.value;

            muteEmoteToggle = additionalUIParent.Find("MasterMutePanel")?.GetComponentInChildren<Toggle>();
            muteEmoteToggle.isOn = AudioManager.muteEmoteAudio;
            muteEmoteToggle.onValueChanged.AddListener(delegate { OnUpdateToggleMuteEmote(muteEmoteToggle); });
            currentMuteSetting = muteEmoteToggle.isOn;

            emoteOnlyModeToggle = additionalUIParent.Find("EmoteOnlyModePanel")?.GetComponentInChildren<Toggle>();
            emoteOnlyModeToggle.isOn = AudioManager.emoteOnlyMode;
            emoteOnlyModeToggle.onValueChanged.AddListener(delegate { OnUpdateToggleEmoteOnlyMode(emoteOnlyModeToggle); });
            currentEmoteOnlyMode = emoteOnlyModeToggle.isOn;

            enableDmcaFreeToggle = additionalUIParent.Find("DmcaFreePanel")?.GetComponentInChildren<Toggle>();
            enableDmcaFreeToggle.isOn = AudioManager.dmcaFreeMode;
            enableDmcaFreeToggle.onValueChanged.AddListener(delegate { OnUpdateToggleDmcaFreeMode(enableDmcaFreeToggle); });
            currentDmcaFreeSetting = enableDmcaFreeToggle.isOn;

            enableFirstPersonEmoteToggle = additionalUIParent.Find("FirstPersonEmotesPanel")?.GetComponentInChildren<Toggle>();
            enableFirstPersonEmoteToggle.isOn = ThirdPersonEmoteController.firstPersonEmotesEnabled;
            enableFirstPersonEmoteToggle.onValueChanged.AddListener(delegate { OnUpdateToggleFirstPerson(enableFirstPersonEmoteToggle); });
            currentFirstPersonEmotes = enableFirstPersonEmoteToggle.isOn;

            allowMovingWhileEmotingGameObject = additionalUIParent.Find("MoveWhileEmotingPanel")?.gameObject;
            allowMovingWhileEmotingToggle = allowMovingWhileEmotingGameObject?.GetComponentInChildren<Toggle>();
            allowMovingWhileEmotingToggle.isOn = ThirdPersonEmoteController.allowMovingWhileEmoting;
            allowMovingWhileEmotingToggle.onValueChanged.AddListener(delegate { OnUpdateToggleAllowMovingWhileEmoting(allowMovingWhileEmotingToggle); });
            currentAllowMovingWhileEmoting = allowMovingWhileEmotingToggle.isOn;

            togglePanelComplementary = additionalUIParent.Find("HideEmotesComplementary").gameObject;
            togglePanel0 = additionalUIParent.Find("HideEmotes0").gameObject;
            togglePanel1 = additionalUIParent.Find("HideEmotes1").gameObject;
            togglePanel2 = additionalUIParent.Find("HideEmotes2").gameObject;
            togglePanel3 = additionalUIParent.Find("HideEmotes3").gameObject;

            hideEmotesComplementaryToggle = togglePanelComplementary?.GetComponentInChildren<Toggle>();
            hideEmotes0Toggle = togglePanel0?.GetComponentInChildren<Toggle>();
            hideEmotes1Toggle = togglePanel1?.GetComponentInChildren<Toggle>();
            hideEmotes2Toggle = togglePanel2?.GetComponentInChildren<Toggle>();
            hideEmotes3Toggle = togglePanel3?.GetComponentInChildren<Toggle>();

            hideEmotesComplementaryToggle?.onValueChanged.AddListener(delegate { OnUpdateFilteredEmotes(); });
            hideEmotes0Toggle?.onValueChanged.AddListener(delegate { OnUpdateFilteredEmotes(); });
            hideEmotes1Toggle?.onValueChanged.AddListener(delegate { OnUpdateFilteredEmotes(); });
            hideEmotes2Toggle?.onValueChanged.AddListener(delegate { OnUpdateFilteredEmotes(); });
            hideEmotes3Toggle?.onValueChanged.AddListener(delegate { OnUpdateFilteredEmotes(); });

            LoadFilterPreferences();

            AnimationPreviewer.InitializeAnimationRenderer();
            menuGameObject.SetActive(false);
        }


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void GetInput()
        {
            if (!isMenuOpen || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (AdditionalPanelUI.hovered || hoveredLoadoutUIIndex != -1)
            {
                if (hoveredEmoteUIIndex != -1)
                    OnHoveredNewElement(-1);
            }
            else
            {
                Vector2 direction;
                RectTransform referenceElement = emoteUIElementsList[0].uiRectTransform;
                float distanceThreshold = Vector2.Distance(menuGameObject.transform.position, referenceElement.position) * 0.815f;
                if (!usingController)
                {
                    Vector3 rawMousePosition = Mouse.current.position.ReadValue();
                    Camera uiCamera = HUDManager.Instance.HUDContainer.GetComponentInParent<Canvas>().worldCamera;
                    rawMousePosition.z = Mathf.Abs(uiCamera.transform.position.z - menuTransform.position.z);
                    direction = uiCamera.ScreenToWorldPoint(rawMousePosition) - menuTransform.position;
                }
                else
                    direction = currentThumbstickPosition;

                int emoteIndex = -1;
                if ((!usingController && direction.magnitude >= distanceThreshold/*0.425f*/) || (usingController && currentThumbstickPosition != Vector2.zero))
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
            if (localPlayerController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled || !context.performed || !isMenuOpen)
                return;

            currentThumbstickPosition = context.ReadValue<Vector2>();
            currentThumbstickPosition = currentThumbstickPosition.magnitude > 0.75f ? currentThumbstickPosition : Vector2.zero;
            StartOfRound.Instance.localPlayerUsingController = true;
            if (currentThumbstickPosition == Vector2.zero && previewingEmote != null)
            {
                emoteControllerLocal.TryPerformingEmoteLocal(previewingEmote);
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
            string prevPageKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.PrevEmotePageAction);
            string nextPageKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmotePageAction);
            string nextEmoteLoadoutUpKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmoteLoadoutUpAction);
            string nextEmoteLoadoutDownKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.NextEmoteLoadoutDownAction);
            string favoriteEmoteKeybind = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.FavoriteEmoteAction);
            //string performEmoteKeybind = ConfigSettings.GetDisplayName(InputUtilsCompat.Enabled ? Keybinds.PerformSelectedEmoteAction.bindings[bindingIndex].path : Keybinds.PerformSelectedEmoteAction.bindings[bindingIndex].overridePath);

            int index = 0;
            if (!usingController)
                customControlTipLines[index++].text = "Swap Page: [Scroll Mouse]";
            else if (prevPageKeybind != "" || nextPageKeybind != "")
                customControlTipLines[index++].text = string.Format("Swap Page: [{0}/{1}]", prevPageKeybind, nextPageKeybind);
                
            if (usingController || nextEmoteLoadoutUpKeybind != "" || nextEmoteLoadoutDownKeybind != "")
                customControlTipLines[index++].text = string.Format("Swap Loadout: [{0}/{1}]", nextEmoteLoadoutUpKeybind, nextEmoteLoadoutDownKeybind);
            customControlTipLines[index++].text = string.Format("Favorite Emote: [{0}]", favoriteEmoteKeybind);
            if (InputUtils_Compat.Enabled && index < customControlTipLines.Length)
                customControlTipLines[index++].text = "Set Quick Emote : [Keybind #]";

            //if (usingController) controlTipLines[index++].text = string.Format("Perform Emote: [{0}]", performEmoteKeybind);

            for (; index < customControlTipLines.Length; index++)
                customControlTipLines[index].text = "";
        }


        public static void UpdateEmoteWheel()
        {
            if (currentLoadoutEmotesList == allUnlockedEmotesFiltered)
            {
                SortFilteredEmotes();
                togglePanelComplementary.SetActive(true);
                togglePanel0.SetActive(true);
                togglePanel1.SetActive(true);
                togglePanel2.SetActive(true);
                togglePanel3.SetActive(true);
            }
            else
            {
                togglePanelComplementary.SetActive(false);
                togglePanel0.SetActive(false);
                togglePanel1.SetActive(false);
                togglePanel2.SetActive(false);
                togglePanel3.SetActive(false);
            }

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
                Color emoteBackgroundColor = defaultUIColor;
                Color emoteTextColor = Color.white;
                if (emoteIndex < currentLoadoutEmotesList.Count)
                {
                    UnlockableEmote emote = currentLoadoutEmotesList[emoteIndex];
                    if (emote != null)
                    {
                        emoteUI.emote = emote;
                        emoteUI.textContainer.text = emote.displayName;
                        int quickEmoteIndex = EmotesManager.allQuickEmotes.IndexOf(emote.emoteName);
                        if (quickEmoteIndex >= 0 && quickEmoteIndex < EmotesManager.allQuickEmotes.Count)
                            emoteUI.textContainer.text += " [" + (quickEmoteIndex + 1) + "]";
                        //emoteTextColor = emote.favorite ? Color.green : Color.white;
                        if (!emote.complementary && ColorUtility.TryParseHtmlString(UnlockableEmote.rarityColorCodes[emote.rarity], out var emoteColor))
                        {
                            if (ConfigSettings.colorCodeEmoteBackgroundInRadialMenu.Value)
                                emoteBackgroundColor = emoteColor;
                            else if (ConfigSettings.colorCodeEmoteNamesInRadialMenu.Value)
                                emoteTextColor = emoteColor;
                        }
                    }
                }
                emoteUI.baseColor = emoteBackgroundColor;
                emoteUI.textContainer.color = emoteTextColor;
                emoteUI.OnHover(false);
            }
            if (hoveredEmoteUIIndex >= 0 && hoveredEmoteUIIndex < 8)
                OnHoveredNewElement(hoveredEmoteUIIndex);
        }


        private static void SortFilteredEmotes()
        {
            allUnlockedEmotesFiltered.Clear();
            if (!hideEmotesComplementaryToggle.isOn)
                allUnlockedEmotesFiltered.AddRange(EmotesManager.complementaryEmotes);
            if (!hideEmotes0Toggle.isOn)
                allUnlockedEmotesFiltered.AddRange(SessionManager.unlockedEmotesTier0);
            if (!hideEmotes1Toggle.isOn)
                allUnlockedEmotesFiltered.AddRange(SessionManager.unlockedEmotesTier1);
            if (!hideEmotes2Toggle.isOn)
                allUnlockedEmotesFiltered.AddRange(SessionManager.unlockedEmotesTier2);
            if (!hideEmotes3Toggle.isOn)
                allUnlockedEmotesFiltered.AddRange(SessionManager.unlockedEmotesTier3);

            for (int i = SessionManager.unlockedFavoriteEmotes.Count - 1; i >= 0; i--)
            {
                if (!allUnlockedEmotesFiltered.Contains(SessionManager.unlockedFavoriteEmotes[i]))
                    allUnlockedEmotesFiltered.Add(SessionManager.unlockedFavoriteEmotes[i]);
            }

            // Try because I don't trust anything
            try
            {
                allUnlockedEmotesFiltered.Sort((item1, item2) => string.Compare(item1.displayName, item2.displayName, true, EmotesManager.defaultSortCulture));
            }
            catch (Exception e)
            {
                if (ConfigSettings.verboseLogs.Value)
                    LogWarningVerbose("Failed to apply default emote sort in emote menu. Reverting to original sort method.");
                allUnlockedEmotesFiltered.Sort((item1, item2) => item1.displayName.CompareTo(item2.displayName));
            }
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


        public static void OpenEmoteMenu()
        {
            if (!localPlayerController)
                return;

            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (firstTimeOpeningMenu)
            {
                Assert(emoteLoadouts != null && emoteLoadouts.Count > 0, "Error opening emote menu. Emote loadouts are null or empty!");
                int loadoutIndex = emoteLoadouts[0].Count > 0 ? 0 : currentLoadoutIndex;
                SetCurrentEmoteLoadout(loadoutIndex);
                if (currentLoadoutIndex < emoteLoadouts.Count - 1)
                    SortFilteredEmotes();
                customControlTipLinesParent.position = HUDManager.Instance.controlTipLines[0].transform.parent.position;
                firstTimeOpeningMenu = false;
            }

            currentMuteSetting = AudioManager.muteEmoteAudio;
            currentEmoteOnlyMode = AudioManager.emoteOnlyMode;
            currentDmcaFreeSetting = AudioManager.dmcaFreeMode;
            currentVolumeSetting = AudioManager.emoteVolumeMultiplier;

            currentFirstPersonEmotes = ThirdPersonEmoteController.firstPersonEmotesEnabled;
            currentAllowMovingWhileEmoting = ThirdPersonEmoteController.allowMovingWhileEmoting;

            if (ConfigSync.instance.syncForceDisableMovingWhileEmoting && allowMovingWhileEmotingGameObject)
                allowMovingWhileEmotingGameObject.SetActive(false);

            if (Assert(menuGameObject != null, "Error opening emote menu. Menu gameobject is null!"))
                menuGameObject.SetActive(true);
            if (Assert(quickMenuManager != null, "Error opening emote menu. Quick menu manager gameobject is null!"))
                quickMenuManager.isMenuOpen = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AnimationPreviewer.UpdatePlayerSuit();
            currentThumbstickPosition = Vector2.zero;

            var defaultControlTipLines = HUDManager.Instance?.controlTipLines;
            if (defaultControlTipLines != null)
            {
                foreach (var controlTipLine in defaultControlTipLines)
                {
                    if (controlTipLine != null)
                        controlTipLine.enabled = false;
                }
            }
            foreach (var controlTipLine in ThirdPersonEmoteController.customControlTipLines)
            {
                if (controlTipLine != null)
                    controlTipLine.enabled = false;
            }
            UpdateControlTipLines();
            UpdateEmoteWheel();
            if (currentLoadoutEmotesList != allUnlockedEmotesFiltered) // UpdateEmoteWheel will do this
                SortFilteredEmotes();
        }


        public static void CloseEmoteMenu()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (localPlayerController)
                localPlayerController.isFreeCamera = false;
            if (menuGameObject)
                menuGameObject.SetActive(false);
            if (quickMenuManager && quickMenuManager.isMenuOpen)
            {
                if (quickMenuManager.settingsPanel.activeSelf)
                {
                    IngamePlayerSettings.Instance.DiscardChangedSettings();
                }

                quickMenuManager.CloseQuickMenuPanels();
                quickMenuManager.menuContainer.SetActive(value: false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                quickMenuManager.isMenuOpen = false;
            }

            OnHoveredNewLoadoutElement(-1);
            AnimationPreviewer.SetPreviewAnimation(null);
            AdditionalPanelUI.hovered = false;

            SaveFilterPreferences();
            if (ThirdPersonEmoteController.firstPersonEmotesEnabled != currentFirstPersonEmotes || ThirdPersonEmoteController.allowMovingWhileEmoting != currentAllowMovingWhileEmoting)
                ThirdPersonEmoteController.SavePreferences();
            if (AudioManager.muteEmoteAudio != currentMuteSetting || AudioManager.emoteOnlyMode != currentEmoteOnlyMode || AudioManager.dmcaFreeMode != currentDmcaFreeSetting || AudioManager.emoteVolumeMultiplier != currentVolumeSetting)
                AudioManager.SavePreferences();

            var defaultControlTipLines = HUDManager.Instance?.controlTipLines;
            if (defaultControlTipLines != null)
            {
                foreach (var controlTipLine in defaultControlTipLines)
                {
                    if (controlTipLine != null)
                        controlTipLine.enabled = true;
                }
            }
            foreach (var controlTipLine in ThirdPersonEmoteController.customControlTipLines)
            {
                if (controlTipLine != null)
                    controlTipLine.enabled = true;
            }
        }


        public static bool CanOpenEmoteMenu()
        {
            if (quickMenuManager.isMenuOpen && !isMenuOpen)
                return false;
            if (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return false;
            if (localPlayerController.isPlayerDead || localPlayerController.inSpecialInteractAnimation || localPlayerController.inTerminalMenu || localPlayerController.isTypingChat || localPlayerController.inSpecialInteractAnimation || localPlayerController.isGrabbingObjectAnimation || localPlayerController.inShockingMinigame || localPlayerController.isClimbingLadder || localPlayerController.isSinking)
                return false;
            if (localPlayerController.inAnimationWithEnemy != null || CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer())
                return false;
            return true;
        }


        public static void OnUpdateToggleMuteEmote(Toggle toggle)
        {
            AudioManager.muteEmoteAudio = toggle.isOn;
            Log("Toggling mute emote audio: " + AudioManager.muteEmoteAudio);
            foreach (var emoteAudioSource in EmoteAudioSource.allEmoteAudioSources)
                emoteAudioSource.UpdateVolume();
            DiscoBallPatcher.OnUpdateMuteEmotes();
        }


        public static void OnUpdateToggleEmoteOnlyMode(Toggle toggle)
        {
            AudioManager.emoteOnlyMode = toggle.isOn;
            Log("Toggling emote only mode: " + AudioManager.emoteOnlyMode);
        }


        public static void OnUpdateToggleDmcaFreeMode(Toggle toggle)
        {
            AudioManager.dmcaFreeMode = toggle.isOn;
            Log("Toggling DMCA free mode: " + AudioManager.dmcaFreeMode);
            foreach (var emoteAudioSource in EmoteAudioSource.allEmoteAudioSources)
                emoteAudioSource.RefreshAudio();
        }

        
        public static void OnUpdateToggleFirstPerson(Toggle toggle)
        {
            ThirdPersonEmoteController.UpdateFirstPersonEmoteMode(toggle.isOn);
            Log("Toggling first person emotes: " + ThirdPersonEmoteController.firstPersonEmotesEnabled);
        }


        public static void OnUpdateToggleAllowMovingWhileEmoting(Toggle toggle)
        {
            ThirdPersonEmoteController.SetCanMoveWhileEmoting(toggle.isOn);
            Log("Toggling allow moving while emoting: " + ThirdPersonEmoteController.allowMovingWhileEmoting);
        }


        public static void OnUpdateEmoteVolume(Slider volumeSlider)
        {
            AudioManager.emoteVolumeMultiplier = Mathf.Clamp(volumeSlider.value, 0, 2);
            foreach (var emoteAudioSource in EmoteAudioSource.allEmoteAudioSources)
                emoteAudioSource.UpdateVolume();
        }


        public static void OnUpdateFilteredEmotes()
        {
            if (currentLoadoutEmotesList == allUnlockedEmotesFiltered)
                UpdateEmoteWheel();
        }


        public static void SaveFilterPreferences()
        {
            try
            {
                Log("Saving TooManyEmotes emote menu preferences.");
                ES3.Save("TooManyEmotes.HideEmotesComplementary", hideEmotesComplementaryToggle.isOn, SaveManager.TooManyEmotesSaveFileName);
                ES3.Save("TooManyEmotes.HideEmotes0", hideEmotes0Toggle.isOn, SaveManager.TooManyEmotesSaveFileName);
                ES3.Save("TooManyEmotes.HideEmotes1", hideEmotes1Toggle.isOn, SaveManager.TooManyEmotesSaveFileName);
                ES3.Save("TooManyEmotes.HideEmotes2", hideEmotes2Toggle.isOn, SaveManager.TooManyEmotesSaveFileName);
                ES3.Save("TooManyEmotes.HideEmotes3", hideEmotes3Toggle.isOn, SaveManager.TooManyEmotesSaveFileName);
            }
            catch (Exception e) { LogErrorVerbose("Error while trying to save TooManyEmotes emote menu data.\n" + e); }
        }


        public static void LoadFilterPreferences()
        {
            try
            {
                // This code will be removed eventually
                ES3.DeleteKey("TooManyEmotes.HideEmotesComplementary");
                ES3.DeleteKey("TooManyEmotes.HideEmotes0");
                ES3.DeleteKey("TooManyEmotes.HideEmotes1");
                ES3.DeleteKey("TooManyEmotes.HideEmotes2");
                ES3.DeleteKey("TooManyEmotes.HideEmotes3");
            } catch { }

            try
            {
                hideEmotesComplementaryToggle.isOn = ES3.Load("TooManyEmotes.HideEmotesComplementary", SaveManager.TooManyEmotesSaveFileName, false);
                hideEmotes0Toggle.isOn = ES3.Load("TooManyEmotes.HideEmotes0", SaveManager.TooManyEmotesSaveFileName, false);
                hideEmotes1Toggle.isOn = ES3.Load("TooManyEmotes.HideEmotes1", SaveManager.TooManyEmotesSaveFileName, false);
                hideEmotes2Toggle.isOn = ES3.Load("TooManyEmotes.HideEmotes2", SaveManager.TooManyEmotesSaveFileName, false);
                hideEmotes3Toggle.isOn = ES3.Load("TooManyEmotes.HideEmotes3", SaveManager.TooManyEmotesSaveFileName, false);
            }
            catch
            {
                try
                {
                    ES3.DeleteKey("TooManyEmotes.HideEmotesComplementary", SaveManager.TooManyEmotesSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.HideEmotes0", SaveManager.TooManyEmotesSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.HideEmotes1", SaveManager.TooManyEmotesSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.HideEmotes2", SaveManager.TooManyEmotesSaveFileName);
                    ES3.DeleteKey("TooManyEmotes.HideEmotes3", SaveManager.TooManyEmotesSaveFileName);
                } catch { }
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed")]
        [HarmonyPrefix]
        public static bool OnScrollMouse(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (!context.performed || __instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled || !isMenuOpen || usingController)
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
        public static bool PreventItemSecondaryUseInMenu(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return true;
            return !isMenuOpen;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ItemTertiaryUse_performed")]
        [HarmonyPrefix]
        public static bool PreventItemTertiaryUseInMenu(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return true;
            return !isMenuOpen;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Interact_performed")]
        [HarmonyPrefix]
        public static bool PreventItemInteractInMenu(InputAction.CallbackContext context, PlayerControllerB __instance)
        {
            if (__instance != localPlayerController || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
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
        public RectTransform uiRectTransform;
        public int id;
        public Image backgroundImage;
        public TextMeshPro textContainer;
        public Color baseColor;
        public UnlockableEmote emote;

        public void OnHover(bool hovered = true)
        {
            Color newColor = baseColor * (hovered ? 1f : 0.5f);
            newColor.a = hovered ? EmoteMenu.hoveredAlpha : EmoteMenu.unhoveredAlpha;
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

        void Start() { }

        public void OnHover(bool hovered = true)
        {
            Color newColor = (EmoteMenu.currentLoadoutIndex == id ? EmoteMenu.selectedLoadoutUIColor : EmoteMenu.defaultUIColor) * (hovered ? 1f : 0.5f);
            newColor.a = hovered ? EmoteMenu.hoveredAlpha : EmoteMenu.unhoveredAlpha;
            backgroundImage.color = newColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EmoteMenu.OnHoveredNewLoadoutElement(id);
            OnHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EmoteMenu.OnHoveredNewLoadoutElement(-1);
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

