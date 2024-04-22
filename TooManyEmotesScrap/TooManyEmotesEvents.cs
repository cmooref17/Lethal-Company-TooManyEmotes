using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotesScrap.Props;
using static TooManyEmotesScrap.CustomLogging;

namespace TooManyEmotesScrap
{
    [HarmonyPatch]
    public static class TooManyEmotesEvents
    {
        [HarmonyPatch(typeof(TooManyEmotes.Networking.ConfigSync), "OnSynced")]
        [HarmonyPostfix]
        private static void OnConfigSynced()
        {
            GrabbableEmotePropManager.AddGrabbableEmotePropsMoons();
            GrabbableEmotePropManager.CheckIfShouldRemovePropEmotesFromUnlockedEmotes();
        }
    }
}
