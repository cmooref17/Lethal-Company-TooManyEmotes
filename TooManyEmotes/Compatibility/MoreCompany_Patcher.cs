using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    public static class MoreCompanyPatcher
    {
        public static bool Enabled = false;
        public static bool appliedPatch = false;
        public static List<GameObject> cosmeticInstances = new List<GameObject>();
        public static bool appliedCosmetics = false;

        [HarmonyPatch(typeof(HUDManager), "AddPlayerChatMessageClientRpc")]
        [HarmonyPrefix]
        public static void ApplyPatch()
        {
            if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                Enabled = true;
                if (!Plugin.IsModLoaded("com.potatoepet.AdvancedCompany"))
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
            Plugin.Log("Applying MoreCompany Cosmetics patch.");
            appliedCosmetics = true;
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
            foreach (var light in transform.gameObject.GetComponents<Light>())
                light.cullingMask = 1 << layer;

            foreach (Transform item in transform)
                SetAllChildrenLayer(item, layer);
        }
    }
}
