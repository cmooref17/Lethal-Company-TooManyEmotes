using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes {

    public class UnlockableEmote {
        public int emoteId;
        public string emoteName;
        public string displayName;
        public AnimationClip animationClip;
        public AnimationClip transitionsToClip = null;
        public bool complementary = false;
        public int price = 0;
        public bool isPose = false;
    }
}