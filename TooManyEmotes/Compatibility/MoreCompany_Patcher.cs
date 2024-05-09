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
        public static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"); } }
        internal static bool Patched = false;

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void Init()
        {
            Patched = false;
        }


        [HarmonyPatch(typeof(HUDManager), "AddTextMessageClientRpc")]
        [HarmonyPrefix]
        private static bool OnAddTextMessageClientRpc(string chatMessage, HUDManager __instance)
        {
            if (networkManager == null || !networkManager.IsListening)
                return true;

            bool isClientExecStage = (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() == 2;
            if (isClientExecStage && (isClient || isHost))
            {
                if (Enabled)
                    Patch();
                return true;
            }
            return !isClientExecStage && (isServer || isHost);
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Patch()
        {
            if (Patched)
                return;

            CosmeticApplication cosmetics = UnityEngine.Object.FindObjectOfType<CosmeticApplication>();
            if (cosmetics == null || cosmetics.spawnedCosmetics.Count > 0)
                return;

            Log("Applying MoreCompany Cosmetics patch.");
            Patched = true;

            if (CosmeticRegistry.locallySelectedCosmetics.Count <= 0)
                return;

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
