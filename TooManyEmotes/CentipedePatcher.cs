using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TooManyEmotes.Patches
{
    [HarmonyPatch]
    public static class CentipedePatcher
    {
        static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static HashSet<CentipedeAI> latchedOnCentipedesLocalPlayer = new HashSet<CentipedeAI>();

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void Init()
        {
            latchedOnCentipedesLocalPlayer.Clear();
        }


        [HarmonyPatch(typeof(CentipedeAI), "ClingToPlayer")]
        [HarmonyPostfix]
        public static void OnCentipedeLatchOntoLocalPlayer(PlayerControllerB playerScript, CentipedeAI __instance)
        {
            if (__instance.clingingToPlayer == localPlayerController)
            {
                latchedOnCentipedesLocalPlayer.Add(__instance);
                if (PlayerPatcher.playerDataLocal.isPerformingEmote)
                {
                    Plugin.LogWarning("Centipede latched onto local player while emoting. Canceling emote.");
                    localPlayerController.performingEmote = false;
                    PlayerPatcher.OnUpdateCustomEmote(-1, localPlayerController);
                    localPlayerController.StopPerformingEmoteServerRpc();
                }
            }
        }


        [HarmonyPatch(typeof(CentipedeAI), "OnDisable")]
        [HarmonyPostfix]
        public static void RemoveCentipedeFromList(CentipedeAI __instance)
        {
            if (latchedOnCentipedesLocalPlayer.Contains(__instance))
                latchedOnCentipedesLocalPlayer.Remove(__instance);
        }


        public static bool IsCentipedeLatchedOntoLocalPlayer()
        {
            if (localPlayerController == null)
                return false;

            foreach (var centipede in latchedOnCentipedesLocalPlayer)
            {
                if (localPlayerController == centipede?.clingingToPlayer)
                    return true;
            }
            return false;
        }

    }
}
