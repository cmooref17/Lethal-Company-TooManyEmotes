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
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("MoreEmotes"))
            {
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("MoreEmotes", out var pluginInfo))
                {
                    Assembly assembly = pluginInfo.Instance.GetType().Assembly;
                    if (assembly != null)
                    {
                        Type internalClassType = assembly.GetType("MoreEmotes.Patch.EmotePatch");
                        FieldInfo runtimeAnimatorControllerField = internalClassType.GetField("local", BindingFlags.Public | BindingFlags.Static);
                        RuntimeAnimatorController runtimeAnimatorController = (RuntimeAnimatorController)runtimeAnimatorControllerField.GetValue(null);
                        if (runtimeAnimatorController != null)
                        {
                            if (!(runtimeAnimatorController is AnimatorOverrideController))
                            {
                                loadedMoreEmotes = true;
                                Plugin.Log("Applying compatibility patch for More_Emotes");
                                runtimeAnimatorController = new AnimatorOverrideController(runtimeAnimatorController);
                                runtimeAnimatorControllerField.SetValue(null, runtimeAnimatorController);
                                return;
                            }
                        }
                    }
                }
                Plugin.LogError("Failed to patch compatibility with More_Emotes");
            }
        }
    }

    /*
    [HarmonyPatch]
    internal class MoreCompanyPatcher {

        public static bool loadedMoreCompany = false;
        public static CosmeticApplication cosmeticApplication;

        [HarmonyPatch(typeof(HUDManager), "AddPlayerChatMessageClientRpc")]
        [HarmonyPostfix]
        public static void ApplyPatch() {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                cosmeticApplication = GameObject.FindObjectOfType<CosmeticApplication>(); // __instance.gameObject.GetComponentInChildren<CosmeticApplication>();
                if (cosmeticApplication != null)
                {
                    if (CosmeticRegistry.locallySelectedCosmetics.Count > 0 && cosmeticApplication.spawnedCosmetics.Count <= 0)
                    {
                        foreach (string id in CosmeticRegistry.locallySelectedCosmetics)
                            cosmeticApplication.ApplyCosmetic(id, false);
                    }
                    loadedMoreCompany = true;
                    Plugin.Log("Applied patch for MoreCompany Cosmetics");
                    return;
                }
                Plugin.Log("Failed to apply patch for MoreCompany Cosmetics");
            }
            else
                Plugin.LogError("MoreCompany not loaded");
        }


        public static void ShowCosmetics(bool show) {
            if (loadedMoreCompany && cosmeticApplication != null && cosmeticApplication.spawnedCosmetics.Count > 0)
            {
                foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
                    cosmetic.gameObject.SetActive(show);
            }
        }
    }
    */
}