using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using UnityEngine.Rendering;
using System.Collections;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using System.Reflection;
using Unity.Netcode;
using MoreEmotes.Patch;
using MoreCompany.Cosmetics;

namespace TooManyEmotes.CompatibilityPatcher {

    [HarmonyPatch]
    internal class MoreEmotesPatcher {

        public static bool loadedMoreEmotes = false;

        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void ApplyPatch() {
            if (Plugin.IsModLoaded("MoreEmotes"))
            {
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("MoreEmotes", out var pluginInfo))
                {
                    Assembly assembly = pluginInfo.Instance.GetType().Assembly;
                    if (assembly != null)
                    {
                        Plugin.Log("Applying compatibility patch for More_Emotes");

                        Type internalClassType = assembly.GetType("MoreEmotes.Patch.EmotePatch");
                        FieldInfo animatorControllerFieldLocal = internalClassType.GetField("local", BindingFlags.Public | BindingFlags.Static);
                        RuntimeAnimatorController animatorControllerLocal = (RuntimeAnimatorController)animatorControllerFieldLocal.GetValue(null);
                        if (animatorControllerLocal != null)
                        {
                            if (!(animatorControllerLocal is AnimatorOverrideController))
                            {
                                loadedMoreEmotes = true;
                                animatorControllerLocal = new AnimatorOverrideController(animatorControllerLocal);
                                animatorControllerFieldLocal.SetValue(null, animatorControllerLocal);
                            }
                        }

                        FieldInfo animatorControllerFieldOther = internalClassType.GetField("others", BindingFlags.Public | BindingFlags.Static);
                        RuntimeAnimatorController animatorControllerOther = (RuntimeAnimatorController)animatorControllerFieldOther.GetValue(null);
                        if (animatorControllerOther != null)
                        {
                            if (!(animatorControllerOther is AnimatorOverrideController))
                            {
                                loadedMoreEmotes = true;
                                animatorControllerOther = new AnimatorOverrideController(animatorControllerOther);
                                animatorControllerFieldOther.SetValue(null, animatorControllerOther);
                            }
                        }
                    }
                }
                if (!loadedMoreEmotes)
                    Plugin.LogError("Failed to patch compatibility with More_Emotes");
            }
        }
    }


    [HarmonyPatch]
    internal class MoreCompanyPatcher {

        public static bool loadedMoreCompany = false;
        public static List<GameObject> cosmeticInstances = new List<GameObject>();

        [HarmonyPatch(typeof(HUDManager), "AddPlayerChatMessageClientRpc")]
        [HarmonyPostfix]
        public static void ApplyPatch() {

            if (Plugin.IsModLoaded("me.swipez.melonloader.morecompany"))
            {
                try
                {
                    CosmeticApplication cosmeticApplication = GameObject.FindObjectOfType<CosmeticApplication>();

                    if (cosmeticApplication.spawnedCosmetics.Count > 0)
                        return;
                    foreach (string id in CosmeticRegistry.locallySelectedCosmetics)
                        cosmeticApplication.ApplyCosmetic(id, true);
                    foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
                    {
                        cosmetic.transform.localScale *= 0.38f;
                        SetAllChildrenLayer(cosmetic.transform, 23);
                    }
                    loadedMoreCompany = true;
                    Plugin.Log("Applied patch for MoreCompany Cosmetics");
                }
                catch { }
            }
        }

        private static void SetAllChildrenLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform item in transform)
                SetAllChildrenLayer(item, layer);
        }


        public static void ShowCosmetics(bool show) {
            if (loadedMoreCompany && cosmeticInstances != null && cosmeticInstances.Count > 0)
            {
                int i = 0;
                foreach (var cosmetic in cosmeticInstances)
                {
                    if (cosmetic == null) continue;
                    cosmetic.gameObject.SetActive(show);
                    i++;
                }
            }
        }
    }

    /*
    [HarmonyPatch]
    internal class MirrorDecorPatcher
    {

        public static bool loadedMirrorDecor = false;

        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void ApplyPatch()
        {
            if (Plugin.IsModLoaded("quackandcheese.mirrordecor"))
            {
                Plugin.Log("MirrorDecor is loaded!");
                loadedMirrorDecor = true;
            }
        }
    }
    */
}