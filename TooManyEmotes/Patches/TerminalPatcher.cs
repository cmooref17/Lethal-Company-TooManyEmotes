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
using Unity.Netcode;
using TooManyEmotes.Compatibility;
using static TooManyEmotes.CustomLogging;
using static TooManyEmotes.HelperTools;

namespace TooManyEmotes.Patches
{

    [HarmonyPatch]
    public static class TerminalPatcher
    {
        public static Terminal terminalInstance;
        public static List<UnlockableEmote> emoteSelection;
        public static List<UnlockableEmote> mysteryEmoteSelection;
        public static int currentEmoteCredits;
        public static Dictionary<string, int> currentEmoteCreditsByPlayer;

        static string confirmEmoteOpeningText = "You have requested to order a new emote.";

        static string[] ignoreTerminalKeywords = { "company", "moons", "help", "switch", "transmit", "store", "beastiary", "storage", "other", "scan", "ping", "view monitor" };

        public static UnlockableEmote purchasingEmote;
        public static bool initializedTerminalNodes = false;

        public static int emoteStoreSeed = 0;

        [HarmonyPatch(typeof(Terminal), "Awake")]
        [HarmonyPostfix]
        private static void InitializeTerminal(Terminal __instance)
        {
            terminalInstance = __instance;
            initializedTerminalNodes = false;
            emoteSelection = new List<UnlockableEmote>();
            mysteryEmoteSelection = new List<UnlockableEmote>();
            currentEmoteCreditsByPlayer = new Dictionary<string, int>();
            EditExistingTerminalNodes();
        }


        [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
        [HarmonyPostfix]
        private static void OnBeginUsingTerminal(Terminal __instance)
        {
            if (!initializedTerminalNodes && ConfigSync.isSynced)
                EditExistingTerminalNodes();
            purchasingEmote = null;
        }


        private static void EditExistingTerminalNodes()
        {
            initializedTerminalNodes = true;

            if (ConfigSync.instance.syncUnlockEverything)
                return;

            foreach (TerminalNode node in terminalInstance.terminalNodes.specialNodes)
            {
                if (node.name == "Start" && !node.displayText.Contains("[TooManyEmotes]"))
                {
                    string keyword = "Type \"Help\" for a list of commands.";
                    int insertIndex = node.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        insertIndex += keyword.Length;
                        string addText = "\n\n[TooManyEmotes]\nType \"Emotes\" for a list of commands.";
                        node.displayText = node.displayText.Insert(insertIndex, addText);
                    }
                    else
                        Debug.LogError("Failed to add emotes tip to terminal. Maybe an update broke it?");
                }

                else if (node.name == "HelpCommands" && !node.displayText.Contains(">EMOTES"))
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
        }


        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        private static void TextPostProcess(ref string modifiedDisplayText, TerminalNode node)
        {
            if (modifiedDisplayText.Length <= 0)
                return;

            string placeholderText = "[[[emoteUnlockablesSelectionList]]]";
            if (modifiedDisplayText.Contains(placeholderText))
            {
                int index0 = modifiedDisplayText.IndexOf(placeholderText);
                int index1 = index0 + placeholderText.Length;
                string textToReplace = modifiedDisplayText.Substring(index0, index1 - index0);
                string replacementText = "";
                if (ConfigSync.instance.syncUnlockEverything)
                    replacementText += "Every emote is already unlocked!\n\n";
                else
                {
                    replacementText += "Remaining emote credit balance: $" + currentEmoteCredits + ".\n";
                    if (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency)
                        replacementText += "Remaining group credit balance: $" + terminalInstance.groupCredits + ".\n";
                    replacementText += "\n";

                    int longestNameSize = 0;
                    foreach (var emote in emoteSelection)
                        longestNameSize = Mathf.Max(longestNameSize, emote.displayName.Length);
                    foreach (var emote in emoteSelection)
                    {
                        string priceText = SessionManager.IsEmoteUnlocked(emote) ? "[Purchased]" : "$" + emote.price;
                        replacementText += string.Format("* {0}{1}   //   {2}\n", emote.displayNameColorCoded, new string(' ', longestNameSize - emote.displayName.Length), priceText);
                    }
                }
                modifiedDisplayText = modifiedDisplayText.Replace(textToReplace, replacementText);
            }
        }




        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPrefix]
        private static bool ParsePlayerSentence(ref TerminalNode __result, Terminal __instance)
        {
            if (__instance.screenText.text.Length <= 0)
                return true;

            if ((ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled) && !ConfigSync.instance.syncShareEverything)
                return true;

            string input = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded).ToLower();
            string[] args = input.Split(' ');
            UnlockableEmote emote = null;

            if (args.Length == 0)
                return true;

            if (!ConfigSync.isSynced)
            {
                if (input.StartsWith("emote"))
                {
                    __result = BuildTerminalNodeNotSynced();
                    return false;
                }
                else
                    return true;
            }


            if (purchasingEmote != null)
            {
                if ("confirm".StartsWith(input))
                {
                    if (SessionManager.IsEmoteUnlocked(purchasingEmote))
                    {
                        Debug.Log("Attempted to confirm purchase on emote that was already unlocked. Emote: " + purchasingEmote.displayName);
                        __result = BuildTerminalNodeAlreadyUnlocked(purchasingEmote);
                    }
                    else if (Mathf.Max(currentEmoteCredits, 0) + (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency ? Mathf.Max(terminalInstance.groupCredits, 0) : 0) < purchasingEmote.price)
                    {
                        Debug.Log("Attempted to confirm purchase with insufficient emote credits. Current credits: " + currentEmoteCredits + ". " + (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency ? ("Group credits: " + terminalInstance.groupCredits + ". ") : "") + "Emote price: " + purchasingEmote.price);
                        __result = BuildTerminalNodeInsufficientFunds(purchasingEmote);
                    }
                    else
                    {
                        //StartOfRoundPatcher.UnlockEmoteLocal(purchasingEmote);

                        int oldEmoteCredits = currentEmoteCredits;
                        int oldGroupCredits = terminalInstance.groupCredits;

                        int dEmoteCredits = -Mathf.Min(Mathf.Max(currentEmoteCredits, 0), purchasingEmote.price);
                        int dGroupCredits = ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency ? -Mathf.Min(Mathf.Max(terminalInstance.groupCredits, 0), purchasingEmote.price + dEmoteCredits) : 0;

                        currentEmoteCredits += dEmoteCredits;
                        terminalInstance.groupCredits += dGroupCredits;

                        if (!ConfigSync.instance.syncShareEverything)
                            SessionManager.UnlockEmoteLocal(purchasingEmote, purchased: true);
                        SyncManager.SendOnUnlockEmoteUpdate(purchasingEmote.emoteId, currentEmoteCredits);
                        if (dGroupCredits > 0)
                            terminalInstance.SyncGroupCreditsServerRpc(terminalInstance.groupCredits, terminalInstance.numberOfItemsInDropship);
                        Debug.Log("Purchasing emote: " + purchasingEmote.displayName + ". Price: " + purchasingEmote.price);
                        __result = BuildTerminalNodeOnPurchased(purchasingEmote, dEmoteCredits != 0 ? currentEmoteCredits : -1, dGroupCredits != 0 ? terminalInstance.groupCredits : -1);
                    }
                }
                else
                {
                    Log("Canceling emote order.");
                    __result = BuildCustomTerminalNode("Canceled order.\n\n");
                }
                purchasingEmote = null;
                return false;
            }

            if (input.StartsWith("emotes"))
                input = input.Replace("emotes", "emote");

            purchasingEmote = null;

            if (input.StartsWith("emote cheat ") && GameNetworkManager.Instance.localPlayerController.playerUsername == "Flip" && GameNetworkManager.Instance.localPlayerController.IsServer)
            {
                input = input.Replace("emote cheat ", "");
                if (input.StartsWith("rotate"))
                {
                    SyncManager.RotateEmoteSelectionServer();
                    __result = BuildCustomTerminalNode("Rotated emotes.\n------------------------------\n[[[emoteUnlockablesSelectionList]]]\n\n", clearPreviousText: true);
                }

                else if (input.StartsWith("resetship"))
                {
                    SessionManager.ResetProgressLocal();
                    __result = BuildCustomTerminalNode("Reset ship emotes.\n\n", clearPreviousText: true);
                }

                else if (input.StartsWith("morecredits"))
                {
                    currentEmoteCredits = 10000;
                    __result = BuildCustomTerminalNode("New emote credit balance: " + currentEmoteCredits + "\n\n", clearPreviousText: true);
                }

                else if (EmotesManager.allUnlockableEmotesDict.TryGetValue(input, out emote) && !SessionManager.IsEmoteUnlocked(emote))
                {
                    SyncManager.SendOnUnlockEmoteUpdate(emote.emoteId);
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
                if (input.StartsWith("buy "))
                    input = input.Replace("buy ", "");
                emote = TryGetEmoteCurrentSelection(input, reliable: true);
                if (emote == null)
                    return true;
            }


            if (emote != null)
            {
                if (SessionManager.IsEmoteUnlocked(emote))
                {
                    Log("Attempted to start purchase on emote that was already unlocked. Emote: " + emote.displayName);
                    __result = BuildTerminalNodeAlreadyUnlocked(emote);
                }
                else if (Mathf.Max(currentEmoteCredits, 0) + (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency ? Mathf.Max(terminalInstance.groupCredits, 0) : 0) < emote.price)
                {
                    Log("Attempted to start purchase with insufficient emote credits. Current credits: " + currentEmoteCredits + ". " + (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency ? ("Group credits: " + terminalInstance.groupCredits + ". ") : "") + "Emote price: " + emote.price);
                    __result = BuildTerminalNodeInsufficientFunds(emote);
                }
                else
                {
                    Log("Started purchasing emote: " + emote.emoteName);
                    purchasingEmote = emote;
                    __result = BuildTerminalNodeConfirmDenyPurchase(emote);
                }
                return false;
            }
            else
            {
                Log("Attempted to start purchase on invalid emote, or emote was not in current rotation. Input emote: " + input);
                __result = BuildTerminalNodeInvalidEmote();
                return false;
            }
        }


        [HarmonyPatch(typeof(DepositItemsDesk), "SellAndDisplayItemProfits")]
        [HarmonyPrefix]
        private static void OnGainGroupCredits(int profit, int newGroupCredits, DepositItemsDesk __instance)
        {
            profit = Mathf.Max(profit, 0);
            if (profit <= 0)
                return;

            int emoteCreditsProfit = (int)(profit * ConfigSync.instance.syncAddEmoteCreditsMultiplier);
            Log("Gained " + profit + " group credits. (GainEmoteCreditsMultiplier: " + ConfigSync.instance.syncAddEmoteCreditsMultiplier + ")");
            currentEmoteCredits += emoteCreditsProfit;
            for (int i = 0; i < currentEmoteCreditsByPlayer.Count; i++)
            {
                string playerName = currentEmoteCreditsByPlayer.ElementAt(i).Key;
                currentEmoteCreditsByPlayer[playerName] += emoteCreditsProfit;
            }
        }


        [HarmonyPatch(typeof(TimeOfDay), "SetNewProfitQuota")]
        [HarmonyPostfix]
        private static void RotateEmoteSelectionPerQuota()
        {
            if (isServer)
                SyncManager.RotateEmoteSelectionServer();
        }


        public static void RotateNewEmoteSelection()
        {
            int seed = emoteStoreSeed + (ConfigSync.instance.syncPersistentUnlocks ? 1000 : 0);
            if (!ConfigSync.instance.syncShareEverything)
            {
                int localClientId = StartOfRound.Instance.localPlayerController != null ? (int)localPlayerController.playerClientId : 0;
                seed += localClientId;
            }

            Log("Rotating emote selection in store. Seed: " + seed);

            System.Random random = new System.Random(seed);
            emoteSelection.Clear();
            for (int i = 0; i < ConfigSync.instance.syncNumEmotesStoreRotation; i++)
            {
                UnlockableEmote emote = null;
                if (ConfigSync.instance.syncDisableRaritySystem)
                    emote = GetRandomEmoteNotUnlocked(EmotesManager.allUnlockableEmotes, random);
                else
                {
                    double itemRarity = random.NextDouble();
                    float threshold = 1 - ConfigSync.instance.syncRotationChanceEmoteTier3;
                    if (itemRarity >= threshold)
                        emote = GetRandomEmoteNotUnlocked(EmotesManager.allEmotesTier3, random);
                    if (emote == null)
                    {
                        threshold -= ConfigSync.instance.syncRotationChanceEmoteTier2;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlocked(EmotesManager.allEmotesTier2, random);
                    }
                    if (emote == null)
                    {
                        threshold -= ConfigSync.instance.syncRotationChanceEmoteTier1;
                        if (itemRarity >= threshold)
                            emote = GetRandomEmoteNotUnlocked(EmotesManager.allEmotesTier1, random);
                    }
                    if (emote == null)
                        emote = GetRandomEmoteNotUnlocked(EmotesManager.allEmotesTier0, random);
                }

                if (emote != null)
                    emoteSelection.Add(emote);
            }
            emoteSelection.Sort((item1, item2) => item1.rarity.CompareTo(item2.rarity));
        }


        public static UnlockableEmote GetRandomEmoteNotUnlocked(List<UnlockableEmote> emoteList, System.Random random)
        {
            var notUnlocked = new List<UnlockableEmote>();
            foreach (var emote in emoteList)
            {
                if (!SessionManager.IsEmoteUnlocked(emote) && !emoteSelection.Contains(emote) && emote.purchasable && !emote.requiresHeldProp)
                    notUnlocked.Add(emote);
            }
            if (notUnlocked.Count > 0)
            {
                int emoteIndex = random.Next(notUnlocked.Count);
                return notUnlocked[emoteIndex];
            }
            return null;
        }


        private static TerminalNode BuildTerminalNodeHome() {

            TerminalNode homeTerminalNode = new TerminalNode {
                displayText = "[TooManyEmotes]\n\n" +
                    "Store\n" +
                    "------------------------------\n" +
                    "[[[emoteUnlockablesSelectionList]]]\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            
            return homeTerminalNode;
        }


        private static TerminalNode BuildTerminalNodeConfirmDenyPurchase(UnlockableEmote emote)
        {
            TerminalNode terminalNode = new TerminalNode {
                displayText = confirmEmoteOpeningText + "\n" +
                "> [" + emote.displayNameColorCoded + "]\n\n",
                isConfirmationNode = true,
                acceptAnything = false,
                clearPreviousText = true
            };

            terminalNode.displayText += "Emote credit balance: $" + currentEmoteCredits + "\n";
            if (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency)
                terminalNode.displayText += "Group credit balance: $" + terminalInstance.groupCredits + "\n";
            terminalNode.displayText += "\n";
            terminalNode.displayText += "Please CONFIRM or DENY.\n\n";

            return terminalNode;
        }


        private static TerminalNode BuildTerminalNodeOnPurchased(UnlockableEmote emote, int newEmoteCredits, int newGroupCredits)
        {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You have successfully purchased a new emote!\n" +
                "> [" + emote.displayNameColorCoded + "]\n\n",
                buyUnlockable = true,
                clearPreviousText = true,
                acceptAnything = false,
                playSyncedClip = 0
            };

            if (newEmoteCredits != -1)
                terminalNode.displayText += "New emote credit balance: $" + newEmoteCredits + "\n";
            if (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency && newGroupCredits != -1)
                terminalNode.displayText += "New group credit balance: $" + newGroupCredits + "\n";
            terminalNode.displayText += "\n";

            int page = Mathf.Max((SessionManager.unlockedEmotes.Count - 1) / 8) + 1;
            int slot = SessionManager.unlockedEmotes.Count % 8;
            terminalNode.displayText += "Your new emote has been added to the emote menu!\n\n";

            return terminalNode;
        }


        private static TerminalNode BuildTerminalNodeAlreadyUnlocked(UnlockableEmote emote)
        {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You have already purchased this emote!\n" +
                "> [" + emote.displayNameColorCoded + "]\n\n",
                clearPreviousText = false,
                acceptAnything = false
            };

            return terminalNode;
        }


        private static TerminalNode BuildTerminalNodeInsufficientFunds(UnlockableEmote emote)
        {
            TerminalNode terminalNode = new TerminalNode {
                displayText = "You could not afford this emote!\n" +
                "> [" + emote.displayNameColorCoded + "]\n\n" +
                "Emote credit balance is $" + currentEmoteCredits + "\n",
                clearPreviousText = true,
                acceptAnything = false
            };

            if (ConfigSync.instance.syncPurchaseEmotesWithDefaultCurrency)
                terminalNode.displayText += "Group credit balance is $" + terminalInstance.groupCredits + "\n";

            terminalNode.displayText += "Cost of emote is $" + emote.price + "\n\n";
            return terminalNode;
        }


        private static TerminalNode BuildTerminalNodeInvalidEmote(string emoteName = "")
        {
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


        private static TerminalNode BuildTerminalNodeNotSynced(string emoteName = "")
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You cannot use the emote commands menu until you are synced with the host.\n\n" +
                "You may also be seeing this because the host does not have this mod.\n" +
                "If this is the case, you will already have access to every emote in your emote wheel. Enjoy!\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            if (emoteName != "")
                terminalNode.displayText += ("\n\"" + emoteName + "\"");
            terminalNode.displayText += "\n";
            return terminalNode;
        }


        private static TerminalNode BuildCustomTerminalNode(string displayText, bool clearPreviousText = false, bool acceptAnything = false, bool isConfirmationNode = false)
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


        private static bool IsAmbiguousKeyword(string keyword)
        {
            keyword = keyword.ToLower();
            foreach (string ignoreKeyword in ignoreTerminalKeywords)
            {
                if (keyword.StartsWith(ignoreKeyword) || ignoreKeyword.StartsWith(keyword))
                    return true;
            }
            return false;
        }


        private static UnlockableEmote TryGetEmote(string emoteNameInput, IEnumerable<UnlockableEmote> emoteList = null, bool reliable = false)
        {
            if (emoteList == null)
                emoteList = EmotesManager.allUnlockableEmotes;
            UnlockableEmote getEmote = null;

            foreach (var emote in emoteList)
            {
                string emoteName = emote.displayName.ToLower();
                bool ambiguousEmoteName = IsAmbiguousKeyword(emoteName);
                if (reliable || ambiguousEmoteName)
                {
                    if (emoteNameInput == emoteName || (!ambiguousEmoteName && emoteNameInput.Length >= 4 && emoteName.StartsWith(emoteNameInput)))
                    {
                        if ((getEmote == null || emoteName.Length < getEmote.displayName.Length) && !"the company".StartsWith(emoteNameInput) && !"company".StartsWith(emoteNameInput))
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
        private static UnlockableEmote TryGetEmoteCurrentSelection(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, emoteSelection, reliable);
        private static UnlockableEmote TryGetEmoteUnlockedEmotes(string emoteNameInput, bool reliable = false) => TryGetEmote(emoteNameInput, SessionManager.unlockedEmotes, reliable);
    }
}