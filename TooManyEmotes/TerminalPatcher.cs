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

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    internal class TerminalPatcher
    {
        public static Terminal terminalInstance;
        public static HashSet<UnlockableEmote> emoteSelection;
        public static List<UnlockableEmote> mysteryEmoteSelection;
        public static int numFreeEmoteCoupons { get { return StartOfRoundPatcher.unlockedEmotes != null ? Mathf.Max(ConfigSync.syncNumFreeEmoteCoupons - StartOfRoundPatcher.unlockedEmotes.Count + StartOfRoundPatcher.complementaryEmotes.Count, 0): 0; } }
        public static int randomEmotesUnlockedThisRotation;
        static string confirmEmoteOpeningText = "You have requested to order a new emote.";

        static UnlockableEmote purchasingEmote;
        static bool initializedTerminalNodes = false;

        [HarmonyPatch(typeof(Terminal), "Awake")]
        [HarmonyPostfix]
        public static void InitializeTerminal(Terminal __instance) {
            terminalInstance = __instance;
            emoteSelection = new HashSet<UnlockableEmote>();
            mysteryEmoteSelection = new List<UnlockableEmote>();
            randomEmotesUnlockedThisRotation = 0;
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
                            "Emote Store - These rotate every day.\n" +
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
                if (numFreeEmoteCoupons > 0)
                    replacementText += "You have " + numFreeEmoteCoupons + " free emote coupons!\n";
                replacementText += "\n";
                foreach (var emote in emoteSelection)
                {
                    string priceText = StartOfRoundPatcher.unlockedEmotes.Contains(emote) ? "[Purchased]" : "$" + emote.price;
                    replacementText += string.Format("* {0}   //   {1}\n", emote.displayName, priceText);
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[emoteUnlockablesSelectionList]", replacementText);
            }



            if (modifiedDisplayText.Contains("[emoteCurrentlyUnlocked]"))
            {
                string replacementText = "";
                for (int i = 0; i < StartOfRoundPatcher.unlockedEmotes.Count; i++)
                {
                    var emote = StartOfRoundPatcher.unlockedEmotes[i];
                    if (emote != null)
                        replacementText += string.Format("[{0}] {1}\n", i + 1, emote.displayName);
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[emoteCurrentlyUnlocked]", replacementText);
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
            UnlockableEmote emote;

            Plugin.Log("Input: " + input);
            if (args.Length == 0)
                return true;

            if (__instance.screenText.text.Contains(confirmEmoteOpeningText) && !__instance.screenText.text.Contains("Canceled order."))
            {
                if ("confirm".StartsWith(input))
                {
                    if (StartOfRoundPatcher.unlockedEmotes.Contains(purchasingEmote))
                    {
                        Debug.Log("Attempted to confirm purchase on emote that was already unlocked. Emote: " + purchasingEmote.displayName);
                        __result = BuildTerminalNodeAlreadyUnlocked(purchasingEmote);
                    }
                    else if (terminalInstance.groupCredits < purchasingEmote.price && numFreeEmoteCoupons == 0)
                    {
                        Debug.Log("Attempted to confirm purchase with insufficient credits and no free coupons. Current credits: " + terminalInstance.groupCredits + ". Emote price: " + purchasingEmote.price);
                        __result = BuildTerminalNodeInsufficientFunds(purchasingEmote);
                    }
                    else
                    {
                        float oldNumCoupons = numFreeEmoteCoupons;
                        float oldGroupCredits = terminalInstance.groupCredits;
                        StartOfRoundPatcher.UnlockEmoteLocal(purchasingEmote);
                        SyncUnlockedEmotes.SendOnUnlockEmoteUpdate(purchasingEmote.emoteId);
                        if (oldNumCoupons > 0)
                        {
                            Debug.Log("Purchasing emote with a free emote coupon: " + purchasingEmote.displayName + ". New coupon balance: " + numFreeEmoteCoupons);
                            __result = BuildTerminalNodeOnPurchased(purchasingEmote, terminalInstance.groupCredits, numFreeEmoteCoupons);
                        }
                        else
                        {
                            Debug.Log("Purchasing emote: " + purchasingEmote.displayName + " for " + purchasingEmote.price + ". New balance: " + terminalInstance.groupCredits + " - Old balance: " + oldGroupCredits);
                            terminalInstance.groupCredits = Mathf.Clamp(terminalInstance.groupCredits - purchasingEmote.price, 0, 10000000);
                            terminalInstance.SyncGroupCreditsServerRpc(terminalInstance.groupCredits, 0);
                            __result = BuildTerminalNodeOnPurchased(purchasingEmote, terminalInstance.groupCredits, -1);
                        }
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
            
            if (input == "emote")
            {
                __result = BuildTerminalNodeHome();
                return false;
            }

            if (input.StartsWith("emote"))
            {
                input = input.Substring(6);
                emote = TryGetEmoteCurrentSelection(input);
            }
            else
            {
                emote = TryGetEmoteCurrentSelection(input, reliable: true);
                if (emote == null)
                    return true;
            }


            if (input.StartsWith("emote random") || input.StartsWith("random"))
            {
                // TODO
            }


            if (emote != null)
            {
                if (StartOfRoundPatcher.unlockedEmotes.Contains(emote))
                {
                    Plugin.Log("Attempted to start purchase on emote that was already unlocked. Emote: " + emote.displayName);
                    __result = BuildTerminalNodeAlreadyUnlocked(emote);
                }
                else if (terminalInstance.groupCredits < emote.price && numFreeEmoteCoupons == 0)
                {
                    Plugin.Log("Attempted to start purchase with insufficient credits and no free coupons. Current credits: " + terminalInstance.groupCredits + ". Emote price: " + emote.price);
                    __result = BuildTerminalNodeInsufficientFunds(emote);
                }
                /*
                else if (StartOfRoundPatcher.unlockedEmotes.Count >= 10)
                {
                    Plugin.Log("Attempted to start purchase when emote limit has been reached.");
                    __result = BuildTerminalNodeMaxEmotes();
                }
                */
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
                Plugin.Log("Attempted to start purchase on invalid emote, or emote was not in current rotation. Emote: " + emote.emoteName);
                __result = BuildTerminalNodeInvalidEmote();
                return false;
            }
        }


        public static TerminalNode BuildTerminalNodeHome() {

            TerminalNode homeTerminalNode = new TerminalNode {
                displayText = "[TooManyEmotes]\n\n" +
                    "Store\n" +
                    "------------------------------\n" +
                    "[emoteUnlockablesSelectionList]\n\n" +
                    "Currently unlocked emotes\n" +
                    "------------------------------\n\n" +
                    "[emoteCurrentlyUnlocked]\n\n",
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

            if (numFreeEmoteCoupons > 0)
                terminalNode.displayText += "You have " + numFreeEmoteCoupons + " free emote coupons available!\n\n";
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
                terminalNode.displayText += "Remaining free emote coupons: " + newCouponCount + "\n\n";
            else
                terminalNode.displayText += "Your new balance is $" + newGroupCredits + "\n\n";

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
                displayText = "You could not afford this emote!\n" +
                "Your balance is $" + terminalInstance.groupCredits + "\n" +
                "Cost of emote is $" + emote.price + "\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeMaxEmotes() {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You've hit the max emote limit!\n" +
                "Future plans for this mod include managing your\n" +
                "emote loadout to allow for more emotes.\n\n",
                clearPreviousText = true,
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



        // This is temporary
        [HarmonyPatch(typeof(Terminal), "RotateShipDecorSelection")]
        [HarmonyPostfix]
        public static void RotateEmoteSelectionPerQuota()
        {
            RotateEmoteSelection();
        }



        [HarmonyPatch(typeof(TimeOfDay), "OnDayChanged")]
        [HarmonyPostfix]
        public static void RotateEmoteSelectionDaily()
        {
            RotateEmoteSelection();
        }



        public static void RotateEmoteSelection()
        {
            //System.Random random = new System.Random(UnityEngine.Random.Range(1, 100000000)); // Not seed based currently

            System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 65);
            emoteSelection.Clear();
            List<UnlockableEmote> emoteList = new List<UnlockableEmote>();

            foreach (var emote in StartOfRoundPatcher.allUnlockableEmotes)
            {
                if (emote != null && !emote.complementary && !StartOfRoundPatcher.unlockedEmotes.Contains(emote)) // && !StartOfRoundPatcher.unlockedEmotes.Contains(unlockableEmote)
                    emoteList.Add(emote);
            }

            for (int i = 0; i < ConfigSync.syncNumEmotesStoreRotation; i++)
            {
                if (emoteList.Count <= 0)
                    break;
                UnlockableEmote emote = emoteList[random.Next(0, emoteList.Count)];
                emoteSelection.Add(emote);
                emoteList.Remove(emote);
            }

            /*
            for (int i = 0; i < ConfigSync.syncNumMysteryEmotesStoreRotation; i++)
            {
                if (emoteList.Count <= 0)
                    break;
                UnlockableEmote emote = emoteList[random.Next(0, emoteList.Count)];
                mysteryEmoteSelection.Add(emote);
                emoteList.Remove(emote);
            }

            randomEmotesUnlockedThisRotation = 0;
            */
        }




        static TerminalNode BuildCustomTerminalNode(string displayText, bool clearPreviousText = false, bool acceptAnything = false, bool isConfirmationNode = false) {
            TerminalNode terminalNode = new TerminalNode {
                displayText = displayText,
                clearPreviousText = clearPreviousText,
                acceptAnything = false,
                isConfirmationNode = isConfirmationNode
            };
            return terminalNode;
        }
    }
}