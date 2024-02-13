using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TooManyEmotes.Networking;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static UnityEngine.EventSystems.EventTrigger;
using System.Text.RegularExpressions;
using System;

namespace TooManyEmotes.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<bool> unlockEverything;
        public static ConfigEntry<bool> shareEverything;
        public static ConfigEntry<bool> syncUnsharedEmotes;
        //public static ConfigEntry<bool> enableFirstPersonEmotes;
        public static ConfigEntry<bool> enableMovingWhileEmoting;
        public static ConfigEntry<bool> disableEmotesForSelf;
        public static ConfigEntry<string> rotateCharacterInEmoteKeybind;
        public static ConfigEntry<bool> toggleRotateCharacterInEmote;

        public static ConfigEntry<bool> disableRaritySystem;
        public static ConfigEntry<int> basePriceEmoteRaritySystemDisabled;

        public static ConfigEntry<int> startingEmoteCredits;
        public static ConfigEntry<float> addEmoteCreditsMultiplier;
        public static ConfigEntry<bool> purchaseEmotesWithDefaultCurrency;

        public static ConfigEntry<float> priceMultiplierEmotesStore;
        public static ConfigEntry<int> basePriceEmoteTier0;
        public static ConfigEntry<int> basePriceEmoteTier1;
        public static ConfigEntry<int> basePriceEmoteTier2;
        public static ConfigEntry<int> basePriceEmoteTier3;

        public static ConfigEntry<int> numEmotesStoreRotation;
        public static ConfigEntry<float> rotationChanceEmoteTier0;
        public static ConfigEntry<float> rotationChanceEmoteTier1;
        public static ConfigEntry<float> rotationChanceEmoteTier2;
        public static ConfigEntry<float> rotationChanceEmoteTier3;
        
        public static ConfigEntry<bool> enableMaskedEnemiesEmoting;
        public static ConfigEntry<float> maskedEnemiesEmoteChanceOnEncounter;
        public static ConfigEntry<bool> maskedEnemiesAlwaysEmoteOnFirstEncounter;
        public static ConfigEntry<bool> enableSyncingEmotesWithMaskedEnemies;
        public static ConfigEntry<bool> overrideStopAndStareDuration;
        public static ConfigEntry<string> maskedEnemyEmoteRandomDelay;
        public static ConfigEntry<string> maskedEnemyEmoteRandomDuration;

        //public static ConfigEntry<int> numMysteryEmotesStoreRotation;
        public static ConfigEntry<string> openEmoteMenuKeybind;
        public static ConfigEntry<bool> toggleEmoteMenu;
        public static ConfigEntry<bool> reverseEmoteWheelScrollDirection;

        public static ConfigEntry<string> quickEmoteFavorite1Keybind;
        public static ConfigEntry<string> quickEmoteFavorite2Keybind;
        public static ConfigEntry<string> quickEmoteFavorite3Keybind;
        public static ConfigEntry<string> quickEmoteFavorite4Keybind;
        public static ConfigEntry<string> quickEmoteFavorite5Keybind;
        public static ConfigEntry<string> quickEmoteFavorite6Keybind;
        public static ConfigEntry<string> quickEmoteFavorite7Keybind;
        public static ConfigEntry<string> quickEmoteFavorite8Keybind;

        public static ConfigEntry<string> emoteNameColorTier0;
        public static ConfigEntry<string> emoteNameColorTier1;
        public static ConfigEntry<string> emoteNameColorTier2;
        public static ConfigEntry<string> emoteNameColorTier3;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();
        public static List<string> configSections = new List<string>();

        public static void BindConfigSettings()
        {
            Plugin.Log("BindingConfigs");

            unlockEverything = AddConfigEntry("Emote Settings", "I am a Party Pooper", false, "[Host only] If true, every emote will be unlocked in your emote wheel at the start of the game. Also, you're not really a party pooper.");
            shareEverything = AddConfigEntry("Emote Settings", "ShareEverything", true, "[Host only] This setting will be ignored if \"I am a Party Pooper\" is enabled. If this setting is set to false, emotes in the store will be different for each player. Unlocking emotes will only unlock for the player that purchased the emote. Each player will have their own emote credits. The amount of emote credits that each player will receive will NOT be reduced.");
            syncUnsharedEmotes = AddConfigEntry("Emote Settings", "CanSyncUnsharedEmotes", true, "[Host only] Only applies if ShareEverything is false. If set to true, players will be able to sync emotes with other players, even if they do not have the emote being performed unlocked.");
            //enableFirstPersonEmotes = AddConfigEntry("Emote Settings", "EnableFirstPersonEmotes", false, "This may currently have bugs.");
            enableMovingWhileEmoting = AddConfigEntry("Emote Settings", "CanMoveWhileEmoting", false, "[Host only] If set to true, rotating while emoting will be automatic. To cancel an emote, you will press the vanilla menu button.");
            disableEmotesForSelf = AddConfigEntry("Emote Settings", "DisableEmotingForSelf", false, "Disabling this will not convert your player's animator controller to an AnimatorOverrideController, and you will not be able to perform custom emotes. Disable this in case of specific mod conflicts. You will still be able to see other players emoting.");
            rotateCharacterInEmoteKeybind = AddConfigEntry("Emote Settings", "RotateCharacterInEmoteKeybind", "<Keyboard>/leftAlt", "Keybind to hold to rotate character while performing a custom emote. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            toggleRotateCharacterInEmote = AddConfigEntry("Emote Settings", "ToggleRotateCharacterInEmote", false, "If true, rotating character while emoting will be toggled, instead of rotating while holding the hotkey.");

            disableRaritySystem = AddConfigEntry("Emote Store", "DisableRaritySystem", false, "[Host only] If true, every emote will have the same likelyhood of appearing in the emote store.");
            basePriceEmoteRaritySystemDisabled = AddConfigEntry("Emote Store", "BasePriceEmote - Rarity System Disabled", 100, "[Host only] Base price of emotes if the rarity system is disabled.");

            startingEmoteCredits = AddConfigEntry("Emote Store", "StartingEmoteCredits", 100, "[Host only] The number of emote credits you start each game with.");
            addEmoteCreditsMultiplier = AddConfigEntry("Emote Store", "AddEmoteCreditsMultiplier", 0.4f, "[Host only] You gain emote credits based off this multiplier of normal group credits earned. Example: If set to the default, 0.25, and you earn 200 group credits, you will also gain 50 emote credits.");
            purchaseEmotesWithDefaultCurrency = AddConfigEntry("Emote Store", "PurchaseEmotesWithDefaultCredits", true, "[Host only] Setting this to true will allow you to purchase emotes with normal group credits once you run out of emote credits. This setting will automatically be disabled if ShareEverything is false.");

            priceMultiplierEmotesStore = AddConfigEntry("Emote Store", "PriceMultiplierEmotesStore", 1.0f, "[Host only] Price multiplier for emotes in the store. Only applies if UnlockEverythingAtStart is false.");
            basePriceEmoteTier0 = AddConfigEntry("Emote Store", "PriceCommonEmote", 50, "[Host only] The base price of [common]emotes in the store.");
            basePriceEmoteTier1 = AddConfigEntry("Emote Store", "PriceRareEmote", 100, "[Host only] The base price of [rare] emotes in the store.");
            basePriceEmoteTier2 = AddConfigEntry("Emote Store", "PriceEpicEmote", 200, "[Host only] The base price of [epic] emotes in the store.");
            basePriceEmoteTier3 = AddConfigEntry("Emote Store", "PriceLegendaryEmote", 300, "[Host only] The base price of [legendary] emotes in the store.");

            numEmotesStoreRotation = AddConfigEntry("Emote Store", "EmotesInStoreRotation", 6, "[Host only] The number of emotes that will be available at a time in the store. Only applies if UnlockEverythingAtStart is false.");
            rotationChanceEmoteTier0 = AddConfigEntry("Emote Store", "RotationWeightCommonEmote", 0.55f, "[Host only] The likelyhood of [common] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier1 = AddConfigEntry("Emote Store", "RotationWeightRareEmote", 0.35f, "[Host only] The likelyhood of [rare] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier2 = AddConfigEntry("Emote Store", "RotationWeightEpicEmote", 0.08f, "[Host only] The likelyhood of [epic] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier3 = AddConfigEntry("Emote Store", "RotationWeightLegendaryEmote", 0.02f, "[Host only] The likelyhood of [legendary] emotes appearing (per slot) in the store rotation.");

            enableMaskedEnemiesEmoting = AddConfigEntry("MaskedEnemyEmotes - Beta", "EnableMaskedEnemiesEmoting", true, "[Host only] Enabling this alone does not change the behaviour of the Masked Enemies, and shouldn't conflict with other mods.");
            maskedEnemiesEmoteChanceOnEncounter = AddConfigEntry("MaskedEnemyEmotes - Beta", "EmoteChanceOnEncounter", 0.25f, "[Host only] Chance per encounter with a Masked Enemy, for them to perform an emote. Use values between 0 and 1.");
            maskedEnemiesAlwaysEmoteOnFirstEncounter = AddConfigEntry("MaskedEnemyEmotes - Beta", "AlwaysEmoteOnFirstEncounter", true, "[Host only] This will force the first encounter (for each player) with a Masked Enemy to trigger an emote, regardless of EmoteChanceOnEncounter.");
            enableSyncingEmotesWithMaskedEnemies = AddConfigEntry("MaskedEnemyEmotes - Beta", "EnableSyncingEmotesWithMaskedEnemies", true, "[Client-side] Enabling this will allow you to sync emotes with Masked Enemies. This config is mainly here to disable in case of strange issues.");
            maskedEnemyEmoteRandomDelay = AddConfigEntry("MaskedEnemyEmotes - Beta", "RandomEmoteDelay", "1.5,2.0", "[Host only] Random range at which Masked Enemies will delay before performing an emote. These values could be raised a bit if OverrideStopAndStareDuration is enabled, otherwise, you may run into emotes ending quickly.");
            overrideStopAndStareDuration = AddConfigEntry("MaskedEnemyEmotes - Beta", "OverrideStopAndStareDuration", true, "[Host only] Enabling this will allow this mod to extend the stop and stare duration for longer emotes. If disabled, emotes may end very quickly. Disable this setting if you run into mod conflicts.");
            maskedEnemyEmoteRandomDuration = AddConfigEntry("MaskedEnemyEmotes - Beta", "RandomEmoteDuration", "2.0,4.0", "[Host only] Random range on how long Masked Enemies will emote for. This will extend the Masked Enemies' stop and stare duration by this amount. Only applies if OverrideStopAndStareDuration is true.");

            // numMysteryEmotesStoreRotation = "Server settings", "NumMysteryEmotesInStoreRotation", 1, "[Host only] The number of \"mystery\" emotes that will be available at a time in the store. These emotes will be a mystery until unlocked. Only applies if UnlockEverythingAtStart is false.");
            openEmoteMenuKeybind = AddConfigEntry("Emote Radial Menu", "OpenEmoteMenuKeybind", "<Keyboard>/backquote", "NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            toggleEmoteMenu = AddConfigEntry("Emote Radial Menu", "ToggleEmoteMenu", false, "If set to false, the emote menu will open upon pressing the related keybind, and close upon releasing, and will play the currently hovered emote.");
            reverseEmoteWheelScrollDirection = AddConfigEntry("Emote Radial Menu", "ReverseEmoteWheelScrollDirection", false, "Reverses the page swapping direction in your emote when scrolling.");

            /*
            quickEmoteFavorite1Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 1 Keybind", "", "Hotkey for performing favorite emote 1. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite2Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 2 Keybind", "", "Hotkey for performing favorite emote 2. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite3Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 3 Keybind", "", "Hotkey for performing favorite emote 3. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite4Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 4 Keybind", "", "Hotkey for performing favorite emote 4. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite5Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 5 Keybind", "", "Hotkey for performing favorite emote 5. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite6Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 6 Keybind", "", "Hotkey for performing favorite emote 6. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite7Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 7 Keybind", "", "Hotkey for performing favorite emote 7. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            quickEmoteFavorite8Keybind = AddConfigEntry("Emote Radial Menu", "Perform Favorite Emote 8 Keybind", "", "Hotkey for performing favorite emote 8. This keybind will also be used to assign favorited emotes to a hotkey in the emote menu, favorites tab. NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            */

            emoteNameColorTier0 = AddConfigEntry("Accessibility", "EmoteNameColorCommon", "#00FF00", "The color of the [common] emote name in the terminal.");
            emoteNameColorTier1 = AddConfigEntry("Accessibility", "EmoteNameColorRare", "#2828FF", "The color of the [rare] emote name in the terminal.");
            emoteNameColorTier2 = AddConfigEntry("Accessibility", "EmoteNameColorEpic", "#AA00EE", "The color of the [epic] emote name in the terminal.");
            emoteNameColorTier3 = AddConfigEntry("Accessibility", "EmoteNameColorLegendary", "#FF2222", "The color of the [legendary] emote name in the terminal.");

            // fix weights
            float totalChances = rotationChanceEmoteTier0.Value;
            totalChances += rotationChanceEmoteTier1.Value;
            totalChances += rotationChanceEmoteTier2.Value;
            totalChances += rotationChanceEmoteTier3.Value;

            if (totalChances != 1 && totalChances != 0)
            {
                rotationChanceEmoteTier0.Value /= totalChances;
                rotationChanceEmoteTier1.Value /= totalChances;
                rotationChanceEmoteTier2.Value /= totalChances;
                rotationChanceEmoteTier3.Value /= totalChances;
                Plugin.instance.Config.Save();
            }

            TryRemoveOldConfigSettings();
            //RecoverConfigSectionNames();
            ConfigSync.BuildDefaultConfigSync();
        }


        public static ConfigEntry<T> AddConfigEntry<T>(string section, string name, T defaultValue, string description)
        {
            /*
            if (!configSections.Contains(section))
                configSections.Add(section);
            int index = configSections.IndexOf(section);
            ConfigEntry<T> configEntry = Plugin.instance.Config.Bind(string.Format("!!!{0}_{1}", index, section), name, defaultValue, description);
            */
            ConfigEntry<T> configEntry = Plugin.instance.Config.Bind(section, name, defaultValue, description);
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }

        /*
        public static string GetDisplayName(string key)
        {
            try
            {
                if (key.Length <= 1)
                    return key;

                int replaceIndex = key.IndexOf(">/");
                key = replaceIndex >= 0 ? key.Substring(replaceIndex + 2) : key;

                string displayName = key.ToLower();
                if (displayName.Contains("not-bound"))
                    return "";
                
                displayName = displayName.Replace("leftalt", "Alt");
                displayName = displayName.Replace("rightalt", "Alt");
                displayName = displayName.Replace("leftctrl", "Ctrl");
                displayName = displayName.Replace("rightctrl", "Ctrl");
                displayName = displayName.Replace("leftshift", "Shift");
                displayName = displayName.Replace("rightshift", "Shift");
                displayName = displayName.Replace("leftbutton", "LMB");
                displayName = displayName.Replace("rightbutton", "RMB");
                displayName = displayName.Replace("middlebutton", "MMB");
                displayName = displayName.Replace("lefttrigger", "LT");
                displayName = displayName.Replace("righttrigger", "RT");
                displayName = displayName.Replace("leftshoulder", "LB");
                displayName = displayName.Replace("rightshoulder", "RB");
                displayName = displayName.Replace("leftstickpress", "LS");
                displayName = displayName.Replace("rightstickpress", "RS");
                displayName = displayName.Replace("dpad/", "DPad-");

                displayName = displayName.Replace("backquote", "`");

                try { displayName = char.ToUpper(displayName[0]) + displayName.Substring(1); }
                catch { }

                return displayName;
            }
            catch { return ""; }
        }
        */

        /*
        public static void RecoverConfigSectionNames()
        {
            try
            {
                ConfigFile config = Plugin.instance.Config;
                string filepath = config.ConfigFilePath;

                if (File.Exists(filepath))
                {
                    string contents = File.ReadAllText(filepath);
                    contents = Regex.Replace(contents, "!!![0-9]+_", "");
                    File.WriteAllText(filepath, contents);
                    config.Reload();
                }
            }
            catch { }
        }
        */


        public static void TryRemoveOldConfigSettings()
        {
            HashSet<string> headers = new HashSet<string>();
            HashSet<string> keys = new HashSet<string>();

            foreach (ConfigEntryBase entry in currentConfigEntries.Values) {
                headers.Add(entry.Definition.Section);
                keys.Add(entry.Definition.Key);
            }

            try
            {
                Plugin.Log("Cleaning old config entries");
                ConfigFile config = Plugin.instance.Config;
                string filepath = config.ConfigFilePath;

                if (File.Exists(filepath))
                {
                    string contents = File.ReadAllText(filepath);
                    string[] lines = File.ReadAllLines(filepath); // Because contents.Split('\n') is adding strange characters...

                    string currentHeader = "";

                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = lines[i].Replace("\n", "");
                        if (lines[i].Length <= 0)
                            continue;

                        if (lines[i].StartsWith("["))
                        {
                            if (currentHeader != "" && !headers.Contains(currentHeader))
                            {
                                currentHeader = "[" + currentHeader + "]";
                                int index0 = contents.IndexOf(currentHeader);
                                int index1 = contents.IndexOf(lines[i]);
                                contents = contents.Remove(index0, index1 - index0);
                            }
                            currentHeader = lines[i].Replace("[", "").Replace("]", "").Trim();
                        }

                        else if (currentHeader != "")
                        {
                            if (i <= (lines.Length - 4) && lines[i].StartsWith("##"))
                            {
                                int numLinesEntry = 1;
                                while (i + numLinesEntry < lines.Length && lines[i + numLinesEntry].Length > 3) // 3 because idc
                                    numLinesEntry++;

                                if (headers.Contains(currentHeader))
                                {
                                    int indexAssignOperator = lines[i + numLinesEntry - 1].IndexOf("=");
                                    string key = lines[i + numLinesEntry - 1].Substring(0, indexAssignOperator - 1);
                                    if (!keys.Contains(key))
                                    {
                                        int index0 = contents.IndexOf(lines[i]);
                                        int index1 = contents.IndexOf(lines[i + numLinesEntry - 1]) + lines[i + numLinesEntry - 1].Length;
                                        contents = contents.Remove(index0, index1 - index0);
                                    }
                                }
                                i += (numLinesEntry - 1);
                            }
                            else if (lines[i].Length > 3)
                                contents = contents.Replace(lines[i], "");
                        }
                    }

                    if (!headers.Contains(currentHeader)) {
                        currentHeader = "[" + currentHeader + "]";
                        int index0 = contents.IndexOf(currentHeader);
                        contents = contents.Remove(index0, contents.Length - index0);
                    }

                    while (contents.Contains("\n\n\n"))
                        contents = contents.Replace("\n\n\n", "\n\n");

                    File.WriteAllText(filepath, contents);
                    config.Reload();
                }
            }
            catch { } // Probably okay
        }
    }
}