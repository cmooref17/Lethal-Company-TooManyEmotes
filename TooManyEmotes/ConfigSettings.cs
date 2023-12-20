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

namespace TooManyEmotes.Config {
    public static class ConfigSettings {
        public static ConfigEntry<bool> unlockEverything;
        public static ConfigEntry<bool> disableRaritySystem;
        public static ConfigEntry<int> basePriceEmoteRaritySystemDisabled;

        public static ConfigEntry<float> priceMultiplierEmotesStore;
        public static ConfigEntry<int> basePriceCommonEmote;
        public static ConfigEntry<int> basePriceUncommonEmote;
        public static ConfigEntry<int> basePriceRareEmote;
        public static ConfigEntry<int> basePriceLegendaryEmote;

        public static ConfigEntry<int> numEmotesStoreRotation;
        public static ConfigEntry<float> rotationChanceCommonEmote;
        public static ConfigEntry<float> rotationChanceUncommonEmote;
        public static ConfigEntry<float> rotationChanceRareEmote;
        public static ConfigEntry<float> rotationChanceLegendaryEmote;

        public static ConfigEntry<int> numMysteryEmotesStoreRotation;
        public static ConfigEntry<int> numFreeEmoteCredits;
        public static ConfigEntry<string> openEmoteMenuKeybind;
        public static ConfigEntry<bool> toggleEmoteMenu;
        public static ConfigEntry<bool> reverseEmoteWheelScrollDirection;

        public static ConfigEntry<bool> overrideCommonEmoteNameColor;
        public static ConfigEntry<string> emoteNameColorCommon;
        public static ConfigEntry<string> emoteNameColorUncommon;
        public static ConfigEntry<string> emoteNameColorRare;
        public static ConfigEntry<string> emoteNameColorLegendary;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        public static void BindConfigSettings() {
            Plugin.Log("BindingConfigs");


            unlockEverything = Plugin.instance.Config.Bind("Server settings", "I am a Party Pooper", false, "[Host only] If true, every emote will be unlocked in your emote wheel at the start of the game.");
            disableRaritySystem = Plugin.instance.Config.Bind("Server settings", "DisableRaritySystem", false, "[Host only] If true, every emote will have the same likelyhood of appearing in the emote store.");
            basePriceEmoteRaritySystemDisabled = Plugin.instance.Config.Bind("Server settings", "BasePriceEmote - Rarity System Disabled", 100, "[Host only] Base price of emotes if the rarity system is disabled.");

            priceMultiplierEmotesStore = Plugin.instance.Config.Bind("Server settings", "PriceMultiplierEmotesStore", 1.0f, "[Host only] Price multiplier for emotes in the store. Only applies if UnlockEverythingAtStart is false.");
            basePriceCommonEmote = Plugin.instance.Config.Bind("Server settings", "PriceCommonEmote", 50, "[Host only] The base price of [common]emotes in the store.");
            basePriceUncommonEmote = Plugin.instance.Config.Bind("Server settings", "PriceUncommonEmote", 100, "[Host only] The base price of [uncommon] emotes in the store.");
            basePriceRareEmote = Plugin.instance.Config.Bind("Server settings", "PriceRareEmote", 200, "[Host only] The base price of [rare] emotes in the store.");
            basePriceLegendaryEmote = Plugin.instance.Config.Bind("Server settings", "PriceLegendaryEmote", 300, "[Host only] The base price of [legendary] emotes in the store.");

            numEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "EmotesInStoreRotation", 6, "[Host only] The number of emotes that will be available at a time in the store. Only applies if UnlockEverythingAtStart is false.");
            rotationChanceCommonEmote = Plugin.instance.Config.Bind("Server settings", "RotationWeightCommonEmote", 0.55f, "[Host only] The likelyhood of [common] emotes appearing (per slot) in the store rotation.");
            rotationChanceUncommonEmote = Plugin.instance.Config.Bind("Server settings", "RotationWeightUncommonEmote", 0.35f, "[Host only] The likelyhood of [uncommon] emotes appearing (per slot) in the store rotation.");
            rotationChanceRareEmote = Plugin.instance.Config.Bind("Server settings", "RotationWeightRareEmote", 0.08f, "[Host only] The likelyhood of [rare] emotes appearing (per slot) in the store rotation.");
            rotationChanceLegendaryEmote = Plugin.instance.Config.Bind("Server settings", "RotationWeightLegendaryEmote", 0.02f, "[Host only] The likelyhood of [legendary] emotes appearing (per slot) in the store rotation.");

            numMysteryEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "NumMysteryEmotesInStoreRotation", 1, "[Host only] The number of \"mystery\" emotes that will be available at a time in the store. These emotes will be a mystery until unlocked. Only applies if UnlockEverythingAtStart is false.");
            numFreeEmoteCredits = Plugin.instance.Config.Bind("Server settings", "NumFreeEmoteCredits", 100, "[Host only] The number of free emote coupons you start with each round. Only applies if UnlockEverythingAtStart is false.");
            openEmoteMenuKeybind = Plugin.instance.Config.Bind("Client", "OpenEmoteMenuKeybind", "<Keyboard>/backquote", "Keybind for opening the emote radial menu.");
            toggleEmoteMenu = Plugin.instance.Config.Bind("Client", "ToggleEmoteMenu", false, "If set to false, the emote menu will open upon pressing the related keybind, and close upon releasing, and will play the currently hovered emote.");
            reverseEmoteWheelScrollDirection = Plugin.instance.Config.Bind("Client", "ReverseEmoteWheelScrollDirection", false, "Reverses the page swapping direction in your emote when scrolling.");

            overrideCommonEmoteNameColor = Plugin.instance.Config.Bind("Accessibility", "OverrideEmoteNameColorCommon", false, "If true, the terminal will use the color in the config for [common] emote names.");
            emoteNameColorCommon = Plugin.instance.Config.Bind("Accessibility", "CommonEmoteNameColor", "#00FF00", "The color of the [common] emote name in the terminal. Only applies if OverrideEmoteNameColorCommon is true.");
            emoteNameColorUncommon = Plugin.instance.Config.Bind("Accessibility", "UncommonEmoteNameColor", "#2828FF", "The color of the [uncommon] emote name in the terminal.");
            emoteNameColorRare = Plugin.instance.Config.Bind("Accessibility", "RareEmoteNameColor", "#AA00EE", "The color of the [rare] emote name in the terminal.");
            emoteNameColorLegendary = Plugin.instance.Config.Bind("Accessibility", "LegendaryEmoteNameColor", "#FF2222", "The color of the [legendary] emote name in the terminal.");


            currentConfigEntries.Add(unlockEverything.Definition.Key, unlockEverything);
            currentConfigEntries.Add(disableRaritySystem.Definition.Key, disableRaritySystem);
            currentConfigEntries.Add(basePriceEmoteRaritySystemDisabled.Definition.Key, basePriceEmoteRaritySystemDisabled);

            currentConfigEntries.Add(priceMultiplierEmotesStore.Definition.Key, priceMultiplierEmotesStore);
            currentConfigEntries.Add(basePriceCommonEmote.Definition.Key, basePriceCommonEmote);
            currentConfigEntries.Add(basePriceUncommonEmote.Definition.Key, basePriceUncommonEmote);
            currentConfigEntries.Add(basePriceRareEmote.Definition.Key, basePriceRareEmote);
            currentConfigEntries.Add(basePriceLegendaryEmote.Definition.Key, basePriceLegendaryEmote);

            currentConfigEntries.Add(numEmotesStoreRotation.Definition.Key, numEmotesStoreRotation);
            currentConfigEntries.Add(rotationChanceCommonEmote.Definition.Key, rotationChanceCommonEmote);
            currentConfigEntries.Add(rotationChanceUncommonEmote.Definition.Key, rotationChanceUncommonEmote);
            currentConfigEntries.Add(rotationChanceRareEmote.Definition.Key, rotationChanceRareEmote);
            currentConfigEntries.Add(rotationChanceLegendaryEmote.Definition.Key, rotationChanceLegendaryEmote);

            currentConfigEntries.Add(numMysteryEmotesStoreRotation.Definition.Key, numMysteryEmotesStoreRotation);
            currentConfigEntries.Add(numFreeEmoteCredits.Definition.Key, numFreeEmoteCredits);
            currentConfigEntries.Add(openEmoteMenuKeybind.Definition.Key, openEmoteMenuKeybind);
            currentConfigEntries.Add(toggleEmoteMenu.Definition.Key, toggleEmoteMenu);
            currentConfigEntries.Add(reverseEmoteWheelScrollDirection.Definition.Key, reverseEmoteWheelScrollDirection);

            currentConfigEntries.Add(overrideCommonEmoteNameColor.Definition.Key, overrideCommonEmoteNameColor);
            currentConfigEntries.Add(emoteNameColorCommon.Definition.Key, emoteNameColorCommon);
            currentConfigEntries.Add(emoteNameColorUncommon.Definition.Key, emoteNameColorUncommon);
            currentConfigEntries.Add(emoteNameColorRare.Definition.Key, emoteNameColorRare);
            currentConfigEntries.Add(emoteNameColorLegendary.Definition.Key, emoteNameColorLegendary);


            // fix weights
            float totalChances = rotationChanceCommonEmote.Value;
            totalChances += rotationChanceUncommonEmote.Value;
            totalChances += rotationChanceRareEmote.Value;
            totalChances += rotationChanceLegendaryEmote.Value;

            if (totalChances != 1)
            {
                rotationChanceCommonEmote.Value /= totalChances;
                rotationChanceUncommonEmote.Value /= totalChances;
                rotationChanceRareEmote.Value /= totalChances;
                rotationChanceLegendaryEmote.Value /= totalChances;
                Plugin.instance.Config.Save();
            }

            TryRemoveOldConfigSettings();
            ConfigSync.BuildDefaultConfigSync();
        }



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