using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using UnityEngine;
using TooManyEmotes.Compatibility;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public class DressGirlVisibilityPatcher
    {
        [HarmonyPatch(typeof(EnemyAI), "EnableEnemyMesh")]
        [HarmonyPostfix]
        public static void HideGirlMesh(bool enable, EnemyAI __instance, bool overrideDoNotSet = false)
        {
            if (/*!ConfigSettings.enableGirlPatch.Value || */LethalVRM_Compat.Enabled && __instance is DressGirlAI)
            {
                foreach (var renderer in __instance.skinnedMeshRenderers)
                    renderer.enabled = renderer.gameObject.layer != 23;
                foreach (var renderer in __instance.meshRenderers)
                    renderer.enabled = renderer.gameObject.layer != 23;
            }
        }
    }
}
