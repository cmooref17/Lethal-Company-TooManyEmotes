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
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class MoreCompany_Patcher
    {
        public static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"); } }

        [HarmonyPatch(typeof(HUDManager), "AddPlayerChatMessageClientRpc")]
        [HarmonyPrefix]
        private static void ApplyPatch(HUDManager __instance)
        {
            // Not client exec stage
            if ((int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() != 2)
                return;

            if (Enabled)
            {
                if (!Plugin.IsModLoaded("com.potatoepet.AdvancedCompany"))
                    Patch();
            }
        }


        // seperate method without inlining to avoid throwing errors on chat message
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Patch()
        {
            CosmeticApplication val = UnityEngine.Object.FindObjectOfType<CosmeticApplication>();
            if (CosmeticRegistry.locallySelectedCosmetics.Count <= 0 || val.spawnedCosmetics.Count > 0)
                return;

            Log("Applying MoreCompany Cosmetics patch.");

            foreach (string locallySelectedCosmetic in CosmeticRegistry.locallySelectedCosmetics)
                val.ApplyCosmetic(locallySelectedCosmetic, true);

            foreach (CosmeticInstance spawnedCosmetic in val.spawnedCosmetics)
            {
                Transform transform = spawnedCosmetic.transform;
                transform.localScale *= 0.38f;
                SetAllChildrenLayer(spawnedCosmetic.transform, 23);
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
