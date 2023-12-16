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
        public static ConfigEntry<float> priceMultiplierEmotesStore;
        public static ConfigEntry<int> numEmotesStoreRotation;
        public static ConfigEntry<int> numMysteryEmotesStoreRotation;
        public static ConfigEntry<int> numFreeEmoteCoupons;
        public static ConfigEntry<string> openEmoteMenuKeybind;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        public static void BindConfigSettings() {
            Plugin.Log("BindingConfigs");

            priceMultiplierEmotesStore = Plugin.instance.Config.Bind("Server settings", "PriceMultiplierEmotesStore", 1.0f, "[Host only] Price multiplier for emotes in the store.");
            numEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "NumEmotesInStoreRotation", 3, "[Host only] The number of (non-mystery) emotes that will be available at a time in the store.");
            numMysteryEmotesStoreRotation = Plugin.instance.Config.Bind("Server settings", "NumMysteryEmotesInStoreRotation", 1, "[Host only] The number of \"mystery\" emotes that will be available at a time in the store. These emotes will be a mystery until unlocked.");
            numFreeEmoteCoupons = Plugin.instance.Config.Bind("Server settings", "NumFreeEmoteCoupons", 1, "[Host only] The number of free emote coupons you start with each round.");
            openEmoteMenuKeybind = Plugin.instance.Config.Bind("Client", "OpenEmoteMenuKeybind", "<Keyboard>/backquote", "Keybind for opening the emote radial menu.");

            currentConfigEntries.Add(priceMultiplierEmotesStore.Definition.Key, priceMultiplierEmotesStore);
            currentConfigEntries.Add(numEmotesStoreRotation.Definition.Key, numEmotesStoreRotation);
            currentConfigEntries.Add(numMysteryEmotesStoreRotation.Definition.Key, numMysteryEmotesStoreRotation);
            currentConfigEntries.Add(numFreeEmoteCoupons.Definition.Key, numFreeEmoteCoupons);
            currentConfigEntries.Add(openEmoteMenuKeybind.Definition.Key, openEmoteMenuKeybind);

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