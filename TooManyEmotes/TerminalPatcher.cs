using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using DunGen;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    internal class TerminalPatcher
    {
        public static Terminal terminalInstance;
        public static List<UnlockableEmote> emoteSelection;
        public static List<UnlockableEmote> mysteryEmoteSelection;
        public static int randomEmotesUnlockedThisRotation;
        public static int emoteCreditsUsed;
        public static int currentEmoteCredits { get { return ConfigSync.syncNumFreeEmoteCredits - emoteCreditsUsed; } }
        static string confirmEmoteOpeningText = "You have requested to order a new emote.";

        static UnlockableEmote purchasingEmote;
        static bool initializedTerminalNodes = false;

        static int emoteStoreSeedOffset = 80;


        [HarmonyPatch(typeof(Terminal), "Awake")]
        [HarmonyPostfix]
        public static void InitializeTerminal(Terminal __instance) {
            terminalInstance = __instance;
            emoteSelection = new List<UnlockableEmote>();
            mysteryEmoteSelection = new List<UnlockableEmote>();
            randomEmotesUnlockedThisRotation = 0;
            emoteCreditsUsed = 0;
            if (!initializedTerminalNodes)
                EditExistingTerminalNodes();
        }


        public static void EditExistingTerminalNodes() {
            initializedTerminalNodes = true;
            foreach (TerminalNode node in terminalInstance.terminalNodes.specialNodes)
            {
                if (node.name == "Start")
                {
                    string keyword = "Type \"Help\" for a list of commands.";
                    int insertIndex = node.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        Plugin.Log("Appending to terminal start text.");
                        insertIndex += keyword.Length;
                        string addText = "\n\n[TooManyEmotes]\nType \"Emotes\" for a list of commands.";
                        node.displayText = node.displayText.Insert(insertIndex, addText);
                    }
                    else
                        Debug.LogError("Failed to add emotes tip to terminal. Maybe an update broke it?");
                }

                else if (node.name == "HelpCommands")
                {
                    string keyword = "[numberOfItemsOnRoute]";
                    int insertIndex = node.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        string addText = ">EMOTES\n" +
                            "For a list of Emote commands.\n\n";
                        node.displayText = node.displayText.Insert(insertIndex, addText);
                    }
                }
            }

            foreach (TerminalKeyword terminalKeyword in terminalInstance.terminalNodes.allKeywords)
            {
                if (terminalKeyword.name == "Store")
                {
                    TerminalNode storeNode = terminalKeyword.specialKeywordResult;
                    string keyword = "[unlockablesSelectionList]";
                    int insertIndex = storeNode.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        insertIndex += keyword.Length;
                        storeNode.displayText = storeNode.displayText.Insert(insertIndex, "\n\n" +
                            "Emote Store - These rotate per-quota.\n" +
                            "------------------------------\n" +
                            "[emoteUnlockablesSelectionList]");
                    }
                    else
                        Plugin.LogError("Failed to find insert keyword in store node. Maybe an update broke it?");

                    break;
                }
            }
        }


        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        public static void TextPostProcess(ref string modifiedDisplayText, TerminalNode node) {
            if (modifiedDisplayText.Length <= 0)
                return;

            if (modifiedDisplayText.Contains("[emoteUnlockablesSelectionList]"))
            {
                string replacementText = "";
                if (currentEmoteCredits > 0)
                    replacementText += "You have " + currentEmoteCredits + " free emote credits left!\n";
                replacementText += "\n";

                int longestNameSize = 0;
                foreach (var emote in emoteSelection)
                    longestNameSize = Mathf.Max(longestNameSize, emote.displayName.Length);
                foreach (var emote in emoteSelection)
                {
                    string priceText = StartOfRoundPatcher.unlockedEmotes.Contains(emote) ? "[Purchased]" : "$" + emote.price;
                    replacementText += string.Format("* {0}{1}   //   {2}\n", emote.displayNameColorCoded, new string(' ', longestNameSize - emote.displayName.Length), priceText);
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[emoteUnlockablesSelectionList]", replacementText);
            }
        }


        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPrefix]
        public static bool ParsePlayerSentence(ref TerminalNode __result, Terminal __instance) {

            if (__instance.screenText.text.Length == 0)
                return true;

            string screenText = __instance.screenText.text;
            string[] lines = screenText.Split('\n');
            string input = lines.Last().ToLower().Trim(' ');
            string[] args = input.Split(' ');
            UnlockableEmote emote = null;

            if (args.Length == 0)
                return true;

            if (!ConfigSync.isSynced)
            {
                if (input.StartsWith("emote"))
                {
                    __result = BuildTerminalNodeHostDoesNotHaveMod();
                    return false;
                }
                else
                    return true;
            }


            if (__instance.screenText.text.Contains(confirmEmoteOpeningText) && !__instance.screenText.text.Contains("Canceled order."))
            {
                if ("confirm".StartsWith(input))
                {
                    if (StartOfRoundPatcher.unlockedEmotes.Contains(purchasingEmote))
                    {
                        Debug.Log("Attempted to confirm purchase on emote that was already unlocked. Emote: " + purchasingEmote.displayName);
                        __result = BuildTerminalNodeAlreadyUnlocked(purchasingEmote);
                    }
                    else if (terminalInstance.groupCredits < purchasingEmote.price && currentEmoteCredits == 0)
                    {
                        Debug.Log("Attempted to confirm purchase with insufficient credits and no free coupons. Current credits: " + terminalInstance.groupCredits + ". Emote price: " + purchasingEmote.price);
                        __result = BuildTerminalNodeInsufficientFunds(purchasingEmote);
                    }
                    else
                    {
                        StartOfRoundPatcher.UnlockEmoteLocal(purchasingEmote);
                        SyncUnlockedEmotes.SendOnUnlockEmoteUpdate(purchasingEmote.emoteId);

                        int oldEmoteCredits = currentEmoteCredits;
                        int oldGroupCredits = terminalInstance.groupCredits;
                        int dEmoteCredits = -Mathf.Min(purchasingEmote.price, currentEmoteCredits);
                        int dGroupCredits = -(purchasingEmote.price + dEmoteCredits);

                        emoteCreditsUsed -= dEmoteCredits;
                        terminalInstance.groupCredits += dGroupCredits;

                        if (dEmoteCredits >= 0)
                        {

                            if (dGroupCredits > 0)
                                Debug.Log("Purchasing emote with " + dEmoteCredits + " emote credits, and " + dGroupCredits + " group credits. Emote: " + purchasingEmote.displayName + ". New emote credit balance: " + currentEmoteCredits + ". New group credit balance: " + terminalInstance.groupCredits);
                            else
                                Debug.Log("Purchasing emote with " + dEmoteCredits + " emote credits. Emote: " + purchasingEmote.displayName + ". New emote credit balance: " + currentEmoteCredits + ".");
                        }
                        else
                        {
                            dGroupCredits = purchasingEmote.price;
                            Debug.Log("Purchasing emote: " + purchasingEmote.displayName + " for " + purchasingEmote.price + ". New balance: " + terminalInstance.groupCredits + " - Old balance: " + oldGroupCredits);
                        }

                        if (dGroupCredits > 0)
                            terminalInstance.SyncGroupCreditsServerRpc(terminalInstance.groupCredits, 0);
                        __result = BuildTerminalNodeOnPurchased(purchasingEmote, dGroupCredits != 0 ? terminalInstance.groupCredits : -1, dEmoteCredits != 0 ? currentEmoteCredits : -1);
                    }
                }
                else
                {
                    Plugin.Log("Canceling emote order.");
                    __result = BuildCustomTerminalNode("Canceled order.\n\n");
                }
                purchasingEmote = null;
                return false;
            }

            if (input.StartsWith("emotes"))
                input = input.Replace("emotes", "emote");




            if (input.StartsWith("emote cheat ") && GameNetworkManager.Instance.localPlayerController.playerUsername == "Flip" && GameNetworkManager.Instance.localPlayerController.IsServer)
            {
                input = input.Replace("emote cheat ", "");
                if (input.StartsWith("rotate"))
                {
                    emoteStoreSeedOffset++;
                    RotateEmoteSelection();
                    __result = BuildCustomTerminalNode("Rotated emotes.\n------------------------------\n[emoteUnlockablesSelectionList]\n\n", clearPreviousText: true);
                }

                else if (input.StartsWith("resetship"))
                {
                    StartOfRoundPatcher.ResetEmotesLocal();
                    __result = BuildCustomTerminalNode("Reset ship emotes.\n\n", clearPreviousText: true);
                }

                else if (input.StartsWith("morecredits"))
                {
                    emoteCreditsUsed = -10000 + ConfigSync.syncNumFreeEmoteCredits;
                    __result = BuildCustomTerminalNode("New emote credit balance: " + currentEmoteCredits + "\n\n", clearPreviousText: true);
                }

                return false;
            }


            if (input.StartsWith("emote"))
            {
                if (input == "emote")
                {
                    __result = BuildTerminalNodeHome();
                    return false;
                }
                input = input.Substring(6);
                emote = TryGetEmoteCurrentSelection(input);
            }
            else
            {

                emote = TryGetEmoteCurrentSelection(input, reliable: true);
                if (emote == null)
                    return true;
            }


            if (emote != null)
            {
                if (StartOfRoundPatcher.unlockedEmotes.Contains(emote))
                {
                    Plugin.Log("Attempted to start purchase on emote that was already unlocked. Emote: " + emote.displayName);
                    __result = BuildTerminalNodeAlreadyUnlocked(emote);
                }
                else if (terminalInstance.groupCredits < emote.price && currentEmoteCredits == 0)
                {
                    Plugin.Log("Attempted to start purchase with insufficient credits and no free coupons. Current credits: " + terminalInstance.groupCredits + ". Emote price: " + emote.price);
                    __result = BuildTerminalNodeInsufficientFunds(emote);
                }
                else
                {
                    Plugin.Log("Started purchasing emote: " + emote.emoteName);
                    purchasingEmote = emote;
                    __result = BuildTerminalNodeConfirmDenyPurchase(emote);
                }
                return false;
            }
            else
            {
                Plugin.Log("Attempted to start purchase on invalid emote, or emote was not in current rotation. Input emote: " + input);
                __result = BuildTerminalNodeInvalidEmote();
                return false;
            }
        }


        public static int CalculateNumEmoteCredits()
        {
            int credits = ConfigSync.syncNumFreeEmoteCredits;
            foreach (var emote in StartOfRoundPatcher.unlockedEmotes)
            {
                if (credits <= 0)
                    break;

                if (!emote.complementary)
                    credits -= emote.price;
            }
            return Mathf.Max(credits, 0);
        }


        public static TerminalNode BuildTerminalNodeHome() {

            TerminalNode homeTerminalNode = new TerminalNode {
                displayText = "[TooManyEmotes]\n\n" +
                    "Store\n" +
                    "------------------------------\n" +
                    "[emoteUnlockablesSelectionList]\n\n",
                    //"Currently unlocked emotes\n" +
                    //"------------------------------\n\n" +
                    //"[emoteCurrentlyUnlocked]\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            
            return homeTerminalNode;
        }


        static TerminalNode BuildTerminalNodeConfirmDenyPurchase(UnlockableEmote emote) {
            TerminalNode terminalNode = new TerminalNode {
                displayText = confirmEmoteOpeningText + "\n" +
                "> [" + emote.displayName + "]\n\n",
                isConfirmationNode = true,
                acceptAnything = false,
                clearPreviousText = true
            };

            if (currentEmoteCredits > 0)
                terminalNode.displayText += "You have " + currentEmoteCredits + " free emote credits available!\n\n";
            terminalNode.displayText += "Please CONFIRM or DENY.\n\n";

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeOnPurchased(UnlockableEmote emote, int newGroupCredits, int newCouponCount = -1) {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You have successfully purchased a new emote.\n" +
                "> [" + emote.displayName + "]\n\n",
                buyUnlockable = true,
                clearPreviousText = true,
                acceptAnything = false,
                playSyncedClip = 0
            };

            if (newCouponCount != -1)
                terminalNode.displayText += "New emote credit balance: $" + newCouponCount + "\n";
            if (newGroupCredits != -1)
                terminalNode.displayText += "New group credit balance: $" + newGroupCredits + "\n";
            terminalNode.displayText += "\n";

            int page = Mathf.Max((StartOfRoundPatcher.unlockedEmotes.Count - 1) / 8) + 1;
            int slot = StartOfRoundPatcher.unlockedEmotes.Count % 8;
            terminalNode.displayText += "Your new emote is registered in your emote radial menu.\n" +
                "You can find your emote in your emote radial menu.\n" +
                "Page: " + page + "\n" +
                "Slot: " + slot + ".\n\n";

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeAlreadyUnlocked(UnlockableEmote emote) {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You have already purchased this emote!\n\n",
                clearPreviousText = false,
                acceptAnything = false
            };

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeInsufficientFunds(UnlockableEmote emote) {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You could not afford this emote!\n\n" +
                "Your balance is $" + (terminalInstance.groupCredits + currentEmoteCredits) + "\n" +
                "Cost of emote is $" + emote.price + "\n\n",
                clearPreviousText = false,
                acceptAnything = false
            };

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeInvalidEmote(string emoteName = "") {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "Emote does not exist, or is not available in the current rotation.",
                clearPreviousText = false,
                acceptAnything = false
            };
            if (emoteName != "")
                terminalNode.displayText += ("\n\"" + emoteName + "\"");
            terminalNode.displayText += "\n";
            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeHostDoesNotHaveMod(string emoteName = "")
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You cannot use the emote commands menu until you are synced with the host.\n\n" +
                "You may be seeing this because the host does not have this mod.\n" +
                "If this is the case, you will have access to every emote in your emote wheel.\n\n" +
                "If the host does have this mod, then either you haven't synced with them yet, or you may have ran into a bug.\n" +
                "Sorry for the inconvenience!\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            if (emoteName != "")
                terminalNode.displayText += ("\n\"" + emoteName + "\"");
            terminalNode.displayText += "\n";
            return terminalNode;
        }


        static TerminalNode BuildCustomTerminalNode(string displayText, bool clearPreviousText = false, bool acceptAnything = false, bool isConfirmationNode = false)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = displayText,
                clearPreviousText = clearPreviousText,
                acceptAnything = false,
                isConfirmationNode = isConfirmationNode
            };
            return terminalNode;
        }


        static UnlockableEmote TryGetEmote(string emoteNameInput, IEnumerable<UnlockableEmote> emoteList = null, bool reliable = false) {
            if (emoteList == null)
                emoteList = StartOfRoundPatcher.allUnlockableEmotes;
            UnlockableEmote getEmote = null;
            foreach (var emote in emoteList)
            {
                string emoteName = emote.displayName.ToLower();
                if (reliable)
                {
                    if ((emoteNameInput.Length >= 4 && emoteName.StartsWith(emoteNameInput)) || emoteNameInput == emoteName || emoteNameInput == emoteName.Split(' ')[0] || emoteNameInput.Split(' ')[0] == emoteName.Split(' ')[0])
                    {
                        if (getEmote == null || emoteName.Length < getEmote.displayName.Length)
                            getEmote = emote;
                    }
                }
                else if (emoteName.StartsWith(emoteNameInput))
                {
                    if (getEmote == null || emoteName.Length < getEmote.displayName.Length)
                        getEmote = emote;
                }
            }
            return getEmote;
        }
        static UnlockableEmote TryGetEmoteCurrentSelection(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, emoteSelection, reliable);
        static UnlockableEmote TryGetEmoteUnlockedEmotes(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, StartOfRoundPatcher.unlockedEmotes, reliable);



        [HarmonyPatch(typeof(Terminal), "RotateShipDecorSelection")]
        [HarmonyPostfix]
        public static void RotateEmoteSelectionPerQuota()
        {
            RotateEmoteSelection();
        }


        public static void RotateEmoteSelection()
        {
            Plugin.Log("Rotating emote selection in store. Seed: " + (StartOfRound.Instance.randomMapSeed + emoteStoreSeedOffset));
            System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + emoteStoreSeedOffset);
            emoteSelection.Clear();

            for (int i = 0; i < ConfigSync.syncNumEmotesStoreRotation; i++)
            {
                UnlockableEmote emote = null;
                if (ConfigSync.syncDisableRaritySystem)
                    emote = GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allUnlockableEmotes, random);
                else
                {
                    double itemRarity = random.NextDouble();
                    float threshold = 1 - ConfigSync.syncRotationChanceLegendaryEmote;
                    if (itemRarity >= threshold)
                        emote = GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allLegendaryEmotes, random);
                    if (emote == null)
                    {
                        threshold -= ConfigSync.syncRotationChanceRareEmote;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allRareEmotes, random);
                    }
                    if (emote == null)
                    {
                        threshold -= ConfigSync.syncRotationChanceUncommonEmote;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allUncommonEmotes, random);
                    }
                    if (emote == null)
                        emote = GetRandomEmoteNotUnlocked(StartOfRoundPatcher.allCommonEmotes, random);
                }

                if (emote != null)
                    emoteSelection.Add(emote);
            }
            emoteSelection.Sort((item1, item2) => item1.rarity.CompareTo(item2.rarity));
        }


        static UnlockableEmote GetRandomEmoteNotUnlocked(List<UnlockableEmote> emoteList, System.Random random)
        {
            var notUnlocked = new List<UnlockableEmote>();
            foreach(var emote in emoteList)
            {
                if (!StartOfRoundPatcher.unlockedEmotes.Contains(emote) && !emoteSelection.Contains(emote))
                    notUnlocked.Add(emote);
            }
            if (notUnlocked.Count > 0)
            {
                int emoteIndex = random.Next(notUnlocked.Count);
                return notUnlocked[emoteIndex];
            }
            return null;
        }



        
    }
}