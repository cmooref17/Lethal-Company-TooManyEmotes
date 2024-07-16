﻿using GameNetcodeStuff;
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
using TooManyEmotes.Audio;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using TooManyEmotes.Props;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [HarmonyPatch]
    [DefaultExecutionOrder(-2)]
    public class EmoteController : MonoBehaviour
    {
        public static Dictionary<GameObject, EmoteController> allEmoteControllers = new Dictionary<GameObject, EmoteController>();

        public bool initialized = false;
        public ulong emoteControllerId { get { return GetEmoteControllerId(); } }
        public string emoteControllerName { get { return GetEmoteControllerName(); } }

        public Transform metarig;
        //protected Vector3 originalMetarigLocalPosition = Vector3.zero;
        //public Animator originalAnimator;

        public Transform humanoidSkeleton;
        public Animator animator;
        public AnimatorOverrideController animatorController;

        public bool isPerformingEmote = false;
        public UnlockableEmote performingEmote;
        public float currentAnimationTimeNormalized { get { return animator.GetCurrentAnimatorStateInfo(0).normalizedTime; } }
        public float currentAnimationTime { get { var animationClip = GetCurrentAnimationClip(); return animationClip != null ? animationClip.length * (currentAnimationTimeNormalized % 1) : 0; } }
        public int currentStateHash { get { return animator.GetCurrentAnimatorStateInfo(0).shortNameHash; } }
        public bool isLooping { get { return animator.GetBool("loop"); } set { animator.SetBool("loop", value); } }
        public bool isAnimatorInLoopingState { get { return animator.GetCurrentAnimatorStateInfo(0).IsName("emote_loop"); } }

        protected Dictionary<Transform, Transform> boneMap;
        public List<Transform> groundContactPoints = new List<Transform>();

        public Transform propsParent;
        public List<PropObject> emotingProps = new List<PropObject>();

        public Transform ikLeftHand;
        public Transform ikRightHand;
        public Transform ikLeftFoot;
        public Transform ikRightFoot;
        public Transform ikHead;

        public bool isSimpleEmoteController { get { return GetType() == typeof(EmoteController); } }

        public EmoteSyncGroup emoteSyncGroup;
        public int emoteSyncId { get { return emoteSyncGroup != null ? emoteSyncGroup.syncId : -1; } }

        public EmoteAudioSource personalEmoteAudioSource;

        private float timePerformedEmote = 0;
        //private Dictionary<Transform, Vector3> smoothBonePositions = new Dictionary<Transform, Vector3>();
        //private Dictionary<Transform, Quaternion> smoothBoneRotations = new Dictionary<Transform,Quaternion>();

        public bool smoothTransitionToEmote = false;


        protected virtual void Awake()
        {
            if (initialized) return;
            Initialize();
        }


        public virtual void Initialize(string sourceRootBoneName = "metarig")
        {
            if (initialized || Plugin.humanoidSkeletonPrefab == null || Plugin.humanoidAnimatorController == null || Plugin.humanoidAvatar == null)
                return;

            try
            {
                metarig = FindChildRecursive(sourceRootBoneName, transform);

                humanoidSkeleton = GameObject.Instantiate(Plugin.humanoidSkeletonPrefab, metarig.parent).transform;
                humanoidSkeleton.name = "HumanoidSkeleton";

                humanoidSkeleton.SetSiblingIndex(metarig.GetSiblingIndex() + 1);

                animator = humanoidSkeleton.GetComponent<Animator>();
                Debug.Assert(animator != null);

                var ikHandler = animator.gameObject.AddComponent<OnAnimatorIKHandler>();
                ikHandler.SetParentEmoteController(this);

                animatorController = new AnimatorOverrideController(Plugin.humanoidAnimatorController);
                animator.runtimeAnimatorController = animatorController;

                humanoidSkeleton.SetLocalPositionAndRotation(metarig.localPosition + Vector3.down * 0.025f, Quaternion.identity);
                humanoidSkeleton.localScale = metarig.localScale;

                if (!isSimpleEmoteController)
                {
                    allEmoteControllers.Add(gameObject, this);
                    GameObject emoteAudioSourceObject = new GameObject("PersonalEmoteAudioSource");
                    emoteAudioSourceObject.transform.SetParent(humanoidSkeleton);
                    emoteAudioSourceObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    emoteAudioSourceObject.transform.localScale = Vector3.one;
                    personalEmoteAudioSource = emoteAudioSourceObject.AddComponent<EmoteAudioSource>();
                }

                if (propsParent == null)
                {
                    propsParent = transform.Find("EmoteProps");
                    if (propsParent == null)
                    {
                        propsParent = new GameObject("EmoteProps").transform;
                        propsParent.SetParent(humanoidSkeleton);
                        propsParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        propsParent.localScale = Vector3.one;
                    }
                }

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

            if (boneMap == null)
            {
                if (!isSimpleEmoteController)
                    CreateBoneMap();
                else
                    Debug.LogWarning("Using the base emote controller. Remember that when doing this, the bonemap will need to be built manually.");
            }

            FindIkBones();
            //AddGroundContactPoints();
        }


        protected virtual void OnEnable() { }


        protected virtual void OnDisable()
        {
            if (isPerformingEmote)
                StopPerformingEmote();
        }


        protected virtual void OnDestroy()
        {
            if (isPerformingEmote)
                StopPerformingEmote();
            allEmoteControllers?.Remove(gameObject);

            if (SyncPerformingEmoteManager.doNotTriggerAudioDict.ContainsKey(this))
                SyncPerformingEmoteManager.doNotTriggerAudioDict.Remove(this);
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
                    StopPerformingEmote();
            }

            if (animator == null || animatorController == null || boneMap == null || !isPerformingEmote)
                return;

            TranslateAnimation();
        }


        protected virtual void TranslateAnimation()
        {
            if (performingEmote == null || boneMap == null || boneMap.Count <= 0)
                return;

            foreach (var pair in boneMap)
            {
                var sourceBone = pair.Key;
                var targetBone = pair.Value;

                if (sourceBone == null || targetBone == null) continue;

                float emoteTime = Time.time - timePerformedEmote;
                float lerpAmount = smoothTransitionToEmote ? Mathf.Clamp01(emoteTime / 0.2f) : 1;
                targetBone.transform.position = Vector3.Lerp(targetBone.transform.position, sourceBone.transform.position, lerpAmount);
                targetBone.transform.rotation = Quaternion.Slerp(targetBone.transform.rotation, sourceBone.transform.rotation, lerpAmount);

                /*// Apply smooth bone transitions to emote
                if (smoothTransitionToEmote && emoteTime < 0.5f)
                {
                    smoothBonePositions[targetBone] = Vector3.Lerp(smoothBonePositions[targetBone], targetBone.transform.localPosition, lerpAmount);
                    smoothBoneRotations[targetBone] = Quaternion.Lerp(smoothBoneRotations[targetBone], targetBone.transform.localRotation, lerpAmount);
                    targetBone.localPosition = smoothBonePositions[targetBone];
                    targetBone.localRotation = smoothBoneRotations[targetBone];
                }*/
            }

            //CorrectVerticalPosition();
        }


        protected virtual bool CheckIfShouldStopEmoting()
        {
            if (isPerformingEmote)
            {
                bool shouldStop = performingEmote == null || (!performingEmote.loopable && !performingEmote.isPose && currentAnimationTimeNormalized >= 1);
                return shouldStop;
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


        public virtual bool CanPerformEmote() => initialized && animator != null && animator.enabled;


        public virtual bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1, bool doNotTriggerAudio = false)
        {
            if (!initialized || !CanPerformEmote())
                return false;

            if (!isSimpleEmoteController)
                Log("[" + emoteControllerName + "] Performing emote: " + emote.emoteName);

            if (isPerformingEmote)
                ResetPerformingEmote();

            if (emote.emoteSyncGroup != null)
            {
                if (overrideEmoteId >= 0 && overrideEmoteId < emote.emoteSyncGroup.Count && emote.emoteSyncGroup[overrideEmoteId] != null)
                    emote = emote.emoteSyncGroup[overrideEmoteId];
                else if (emote.randomEmote)
                {
                    int randomIndex = UnityEngine.Random.Range(0, emote.emoteSyncGroup.Count);
                    var syncEmote = emote.emoteSyncGroup[randomIndex];
                    if (syncEmote != null)
                        emote = syncEmote;
                }
            }

            animatorController["emote"] = emote.animationClip;
            if (emote.transitionsToClip != null)
                animatorController["emote_loop"] = emote.transitionsToClip;

            animator.SetBool("loop", emote.transitionsToClip != null);
            animator.Play("emote", 0, 0);
            animator.Update(0);

            performingEmote = emote;
            isPerformingEmote = true;

            timePerformedEmote = Time.time;
            RecordStartingBonePositions();

            // Emote on props (if they exist)
            PerformEmoteProps();

            // Create emote sync group and try to play emote audio
            if (!isSimpleEmoteController)
                CreateEmoteSyncGroup(doNotTriggerAudio);

            DiscoBallPatcher.OnPerformEmote(this);
            return true;
        }


        public virtual bool SyncWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
        {
            if (!initialized || !CanPerformEmote() || emoteController == null || !emoteController.IsPerformingCustomEmote())
                return false;

            if (!isSimpleEmoteController)
                Log("[" + emoteControllerName + "] Attempting to sync with emote controller: " + emoteController.name + " Emote: " + emoteController.performingEmote.emoteName + " PlayEmoteAtTimeNormalized: " + (emoteController.currentAnimationTimeNormalized % 1));

            if (isPerformingEmote)
                ResetPerformingEmote();

            var syncGroup = emoteController.emoteSyncGroup;

            if (syncGroup == null)
                LogWarning("[" + emoteControllerName + "] Attempted to sync with emote controller who is not a part of an emote sync group. Continuing anyways.");

            var emote = emoteController.performingEmote;
            if (emote.emoteSyncGroup != null)
            {
                if (overrideEmoteId >= 0 && overrideEmoteId < emote.emoteSyncGroup.Count && emote.emoteSyncGroup[overrideEmoteId] != null)
                    emote = emote.emoteSyncGroup[overrideEmoteId];
                else if (emote.randomEmote)
                {
                    if (emote.hasAudio && !emote.isBoomboxAudio)
                        emote = emoteController.performingEmote;
                    else
                    {
                        int randomIndex = UnityEngine.Random.Range(0, emote.emoteSyncGroup.Count);
                        var syncEmote = emote.emoteSyncGroup[randomIndex];
                        if (syncEmote != null)
                            emote = syncEmote;
                    }
                }
                else
                {
                    bool setEmote = false;
                    foreach (var syncEmote in emote.emoteSyncGroup)
                    {
                        if (!syncGroup.leadEmoteControllerByEmote.ContainsKey(syncEmote) || syncGroup.leadEmoteControllerByEmote[emote] == null)
                        {
                            emote = syncEmote;
                            setEmote = true;
                            break;
                        }
                    }
                    if (!setEmote)
                    {
                        int index = emote.emoteSyncGroup.IndexOf(emote);
                        if (index >= 0)
                        {
                            index = (index + 1) % emote.emoteSyncGroup.Count;
                            emote = emote.emoteSyncGroup[index];
                        }
                    }
                }
            }

            var animationClip = emoteController.GetCurrentAnimationClip();
            if (!emote.ClipIsInEmote(animationClip))
            {
                LogError("[" + emoteControllerName + "] Attempted to sync with emote controller whose animation clip is not a part of their performing emote? Emote: " + emoteController.performingEmote + " AnimationClip: " + animationClip.name);
                return false;
            }

            animatorController["emote"] = emote.animationClip;
            if (emote.transitionsToClip != null)
                animatorController["emote_loop"] = emote.transitionsToClip;

            float playAtTimeNormalized = emoteController.currentAnimationTimeNormalized % 1;
            animator.SetBool("loop", emote.transitionsToClip != null);
            animator.Play(animationClip == emote.transitionsToClip ? "emote_loop" : "emote", 0, playAtTimeNormalized);
            animator.Update(0);

            performingEmote = emote;
            isPerformingEmote = true;

            // Emote on props (if they exist)
            PerformEmoteProps();

            // Create emote sync group and try to play emote audio
            if (!isSimpleEmoteController && emoteController.emoteSyncGroup != null)
                AddToEmoteSyncGroup(emoteController.emoteSyncGroup);

            DiscoBallPatcher.OnPerformEmote(this);
            return true;
        }


        protected void PerformEmoteProps()
        {
            if (propsParent != null && performingEmote.propNamesInEmote != null)
                LoadEmoteProps();

            if (emotingProps != null)
            {
                foreach (var prop in emotingProps)
                    prop.SyncWithEmoteController(this);
            }
        }


        protected void LoadEmoteProps()
        {
            UnloadEmoteProps();

            if (performingEmote.propNamesInEmote == null)
                return;

            foreach (string propName in performingEmote.propNamesInEmote)
            {
                var propObject = EmotePropManager.LoadEmoteProp(propName);
                propObject.SetPropLayer(6); // Prop layer
                emotingProps.Add(propObject);
                propObject.transform.SetParent(propsParent);
                propObject.transform.localPosition = Vector3.zero;
                propObject.transform.localRotation = Quaternion.identity;
            }
        }


        protected void UnloadEmoteProps()
        {
            if (emotingProps != null)
            {
                foreach (var prop in emotingProps)
                {
                    prop.transform.SetParent(EmotePropManager.propPoolParent);
                    prop.active = false;
                }
                emotingProps.Clear();
            }
        }


        public virtual void StopPerformingEmote()
        {
            if (!initialized)
                return;

            isPerformingEmote = false;

            if (!isSimpleEmoteController)
                Log(string.Format("[" + emoteControllerName + "] Stopping emote."));

            animatorController["emote"] = null;
            animatorController["emote_loop"] = null;

            RemoveFromEmoteSyncGroup();
            UnloadEmoteProps();

            if (ikLeftHand != null) ikLeftHand.localPosition = Vector3.zero;
            if (ikRightHand != null) ikRightHand.localPosition = Vector3.zero;
            if (ikLeftFoot != null) ikLeftFoot.localPosition = Vector3.zero;
            if (ikRightFoot != null) ikRightFoot.localPosition = Vector3.zero;
            if (ikHead != null) ikHead.localPosition = Vector3.zero;

            DiscoBallPatcher.OnStopPerformingEmote(this);
        }


        public virtual void ResetPerformingEmote()
        {
            if (!initialized)
                return;

            isPerformingEmote = false;

            animatorController["emote"] = null;
            animatorController["emote_loop"] = null;

            RemoveFromEmoteSyncGroup();
            UnloadEmoteProps();

            if (ikLeftHand != null) ikLeftHand.localPosition = Vector3.zero;
            if (ikRightHand != null) ikRightHand.localPosition = Vector3.zero;
            if (ikLeftFoot != null) ikLeftFoot.localPosition = Vector3.zero;
            if (ikRightFoot != null) ikRightFoot.localPosition = Vector3.zero;
            if (ikHead != null) ikHead.localPosition = Vector3.zero;
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


        public void CreateBoneMap(List<string> sourceBoneNames, List<string> targetBoneNames = null)
        {
            boneMap = BoneMapper.CreateBoneMap(humanoidSkeleton, metarig, sourceBoneNames, targetBoneNames);
            /*foreach (var bone in boneMap.Values)
            {
                smoothBonePositions.Add(bone, bone.localPosition);
                smoothBoneRotations.Add(bone, bone.localRotation);
            }*/
        }


        protected virtual void FindIkBones()
        {
            var rootIkBone = FindChildRecursive("root_ik");
            if (rootIkBone == null)
            {
                LogError("Failed to find root ik bone called \"root_ik\" in humanoid skeleton: " + emoteControllerName);
                return;
            }

            ikLeftHand = rootIkBone.Find("hand_ik_l");
            ikRightHand = rootIkBone.Find("hand_ik_r");
            ikLeftFoot = rootIkBone.Find("foot_ik_l");
            ikRightFoot = rootIkBone.Find("foot_ik_r");
            ikHead = rootIkBone.Find("head_ik");
        }


        protected virtual Transform FindChildRecursive(string objectName, Transform root = null)
        {
            if (root == null)
                root = humanoidSkeleton;

            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var bone = FindChildRecursive(objectName, child);
                if (bone != null)
                    return bone;
            }
            return null;
        }


        protected virtual ulong GetEmoteControllerId() => 0;
        protected virtual string GetEmoteControllerName() => name;


        protected void CreateEmoteSyncGroup(bool doNotTriggerAudio = false)
        {
            emoteSyncGroup = EmoteSyncGroup.CreateEmoteSyncGroup(this, !doNotTriggerAudio);
        }

        
        protected void AddToEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup)
        {
            Log("Adding to emote sync group with id: " + emoteSyncGroup.syncId);
            emoteSyncGroup.AddToEmoteSyncGroup(this);
            this.emoteSyncGroup = emoteSyncGroup;
        }


        protected void RemoveFromEmoteSyncGroup()
        {
            if (emoteSyncGroup != null)
                emoteSyncGroup.RemoveFromEmoteSyncGroup(this);
            emoteSyncGroup = null;
        }


        protected void RecordStartingBonePositions()
        {
            /*if (!smoothTransitionToEmote)
                return;

            for (int i = 0; i < smoothBonePositions.Count; i++)
            {
                Transform bone = smoothBonePositions.ElementAt(i).Key;
                smoothBonePositions[bone] = bone.localPosition;
                smoothBoneRotations[bone] = bone.localRotation;
            }*/
        }
    }
}