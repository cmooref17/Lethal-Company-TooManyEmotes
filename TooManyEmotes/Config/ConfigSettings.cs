using BepInEx.Configuration;
using TooManyEmotes.Networking;
using System.Collections.Generic;
using System.IO;
using static TooManyEmotes.CustomLogging;
using TooManyEmotes.Patches;

namespace TooManyEmotes.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<bool> unlockEverything;
        public static ConfigEntry<bool> shareEverything;
        public static ConfigEntry<bool> persistentUnlocks;
        public static ConfigEntry<bool> persistentUnlocksGlobal;
        public static ConfigEntry<bool> persistentEmoteCredits;
        public static ConfigEntry<bool> syncUnsharedEmotes;

        public static ConfigEntry<bool> disableEmotesForSelf;
        public static ConfigEntry<bool> toggleRotateCharacterInEmote;

        public static ConfigEntry<bool> enableGrabbableEmoteProps;

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

        public static ConfigEntry<string> openEmoteMenuKeybind;
        public static ConfigEntry<bool> toggleEmoteMenu;
        public static ConfigEntry<bool> reverseEmoteWheelScrollDirection;
        public static ConfigEntry<bool> colorCodeEmoteNamesInRadialMenu;
        public static ConfigEntry<bool> colorCodeEmoteBackgroundInRadialMenu;

        public static ConfigEntry<string> quickEmoteFavorite1Keybind;
        public static ConfigEntry<string> quickEmoteFavorite2Keybind;
        public static ConfigEntry<string> quickEmoteFavorite3Keybind;
        public static ConfigEntry<string> quickEmoteFavorite4Keybind;
        public static ConfigEntry<string> quickEmoteFavorite5Keybind;
        public static ConfigEntry<string> quickEmoteFavorite6Keybind;
        public static ConfigEntry<string> quickEmoteFavorite7Keybind;
        public static ConfigEntry<string> quickEmoteFavorite8Keybind;

        public static ConfigEntry<bool> disableBoomboxRequirement;
        public static ConfigEntry<bool> disableAudioShipSpeaker;
        public static ConfigEntry<float> baseEmoteAudioVolume;
        public static ConfigEntry<float> emoteAudioMaxVolume;
        public static ConfigEntry<float> emoteAudioIncreasePerPlayerSyncing;
        public static ConfigEntry<float> emoteAudioMinDistance;
        public static ConfigEntry<float> emoteAudioMaxDistance;

        public static ConfigEntry<string> emoteNameColorTier0;
        public static ConfigEntry<string> emoteNameColorTier1;
        public static ConfigEntry<string> emoteNameColorTier2;
        public static ConfigEntry<string> emoteNameColorTier3;

        public static ConfigEntry<bool> enableGirlPatch;
        public static ConfigEntry<bool> resetFavoriteOnNextStart;
        public static ConfigEntry<bool> resetGlobalUnlocksOnNextStart;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();
        public static List<string> configSections = new List<string>();

        internal static bool resetFavoriteEmotes = false;
        internal static bool resetGloballyUnlockedEmotes = false;

        public static void BindConfigSettings()
        {
            Log("BindingConfigs");

            unlockEverything = AddConfigEntry("Emote Settings", "I am a Party Pooper", false, "[Host only] If true, every emote will be unlocked at the start of the game. (You're not really a party pooper)");
            shareEverything = AddConfigEntry("Emote Settings", "ShareEverything", false, "[Host only] This setting will be ignored if \"I am a Party Pooper\" is enabled. If this setting is set to false, emotes in the store will be different for each player. Unlocking emotes will only unlock for the player that purchased the emote. Each player will have their own emote credits. The amount of emote credits that each player will receive will NOT be reduced.");
            persistentUnlocks = AddConfigEntry("Emote Settings", "PersistentUnlocks", false, "[Host only] If enabled, emotes will be unlocked per save, and will not reset upon ship resets, unless a new save is created.\nNOTE: This setting (as well as the other persistent settings) will be disabled if UnlockEverything (I am a Party Pooper) is enabled.");
            persistentUnlocksGlobal = AddConfigEntry("Emote Settings", "PersistentUnlocksGlobal", false, "[Host only] If enabled, emotes will be permanently unlocked for your character, and will be available when playing on any save. Only applies if PersistentUnlocks is set to true.\nIf ShareEverything is enabled, emotes that the host already has unlocked will NOT unlock for you upon joining the game, unless you also have them unlocked.\nIf ShareEverything is enabled, emotes unlocked by other players DURING the session will still permanently unlock for your character.\nNOTE: If enabled, all config settings are subject to be limited, or forced to their default values, in order to prevent unlocking emotes globally too fast. These settings may include starting emote credits, emote credits earned, number of emotes in store rotation, etc.");
            persistentEmoteCredits = AddConfigEntry("Emote Settings", "PersistentEmoteCredits", false, "[Host only] If enabled, emote credits will not reset upon ship resets. Only applies if PersistentUnlocks is enabled.\nThis setting will be disabled if PersistentUnlocksGlobal is enabled.");
            syncUnsharedEmotes = AddConfigEntry("Emote Settings", "CanSyncUnsharedEmotes", true, "[Host only] Only applies if ShareEverything is false. If set to true, players will be able to sync emotes with other players, even if they do not have the emote being performed unlocked.");

            disableEmotesForSelf = AddConfigEntry("Emote Settings", "DisableEmotingForSelf", false, "Disabling this will not convert your player's animator controller to an AnimatorOverrideController, and you will not be able to perform custom emotes. Disable this in case of specific mod conflicts. You will still be able to see other players emoting.");
            toggleRotateCharacterInEmote = AddConfigEntry("Emote Settings", "ToggleRotateCharacterInEmote", false, "If true, rotating character while emoting will be toggled, instead of rotating while holding the hotkey.");

            enableGrabbableEmoteProps = AddConfigEntry("Emote Settings", "EnableGrabbableEmoteProps", true, "[Host only] If true, certain emote props can be found as items in the world, and will be the trigger for specific emotes. The emotes performed on these props cannot be purchased as emotes from the terminal, but certain emote props may be purchasable as items in the terminal. If false, the emotes can be purchased normally via the terminal without the need of the emote prop.");

            disableRaritySystem = AddConfigEntry("Emote Store", "DisableRaritySystem", false, "[Host only] If true, every emote will have the same likelyhood of appearing in the emote store.");
            basePriceEmoteRaritySystemDisabled = AddConfigEntry("Emote Store", "BasePriceEmote - Rarity System Disabled", 100, "[Host only] Base price of emotes if the rarity system is disabled.");

            startingEmoteCredits = AddConfigEntry("Emote Store", "StartingEmoteCredits", 100, "[Host only] The number of emote credits you start each game with.");
            addEmoteCreditsMultiplier = AddConfigEntry("Emote Store", "AddEmoteCreditsMultiplier", 0.3333f, "[Host only] You gain emote credits based off this multiplier of normal group credits earned. Example: If set to the default, 0.25, and you earn 200 group credits, you will also gain 50 emote credits.");
            purchaseEmotesWithDefaultCurrency = AddConfigEntry("Emote Store", "PurchaseEmotesWithDefaultCredits", true, "[Host only] Setting this to true will allow you to purchase emotes with normal group credits once you run out of emote credits. This setting will automatically be disabled if ShareEverything is false.");

            priceMultiplierEmotesStore = AddConfigEntry("Emote Store", "PriceMultiplierEmotesStore", 1.0f, "[Host only] Price multiplier for emotes in the store. Only applies if UnlockEverythingAtStart is false.");
            basePriceEmoteTier0 = AddConfigEntry("Emote Store", "PriceCommonEmote", 50, "[Host only] The base price of [common]emotes in the store.");
            basePriceEmoteTier1 = AddConfigEntry("Emote Store", "PriceRareEmote", 100, "[Host only] The base price of [rare] emotes in the store.");
            basePriceEmoteTier2 = AddConfigEntry("Emote Store", "PriceEpicEmote", 200, "[Host only] The base price of [epic] emotes in the store.");
            basePriceEmoteTier3 = AddConfigEntry("Emote Store", "PriceLegendaryEmote", 300, "[Host only] The base price of [legendary] emotes in the store.");

            numEmotesStoreRotation = AddConfigEntry("Emote Store", "EmotesInStoreRotation", 6, "[Host only] The number of emotes that will be available at a time in the store. Only applies if UnlockEverythingAtStart is false.");
            rotationChanceEmoteTier0 = AddConfigEntry("Emote Store", "RotationWeightCommonEmote", 0.5f, "[Host only] The likelyhood of [common] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier1 = AddConfigEntry("Emote Store", "RotationWeightRareEmote", 0.35f, "[Host only] The likelyhood of [rare] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier2 = AddConfigEntry("Emote Store", "RotationWeightEpicEmote", 0.135f, "[Host only] The likelyhood of [epic] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier3 = AddConfigEntry("Emote Store", "RotationWeightLegendaryEmote", 0.015f, "[Host only] The likelyhood of [legendary] emotes appearing (per slot) in the store rotation.");

            enableMaskedEnemiesEmoting = AddConfigEntry("Masked Enemy Emotes", "EnableMaskedEnemiesEmoting", true, "[Host only] Enabling this alone does not change the behaviour of the Masked Enemies, and shouldn't conflict with other mods.");
            maskedEnemiesEmoteChanceOnEncounter = AddConfigEntry("Masked Enemy Emotes", "EmoteChanceOnEncounter", 0.25f, "[Host only] Chance per encounter with a Masked Enemy, for them to perform an emote. Use values between 0 and 1.");
            maskedEnemiesAlwaysEmoteOnFirstEncounter = AddConfigEntry("Masked Enemy Emotes", "AlwaysEmoteOnFirstEncounter", true, "[Host only] This will force the first encounter (for each player) with a Masked Enemy to trigger an emote, regardless of EmoteChanceOnEncounter.");
            enableSyncingEmotesWithMaskedEnemies = AddConfigEntry("Masked Enemy Emotes", "EnableSyncingEmotesWithMaskedEnemies", true, "[Client-side] Enabling this will allow you to sync emotes with Masked Enemies. This config is mainly here to disable in case of strange issues.");
            maskedEnemyEmoteRandomDelay = AddConfigEntry("Masked Enemy Emotes", "RandomEmoteDelay", "1.5,2.0", "[Host only] Random range at which Masked Enemies will delay before performing an emote. These values could be raised a bit if OverrideStopAndStareDuration is enabled, otherwise, you may run into emotes ending quickly.");
            overrideStopAndStareDuration = AddConfigEntry("Masked Enemy Emotes", "OverrideStopAndStareDuration", true, "[Host only] Enabling this will allow this mod to extend the stop and stare duration for longer emotes. If disabled, emotes may end very quickly. Disable this setting if you run into mod conflicts.");
            maskedEnemyEmoteRandomDuration = AddConfigEntry("Masked Enemy Emotes", "RandomEmoteDuration", "2.0,4.0", "[Host only] Random range on how long Masked Enemies will emote for. This will extend the Masked Enemies' stop and stare duration by this amount. Only applies if OverrideStopAndStareDuration is true.");

            openEmoteMenuKeybind = AddConfigEntry("Emote Radial Menu", "OpenEmoteMenuKeybind", "<Keyboard>/backquote", "NOTE: This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)");
            toggleEmoteMenu = AddConfigEntry("Emote Radial Menu", "ToggleEmoteMenu", false, "If set to false, the emote menu will open upon pressing the related keybind, and close upon releasing, and will play the currently hovered emote.");
            reverseEmoteWheelScrollDirection = AddConfigEntry("Emote Radial Menu", "ReverseEmoteWheelScrollDirection", false, "Reverses the page swapping direction in your emote when scrolling.");
            colorCodeEmoteNamesInRadialMenu = AddConfigEntry("Emote Radial Menu", "ColorCodeEmoteNamesInRadialMenuByRarity", false, "If true, emote names in the radial menu will be colored based on their rarity.");
            colorCodeEmoteBackgroundInRadialMenu = AddConfigEntry("Emote Radial Menu", "ColorCodeEmoteBackgroundInRadialMenu", false, "If true, the background UI element for each element in the radial menu will be colored based on their rarity.\nNOTE: Enabling this will force the emote names in the radial menu to have their default color.");

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

            disableBoomboxRequirement = AddConfigEntry("Emote Audio", "DisableBoomboxRequirement", false, "If set to true, emote audio that normally requires a nearby boombox will be played from your character instead.");
            disableAudioShipSpeaker = AddConfigEntry("Emote Audio", "DisableAudioOnShipSpeaker", false, "[Host only] This does nothing if DisableBoomboxRequirement is true. This setting is host only to ensure no de-synced audio sources.");
            baseEmoteAudioVolume = AddConfigEntry("Emote Audio", "BaseEmoteAudioVolume", 0.25f, "The base emote audio volume. The volume slider in the emote menu will be based off of this value.");
            emoteAudioMaxVolume = AddConfigEntry("Emote Audio", "MaxEmoteAudioVolume", 0.8f, "The max volume that emote audio will reach. Emote audio volume may dynamically change by increasing the number of players syncing an emote, or adjusting the volume slider in the emote menu. This setting will not affect emote audio volume, aside from preventing the volume from going higher than this value.");
            emoteAudioIncreasePerPlayerSyncing = AddConfigEntry("Emote Audio", "VolumeGainPerPlayerSyncingEmote", 0.05f, "By how much emote audio volume will increase by per player syncing with that emote.");
            emoteAudioMinDistance = AddConfigEntry("Emote Audio", "MinAudioDistance", 10f, "The range from an emote audio source at which the volume will start to fade.");
            emoteAudioMaxDistance = AddConfigEntry("Emote Audio", "MaxAudioDistance", 40f, "The range from an emote audio source at which the audio can no longer be heard.");
            
            emoteNameColorTier0 = AddConfigEntry("Accessibility", "EmoteNameColorCommon", "#00FF00", "The color of the [common] emote name in the terminal.");
            emoteNameColorTier1 = AddConfigEntry("Accessibility", "EmoteNameColorRare", "#2828FF", "The color of the [rare] emote name in the terminal.");
            emoteNameColorTier2 = AddConfigEntry("Accessibility", "EmoteNameColorEpic", "#AA00EE", "The color of the [epic] emote name in the terminal.");
            emoteNameColorTier3 = AddConfigEntry("Accessibility", "EmoteNameColorLegendary", "#FF2222", "The color of the [legendary] emote name in the terminal.");

            enableGirlPatch = AddConfigEntry("Other", "EnableGirlPatch", true, "If true, this mod will disable the girl's mesh while she's un the \"unrendered\" layer to prevent the third-person emote camera from seeing her when not supposed do. Disable this if this causes conflicts with another mod.");
            resetFavoriteOnNextStart = AddConfigEntry("Other", "ResetFavoritedEmotesOnNextStart", false, "Set this to true to force remove all emotes from your favorites when the game starts up next. This may resolve any issues that might be related to having favorited emotes that don't exist.\nThis setting will reset back to false once reset.");
            resetGlobalUnlocksOnNextStart = AddConfigEntry("Other", "ResetGlobalUnlocksOnNextStart", false, "Set this to true to force reset all globally unlocked emotes for your local player. These emotes are only usable when the host has PersistentUnlocksGlobal enabled in the config.\nThis setting will reset back to false once reset.");

            if (resetFavoriteOnNextStart.Value)
            {
                resetFavoriteEmotes = true;
                resetFavoriteOnNextStart.Value = false;
                Plugin.instance.Config.Save();
            }

            if (resetGlobalUnlocksOnNextStart.Value)
            {
                resetGloballyUnlockedEmotes = true;
                resetGlobalUnlocksOnNextStart.Value = false;
                Plugin.instance.Config.Save();
            }

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
        }


        public static ConfigEntry<T> AddConfigEntry<T>(string section, string name, T defaultValue, string description)
        {
            ConfigEntry<T> configEntry = Plugin.instance.Config.Bind(section, name, defaultValue, description);
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }


        public static void TryRemoveOldConfigSettings()
        {
            HashSet<string> headers = new HashSet<string>();
            HashSet<string> keys = new HashSet<string>();

            foreach (ConfigEntryBase entry in currentConfigEntries.Values)
            {
                headers.Add(entry.Definition.Section);
                keys.Add(entry.Definition.Key);
            }

            try
            {
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