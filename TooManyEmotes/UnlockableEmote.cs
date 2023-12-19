using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using UnityEngine;

namespace TooManyEmotes
{

    public class UnlockableEmote
    {
        public int emoteId;
        public string emoteName;
        public string displayName;
        public string displayNameColorCoded { get { return (rarity == 0 && !ConfigSettings.overrideCommonEmoteNameColor.Value) ? displayName : string.Format("<color={0}>{1}</color>", nameColor, displayName); } }
        public AnimationClip animationClip;
        public AnimationClip transitionsToClip = null;
        public bool complementary = false;
        public int rarity = 0;
        public string rarityText
        {
            get
            {
                if (rarity == 0) return "Common";
                else if (rarity == 1) return "Uncommon";
                else if (rarity == 2) return "Rare";
                else if (rarity == 3) return "Legendary";
                else return "Invalid";
            }
        }
        public int price
        {
            get
            {
                int price = -1;
                if (rarity == 0) price = ConfigSync.syncBasePriceCommonEmote;
                else if (rarity == 1) price = ConfigSync.syncBasePriceUncommonEmote;
                else if (rarity == 2) price = ConfigSync.syncBasePriceRareEmote;
                else if (rarity == 3) price = ConfigSync.syncBasePriceLegendaryEmote;
                return (int)Mathf.Max(price * ConfigSync.syncPriceMultiplierEmotesStore, 0);
            }
        }
        public string nameColor { get { return rarityColorCodes[rarity]; } }
        public static string[] rarityColorCodes = new string[] { ConfigSettings.emoteNameColorCommon.Value, ConfigSettings.emoteNameColorUncommon.Value, ConfigSettings.emoteNameColorRare.Value, ConfigSettings.emoteNameColorLegendary.Value };
    }
}