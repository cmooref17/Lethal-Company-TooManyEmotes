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
using TooManyEmotes.Compatibility;
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
        public ulong emoteControllerId { get { return GetEmoteControllerId(); } }
        public string emoteControllerName { get { return GetEmoteControllerName(); } }


        public Transform metarig;
        protected Vector3 originalMetarigLocalPosition = Vector3.zero;
        public Animator originalAnimator;
        public Transform humanoidSkeleton;

        public Animator animator;
        public AnimatorOverrideController animatorController;

        //public AudioSource audioSource;

        protected bool isPerformingEmote = false;
        public UnlockableEmote performingEmote;

        public List<Transform> groundContactPoints = new List<Transform>();
        public float normalizedTimeAnimation { get { return animator.GetCurrentAnimatorStateInfo(0).normalizedTime; } }

        protected Dictionary<Transform, Transform> boneMap;

        public bool isSimpleEmoteController { get { return GetType() == typeof(EmoteController); } }



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

                //audioSource = gameObject.AddComponent<AudioSource>();
                //audioSource.dopplerLevel = 0;
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

            if (!isSimpleEmoteController)
                CreateBoneMap();
            else
                Debug.LogWarning("Using the base emote controller. Remember that when doing this, the bonemap will need to be built manually.");

            //AddGroundContactPoints();
        }


        protected virtual void OnEnable() { }


        protected virtual void OnDisable()
        {
            if (initialized && isPerformingEmote)
                StopPerformingEmote();
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
                if (CheckIfShouldStopEmoting())
                {
                    Plugin.LogWarning("OnCheckIfShouldStopEmoting. Stopping emote. " + name + " NormTime: " + normalizedTimeAnimation);
                    StopPerformingEmote();
                }
            }

            if (animator == null || animatorController == null || boneMap == null || !isPerformingEmote)
                return;

            TranslateAnimation();
        }


        protected virtual void TranslateAnimation()
        {
            if (boneMap == null || performingEmote == null || boneMap.Count <= 0)
                return;


            foreach (var pair in boneMap)
            {
                var sourceBone = pair.Key;
                var targetBone = pair.Value;

                targetBone.transform.position = sourceBone.transform.position;
                targetBone.transform.rotation = sourceBone.transform.rotation;
            }
            //CorrectVerticalPosition();
        }


        protected virtual bool CheckIfShouldStopEmoting()
        {
            if (isPerformingEmote)
            {
                return performingEmote == null || (!performingEmote.loopable && !performingEmote.isPose && normalizedTimeAnimation >= 1);
                /*
                if (performingEmote == null || (!performingEmote.loopable && !performingEmote.isPose && normalizedTimeAnimation >= 1))
                {
                    if (performingEmote == null)
                        Plugin.LogWarning("Stopping emote. Loaded emote is null. Ignore this message.");
                    else if (!performingEmote.loopable && !performingEmote.isPose && normalizedTimeAnimation >= 1)
                        Plugin.LogWarning("Stopping emote. Emote has ended at time: " + normalizedTimeAnimation + " Loopable: " + performingEmote.loopable + " IsPose: " + performingEmote.isPose + " Ignore this message.");
                    else
                        Plugin.LogWarning("Why are we stopping the emote? Ignore this message.");
                    return true;
                }
                */
            }
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


        public virtual bool CanPerformEmote() => animator != null && animator.enabled;


        public virtual void PerformEmote(UnlockableEmote emote, AnimationClip overrideAnimationClip = null, float playAtTimeNormalized = 0)
        {
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
                    Debug.LogError("Failed to perform emote where overrideAnimationClip is not the start or loop clip of the passed emote. Clip: " + overrideAnimationClip.name + " Emote: " + emote.emoteName);
                    return;
                }
                animationClip = overrideAnimationClip;
            }

            playAtTimeNormalized %= 1;
            if (!isSimpleEmoteController)
                Plugin.Log("[" + name + "] Performing emote: " + emote.emoteName + (animationClip == overrideAnimationClip ? " OverrideClip: " + animationClip.name : "") + (playAtTimeNormalized > 0 ? " PlayAtTime: " + playAtTimeNormalized : ""));
            animator.avatar = emote.humanoidAnimation ? Plugin.humanoidAvatar : null;

            animatorController["emote"] = emote.animationClip;
            if (emote.transitionsToClip != null)
                animatorController["emote_loop"] = emote.transitionsToClip;

            //if (!isSimpleEmoteController) Plugin.LogWarning("EMOTE: " + emote.emoteName + " SET EMOTE CLIP: " + animatorController["emote"].name + " SET LOOP CLIP: " + (animatorController["emote_loop"] != null ? animatorController["emote_loop"].name : "NULL"));

            animator.SetBool("loop", emote.transitionsToClip != null);
            animator.Play(animationClip == emote.transitionsToClip ? "emote_loop" : "emote", 0, playAtTimeNormalized);

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
            if (emoteController == null || !emoteController.IsPerformingCustomEmote())
                return;
            if (!isSimpleEmoteController)
                Plugin.Log("[" + name + "] Attempting to sync with emote controller: " + emoteController.name + " Emote: " + emoteController.performingEmote.emoteName + " PlayEmoteAtTimeNormalized: " + (emoteController.normalizedTimeAnimation % 1));
            PerformEmote(emoteController.performingEmote, emoteController.GetCurrentAnimationClip(), emoteController.normalizedTimeAnimation);
        }


        public virtual void StopPerformingEmote()
        {
            if (!isSimpleEmoteController)
                Plugin.Log(string.Format("[" + name + "] Stopping emote."));
            isPerformingEmote = false;
            metarig.localPosition = new Vector3(metarig.localPosition.x, 0, metarig.localPosition.z);
            //audioSource.Stop();
        }


        public AnimationClip GetCurrentAnimationClip()
        {
            if (!IsPerformingCustomEmote())
                return null;

            if (!animator.GetBool("loop"))
                return animatorController["emote"];

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return animatorController[stateInfo.IsName("emote_loop") ? "emote_loop" : "emote"];
        }


        protected virtual void CreateBoneMap() { }


        protected virtual ulong GetEmoteControllerId() => 0;
        protected virtual string GetEmoteControllerName() => name;
    }
}