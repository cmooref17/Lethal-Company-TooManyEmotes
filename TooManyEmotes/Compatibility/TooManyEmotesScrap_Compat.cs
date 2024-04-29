using HarmonyLib;
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;
using UnityEngine;
using System;

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

            if (!ES3.KeyExists("shipGrabbableItemIDs", currentSaveFileName))
                return;
            
            try
            {
                if (!ES3.KeyExists("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName))
                    return;
            }
            catch (Exception e)
            {
                LogErrorVerbose("Failed to load existing TooManyEmotesScrap.GrabbablePropIndexes from file: " + currentSaveFileName + ". Deleting key.");
                LogErrorVerbose(e.ToString());
                ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
                return;
            }

            int[] itemIds = null;
            int[] grabbablePropIndexes = null;
            int startId = 0;
            int numProps = 0;
            try
            {
                itemIds = ES3.Load<int[]>("shipGrabbableItemIDs", currentSaveFileName);
                grabbablePropIndexes = ES3.Load<int[]>("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
                startId = ES3.Load<int>("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
                numProps = ES3.Load<int>("TooManyEmotes.NumGrabbableProps", currentSaveFileName);
            }
            catch (Exception e)
            {
                LogErrorVerbose("Failed to load existing TooManyEmotesScrap data from file: " + currentSaveFileName + ". Deleting keys.");
                LogErrorVerbose(e.ToString());
                ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
                ES3.DeleteKey("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
                ES3.DeleteKey("TooManyEmotes.NumGrabbableProps", currentSaveFileName);
                return;
            }

            LogWarningVerbose("Fixing TooManyEmotes ghost emote props. NumProps: " + grabbablePropIndexes.Length + " StartId: " + startId + " NumPropsVar: " + numProps + " ItemListSize: " + allItems.Count);

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
                LogWarningVerbose("Removed " + numPropsFixed + " ghost emote props.");
            }

            ES3.DeleteKey("TooManyEmotes.GrabbablePropIndexes", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.StartGrabbablePropItemId", currentSaveFileName);
            ES3.DeleteKey("TooManyEmotes.NumGrabbableProps", currentSaveFileName);
        }
    }
}
