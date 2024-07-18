using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany;
using MoreCompany.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
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
    internal static class MoreCompany_Patcher
    {
        internal static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"); } }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ShowLocalCosmetics(Transform playerRoot = null)
        {
            // If cosmetics not enabled in MoreCompany
            if (!MainClass.cosmeticsSyncOther.Value || CosmeticRegistry.locallySelectedCosmetics.Count <= 0)
                return;

            Transform cosmeticRoot = playerRoot != null ? playerRoot : StartOfRound.Instance.localPlayerController.transform;
            var cosmeticApplication = cosmeticRoot?.GetComponentInChildren<CosmeticApplication>();

            if (cosmeticApplication && cosmeticApplication.spawnedCosmetics.Count != 0)
            {
                foreach (var item in cosmeticApplication.spawnedCosmetics)
                {
                    SetAllChildrenLayer(item.transform, 0);
                    item.gameObject.SetActive(true);
                }
                return;
            }

            if (!cosmeticApplication)
                cosmeticApplication = cosmeticRoot.gameObject.AddComponent<CosmeticApplication>();
            foreach (var cosmetic in CosmeticRegistry.locallySelectedCosmetics)
                cosmeticApplication.ApplyCosmetic(cosmetic, true);
            foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
                cosmetic.transform.localScale *= CosmeticRegistry.COSMETIC_PLAYER_SCALE_MULT;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HideLocalCosmetics(Transform playerRoot = null)
        {
            Transform cosmeticRoot = playerRoot != null ? playerRoot : StartOfRound.Instance.localPlayerController.transform;
            var cosmeticApplication = cosmeticRoot?.GetComponentInChildren<CosmeticApplication>();

            if (cosmeticApplication && cosmeticApplication.spawnedCosmetics.Count != 0)
            {
                foreach (var item in cosmeticApplication.spawnedCosmetics)
                    SetAllChildrenLayer(item.transform, 23);
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
