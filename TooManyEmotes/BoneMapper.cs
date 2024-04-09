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
    public static class BoneMapper
    {
        public static Dictionary<Transform, Transform> CreateBoneMap(Transform sourceSkeleton, Transform targetSkeleton, List<string> sourceBoneNames, List<string> targetBoneNames = null)
        {
            if (sourceSkeleton == null || targetSkeleton == null || sourceBoneNames == null)
                return null;

            if (targetBoneNames == null)
                targetBoneNames = sourceBoneNames;

            if (sourceBoneNames.Count != targetBoneNames.Count)
            {
                LogError("Attempted to map humanoid skeleton, but passed two sets of bone names with differing sizes.");
                return null;
            }

            int size = sourceBoneNames.Count;
            var sourceBones = new Transform[size];
            var targetBones = new Transform[size];

            FindBones(sourceSkeleton, sourceBoneNames, sourceBones);
            FindBones(targetSkeleton, targetBoneNames, targetBones);

            var boneMap = new Dictionary<Transform, Transform>();
            for (int i = 0; i < size; i++)
            {
                if (sourceBones[i] != null && !boneMap.ContainsKey(sourceBones[i]))
                    boneMap.Add(sourceBones[i], targetBones[i]);
            }

            return boneMap;
        }


        static void FindBones(Transform bone, List<string> boneNames, Transform[] boneArray)
        {
            if (bone.GetComponent<Rig>() != null || bone.name == "ScavengerModelArmsOnly")
                return;

            if (boneNames.Contains(bone.name))
            {
                int indexInArray = boneNames.IndexOf(bone.name);
                if (boneArray[indexInArray] != null)
                {
                    //Debug.LogWarning("Already mapped bone with name: " + bone.name + ". It's recommended to use unique bone names in your skeleton.");
                }
                else
                {
                    boneArray[indexInArray] = bone;
                }
            }
            
            for (int i = 0; i < bone.childCount; i++)
                FindBones(bone.GetChild(i), boneNames, boneArray);
        }
    }
}