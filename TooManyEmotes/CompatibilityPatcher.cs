﻿using System;
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
using TooManyEmotes.Patches;
using System.Reflection;
using Unity.Netcode;
using MoreCompany.Cosmetics;

namespace TooManyEmotes.CompatibilityPatcher {

    [HarmonyPatch]
    public class MoreEmotesPatcher
    {

        public static bool loadedMoreEmotes = false;

        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void ApplyPatch()
        {
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
                                animatorControllerFieldLocal.SetValue(null, new AnimatorOverrideController(animatorControllerLocal));
                        }

                        FieldInfo animatorControllerFieldOther = internalClassType.GetField("others", BindingFlags.Public | BindingFlags.Static);
                        RuntimeAnimatorController animatorControllerOther = (RuntimeAnimatorController)animatorControllerFieldOther.GetValue(null);
                        if (animatorControllerOther != null)
                        {
                            if (!(animatorControllerOther is AnimatorOverrideController))
                                animatorControllerFieldOther.SetValue(null, new AnimatorOverrideController(animatorControllerOther));
                        }
                    }
                }
            }
        }
    }

    internal class BiggerLobbyPatcher
    {

        public static bool loadedBiggerLobby = false;

        [HarmonyPatch(typeof(NetworkSceneManager), "PopulateScenePlacedObjects")]
        [HarmonyPostfix]
        public static void ApplyPatch()
        {
            if (Plugin.IsModLoaded("BiggerLobby"))
            {
                var startOfRound = StartOfRound.Instance;
                if (startOfRound == null) return;
                for (int i = 0; i < startOfRound.allPlayerScripts.Length; i++)
                {
                    if (startOfRound.allPlayerScripts[i]?.playerBodyAnimator?.runtimeAnimatorController != null)
                    {
                        startOfRound.allPlayerScripts[i].playerBodyAnimator.runtimeAnimatorController = new AnimatorOverrideController(startOfRound.otherClientsAnimatorController);
                    }
                }
                loadedBiggerLobby = true;
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
    }
}