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
using TooManyEmotes.Patches;
using System.Reflection;
using Unity.Netcode;
using MoreCompany.Cosmetics;
using BepInEx.Bootstrap;
using System.Runtime.CompilerServices;

namespace TooManyEmotes.CompatibilityPatcher
{
    [HarmonyPatch]
    internal class MoreEmotesPatcher
    {
        public static bool loadedMoreEmotes = false;

        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void ApplyPatch()
        {
            if (Plugin.IsModLoaded("MoreEmotes"))
            {
                if (Chainloader.PluginInfos.TryGetValue("MoreEmotes", out var pluginInfo))
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


    [HarmonyPatch]
    internal class BetterEmotesPatcher
    {
        public static bool loadedBetterEmotes = false;

        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPostfix]
        public static void ApplyPatch()
        {
            if (Plugin.IsModLoaded("BetterEmotes"))
            {
                if (Chainloader.PluginInfos.TryGetValue("BetterEmotes", out var pluginInfo))
                {
                    Assembly assembly = pluginInfo.Instance.GetType().Assembly;
                    if (assembly != null)
                    {
                        Plugin.Log("Applying compatibility patch for BetterEmotes");

                        Type internalClassType = assembly.GetType("BetterEmote.EmotePatch");
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

            if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                MoreCompanyPatch();
            }
        }

        // seperate method without inlining to avoid throwing errors on chat message
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MoreCompanyPatch()
        {
            CosmeticApplication val = UnityEngine.Object.FindObjectOfType<CosmeticApplication>();
            if (CosmeticRegistry.locallySelectedCosmetics.Count <= 0 || val.spawnedCosmetics.Count > 0)
            {
                return;
            }
            foreach (string locallySelectedCosmetic in CosmeticRegistry.locallySelectedCosmetics)
            {
                val.ApplyCosmetic(locallySelectedCosmetic, true);
            }
            foreach (CosmeticInstance spawnedCosmetic in val.spawnedCosmetics)
            {
                Transform transform = ((Component)spawnedCosmetic).transform;
                transform.localScale *= 0.38f;
                SetAllChildrenLayer(((Component)spawnedCosmetic).transform, 23);
            }
        }

        private static void SetAllChildrenLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform item in transform)
                SetAllChildrenLayer(item, layer);
        }
    }


    [HarmonyPatch]
    internal class MirrorDecorPatcher
    {
        public static bool loadedMirrorDecor = false;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        public static void ApplyPatch()
        {
            if (Plugin.IsModLoaded("quackandcheese.mirrordecor"))
            {
                loadedMirrorDecor = true;
                ThirdPersonEmoteController.localPlayerBodyLayer = 23;
                ThirdPersonEmoteController.defaultShadowCastingMode = ShadowCastingMode.On;
                Plugin.Log("Applied patch for MirrorDecor");
            }
        }
    }
}