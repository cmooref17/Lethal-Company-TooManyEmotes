using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Props
{
    public class EmotePropData
    {
        public GameObject propPrefab;
        public string propName;
        public bool registered = false;
        public List<UnlockableEmote> parentEmotes = new List<UnlockableEmote>();

        public Item itemData;
        public SpawnableItemWithRarity itemRarityData;

        public bool isGrabbableObject = false;
        public string itemName = "";
        public GrabbablePropObject grabbablePropObject = null;
        public bool isScrap = true;
        public int rarity = 10;
        public bool twoHanded = false;
        public int minValue = 0;
        public int maxValue = 0;
        public float weight = 0;
        public Vector3 positionOffset = default;
        public Vector3 rotationOffset = default;
        public Vector3 restingRotation = default;
        public float verticalOffset = 0;
    }
}
