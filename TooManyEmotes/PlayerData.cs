using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace TooManyEmotes
{
    public class PlayerData
    {
        public PlayerControllerB playerController;
        public bool isLocalPlayer { get { return playerController != null && playerController == StartOfRound.Instance?.localPlayerController; } }
        public Animator animator { get { return playerController?.playerBodyAnimator; } }
        public AnimatorOverrideController animatorController { get { if (!(animator?.runtimeAnimatorController is AnimatorOverrideController)) animator.runtimeAnimatorController = new AnimatorOverrideController(animator.runtimeAnimatorController); return (AnimatorOverrideController)animator.runtimeAnimatorController; } }
        public RigBuilder rigBuilder { get { return playerController.GetComponentInChildren<RigBuilder>(); } }
        public UnlockableEmote performingEmote;

        public PlayerData(PlayerControllerB playerController)
        {
            this.playerController = playerController;
        }
    }
}
