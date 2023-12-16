using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;
//using UnityEngine.EventSystems;
using UnityEditor;
using UnityEngine.InputSystem;
using TMPro;

namespace TooManyEmotes {
    /*
    [HarmonyPatch]
    public class EmoteMenuManager_Old {
        
        public static int numEmoteSlots = 8;
        public static GameObject emoteElementPrefab;
        public static HUDElement[] hudElements;
        public static HUDElement hudElement;
        public static CanvasGroup uiContainer;
        private static List<EmoteElementUI> emoteElements = new List<EmoteElementUI>();
        private static EmoteElementUI currentHoveredElement;

        [HarmonyPatch(typeof(HUDManager), "Awake")]
        [HarmonyPostfix]
        public static void InitializeUI(HUDManager __instance) {

            GameObject emoteUIContainerObject = new GameObject("EmoteWheelUI", new Type[] { typeof(RectTransform), typeof(CanvasGroup) });
            uiContainer = emoteUIContainerObject.GetComponent<CanvasGroup>();

            uiContainer.transform.parent = __instance.HUDContainer.transform.parent;
            RectTransform rectTransform = uiContainer.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localScale = Vector3.one;

            //uiContainer.interactable = false;

            float angleStep = 360f / numEmoteSlots;
            for (int i = 0; i < numEmoteSlots; i++)
            {
                var emoteElementUI = new EmoteElementUI(i);
                emoteElements.Add(emoteElementUI);
                emoteElementUI.rectTransform.parent = uiContainer.transform;
                emoteElementUI.rectTransform.localScale = Vector3.one;
                float angle = i * angleStep * Mathf.Deg2Rad;
                emoteElementUI.rectTransform.anchoredPosition3D = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 100f;
            }

        }


        [HarmonyPatch(typeof(HUDManager), "Update")]
        [HarmonyPostfix]
        public static void GetInput() {
            
        }

    }


    public class EmoteElementUI {


        public int id = -1;
        public GameObject gameObject;
        public RectTransform rectTransform;
        private Image backgroundImage;
        private TextMeshProUGUI textComponent;
        private bool hovered;


        public EmoteElementUI(int id) {
            this.id = id;
            gameObject = new GameObject("EmoteUIElement_" + id, new Type[] { typeof(RectTransform), typeof(Image) });
            rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 100); // Set the size of the circle here

            backgroundImage = gameObject.GetComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 64f / 255f);

            backgroundImage.material = new Material(Shader.Find("UI/Default"));
            //Texture2D circleTexture = Texture2D.blackTexture;
            //backgroundImage.sprite = Sprite.Create(circleTexture, new Rect(0.0f, 0.0f, circleTexture.width, circleTexture.height), new Vector2(0.5f, 0.5f), 100.0f);


            backgroundImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0.0f, 0.0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
            backgroundImage.type = Image.Type.Filled;
            backgroundImage.fillMethod = Image.FillMethod.Radial360;
            backgroundImage.fillAmount = 1.0f;



            GameObject textContainer = new GameObject("Text");
            textContainer.transform.parent = rectTransform;
            textComponent = textContainer.AddComponent<TextMeshProUGUI>();
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            textComponent.fontSize = 8;
            textComponent.text = "Test";
        }


        public void SetText(string text) {
            textComponent.text = text;
        }

        public void OnHover(bool isHovering) {
            if (isHovering && !hovered)
            {
                Plugin.Log("OnHover EmoteElementUI");
                hovered = isHovering;
            }
        }


        public void OnClick() {
            Plugin.Log("OnClick EmoteElementUI");
        }
        
    }
*/
}

