using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using Unity.Netcode;
using UnityEditor.Analytics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace TooManyEmotes
{
    [HarmonyPatch]
    public class EmoteController : MonoBehaviour
    {
        public static Dictionary<GameObject, EmoteController> allEmoteControllers = new Dictionary<GameObject, EmoteController>();

        public bool initialized = false;
        public Transform metarig;
        protected Vector3 originalMetarigLocalPosition = Vector3.zero;
        public Animator originalAnimator;
        public Transform humanoidSkeleton;

        public Animator animator;
        public AnimatorOverrideController animatorController;
        public int defaultAnimatorStateHash { get { return animator != null ? Animator.StringToHash(animator.GetLayerName(0) + ".emote") : -1; } }

        protected bool isPerformingEmote = false;
        public UnlockableEmote performingEmote;

        public List<Transform> groundContactPoints = new List<Transform>();
        public float normalizedTimeAnimation { get { return animator.GetCurrentAnimatorStateInfo(0).normalizedTime; } }


        public Dictionary<Transform, Transform> boneMapBody;
        public Dictionary<Transform, Transform> boneMapHead;
        public Dictionary<Transform, Transform> boneMapLeftArm;
        public Dictionary<Transform, Transform> boneMapRightArm;
        public Dictionary<Transform, Transform> boneMapLeftLeg;
        public Dictionary<Transform, Transform> boneMapRightLeg;
        public Dictionary<Transform, Transform> boneMapLeftFingers;
        public Dictionary<Transform, Transform> boneMapRightFingers;
        public Dictionary<Transform, Transform> boneMapLeftToes;
        public Dictionary<Transform, Transform> boneMapRightToes;

        public Dictionary<Transform, Transform> boneMapLeftHandIK;
        public Dictionary<Transform, Transform> boneMapRightHandIK;
        public Dictionary<Transform, Transform> boneMapLeftFootIK;
        public Dictionary<Transform, Transform> boneMapRightFootIK;
        public Dictionary<Transform, Transform> boneMapHeadIK;

        public Transform leftHandIKTarget;
        public Transform rightHandIKTarget;
        public Transform leftFootIKTarget;
        public Transform rightFootIKTarget;
        public Transform headIKTarget;


        protected virtual void Awake()
        {
            if (Plugin.humanoidSkeletonPrefab == null || Plugin.humanoidAnimatorController == null || Plugin.humanoidAvatar == null)
                return;

            try
            {
                if (originalAnimator == null)
                {
                    foreach (var findAnimator in GetComponentsInChildren<Animator>())
                    {
                        if (findAnimator.name == "metarig")
                        {
                            originalAnimator = findAnimator;
                            break;
                        }
                    }
                }
                if (originalAnimator == null)
                {
                    Debug.LogError("Failed to find animator component in children. Make sure you place this component on one of the parents of this character's metarig.");
                    return;
                }

                metarig = originalAnimator.transform;
                Debug.Assert(metarig.parent != null);

                originalMetarigLocalPosition = metarig.localPosition;
                humanoidSkeleton = GameObject.Instantiate(Plugin.humanoidSkeletonPrefab, metarig.parent).transform;
                humanoidSkeleton.name = "HumanoidSkeleton";

                humanoidSkeleton.SetSiblingIndex(metarig.GetSiblingIndex() + 1);

                animator = humanoidSkeleton.GetComponentInChildren<Animator>();
                Debug.Assert(animator != null);

                animatorController = new AnimatorOverrideController(Plugin.humanoidAnimatorController);
                animator.runtimeAnimatorController = animatorController;

                humanoidSkeleton.SetLocalPositionAndRotation(metarig.localPosition + Vector3.down * 0.025f, Quaternion.identity);
                humanoidSkeleton.localScale = metarig.localScale;

                allEmoteControllers.Add(gameObject, this);
                initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to initialize EmoteController. Error: " + e);
            }
        }


        protected virtual void Start()
        {
            if (!initialized)
                return;

            CreateBoneMap();
            AddGroundContactPoints();
        }


        protected virtual void AddGroundContactPoints()
        {
            try
            {
                foreach (var bone in boneMapLeftToes.Keys)
                {
                    if (bone.name.ToLower().Contains("heel") || bone.name.ToLower().Contains("toe"))
                        groundContactPoints.Add(bone);
                }
                foreach (var bone in boneMapRightToes.Keys)
                {
                    if (bone.name.ToLower().Contains("heel") || bone.name.ToLower().Contains("toe"))
                        groundContactPoints.Add(bone);
                }
            }
            catch
            {
                Plugin.LogError("[" + name + "] Failed to find ground contact points.");
            }
        }


        protected virtual void OnDestroy()
        {
            allEmoteControllers?.Remove(gameObject);
        }


        protected virtual void Update()
        {
            if (!initialized)
                return;
        }


        protected virtual void LateUpdate()
        {
            if (!initialized)
                return;

            if (isPerformingEmote)
            {
                Plugin.LogWarning("NormalizedTime: " + normalizedTimeAnimation);
                if (CheckIfShouldStopEmoting())
                {
                    Plugin.LogWarning("OnCheckIfShouldStopEmoting. Stopping emote. " + name + " NormTime: " + normalizedTimeAnimation);
                    StopPerformingEmote();
                }
            }

            if (animator == null || animatorController == null || !isPerformingEmote)
                return;

            TranslateAnimation();
        }


        protected virtual void TranslateAnimation()
        {
            if (performingEmote == null)
                return;

            TranslateBoneMapInAnimation(boneMapBody);

            TranslateBoneMapInAnimation(performingEmote.useLeftHandIK ? boneMapLeftHandIK : boneMapLeftArm);
            TranslateBoneMapInAnimation(performingEmote.useRightHandIK ? boneMapRightHandIK : boneMapRightArm);
            TranslateBoneMapInAnimation(performingEmote.useLeftFootIK ? boneMapLeftFootIK : boneMapLeftLeg);
            TranslateBoneMapInAnimation(performingEmote.useRightFootIK ? boneMapRightFootIK : boneMapRightLeg);
            TranslateBoneMapInAnimation(performingEmote.useHeadIK ? boneMapHeadIK : boneMapHead);

            TranslateBoneMapInAnimation(boneMapLeftFingers, true);
            TranslateBoneMapInAnimation(boneMapRightFingers, true);
            TranslateBoneMapInAnimation(boneMapLeftToes, true);
            TranslateBoneMapInAnimation(boneMapRightToes, true);

            CorrectVerticalPosition();
        }


        protected virtual void TranslateBoneMapInAnimation(Dictionary<Transform, Transform> boneMap, bool useLocalPositionRotation = false)
        {
            foreach (var pair in boneMap)
            {
                if (useLocalPositionRotation)
                {
                    pair.Value.transform.localPosition = pair.Key.localPosition;
                    pair.Value.transform.localRotation = pair.Key.localRotation;
                }
                else
                {
                    pair.Value.transform.position = pair.Key.position;
                    pair.Value.transform.rotation = pair.Key.rotation;
                }
            }
        }


        protected virtual bool CheckIfShouldStopEmoting()
        {
            Plugin.Log("AAA");
            if (isPerformingEmote)
            {
                Plugin.Log("BBB");
                if (performingEmote == null || (!performingEmote.loopable && !performingEmote.isPose && normalizedTimeAnimation >= 1))
                {
                    if (performingEmote == null)
                        Plugin.LogWarning("Stopping emote. Loaded emote is null!");
                    else if (!performingEmote.loopable && !performingEmote.isPose && normalizedTimeAnimation >= 1)
                        Plugin.LogWarning("Stopping emote. Emote has ended at time: " + normalizedTimeAnimation + " Loopable: " + performingEmote.loopable + " IsPose: " + performingEmote.isPose);
                    else
                        Plugin.LogWarning("Why are we stopping the emote?");
                    Plugin.Log("CCC");
                    return true;
                }
                Plugin.Log("DDD");
            }
            Plugin.Log("ZZZ");
            return false;
        }


        protected virtual void CorrectVerticalPosition()
        {
            if (groundContactPoints == null)
                return;

            float lowestPoint = 0;
            foreach (var bone in groundContactPoints)
                lowestPoint = Mathf.Min(lowestPoint, bone.transform.position.y - metarig.position.y);
            if (lowestPoint < 0)
                metarig.position = new Vector3(metarig.position.x, metarig.position.y - lowestPoint, metarig.position.z);
        }


        public virtual bool IsPerformingCustomEmote() { return isPerformingEmote && performingEmote != null; }


        public virtual bool CanPerformEmote() => true;


        public virtual void PerformEmote(UnlockableEmote emote, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
            bool canPerform = CanPerformEmote();
            Plugin.LogWarning("PreOnPerformEmote - CanPerform: " + canPerform);
            if (!initialized || !CanPerformEmote())
                return;

            AnimationClip animationClip = emote.animationClip;
            if (overrideAnimationClip != null)
            {
                if (emote == null)
                {
                    Debug.LogError("Failed to perform emote with overrideAnimationClip while passed emote is null.");
                    return;
                }
                if (!emote.ClipIsInEmote(overrideAnimationClip))
                {
                    Debug.LogError("Failed to perform emote where overrideAnimationClip is not the start or loop clip of the passed emote.");
                    return;
                }
                animationClip = overrideAnimationClip;
            }

            Plugin.Log("[" + name + "] Performing emote: " + emote.emoteName + (animationClip == overrideAnimationClip ? " OverrideClip: " + animationClip.name : "") + (playAtTimeNormalized > 0 ? " PlayAtTime: " + playAtTimeNormalized : ""));
            animator.SetBool("loop", emote.transitionsToClip != null);

            if (emote.transitionsToClip != null)
            {
                if (animationClip == emote.animationClip)
                {
                    animatorController["emote_start"] = animationClip;
                    animatorController["emote_loop"] = emote.transitionsToClip;
                    animator.Play("emote_start", 0, playAtTimeNormalized);
                }
                else
                {
                    animatorController["emote_loop"] = animationClip;
                    animator.Play("emote_loop", 0, playAtTimeNormalized);
                }
            }
            else
            {
                animatorController["emote"] = animationClip;
                animator.Play("emote", 0, playAtTimeNormalized);
            }
            animator.Update(playAtTimeNormalized);

            Plugin.Log("NORMALIZED TIME SHOULD BE 0. IT IS: " + normalizedTimeAnimation);

            performingEmote = emote;
            isPerformingEmote = true;
        }


        public void PerformEmoteDelayed(UnlockableEmote emote, float delayForSeconds, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
            IEnumerator PerformAfterDelay()
            {
                yield return new WaitForSeconds(delayForSeconds);
                if (CanPerformEmote())  
                    PerformEmote(emote, overrideAnimationClip, playAtTimeNormalized);
            }
            StartCoroutine(PerformAfterDelay());
        }


        public void SyncWithEmoteController(EmoteController emoteController)
        {
            if (emoteController == null || !emoteController.IsPerformingCustomEmote() || !CanPerformEmote())
                return;
            Plugin.Log("[" + name + "] Attempting to sync with emote controller: " + emoteController.name + " Emote: " + emoteController.performingEmote.emoteName);
            PerformEmote(emoteController.performingEmote, emoteController.GetCurrentAnimationClip(), emoteController.normalizedTimeAnimation);
        }


        public virtual void StopPerformingEmote()
        {
            Plugin.Log(string.Format("[" + name + "] Stopping emote."));
            isPerformingEmote = false;
            metarig.localPosition = new Vector3(metarig.localPosition.x, 0, metarig.localPosition.z);
        }


        public AnimationClip GetCurrentAnimationClip()
        {
            if (!IsPerformingCustomEmote())
                return null;

            if (!animator.GetBool("loop"))
                return animatorController["emote"];

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("emote_loop"))
                return animatorController["emote_loop"];
            return animatorController["emote_start"];
        }


        public void CreateBoneMap()
        {
            boneMapBody = BoneMap.CreateBoneMapBody(humanoidSkeleton, metarig);
            boneMapHead = BoneMap.CreateBoneMapHead(humanoidSkeleton, metarig);
            boneMapLeftArm = BoneMap.CreateBoneMapLeftArm(humanoidSkeleton, metarig);
            boneMapRightArm = BoneMap.CreateBoneMapRightArm(humanoidSkeleton, metarig);
            boneMapLeftLeg = BoneMap.CreateBoneMapLeftLeg(humanoidSkeleton, metarig);
            boneMapRightLeg = BoneMap.CreateBoneMapRightLeg(humanoidSkeleton, metarig);
            boneMapLeftFingers = BoneMap.CreateBoneMapLeftFingers(humanoidSkeleton, metarig);
            boneMapRightFingers = BoneMap.CreateBoneMapRightFingers(humanoidSkeleton, metarig);
            boneMapLeftToes = BoneMap.CreateBoneMapLeftToes(humanoidSkeleton, metarig);
            boneMapRightToes = BoneMap.CreateBoneMapRightToes(humanoidSkeleton, metarig);

            boneMapLeftHandIK = BoneMap.CreateBoneMapLeftHandTargetIK(humanoidSkeleton, metarig);
            boneMapRightHandIK = BoneMap.CreateBoneMapRightHandTargetIK(humanoidSkeleton, metarig);
            boneMapLeftFootIK = BoneMap.CreateBoneMapLeftFootTargetIK(humanoidSkeleton, metarig);
            boneMapRightFootIK = BoneMap.CreateBoneMapRightFootTargetIK(humanoidSkeleton, metarig);
            boneMapHeadIK = BoneMap.CreateBoneMapHeadTargetIK(humanoidSkeleton, metarig);

            if (boneMapLeftHandIK != null && boneMapLeftHandIK.Count > 0)
                leftHandIKTarget = boneMapLeftHandIK.Values.First();
            if (boneMapRightHandIK != null && boneMapRightHandIK.Count > 0)
                rightHandIKTarget = boneMapRightHandIK.Values.First();
            if (boneMapLeftFootIK != null && boneMapLeftFootIK.Count > 0)
                leftFootIKTarget = boneMapLeftFootIK.Values.First();
            if (boneMapRightFootIK != null && boneMapRightFootIK.Count > 0)
                rightFootIKTarget = boneMapRightFootIK.Values.First();
            if (boneMapLeftHandIK != null && boneMapLeftHandIK.Count > 0)
                leftHandIKTarget = boneMapLeftHandIK.Values.First();
            if (boneMapHeadIK != null && boneMapHeadIK.Count > 0)
                headIKTarget = boneMapHeadIK.Values.First();
        }


        public virtual ulong GetEmoteControllerId() => 0;
    }
}
