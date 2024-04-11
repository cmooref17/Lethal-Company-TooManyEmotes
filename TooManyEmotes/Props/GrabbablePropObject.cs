using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Props
{
    public class GrabbablePropObject : PhysicsProp
    {
        public EmotePropData emotePropData;
        public UnlockableEmote emote;

        public EmoteControllerPlayer heldByPlayerEmoteController { get { return playerHeldBy != null && EmoteControllerPlayer.allPlayerEmoteControllers.TryGetValue(playerHeldBy, out var emoteController) ? emoteController : null; } }

        public ScanNodeProperties scanNodeProperties;
        public AudioSource sfxAudioSource;


        public void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.enabled = false;
            if (emotePropData == null)
            {
                if (itemProperties.spawnPrefab == null)
                {
                    LogError("Failed to initialize grabbable emote prop object. Prefab reference is missing!");
                    return;
                }
                if (!EmotePropManager.emotePropsDataDict.TryGetValue(itemProperties.spawnPrefab.name, out emotePropData))
                {
                    LogError("Failed to initialize grabbable emote prop object. Failed to find EmotePropData for object: " + itemProperties.spawnPrefab.name);
                    return;
                }
            }

            if (emotePropData.parentEmotes != null && emotePropData.parentEmotes.Count > 0)
            {
                if (emote == null)
                    emote = emotePropData.parentEmotes[0];
            }
            else
                LogError("Failed to assign emote to grabbable emote prop: " + name + ". Emote is null.");

            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = false;
        }


        public override void Start()
        {
            base.Start();
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
                GameObject.DestroyImmediate(animator);
            StopEmote();
        }


        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!buttonDown)
                return;

            if (emote == null)
            {
                LogWarning("Failed to interact with prop: " + itemProperties.itemName + ". Emote is null!");
                return;
            }

            if (heldByPlayerEmoteController == null)
            {
                LogWarning("Failed to interact with prop: " + itemProperties.itemName + ". Parent player emote controller is null!");
                return;
            }

            if (heldByPlayerEmoteController.IsPerformingCustomEmote())
                StopEmote();
            else
                PerformEmote();
        }


        public override void Update()
        {
            base.Update();
        }


        private void PerformEmote()
        {
            if (emote == null || heldByPlayerEmoteController == null) // !propAnimator
                return;

            if (!heldByPlayerEmoteController.IsPerformingCustomEmote())
            {
                heldByPlayerEmoteController.TryPerformingEmoteLocal(emote, sourcePropObject:this);
                if (heldByPlayerEmoteController.IsPerformingCustomEmote())
                {
                    foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
                        renderer.updateWhenOffscreen = true;
                }
            }
        }


        private void StopEmote()
        {
            if (heldByPlayerEmoteController)
            {
                if (heldByPlayerEmoteController.IsPerformingCustomEmote())
                    heldByPlayerEmoteController.StopPerformingEmote();
                parentObject = heldByPlayerEmoteController.isLocalPlayer ? heldByPlayerEmoteController.playerController.localItemHolder : heldByPlayerEmoteController.playerController.serverItemHolder;
            }

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.updateWhenOffscreen = false;
        }
    }
}