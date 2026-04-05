using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using TooManyEmotes.Audio;
using TooManyEmotes.Compatibility;
using TooManyEmotes.Config;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;
using TooManyEmotes.Props;
using Unity.Netcode;
using UnityEngine;

namespace TooManyEmotes;

[DefaultExecutionOrder(-2)]
public class EmoteControllerPlayer : EmoteController
{
	public static Dictionary<PlayerControllerB, EmoteControllerPlayer> allPlayerEmoteControllers = new Dictionary<PlayerControllerB, EmoteControllerPlayer>();

	public PlayerControllerB playerController;

	public Animator originalAnimator;

	private Dictionary<Transform, Transform> boneMapLocalPlayerArms;

	internal Transform humanoidHead;

	private Transform cameraContainerTarget;

	private Transform cameraContainerLerp;

	public static List<string> sourceBoneNames = new List<string>
	{
		"spine", "spine.001", "spine.002", "spine.003", "spine.004", "shoulder.L", "arm.L_upper", "arm.L_lower", "hand.L", "finger1.L",
		"finger1.L.001", "finger2.L", "finger2.L.001", "finger3.L", "finger3.L.001", "finger4.L", "finger4.L.001", "finger5.L", "finger5.L.001", "shoulder.R",
		"arm.R_upper", "arm.R_lower", "hand.R", "finger1.R", "finger1.R.001", "finger2.R", "finger2.R.001", "finger3.R", "finger3.R.001", "finger4.R",
		"finger4.R.001", "finger5.R", "finger5.R.001", "thigh.L", "shin.L", "foot.L", "heel.02.L", "toe.L", "thigh.R", "shin.R",
		"foot.R", "heel.02.R", "toe.R"
	};

	public GrabbablePropObject sourceGrabbableEmoteProp;

	internal static float timeLastPeformedEmoteLocalPlayer = 0f;

	public static EmoteControllerPlayer emoteControllerLocal => (HelperTools.localPlayerController != null && allPlayerEmoteControllers.ContainsKey(HelperTools.localPlayerController)) ? allPlayerEmoteControllers[HelperTools.localPlayerController] : null;

	public bool isLocalPlayer => playerController == StartOfRound.Instance?.localPlayerController;

	public ulong clientId => playerController.actualClientId;

	public ulong playerId => playerController.playerClientId;

	public ulong steamId => playerController.playerSteamId;

	public string username => playerController.playerUsername;

	public float timeSinceStartingEmote
	{
		get
		{
			return (float)Traverse.Create(playerController).Field("timeSinceStartingEmote").GetValue();
		}
		set
		{
			Traverse.Create(playerController).Field("timeSinceStartingEmote").SetValue(value);
		}
	}

	public override void Initialize(string sourceRootBoneName = "metarig")
	{
		base.Initialize();
		if (initialized)
		{
			originalAnimator = metarig.GetComponentInChildren<Animator>();
			playerController = GetComponentInParent<PlayerControllerB>();
			if (playerController == null)
			{
				CustomLogging.LogError("Failed to find PlayerControllerB component in parent of EmoteControllerPlayer.");
			}
			else
			{
				allPlayerEmoteControllers.Add(playerController, this);
			}
		}
	}

	protected override void Start()
	{
		base.Start();
		if (initialized)
		{
			Transform spine = FindChildRecursive("spine.004", metarig);
			if (spine != null)
			{
				cameraContainerTarget = new GameObject("CameraContainer_Target").transform;
				cameraContainerTarget.SetParent(spine);
				cameraContainerTarget.localPosition = new Vector3(0f, 0.22f, 0f);
				cameraContainerTarget.localEulerAngles = new Vector3(-3f, 0f, 0f);
				cameraContainerLerp = new GameObject("CameraContainer_Lerp").transform;
				cameraContainerLerp.SetParent(humanoidSkeleton);
				cameraContainerLerp.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
			humanoidHead = FindChildRecursive("head", humanoidSkeleton);
			if (humanoidHead == null)
			{
				CustomLogging.LogError("Failed to find Head on: " + base.emoteControllerName);
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		allPlayerEmoteControllers?.Remove(playerController);
	}

	protected override void Update()
	{
		if (initialized && playerController != null && (playerController != HelperTools.localPlayerController || (!ConfigSettings.disableEmotesForSelf.Value && !LCVR_Compat.LoadedAndEnabled)))
		{
			base.Update();
		}
	}

	protected override void LateUpdate()
	{
		if (!initialized || playerController == null || (playerController == HelperTools.localPlayerController && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
		{
			return;
		}
		bool flag = isPerformingEmote;
		base.LateUpdate();
		if (flag && !isPerformingEmote && playerController.performingEmote)
		{
			playerController.performingEmote = false;
			originalAnimator.SetInteger("emoteNumber", 0);
			AnimatorStateInfo currentAnimatorStateInfo = originalAnimator.GetCurrentAnimatorStateInfo(0);
			animator.Play(currentAnimatorStateInfo.fullPathHash, 0, 0f);
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
		{
			return;
		}
		base.TranslateAnimation();
		if (humanoidHead != null && cameraContainerLerp != null && cameraContainerTarget != null)
		{
			cameraContainerLerp.position = Vector3.Lerp(cameraContainerLerp.position, cameraContainerTarget.position, 25f * Time.deltaTime);
			cameraContainerLerp.rotation = Quaternion.Lerp(cameraContainerLerp.rotation, cameraContainerTarget.rotation, 25f * Time.deltaTime);
			if (!isLocalPlayer || !ThirdPersonEmoteController.firstPersonEmotesEnabled || !ThirdPersonEmoteController.isMovingWhileEmoting)
			{
				playerController.cameraContainerTransform.position = cameraContainerLerp.position;
				playerController.cameraContainerTransform.rotation = cameraContainerLerp.rotation;
			}
			if (isLocalPlayer)
			{
				playerController.localVisor.position = playerController.localVisorTargetPoint.position;
				playerController.localVisor.rotation = playerController.localVisorTargetPoint.rotation;
			}
		}
		if (!isLocalPlayer)
		{
			return;
		}
		playerController.playerModelArmsMetarig.rotation = playerController.localArmsRotationTarget.rotation;
		if (boneMapLocalPlayerArms == null)
		{
			return;
		}
		foreach (KeyValuePair<Transform, Transform> boneMapLocalPlayerArm in boneMapLocalPlayerArms)
		{
			Transform key = boneMapLocalPlayerArm.Key;
			Transform value = boneMapLocalPlayerArm.Value;
			if (key != null && value != null)
			{
				value.transform.position = key.transform.position;
				value.transform.rotation = key.transform.rotation;
			}
		}
	}

	protected override bool CheckIfShouldStopEmoting()
	{
		if (playerController == null || !isPerformingEmote)
		{
			return false;
		}
		if (base.CheckIfShouldStopEmoting() || !playerController.performingEmote || performingEmote == null)
		{
			return true;
		}
		if (sourceGrabbableEmoteProp != null && !playerController.HasHeldGrabbable(sourceGrabbableEmoteProp))
		{
			return true;
		}
		return false;
	}

	public override bool IsPerformingCustomEmote()
	{
		return base.IsPerformingCustomEmote();
	}

	public bool TryPerformingEmoteLocal(UnlockableEmote emote, int overrideEmoteId = -1, GrabbablePropObject sourcePropObject = null)
	{
		if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return false;
		}
		if (!isLocalPlayer)
		{
			CustomLogging.LogWarning("Cannot run TryPerformEmoteLocal on a character who does not belong to the local player. This is not allowed.");
			return false;
		}
		if (Time.time - timeLastPeformedEmoteLocalPlayer < 0.25f)
		{
			return false;
		}
		CustomLogging.Log("Attempting to perform emote on local player.");
		if (!CanPerformEmote())
		{
			return false;
		}
		if (overrideEmoteId >= 0 && (emote.emoteSyncGroup == null || emote.emoteSyncGroup.Count <= 1 || overrideEmoteId < 0 || overrideEmoteId >= emote.emoteSyncGroup.Count))
		{
			overrideEmoteId = -1;
		}
		if (emote.emoteSyncGroup != null && emote.emoteSyncGroup.Count > 1)
		{
			if (emote.randomEmote)
			{
				if (overrideEmoteId < 0)
				{
					overrideEmoteId = Random.Range(0, emote.emoteSyncGroup.Count);
				}
			}
			else
			{
				emote = emote.emoteSyncGroup[0];
			}
		}
		if (overrideEmoteId >= 0 && emote.emoteSyncGroup != null && overrideEmoteId < emote.emoteSyncGroup.Count)
		{
			emote = emote.emoteSyncGroup[overrideEmoteId];
		}
		else
		{
			overrideEmoteId = -1;
		}
		EmoteController emoteController = null;
		if (isPerformingEmote && performingEmote.IsEmoteInEmoteGroup(emote) && (!performingEmote.randomEmote || performingEmote.loopable))
		{
			if (performingEmote.emoteSyncGroup != null && performingEmote.emoteSyncGroup.Count > 1)
			{
				overrideEmoteId = (performingEmote.emoteSyncGroup.IndexOf(performingEmote) + 1) % performingEmote.emoteSyncGroup.Count;
				if (performingEmote.emoteSyncGroup[overrideEmoteId] != null)
				{
					emote = performingEmote.emoteSyncGroup[overrideEmoteId];
				}
			}
			if (emoteSyncGroup?.syncGroup != null && emoteSyncGroup.syncGroup.Count > 1)
			{
				if (emoteSyncGroup.syncGroup.Count > 1 && (performingEmote?.emoteSyncGroup == null || performingEmote.emoteSyncGroup.Count <= 1))
				{
					timeLastPeformedEmoteLocalPlayer = Time.time;
					return true;
				}
				foreach (EmoteController item in emoteSyncGroup.syncGroup)
				{
					if (item != this)
					{
						emoteController = item;
						break;
					}
				}
			}
		}
		if (emoteController != null)
		{
			return TrySyncingEmoteWithEmoteController(emoteController, overrideEmoteId);
		}
		CustomLogging.LogWarningVerbose("Trying to perform emote on local player. Emote: " + emote.emoteName + " | Emote id: " + emote.emoteId);
		bool result = ((!HelperTools.localPlayerController.HasHeldGrabbable(sourcePropObject)) ? PerformEmote(emote, overrideEmoteId, AudioManager.emoteOnlyMode) : PerformEmote(emote, sourcePropObject, overrideEmoteId, AudioManager.emoteOnlyMode));
		playerController.StartPerformingEmoteServerRpc();
		SyncPerformingEmoteManager.SendPerformingEmoteUpdateToServer(emote, AudioManager.emoteOnlyMode);
		timeSinceStartingEmote = 0f;
		playerController.performingEmote = true;
		return result;
	}

	public bool TrySyncingEmoteWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
	{
		if (!initialized || emoteController == null || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return false;
		}
		if (!isLocalPlayer)
		{
			CustomLogging.LogWarning("Cannot run TrySyncingEmoteWithEmoteController on a character who does not belong to the local player. This is not allowed.");
			return false;
		}
		if (Time.time - timeLastPeformedEmoteLocalPlayer < 0.25f)
		{
			return false;
		}
		CustomLogging.Log("Attempting to sync emote for player: " + playerController.name + " with emote controller with id: " + emoteController.emoteControllerId);
		if (!CanPerformEmote() || !emoteController.IsPerformingCustomEmote())
		{
			return false;
		}
		if (overrideEmoteId >= 0 && (emoteController.performingEmote?.emoteSyncGroup == null || overrideEmoteId >= emoteController.performingEmote.emoteSyncGroup.Count || emoteController.performingEmote.emoteSyncGroup[overrideEmoteId] == null))
		{
			overrideEmoteId = -1;
		}
		SyncWithEmoteController(emoteController, overrideEmoteId);
		if (performingEmote != null)
		{
			if (performingEmote.inEmoteSyncGroup)
			{
				overrideEmoteId = performingEmote.emoteSyncGroup.IndexOf(performingEmote);
			}
			playerController.StartPerformingEmoteServerRpc();
			SyncPerformingEmoteManager.SendSyncEmoteUpdateToServer(emoteController, overrideEmoteId);
			timeSinceStartingEmote = 0f;
			playerController.performingEmote = true;
			timeLastPeformedEmoteLocalPlayer = Time.time;
			return true;
		}
		return false;
	}

	public override bool CanPerformEmote()
	{
		if (!isLocalPlayer)
		{
			return true;
		}
		if (!initialized || ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)
		{
			return false;
		}
		bool flag = base.CanPerformEmote();
		MethodInfo method = playerController.GetType().GetMethod("CheckConditionsForEmote", BindingFlags.Instance | BindingFlags.NonPublic);
		flag &= (bool)method.Invoke(playerController, new object[0]);
		bool flag2 = playerController.inAnimationWithEnemy == null && (!isLocalPlayer || !CentipedePatcher.IsCentipedeLatchedOntoLocalPlayer());
		return flag && flag2;
	}

	[HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
	[HarmonyPostfix]
	private static void OnSwapItem(int slot, PlayerControllerB __instance)
	{
		if (allPlayerEmoteControllers.TryGetValue(__instance, out var value) && value.IsPerformingCustomEmote())
		{
			HeldItemResult heldItem = __instance.ResolveHeldItem();
			if (value.sourceGrabbableEmoteProp != null && !__instance.HasHeldGrabbable(value.sourceGrabbableEmoteProp))
			{
				value.StopPerformingEmote();
			}
			else if (heldItem.IsValid && heldItem.HasItem && value.emotingProps.Count > 0)
			{
				heldItem.Item.EnableItemMeshes(false);
			}
		}
	}

	public bool PerformEmote(UnlockableEmote emote, GrabbablePropObject sourcePropObject, int overrideEmoteId = -1, bool doNotTriggerAudio = false)
	{
		if (playerController.HasHeldGrabbable(sourcePropObject))
		{
			sourceGrabbableEmoteProp = sourcePropObject;
		}
		bool result = PerformEmote(emote, overrideEmoteId, doNotTriggerAudio);
		if (isPerformingEmote)
		{
			if (!isLocalPlayer && SyncManager.isSynced && ConfigSync.instance.syncPersistentUnlocksGlobal && !SessionManager.unlockedEmotesByPlayer.TryGetValue(playerController.playerUsername, out var value) && !value.Contains(performingEmote))
			{
				SessionManager.UnlockEmoteLocal(emote.emoteId, purchased: false, playerController.playerUsername);
			}
		}
		else
		{
			StopPerformingEmote();
		}
		return result;
	}

	public override bool PerformEmote(UnlockableEmote emote, int overrideEmoteId = -1, bool doNotTriggerAudio = false)
	{
		if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
		{
			return false;
		}
		bool result = base.PerformEmote(emote, overrideEmoteId, doNotTriggerAudio);
		if (isPerformingEmote)
		{
			cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);
			playerController.performingEmote = true;
			originalAnimator.SetInteger("emoteNumber", 1);
			GrabbableObject heldItem = playerController.GetHeldGrabbableSafe();
			if (heldItem != null && emotingProps.Count > 0)
			{
				heldItem.EnableItemMeshes(false);
			}
			if (isLocalPlayer)
			{
				ThirdPersonEmoteController.OnStartCustomEmoteLocal();
				playerController.StartPerformingEmoteServerRpc();
			}
		}
		return result;
	}

	public override bool SyncWithEmoteController(EmoteController emoteController, int overrideEmoteId = -1)
	{
		if (playerController == null || (isLocalPlayer && (ConfigSettings.disableEmotesForSelf.Value || LCVR_Compat.LoadedAndEnabled)))
		{
			return false;
		}
		bool result = base.SyncWithEmoteController(emoteController, overrideEmoteId);
		if (isPerformingEmote)
		{
			cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);
			playerController.performingEmote = true;
			originalAnimator.SetInteger("emoteNumber", 1);
			if (isLocalPlayer)
			{
				ThirdPersonEmoteController.OnStartCustomEmoteLocal();
				playerController.StartPerformingEmoteServerRpc();
			}
		}
		return result;
	}

	public override void StopPerformingEmote()
	{
		if (playerController == null || (isLocalPlayer && ConfigSettings.disableEmotesForSelf.Value))
		{
			return;
		}
		base.StopPerformingEmote();
		cameraContainerLerp.SetPositionAndRotation(cameraContainerTarget.position, cameraContainerTarget.rotation);
		GrabbableObject heldItem = playerController.GetHeldGrabbableSafe();
		if (heldItem != null)
		{
			heldItem.EnableItemMeshes(true);
		}
		if (sourceGrabbableEmoteProp != null)
		{
			if (sourceGrabbableEmoteProp.isPerformingEmote)
			{
				sourceGrabbableEmoteProp.StopEmote();
			}
			sourceGrabbableEmoteProp = null;
		}
		playerController.playerBodyAnimator.SetInteger("emote_number", 0);
		playerController.performingEmote = false;
		playerController.playerBodyAnimator.Update(0f);
		if (isLocalPlayer)
		{
			ThirdPersonEmoteController.OnStopCustomEmoteLocal();
			playerController.gameplayCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			playerController.StopPerformingEmoteServerRpc();
		}
	}

	public override void ResetPerformingEmote()
	{
		if (playerController == null || (isLocalPlayer && ConfigSettings.disableEmotesForSelf.Value))
		{
			return;
		}
		base.ResetPerformingEmote();
		if (sourceGrabbableEmoteProp != null)
		{
			if (sourceGrabbableEmoteProp.isPerformingEmote)
			{
				sourceGrabbableEmoteProp.StopEmote();
			}
			sourceGrabbableEmoteProp = null;
		}
		GrabbableObject heldItem = playerController.GetHeldGrabbableSafe();
		if (heldItem != null)
		{
			heldItem.EnableItemMeshes(true);
		}
	}

	protected override void CreateBoneMap()
	{
		boneMap = BoneMapper.CreateBoneMap(humanoidSkeleton, metarig, sourceBoneNames);
		List<string> list = new List<string>
		{
			"arm.L_upper", "arm.L_lower", "hand.L", "finger1.L", "finger1.L.001", "finger2.L", "finger2.L.001", "finger3.L", "finger3.L.001", "finger4.L",
			"finger4.L.001", "finger5.L", "finger5.L.001", "arm.R_upper", "arm.R_lower", "hand.R", "finger1.R", "finger1.R.001", "finger2.R", "finger2.R.001",
			"finger3.R", "finger3.R.001", "finger4.R", "finger4.R.001", "finger5.R", "finger5.R.001"
		};
		boneMapLocalPlayerArms = BoneMapper.CreateBoneMap(humanoidSkeleton, playerController.localArmsTransform, list);
	}

	protected override ulong GetEmoteControllerId()
	{
		return (playerController != null) ? ((NetworkBehaviour)playerController).NetworkObjectId : 0;
	}

	protected override string GetEmoteControllerName()
	{
		return (playerController != null) ? playerController.playerUsername : base.GetEmoteControllerName();
	}
}
