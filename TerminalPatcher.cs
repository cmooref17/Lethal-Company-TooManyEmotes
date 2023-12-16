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

        public static List<UnlockableEmote> allUnlockableEmotes = new List<UnlockableEmote>();
        public static Dictionary<string, UnlockableEmote> allUnlockableEmotesDict = new Dictionary<string, UnlockableEmote>();
        public static HashSet<UnlockableEmote> emoteSelection = new HashSet<UnlockableEmote>();
        public static List<UnlockableEmote> mysteryEmoteSelection = new List<UnlockableEmote>();
        public static int numFreeEmoteCoupons { get { return StartOfRoundPatcher.unlockedEmotes != null ? Mathf.Max(ConfigSync.syncNumFreeEmoteCoupons - StartOfRoundPatcher.unlockedEmotes.Count, 0): 0; } }
        public static int randomEmotesUnlockedThisRotation = 0;
        static string confirmEmoteOpeningText = "You have requested to order a new emote.";

        static UnlockableEmote purchasingEmote;

        [HarmonyPatch(typeof(Terminal), "Awake")]
        [HarmonyPostfix]
        public static void InitializeTerminal(Terminal __instance) {
            if (allUnlockableEmotes != null && allUnlockableEmotes.Count > 0)
                return;
            terminalInstance = __instance;
            int id = 0;
            foreach (var emoteClip in Plugin.customAnimationClips)
            {
                UnlockableEmote emote = new UnlockableEmote {
                    emoteId = id,
                    emoteName = emoteClip.name,
                    displayName = emoteClip.name,
                    animationClip = emoteClip,
                    price = 100
                };

                emote.price = (int)Mathf.Max(emote.price * ConfigSync.syncPriceMultiplierEmotesStore, 0);

                if (emote.emoteName.Contains("_start"))
                {
                    string emoteLoopName = emote.emoteName.Replace("_start", "_loop");
                    var emoteLoop = Plugin.customAnimationClipsLoopDict[emoteLoopName];
                    emote.transitionsToClip = emoteLoop;
                    emote.displayName = emote.emoteName.Replace("_start", ""); ;
                }
                else if (emote.emoteName.Contains("_pose"))
                    emote.isPose = true;

                emote.displayName = emote.displayName.Replace('_', ' ').Trim(' ');
                emote.displayName = char.ToUpper(emote.displayName[0]) + emote.displayName.Substring(1).ToLower();
                allUnlockableEmotes.Add(emote);
                allUnlockableEmotesDict.Add(emote.emoteName, emote);
                id++;
            }

            EditExistingTerminalNodes();
        }

        public static void EditExistingTerminalNodes() {
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
                            "Emote Store - These rotate per-quota\n" +
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
                for (int i = 0; i < StartOfRoundPatcher.currentEmoteLoadout.Length; i++)
                {
                    var emote = StartOfRoundPatcher.currentEmoteLoadout[i];
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
                else if (StartOfRoundPatcher.unlockedEmotes.Count >= 10)
                {
                    Plugin.Log("Attempted to start purchase when emote limit has been reached.");
                    __result = BuildTerminalNodeMaxEmotes();
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
                Plugin.Log("Attempted to start purchase on invalid emote, or emote was not in current rotation. Emote: " + emote.emoteName);
                __result = BuildTerminalNodeInvalidEmote();
                return false;
            }
        }


        public static TerminalNode BuildTerminalNodeHome() {
            /*
            TerminalNode homeTerminalNode = new TerminalNode {
                displayText = "[TooManyEmotes]\n\n" +
                    "Unlockable emotes can be found in the store.\n\n" +
                    "Assigning emotes to your loadout\n" +
                    "> Assign [Emote name] [1-10]\n\n" +
                    "Currently unlocked emotes\n" +
                    "------------------------------\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            */

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
            /*
            for (int i = 0; i < StartOfRoundPatcher.currentEmoteLoadout.Length; i++)
            {
                UnlockableEmote emote = StartOfRoundPatcher.currentEmoteLoadout[i];
                if (emote != null)
                    homeTerminalNode.displayText += string.Format("[{0}] {1}\n", i + 1, emote.displayName);
            }
            */
            //homeTerminalNode.displayText += "\n\n";

            /*
            bool hasUnassignedEmotes = false;
            foreach (var emote in StartOfRoundPatcher.unlockedEmotes)
            {
                if (StartOfRoundPatcher.currentEmoteLoadout.Contains(emote))
                    continue;
                if (!hasUnassignedEmotes)
                {
                    hasUnassignedEmotes = true;
                    homeTerminalNode.displayText += "\nUnassigned emotes\n" +
                        "------------------------------\n\n";
                }
                homeTerminalNode.displayText += emote.displayName + "\n";
            }
            homeTerminalNode.displayText += "\n";
            */
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

            int emoteIndex = Array.IndexOf(StartOfRoundPatcher.currentEmoteLoadout, emote);
            if (emoteIndex != -1)
                terminalNode.displayText += "Your new emote is registered in your emote loadout.\nEmote slot: " + (emoteIndex + 1) + ".\n\n";
            else
                terminalNode.displayText += "Your current emote loadout is full.\nTo use this emote, assign it to an emote slot.\n\n";

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
                emoteList = allUnlockableEmotes;
            foreach (var emote in emoteList)
            {
                string emoteName = emote.displayName.ToLower();
                if (reliable)
                {
                    if (emoteNameInput == emoteName || emoteNameInput == emoteName.Split(' ')[0] || emoteNameInput.Split(' ')[0] == emoteName.Split(' ')[0])
                        return emote;
                }
                else if (emoteName.StartsWith(emoteNameInput))
                    return emote;
            }
            return null;
        }
        static UnlockableEmote TryGetEmoteCurrentSelection(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, emoteSelection, reliable);
        static UnlockableEmote TryGetEmoteUnlockedEmotes(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, StartOfRoundPatcher.unlockedEmotes, reliable);


        [HarmonyPatch(typeof(Terminal), "RotateShipDecorSelection")]
        [HarmonyPostfix]
        public static void RotateEmoteSelection() {
            System.Random random = new System.Random(UnityEngine.Random.Range(1, 100000000)); // Not seed based currently
            emoteSelection.Clear();
            List<UnlockableEmote> emoteList = new List<UnlockableEmote>();

            foreach (var unlockableEmote in allUnlockableEmotes)
            {
                if (unlockableEmote != null) // && !StartOfRoundPatcher.unlockedEmotes.Contains(unlockableEmote)
                    emoteList.Add(unlockableEmote);
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