using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
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
using static TooManyEmotes.HelperTools;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [DefaultExecutionOrder(-2)]
    public class EmoteControllerPlayer : EmoteController
    {
        public static Dictionary<PlayerControllerB, EmoteControllerPlayer> allPlayerEmoteControllers = new Dictionary<PlayerControllerB, EmoteControllerPlayer>();
        public static EmoteControllerPlayer emoteControllerLocal { get { return localPlayerController != null && allPlayerEmoteControllers.ContainsKey(localPlayerController) ? allPlayerEmoteControllers[localPlayerController] : null; } }
        
        public PlayerControllerB playerController;

        public bool isLocalPlayer { get { return playerController == StartOfRound.Instance?.localPlayerController; } }
        public ulong clientId { get { return playerController.actualClientId; } }
        public ulong playerId { get { return playerController.playerClientId; } }
        public ulong steamId { get { return playerController.playerSteamId; } }
        public string username { get { return playerController.playerUsername; } }

        public Animator originalAnimator;
        public float timeSinceStartingEmote { get { return (float)Traverse.Create(playerController).Field("timeSinceStartingEmote").GetValue(); } set { Traverse.Create(playerController).Field("timeSinceStartingEmote").SetValue(value); } }

        private Dictionary<Transform, Transform> boneMapLocalPlayerArms;
        internal Transform humanoidHead;

        private Transform cameraContainerTarget;
        private Transform cameraContainerLerp;

        public static List<string> sourceBoneNames = new List<string>
        {
            "spine", "spine.001", "spine.002", "spine.003", "spine.004",
            "shoulder.L", "arm.L_upper", "arm.L_lower", "hand.L", "finger1.L", "finger1.L.001", "finger2.L", "finger2.L.001", "finger3.L", "finger3.L.001", "finger4.L", "finger4.L.001", "finger5.L", "finger5.L.001",
            "shoulder.R", "arm.R_upper", "arm.R_lower", "hand.R", "finger1.R", "finger1.R.001", "finger2.R", "finger2.R.001", "finger3.R", "finger3.R.001", "finger4.R", "finger4.R.001", "finger5.R", "finger5.R.001",
            "thigh.L", "shin.L", "foot.L", "heel.02.L", "toe.L",
            "thigh.R", "shin.R", "foot.R", "heel.02.R", "toe.R"
        };

        public GrabbablePropObject sourceGrabbableEmoteProp;


        public override void Initialize(string sourceRootBoneName = "metarig")
        {
            base.Initialize();
            if (!initialized)
                return;

            originalAnimator = metarig.GetComponentInChildren<Animator>();
            playerController = GetComponentInParent<PlayerControllerB>();
            if (playerController == null)
            {
                LogError("Failed to find PlayerControllerB component in parent of EmoteControllerPlayer.");
                return;
            }
            allPlayerEmoteControllers.Add(playerController, this);
        }


        protected override void Start()
        {
            base.Start();
            if (initialized)
            {
                Transform spine4 = FindChildRecursive("spine.004", metarig);
                if (spine4 != null)
                {
                    cameraContainerTarget = new GameObject("CameraContainer_Target").transform;
                    cameraContainerTarget.SetParent(spine4);
                    cameraContainerTarget.localPosition = new Vector3(0, 0.22f, 0);
                    cameraContainerTarget.localEulerAngles = new Vector3(-3, 0, 0);

                    cameraContainerLerp = new GameObject("CameraContainer_Lerp").transform;
                    cameraContainerLerp.SetParent(humanoidSkeleton);
                    cameraContainerLerp.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }

                humanoidHead = FindChildRecursive("head", humanoidSkeleton);
                if (!humanoidHead)
                    LogError("Failed to find Head on: " + emoteControllerName);
            }
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            allPlayerEmoteControllers?.Remove(playerController);
        }


        /*
        protected override void AddGroundContactPoints()
        {
            base.AddGroundContactPoints();
        }
        */


        protected override void Update()
        {
            if (!initialized || playerController == null || (playerController == localPlayerController && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
                return;
            base.Update();
        }


        protected override void LateUpdate()
        {
            if (!initialized || playerController == null || (playerController == localPlayerController && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
                return;

            bool isEmoting = isPerformingEmote;
            base.LateUpdate();

            if (isEmoting && !isPerformingEmote && playerController.performingEmote)
            {
                playerController.performingEmote = false;
                originalAnimator.SetInteger("emoteNumber", 0);
                var currentStateInfo = originalAnimator.GetCurrentAnimatorStateInfo(0);
                animator.Play(currentStateInfo.fullPathHash, 0, 0);
                if (isLocalPlayer)
                {
                    timeSinceStartingEmote = 0f;
                    playerController.StopPerformingEmoteServerRpc();
                }
            }
        }


        protected override void TranslateAnimation()
        {
            if (!initialized || !isPerformingEmote || playerController == null)
                return;

            base.TranslateAnimation();
            if (humanoidHead && cameraContainerLerp && cameraContainerTarget)
            {
                cameraContainerLerp.position = Vector3.Lerp(cameraContainerLerp.position, cameraContainerTarget.position, 25f * Time.deltaTime);
                cameraContainerLerp.rotation = Quaternion.Lerp(cameraContainerLerp.rotation, cameraContainerTarget.rotation, 25f * Time.deltaTime);

                playerController.cameraContainerTransform.position = cameraContainerLerp.position;
                playerController.cameraContainerTransform.rotation = cameraContainerLerp.rotation;
                if (isLocalPlayer)
                {
                    playerController.localVisor.position = playerController.localVisorTargetPoint.position;
                    playerController.localVisor.rotation = playerController.localVisorTargetPoint.rotation;
                }
            }


            if (isLocalPlayer)
            {
                //playerController.localArmsTransform.position = playerController.cameraContainerTransform.transform.position + playerController.gameplayCamera.transform.up * -0.5f;
                playerController.playerModelArmsMetarig.rotation = playerController.localArmsRotationTarget.rotation;

                if (boneMapLocalPlayerArms != null)
                {
                    foreach (var pair in boneMapLocalPlayerArms)
                    {
                        var sourceBone = pair.Key;
                        var targetBone = pair.Value;

                        if (sourceBone == null || targetBone == null)
                            continue;
                        targetBone.transform.position = sourceBone.transform.position;
                        targetBone.transform.rotation = sourceBone.transform.rotation;
                    }
                }
            }
        }


        protected override bool CheckIfShouldStopEmoting()
        {
            if (playerController == null || !isPerformingEmote)
                return false;
            if (base.CheckIfShouldStopEmoting() || !playerController.performingEmote || performingEmote == null)
                return true;

            var heldObject = playerController.ItemSlots[playerController.currentItemSlot];
            if (sourceGrabbableEmoteProp != null && sourceGrabbableEmoteProp != heldObject)
                return true;

            return false;
        }


        public override bool IsPerformingCustomEmote()
        {
            return base.IsPerformingCustomEmote();
        }


        public bool TryPerformingEmoteLocal(UnlockableEmote emote, int overrideEmoteId = -1, GrabbablePropObject sourcePropObject = null)
        {
            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return false;

            if (!isLocalPlayer)
            {
                LogWarning("Cannot run TryPerformEmoteLocal on a character who does not belong to the local player. This is not allowed.");
                return false;
            }

            Log("Attempting to emote for player: " + playerController.name);

            if (!CanPerformEmote())
                return false;

            if (overrideEmoteId >= 0 && (emote.emoteSyncGroup == null || emote.emoteSyncGroup.Count <= 1 || overrideEmoteId < 0 || overrideEmoteId >= emote.emoteSyncGroup.Count))
                overrideEmoteId = -1;

            if (emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 1)
            {
                if (emote.randomEmote)
                {
                    if (overrideEmoteId < 0)
                        overrideEmoteId = UnityEngine.Random.Range(0, emote.emoteSyncGroup.Count);
                }
                else
                    emote = emote.emoteSyncGroup[0];
            }

            if (overrideEmoteId >= 0 && emote.emoteSyncGroup != null && overrideEmoteId < emote.emoteSyncGroup.Count)
                emote = emote.emoteSyncGroup[overrideEmoteId];
            else
                overrideEmoteId = -1;

            EmoteController syncWithEmoteController = null;

            // Already performing emote, and we perform the same emote
            if (isPerformingEmote && performingEmote.IsEmoteInEmoteGroup(emote))
            {
                // Perform the next emote in the sync group
                if (performingEmote.emoteSyncGroup != null && performingEmote.emoteSyncGroup.Count > 1)
                {
                    overrideEmoteId = (performingEmote.emoteSyncGroup.IndexOf(performingEmote) + 1) % performingEmote.emoteSyncGroup.Count;
                    if (performingEmote.emoteSyncGroup[overrideEmoteId] != null)
                        emote = performingEmote.emoteSyncGroup[overrideEmoteId];
                }
                // Perform emote at same time as before
                if (isPerformingEmote && emoteSyncGroup?.syncGroup != null && emoteSyncGroup.syncGroup.Count > 1)
                {
                    foreach (var emoteController in emoteSyncGroup.syncGroup)
                    {
                        if (emoteController != this)
                        {
                            syncWithEmoteController = emoteController;
                            break;
                        }
                    }
                }
            }

            bool success;
            if (syncWithEmoteController != null)
                success = SyncWithEmoteController(syncWithEmoteController, overrideEmoteId);
            else
            {
                if (sourcePropObject != null && sourcePropObject == localPlayerController.ItemSlots[localPlayerController.currentItemSlot])
                    success = PerformEmote(emote, sourcePropObject, overrideEmoteId, AudioManager.emoteOnlyMode);
                else
                    success = PerformEmote(emote, overrideEmoteId, AudioManager.emoteOnlyMode);
            }

            playerController.StartPerformingEmoteServerRpc();
            SyncPerformingEmoteManager.SendPerformingEmoteUpdateToServer(emote, AudioManager.emoteOnlyMode);
            timeSinceStartingEmote = 0;
            playerController.performingEmote = true;
            originalAnimator.SetInteger("emoteNumber", 1);

            return success;
        }


        public void TrySyncingEmoteWithEmoteController(EmoteController emoteController)
        {
            if (!initialized || emoteController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return;

            if (!isLocalPlayer)
            {
                LogWarning("Cannot run TrySyncingEmoteWithEmoteController on a character who does not belong to the local player. This is not allowed.");
                return;
            }

            Log("Attempting to sync emote for player: " + playerController.name + " with emote controller with id: " + emoteController.emoteControllerId);

            if (!CanPerformEmote() || !emoteController.IsPerformingCustomEmote())
                return;

            /*
            short overrideEmoteId = -1;
            if (emoteController.emoteSyncGroup != null)
            {
                emoteController.emoteSyncGroup.AddToEmoteSyncGroup(this);
                emoteSyncGroup = emoteController.emoteSyncGroup;
                if (!emoteController.performingEmote.randomEmote)
                {
                    overrideEmoteId = (short)emoteSyncGroup.syncGroup.IndexOf(this);
                    if (overrideEmoteId != -1)
                        overrideEmoteId %= (short)emoteController.performingEmote.emoteSyncGroup.Count;
                }
            }
            */
            SyncWithEmoteController(emoteController);
            if (performingEmote != null)
            {
                short overrideEmoteId = -1;
                if (performingEmote.inEmoteSyncGroup)
                    overrideEmoteId = (short)performingEmote.emoteSyncGroup.IndexOf(performingEmote);

                playerController.StartPerformingEmoteServerRpc();
                SyncPerformingEmoteManager.SendSyncEmoteUpdateToServer(emoteController, overrideEmoteId);
                timeSinceStartingEmote = 0;
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 1);
            }
        }


        public override bool CanPerformEmote()
        {
            if (!isLocalPlayer)
                return true;

            if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
                return false;

            bool canPerformEmote = base.CanPerformEmote();

            MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.NonPublic | BindingFlags.Instance);
            canPerformEmote &= (bool)method.Invoke(playerController, new object[] { });

            bool otherConditions = playerController.inAnimationWithEnemy == null && !(isLocalPlayer && CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
            return canPerformEmote && otherConditions;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPostfix]
        private static void OnSwapItem(int slot, PlayerControllerB __instance)
        {
            if (allPlayerEmoteControllers.TryGetValue(__instance, out var emoteController) && emoteController.IsPerformingCustomEmote())
            {
                var heldObject = __instance.ItemSlots[slot];
                if (emoteController.sourceGrabbableEmoteProp != null && emoteController.sourceGrabbableEmoteProp != heldObject)
                    emoteController.StopPerformingEmote();
                else if (heldObject != null && heldObject is GrabbablePropObject)
                    heldObject.EnableItemMeshes(false);
            }
        }


        public bool PerformEmote(UnlockableEmote emote, GrabbablePropObject sourcePropObject, int overrideEmoteId = -1, bool doNotTriggerAudio = false)
        {
            if (sourcePropObject != null && sourcePropObject == playerController.ItemSlots[playerController.currentItemSlot])
                sourceGrabbableEmoteProp = sourcePropObject;

            bool success = PerformEmote(emote, overrideEmoteId, doNotTriggerAudio);
            if (!isPerformingEmote)
                sourceGrabbableEmoteProp = null;
            return success;
        }


        public override bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1, bool doNotTriggerAudio = false)
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
                return false;

            LogWarning("222 OverrideEmoteId: " + overrideEmoteId + " EmoteName: " + emote.emoteName);

            bool success = base.PerformEmote(emote, overrideEmoteId, doNotTriggerAudio);
            if (isPerformingEmote)
            {
                cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);
                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 0);

                var heldProp = playerController.ItemSlots[playerController.currentItemSlot] as GrabbablePropObject;
                if (heldProp)
                    heldProp.EnableItemMeshes(false);

                if (isLocalPlayer)
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
            }
            return success;
        }


        public override bool SyncWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
        {
            if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
                return false;

            bool success = base.SyncWithEmoteController(emoteController, overrideEmoteId);
            if (isPerformingEmote)
            {
                cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);

                playerController.performingEmote = true;
                originalAnimator.SetInteger("emoteNumber", 0);
                if (isLocalPlayer)
                {
                    ThirdPersonEmoteController.OnStartCustomEmoteLocal();
                    playerController.StartPerformingEmoteServerRpc();
                }
            }
            return success;
        }


        public override void StopPerformingEmote()
        {
            if (playerController == null || (isLocalPlayer && ConfigSettings.disableEmotesForSelf.Value))
                return;

            base.StopPerformingEmote();
            cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);

            var heldProp = playerController.ItemSlots[playerController.currentItemSlot] as GrabbablePropObject;
            if (heldProp)
                heldProp.EnableItemMeshes(true);

            playerController.playerBodyAnimator.SetInteger("emote_number", 0);
            playerController.performingEmote = false;
            if (isLocalPlayer)
            {
                playerController.StopPerformingEmoteServerRpc();
                StartCoroutine(StopEmoteCameraEndOfFrame());
            }
        }


        /// <summary>
        /// Stops emoting, and switches camera back to the player's view immediately.
        /// </summary>
        public void StopPerformingEmoteImmediately()
        {
            if (playerController == null || (isLocalPlayer && ConfigSettings.disableEmotesForSelf.Value))
                return;

            base.StopPerformingEmote();
            cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);

            var heldProp = playerController.ItemSlots[playerController.currentItemSlot] as GrabbablePropObject;
            if (heldProp)
                heldProp.EnableItemMeshes(true);

            playerController.playerBodyAnimator.SetInteger("emote_number", 0);
            playerController.performingEmote = false;

            if (isLocalPlayer)
            {
                playerController.StopPerformingEmoteServerRpc();
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
            }
        }


        IEnumerator StopEmoteCameraEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!isPerformingEmote)
                ThirdPersonEmoteController.OnStopCustomEmoteLocal();
        }


        protected override void CreateBoneMap()
        {
            boneMap = BoneMapper.CreateBoneMap(humanoidSkeleton, metarig, sourceBoneNames);
            var localArmBoneNames = new List<string>
            {
                "arm.L_upper", "arm.L_lower", "hand.L", "finger1.L", "finger1.L.001", "finger2.L", "finger2.L.001", "finger3.L", "finger3.L.001", "finger4.L", "finger4.L.001", "finger5.L", "finger5.L.001",
                "arm.R_upper", "arm.R_lower", "hand.R", "finger1.R", "finger1.R.001", "finger2.R", "finger2.R.001", "finger3.R", "finger3.R.001", "finger4.R", "finger4.R.001", "finger5.R", "finger5.R.001"
            };
            boneMapLocalPlayerArms = BoneMapper.CreateBoneMap(humanoidSkeleton, playerController.localArmsTransform, localArmBoneNames);
        }


        protected override ulong GetEmoteControllerId() => playerController != null ? playerController.NetworkObjectId : 0;
        protected override string GetEmoteControllerName() => playerController != null ? playerController.playerUsername : base.GetEmoteControllerName();
    }
}