using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes
{
    public class BoneMap
    {
        public Dictionary<Transform, Transform> boneMap;

        public static void CreateBoneMap(BoneMap instance)
        {
            instance.boneMap.Clear();
        }

    }
}
