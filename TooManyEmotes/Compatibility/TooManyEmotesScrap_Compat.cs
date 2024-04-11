using HarmonyLib;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using UnityEngine;

namespace TooManyEmotes.Compatibility
{
    [HarmonyPatch]
    internal static class TooManyEmotesScrap_Compat
    {
        internal static bool Enabled { get { return Plugin.IsModLoaded("FlipMods.TooManyEmotesScrap"); } }

        [HarmonyPatch(typeof(StartOfRound), "LoadShipGrabbableItems")]
        [HarmonyPrefix]
        private static void FixGhostEmotePropsIfModDisabled(StartOfRound __instance)
        {
            // Only remove ghost props if TooManyEmotesScrap is disabled
            if (Enabled)
                return;

            if (!ES3.KeyExists("shipGrabbableItemIDs", currentSaveFileName) || !ES3.KeyExists("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName))
                return;
            
            var itemIds = ES3.Load<int[]>("shipGrabbableItemIDs", currentSaveFileName);
            var grabbablePropIndexes = ES3.Load<int[]>("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
            int startId = ES3.Load<int>("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
            int numProps = ES3.Load<int>("TooManyEmotes.NumGrabbableProps", currentSaveFileName);

            LogWarning("NumProps: " + grabbablePropIndexes.Length + " StartId: " + startId + " NumPropsVar: " + numProps + " ItemListSize: " + allItems.Count);

            int numPropsFixed = 0;
            foreach (int index in grabbablePropIndexes)
            {
                if (index < 0 || index >= itemIds.Length)
                    continue;

                int itemId = itemIds[index];
                if (itemId >= Mathf.Max(startId, 0) && itemId < Mathf.Min(startId + numProps, allItems.Count))
                    continue;

                numPropsFixed++;
                itemIds[index] = 19; // Turn to big bolt?
            }

            if (numPropsFixed > 0)
            {
                ES3.Save("shipGrabbableItemIDs", itemIds, currentSaveFileName);
                LogWarning("Removed " + numPropsFixed + " ghost emote props.");
            }

            ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.NumGrabbableProps", currentSaveFileName);
        }
    }
}
