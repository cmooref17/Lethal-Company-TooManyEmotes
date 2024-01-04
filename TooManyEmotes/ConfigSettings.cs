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

        //public static ConfigEntry<int> numMysteryEmotesStoreRotation;
        public static ConfigEntry<string> openEmoteMenuKeybind;
        public static ConfigEntry<string> rotateCharacterInEmoteKeybind;
        public static ConfigEntry<bool> toggleEmoteMenu;
        public static ConfigEntry<bool> reverseEmoteWheelScrollDirection;

        public static ConfigEntry<string> emoteNameColorTier0;
        public static ConfigEntry<string> emoteNameColorTier1;
        public static ConfigEntry<string> emoteNameColorTier2;
        public static ConfigEntry<string> emoteNameColorTier3;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        public static void BindConfigSettings() {
            Plugin.Log("BindingConfigs");


            unlockEverything = Plugin.instance.Config.Bind("Server settings", "I am a Party Pooper", false, "[Host only] If true, every emote will be unlocked in your emote wheel at the start of the game.");
            disableRaritySystem = Plugin.instance.Config.Bind("Server settings", "DisableRaritySystem", false, "[Host only] If true, every emote will have the same likelyhood of appearing in the emote store.");
            basePriceEmoteRaritySystemDisabled = Plugin.instance.Config.Bind("Server settings", "BasePriceEmote - Rarity System Disabled", 100, "[Host only] Base price of emotes if the rarity system is disabled.");

            startingEmoteCredits = Plugin.instance.Config.Bind("Server settings", "StartingEmoteCredits", 100, "[Host only] The number of emote credits you start each game with.");
            addEmoteCreditsMultiplier = Plugin.instance.Config.Bind("Server settings", "AddEmoteCreditsMultiplier", 0.5f, "[Host only] You gain emote credits based off this multiplier of normal group credits earned. Example: If set to the default, 0.5, and you earn 200 group credits, you will also gain 100 emote credits.");
            purchaseEmotesWithDefaultCurrency = Plugin.instance.Config.Bind("Server settings", "PurchaseEmotesWithDefaultCredits", false, "[Host only] Setting this to true will allow you to purchase emotes with normal group credits once you run out of emote credits.");
            purchaseEmotesWithDefaultCurrency = Plugin.instance.Config.Bind("Server settings", "PurchaseEmotesWithDefaultCredits", false, "[Host only] Setting this to true will allow you to purchase emotes with normal group credits once you run out of emote credits.");

            priceMultiplierEmotesStore = Plugin.instance.Config.Bind("Server settings", "PriceMultiplierEmotesStore", 1.0f, "[Host only] Price multiplier for emotes in the store. Only applies if UnlockEverythingAtStart is false.");
            basePriceEmoteTier0 = Plugin.instance.Config.Bind("Server settings", "PriceCommonEmote", 50, "[Host only] The base price of [common]emotes in the store.");
            basePriceEmoteTier1 = Plugin.instance.Config.Bind("Server settings", "PriceUncommonEmote", 100, "[Host only] The base price of [uncommon] emotes in the store.");
            basePriceEmoteTier2 = Plugin.instance.Config.Bind("Server settings", "PriceRareEmote", 200, "[Host only] The base price of [rare] emotes in the store.");
            basePriceEmoteTier3 = Plugin.instance.Config.Bind("Server settings", "PriceLegendaryEmote", 300, "[Host only] The base price of [legendary] emotes in the store.");

            numEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "EmotesInStoreRotation", 6, "[Host only] The number of emotes that will be available at a time in the store. Only applies if UnlockEverythingAtStart is false.");
            rotationChanceEmoteTier0 = Plugin.instance.Config.Bind("Server settings", "RotationWeightCommonEmote", 0.55f, "[Host only] The likelyhood of [common] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier1 = Plugin.instance.Config.Bind("Server settings", "RotationWeightUncommonEmote", 0.35f, "[Host only] The likelyhood of [uncommon] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier2 = Plugin.instance.Config.Bind("Server settings", "RotationWeightRareEmote", 0.08f, "[Host only] The likelyhood of [rare] emotes appearing (per slot) in the store rotation.");
            rotationChanceEmoteTier3 = Plugin.instance.Config.Bind("Server settings", "RotationWeightLegendaryEmote", 0.02f, "[Host only] The likelyhood of [legendary] emotes appearing (per slot) in the store rotation.");

            //numMysteryEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "NumMysteryEmotesInStoreRotation", 1, "[Host only] The number of \"mystery\" emotes that will be available at a time in the store. These emotes will be a mystery until unlocked. Only applies if UnlockEverythingAtStart is false.");
            openEmoteMenuKeybind = Plugin.instance.Config.Bind("Client", "OpenEmoteMenuKeybind", "<Keyboard>/backquote", "Keybind for opening the emote radial menu.");
            rotateCharacterInEmoteKeybind = Plugin.instance.Config.Bind("Client", "RotateCharacterInEmoteKeybind", "<Keyboard>/leftAlt", "Keybind to hold to rotate character while performing a custom emote.");
            toggleEmoteMenu = Plugin.instance.Config.Bind("Client", "ToggleEmoteMenu", true, "If set to false, the emote menu will open upon pressing the related keybind, and close upon releasing, and will play the currently hovered emote.");
            reverseEmoteWheelScrollDirection = Plugin.instance.Config.Bind("Client", "ReverseEmoteWheelScrollDirection", false, "Reverses the page swapping direction in your emote when scrolling.");

            emoteNameColorTier0 = Plugin.instance.Config.Bind("Accessibility", "CommonEmoteNameColor", "#00FF00", "The color of the [common] emote name in the terminal.");
            emoteNameColorTier1 = Plugin.instance.Config.Bind("Accessibility", "UncommonEmoteNameColor", "#2828FF", "The color of the [uncommon] emote name in the terminal.");
            emoteNameColorTier2 = Plugin.instance.Config.Bind("Accessibility", "RareEmoteNameColor", "#AA00EE", "The color of the [rare] emote name in the terminal.");
            emoteNameColorTier3 = Plugin.instance.Config.Bind("Accessibility", "LegendaryEmoteNameColor", "#FF2222", "The color of the [legendary] emote name in the terminal.");
            


            currentConfigEntries.Add(unlockEverything.Definition.Key, unlockEverything);
            currentConfigEntries.Add(disableRaritySystem.Definition.Key, disableRaritySystem);
            currentConfigEntries.Add(basePriceEmoteRaritySystemDisabled.Definition.Key, basePriceEmoteRaritySystemDisabled);

            currentConfigEntries.Add(startingEmoteCredits.Definition.Key, startingEmoteCredits);
            currentConfigEntries.Add(addEmoteCreditsMultiplier.Definition.Key, addEmoteCreditsMultiplier);
            currentConfigEntries.Add(purchaseEmotesWithDefaultCurrency.Definition.Key, purchaseEmotesWithDefaultCurrency);

            currentConfigEntries.Add(priceMultiplierEmotesStore.Definition.Key, priceMultiplierEmotesStore);
            currentConfigEntries.Add(basePriceEmoteTier0.Definition.Key, basePriceEmoteTier0);
            currentConfigEntries.Add(basePriceEmoteTier1.Definition.Key, basePriceEmoteTier1);
            currentConfigEntries.Add(basePriceEmoteTier2.Definition.Key, basePriceEmoteTier2);
            currentConfigEntries.Add(basePriceEmoteTier3.Definition.Key, basePriceEmoteTier3);

            currentConfigEntries.Add(numEmotesStoreRotation.Definition.Key, numEmotesStoreRotation);
            currentConfigEntries.Add(rotationChanceEmoteTier0.Definition.Key, rotationChanceEmoteTier0);
            currentConfigEntries.Add(rotationChanceEmoteTier1.Definition.Key, rotationChanceEmoteTier1);
            currentConfigEntries.Add(rotationChanceEmoteTier2.Definition.Key, rotationChanceEmoteTier2);
            currentConfigEntries.Add(rotationChanceEmoteTier3.Definition.Key, rotationChanceEmoteTier3);

            //currentConfigEntries.Add(numMysteryEmotesStoreRotation.Definition.Key, numMysteryEmotesStoreRotation);
            currentConfigEntries.Add(openEmoteMenuKeybind.Definition.Key, openEmoteMenuKeybind);
            currentConfigEntries.Add(rotateCharacterInEmoteKeybind.Definition.Key, rotateCharacterInEmoteKeybind);
            currentConfigEntries.Add(toggleEmoteMenu.Definition.Key, toggleEmoteMenu);
            currentConfigEntries.Add(reverseEmoteWheelScrollDirection.Definition.Key, reverseEmoteWheelScrollDirection);

            currentConfigEntries.Add(emoteNameColorTier0.Definition.Key, emoteNameColorTier0);
            currentConfigEntries.Add(emoteNameColorTier1.Definition.Key, emoteNameColorTier1);
            currentConfigEntries.Add(emoteNameColorTier2.Definition.Key, emoteNameColorTier2);
            currentConfigEntries.Add(emoteNameColorTier3.Definition.Key, emoteNameColorTier3);


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