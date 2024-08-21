using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany;
using MoreCompany.Cosmetics;
using AdvancedCompany.Cosmetics;
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
    internal static class AdvancedCompany_Compat
    {
        internal static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("com.potatoepet.AdvancedCompany"); } }

        private static PlayerControllerB localPlayerController;
        private static List<GameObject> acCosmetics = new List<GameObject>();
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ShowLocalCosmetics()
        {
            if (localPlayerController != StartOfRound.Instance.localPlayerController)
            {
                localPlayerController = StartOfRound.Instance.localPlayerController;
                acCosmetics.Clear();
                var cosmeticInstances = localPlayerController.GetComponentsInChildren<AdvancedCompany.Cosmetics.CosmeticInstance>();
                foreach (var cosmetic in cosmeticInstances)
                    acCosmetics.Add(cosmetic.gameObject);
            }

            foreach (var cosmetic in acCosmetics)
            {
                SetAllChildrenLayer(cosmetic.transform, 0);
                cosmetic.SetActive(true);
            }
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HideLocalCosmetics()
        {
            if (localPlayerController != StartOfRound.Instance.localPlayerController)
                return;

            foreach (var cosmetic in acCosmetics)
                SetAllChildrenLayer(cosmetic.transform, 23);
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