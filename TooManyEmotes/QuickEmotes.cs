using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    public static class QuickEmotes
    {
        public static UnlockableEmote GetQuickEmote(int index)
        {
            if (index < 0 || index >= 8)
            {
                LogError("Failed to get quick emote name at index: " + index + ". Index must be within range: 0 and 8");
                return null;
            }

            string emoteName = ES3.Load("QuickEmote" + index, SaveManager.TooManyEmotesSaveFileName, string.Empty);

            UnlockableEmote emote = null;
            EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out emote);

            return emote;
        }
    }
}