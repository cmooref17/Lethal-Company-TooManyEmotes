using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes
{
    public static class BoneMap
    {
        static Dictionary<Transform, Transform> boneMap = new Dictionary<Transform, Transform>();
        static Transform humanoidRig;
        static Transform playerRig;


        public static Dictionary<Transform, Transform> CreateBoneMapFullBody(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            // Let us pray to lasso man that none of these paths change
            boneMap.Union(CreateBoneMapBody(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapHead(humanoidRig, playerRig));

            boneMap.Union(CreateBoneMapLeftArm(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapLeftFingers(humanoidRig, playerRig));

            boneMap.Union(CreateBoneMapRightArm(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapRightFingers(humanoidRig, playerRig));

            boneMap.Union(CreateBoneMapLeftLeg(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapLeftToes(humanoidRig, playerRig));

            boneMap.Union(CreateBoneMapRightLeg(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapRightToes(humanoidRig, playerRig));

            boneMap.Union(CreateBoneMapLeftHandTargetIK(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapRightHandTargetIK(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapLeftFootTargetIK(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapRightFootTargetIK(humanoidRig, playerRig));
            boneMap.Union(CreateBoneMapHeadTargetIK(humanoidRig, playerRig));

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapBody(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine");
            MapBone("spine/spine.001");
            MapBone("spine/spine.001/spine.002");
            MapBone("spine/spine.001/spine.002/spine.003");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapHead(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/spine.004");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapLeftArm(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L");

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger1.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger1.L/finger1.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger2.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger2.L/finger2.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger3.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger3.L/finger3.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger4.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger4.L/finger4.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger5.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger5.L/finger5.L.001");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapLeftFingers(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger1.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger1.L/finger1.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger2.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger2.L/finger2.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger3.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger3.L/finger3.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger4.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger4.L/finger4.L.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger5.L");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L/finger5.L/finger5.L.001");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightArm(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightFingers(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger1.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger1.R/finger1.R.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger2.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger2.R/finger2.R.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger3.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger3.R/finger3.R.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger4.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger4.R/finger4.R.001");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger5.R");
            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R/finger5.R/finger5.R.001");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapLeftLeg(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.L");
            MapBone("spine/thigh.L/shin.L");
            MapBone("spine/thigh.L/shin.L/foot.L");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapLeftToes(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.L/shin.L/foot.L/heel.02.L");
            MapBone("spine/thigh.L/shin.L/foot.L/toe.L");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightLeg(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.R");
            MapBone("spine/thigh.R/shin.R");
            MapBone("spine/thigh.R/shin.R/foot.R");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightToes(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.R/shin.R/foot.R/heel.02.R");
            MapBone("spine/thigh.R/shin.R/foot.R/toe.R");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapLeftHandTargetIK(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L", "spine/spine.001/spine.002/spine.003/LeftArm_target");
            
            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightHandTargetIK(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R", "spine/spine.001/spine.002/spine.003/RightArm_target");

            return boneMap;
        }

        public static Dictionary<Transform, Transform> CreateBoneMapLeftFootTargetIK(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.L/shin.L/foot.L", "Rig 1/LeftLeg/LeftLeg_target");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapRightFootTargetIK(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/thigh.R/shin.R/foot.R", "Rig 1/RightLeg/RightLeg_target");

            return boneMap;
        }


        public static Dictionary<Transform, Transform> CreateBoneMapHeadTargetIK(Transform humanoidRig, Transform playerRig)
        {
            boneMap = new Dictionary<Transform, Transform>();
            BoneMap.humanoidRig = humanoidRig;
            BoneMap.playerRig = playerRig;

            MapBone("spine/spine.001/spine.002/spine.003/spine.004", "CameraContainer");

            return boneMap;
        }


        static void MapBone(string bonePath, string sourceBonePath = "")
        {
            if (sourceBonePath == "")
                sourceBonePath = bonePath;
            Transform humanoidBone = humanoidRig.Find(bonePath);
            Transform playerBone = playerRig.Find(sourceBonePath);
            if (humanoidBone == null)
            {
                if (playerBone == null)
                    Plugin.LogError("Failed to find humanoid bone at path: " + bonePath + " and player bone at path: " + sourceBonePath);
                else
                    Plugin.LogError("Failed to find humanoid bone at path: " + bonePath);
            }
            else if (playerBone == null)
                Plugin.LogError("Failed to find player bone at path: " + sourceBonePath);
            else
                boneMap.Add(humanoidBone, playerBone);
        }
    }
}
