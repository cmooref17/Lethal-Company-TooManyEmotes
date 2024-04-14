using BepInEx.Bootstrap;
using Discord;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class MoreCompany_Patcher
    {
        public static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"); } }


        [HarmonyPatch(typeof(HUDManager), "AddTextMessageClientRpc")]
        [HarmonyPrefix]
        private static bool ApplyPatch(string chatMessage, HUDManager __instance)
        {
            if (Enabled)
            {
                if (!Plugin.IsModLoaded("com.potatoepet.AdvancedCompany"))
                    Patch();
            }
            bool isClientExecStage = (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() == 2;
            return (!isClientExecStage && (networkManager.IsServer || networkManager.IsHost)) || (isClientExecStage && (networkManager.IsClient || networkManager.IsHost));
        }


        // seperate method without inlining to avoid throwing errors on chat message
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Patch()
        {
            CosmeticApplication cosmetics = UnityEngine.Object.FindObjectOfType<CosmeticApplication>();
            if (cosmetics == null)
                return;

            if (CosmeticRegistry.locallySelectedCosmetics.Count <= 0 || cosmetics.spawnedCosmetics.Count > 0)
                return;

            Log("Applying MoreCompany Cosmetics patch.");

            foreach (string locallySelectedCosmetic in CosmeticRegistry.locallySelectedCosmetics)
                cosmetics.ApplyCosmetic(locallySelectedCosmetic, true);

            foreach (CosmeticInstance spawnedCosmetic in cosmetics.spawnedCosmetics)
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
