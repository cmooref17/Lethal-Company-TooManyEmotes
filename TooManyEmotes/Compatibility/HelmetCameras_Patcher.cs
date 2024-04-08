using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class HelmetCameras_Patcher
    {
        public static bool Enabled { get { return Chainloader.PluginInfos.ContainsKey("RickArg.lethalcompany.helmetcameras"); } }


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        private static void ApplyPatch(StartOfRound __instance)
        {
            if (!Enabled)
                return;

            __instance.StartCoroutine(ApplyPatchDelayed());
        }


        private static IEnumerator ApplyPatchDelayed()
        {
            yield return new WaitForSeconds(6);

            var camera = GameObject.Find("HelmetCamera")?.GetComponent<Camera>();
            if (camera)
            {
                camera.cullingMask |= 1 << 23;
                camera.cullingMask &= ~(1 << 5);
            }
        }
    }
}
