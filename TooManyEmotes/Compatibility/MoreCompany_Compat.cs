using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany;
using MoreCompany.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Audio;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class MoreCompany_Compat
    {
        internal static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"); } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ShowLocalCosmetics(Transform playerRoot = null)
        {
            try
            {
                // If cosmetics not enabled in MoreCompany
                if (CosmeticRegistry.locallySelectedCosmetics == null || CosmeticRegistry.locallySelectedCosmetics.Count <= 0)
                    return;

                Transform cosmeticRoot = playerRoot != null ? playerRoot : StartOfRound.Instance?.localPlayerController?.transform;
                if (cosmeticRoot == null) return;

                var cosmeticApplication = cosmeticRoot.GetComponentInChildren<CosmeticApplication>();

                if (cosmeticApplication && cosmeticApplication.spawnedCosmetics != null && cosmeticApplication.spawnedCosmetics.Count != 0)
                {
                    foreach (var item in cosmeticApplication.spawnedCosmetics)
                    {
                        if (item == null) continue;
                        SetAllChildrenLayer(item.transform, 0);
                        item.gameObject.SetActive(true);
                    }
                    return;
                }

                // Guard: don't add CosmeticApplication to roots that lack cosmetic anchors (e.g. preview models)
                if (!cosmeticApplication)
                {
                    // Check if root has a bone structure that cosmetics can attach to (e.g. spine or HEAD anchor)
                    var spine = cosmeticRoot.Find("spine");
                    if (spine == null)
                    {
                        // Try finding any child named spine recursively
                        spine = FindChildRecursive("spine", cosmeticRoot);
                    }
                    if (spine == null)
                    {
                        LogWarning("MoreCompany: Skipping cosmetic application — no valid bone anchors found on root: " + cosmeticRoot.name);
                        return;
                    }
                    cosmeticApplication = cosmeticRoot.gameObject.AddComponent<CosmeticApplication>();
                }

                foreach (var cosmetic in CosmeticRegistry.locallySelectedCosmetics)
                {
                    try
                    {
                        ApplyCosmeticSafe(cosmeticApplication, cosmetic);
                    }
                    catch (Exception e)
                    {
                        LogWarning("MoreCompany: Failed to apply cosmetic '" + cosmetic + "': " + e.Message);
                    }
                }

                if (cosmeticApplication.spawnedCosmetics != null)
                {
                    foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
                    {
                        if (cosmetic != null)
                            cosmetic.transform.localScale *= CosmeticRegistry.COSMETIC_PLAYER_SCALE_MULT;
                    }
                }
            }
            catch (Exception e)
            {
                LogWarning("MoreCompany ShowLocalCosmetics failed: " + e.Message);
            }
        }


        /// <summary>
        /// Calls ApplyCosmetic via reflection so return-type or signature changes in MoreCompany updates
        /// do not cause MissingMethodException at runtime.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyCosmeticSafe(CosmeticApplication cosmeticApp, string cosmeticId)
        {
            try
            {
                // Try direct call first (fastest path)
                cosmeticApp.ApplyCosmetic(cosmeticId, true);
            }
            catch (MissingMethodException)
            {
                // Fallback: use reflection to find whatever ApplyCosmetic signature exists
                try
                {
                    var methods = cosmeticApp.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(m => m.Name == "ApplyCosmetic").ToArray();

                    if (methods.Length > 0)
                    {
                        var method = methods[0];
                        var parameters = method.GetParameters();
                        if (parameters.Length == 2)
                            method.Invoke(cosmeticApp, new object[] { cosmeticId, true });
                        else if (parameters.Length == 1)
                            method.Invoke(cosmeticApp, new object[] { cosmeticId });
                        else
                            LogWarning("MoreCompany: ApplyCosmetic has unexpected parameter count: " + parameters.Length);
                    }
                    else
                    {
                        LogWarning("MoreCompany: ApplyCosmetic method not found via reflection.");
                    }
                }
                catch (Exception e)
                {
                    LogWarning("MoreCompany: Reflection fallback for ApplyCosmetic failed: " + e.Message);
                }
            }
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HideLocalCosmetics()
        {
            try
            {
                Transform cosmeticRoot = StartOfRound.Instance?.localPlayerController?.transform;
                if (cosmeticRoot == null) return;

                var cosmeticApplication = cosmeticRoot.GetComponentInChildren<CosmeticApplication>();

                if (cosmeticApplication && cosmeticApplication.spawnedCosmetics != null && cosmeticApplication.spawnedCosmetics.Count != 0)
                {
                    foreach (var item in cosmeticApplication.spawnedCosmetics)
                    {
                        if (item != null)
                            SetAllChildrenLayer(item.transform, 23);
                    }
                }
            }
            catch (Exception e)
            {
                LogWarning("MoreCompany HideLocalCosmetics failed: " + e.Message);
            }
        }


        private static void SetAllChildrenLayer(Transform transform, int layer)
        {
            try
            {
                transform.gameObject.layer = layer;
                foreach (var light in transform.gameObject.GetComponents<Light>())
                    light.cullingMask = 1 << layer;

                foreach (Transform item in transform)
                    SetAllChildrenLayer(item, layer);
            }
            catch { } // Probably fine
        }


        private static Transform FindChildRecursive(string name, Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var result = FindChildRecursive(name, child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}