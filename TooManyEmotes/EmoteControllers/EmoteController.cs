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
using TooManyEmotes.Audio;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using TooManyEmotes.Props;
using Unity.Netcode;
using UnityEditor.Analytics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

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

        public bool isSimpleEmoteController { get { return false; } }// { get { return GetType() == typeof(EmoteController); } }

        public EmoteSyncGroup emoteSyncGroup;
        public int emoteSyncId { get { return emoteSyncGroup != null ? emoteSyncGroup.syncId : -1; } }

        public EmoteAudioSource personalEmoteAudioSource;


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
                    personalEmoteAudioSource = humanoidSkeleton.gameObject.AddComponent<EmoteAudioSource>();
                }

                if (propsParent == null)
                {
                    propsParent = transform.Find("props");
                    if (propsParent == null)
                    {
                        propsParent = new GameObject("props").transform;
                        propsParent.parent = humanoidSkeleton;
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

                targetBone.transform.position = sourceBone.transform.position;
                targetBone.transform.rotation = sourceBone.transform.rotation;
            }
            //CorrectVerticalPosition();
        }


        protected virtual bool CheckIfShouldStopEmoting()
        {
            if (isPerformingEmote)
            {
                return performingEmote == null || (!performingEmote.loopable && !performingEmote.isPose && currentAnimationTimeNormalized >= 1);
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


        public virtual bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1)
        {
            if (!initialized || !CanPerformEmote())
                return false;

            if (!isSimpleEmoteController)
                Plugin.Log("[" + emoteControllerName + "] Performing emote: " + emote.emoteName);

            if (isPerformingEmote)
                StopPerformingEmote();

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

            Plugin.Log("Performing emote: " + (performingEmote == null ? "NULL" : performingEmote.emoteName));

            // Emote on props (if they exist)
            PerformPropEmotes();

            // Create emote sync group and try to play emote audio
            if (!isSimpleEmoteController)
                CreateEmoteSyncGroup();

            return true;
        }


        public virtual bool SyncWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
        {
            if (!initialized || !CanPerformEmote() || emoteController == null || !emoteController.IsPerformingCustomEmote())
                return false;

            if (!isSimpleEmoteController)
                Plugin.Log("[" + emoteControllerName + "] Attempting to sync with emote controller: " + emoteController.name + " Emote: " + emoteController.performingEmote.emoteName + " PlayEmoteAtTimeNormalized: " + (emoteController.currentAnimationTimeNormalized % 1));

            if (isPerformingEmote)
            {
                //Plugin.LogWarning("Syncing with emote controller while performing emote already. Ignore this.");
                //if (performingEmote != null) Plugin.LogWarning("Emote: " + performingEmote.emoteName);
                StopPerformingEmote();
            }

            var syncGroup = emoteController.emoteSyncGroup;

            if (syncGroup == null)
                Plugin.LogWarning("[" + emoteControllerName + "] Attempted to sync with emote controller who is not a part of an emote sync group. Continuing anyways.");

            var emote = emoteController.performingEmote;
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
                Plugin.LogError("[" + emoteControllerName + "] Attempted to sync with emote controller whose animation clip is not a part of their performing emote? Emote: " + emoteController.performingEmote + " AnimationClip: " + animationClip.name);
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
            PerformPropEmotes();

            // Create emote sync group and try to play emote audio
            if (!isSimpleEmoteController && emoteController.emoteSyncGroup != null)
                AddToEmoteSyncGroup(emoteController.emoteSyncGroup);

            return true;
        }


        protected void PerformPropEmotes()
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
            if (performingEmote.propNamesInEmote == null)
                return;

            UnloadEmoteProps();

            foreach (string propName in performingEmote.propNamesInEmote)
            {
                var propObject = EmotePropManager.LoadEmoteProp(propName);
                propObject.SetPropLayer(6);
                emotingProps.Add(propObject);
                propObject.transform.parent = propsParent;
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
                    prop.transform.parent = EmotePropManager.propPoolParent;
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
                Plugin.Log(string.Format("[" + emoteControllerName + "] Stopping emote."));

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
        }


        protected virtual void FindIkBones()
        {
            var rootIkBone = FindChildRecursive("root_ik");
            if (rootIkBone == null)
            {
                Plugin.LogError("Failed to find root ik bone called \"root_ik\" in humanoid skeleton: " + emoteControllerName);
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


        protected void CreateEmoteSyncGroup()
        {
            emoteSyncGroup = EmoteSyncGroup.CreateEmoteSyncGroup(this);
        }

        
        protected void AddToEmoteSyncGroup(EmoteSyncGroup emoteSyncGroup)
        {
            Plugin.Log("Adding to emote sync group with id: " + emoteSyncGroup.syncId);
            emoteSyncGroup.AddToEmoteSyncGroup(this);
            this.emoteSyncGroup = emoteSyncGroup;
        }


        protected void RemoveFromEmoteSyncGroup()
        {
            if (emoteSyncGroup != null)
                emoteSyncGroup.RemoveFromEmoteSyncGroup(this);
            emoteSyncGroup = null;
        }
    }
}