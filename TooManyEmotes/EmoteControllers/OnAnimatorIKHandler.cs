﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    public class OnAnimatorIKHandler : MonoBehaviour
    {
        EmoteController emoteController;
        Animator animator;
        public float handIKWeight = 0.8f;


        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                LogWarning("OnIKHandler must be attached to a gameobject with an animator component.");
        }


        public void SetParentEmoteController(EmoteController emoteController)
        {
            this.emoteController = emoteController;
        }


        protected void OnAnimatorIK(int layerIndex)
        {
            if (emoteController && emoteController.initialized && emoteController.IsPerformingCustomEmote())
            {
                if (emoteController.ikLeftHand && emoteController.ikLeftHand.localPosition != Vector3.zero)
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handIKWeight);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handIKWeight);
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, emoteController.ikLeftHand.position);
                    animator.SetIKRotation(AvatarIKGoal.LeftHand, emoteController.ikLeftHand.rotation);
                }
                if (emoteController.ikRightHand && emoteController.ikRightHand.localPosition != Vector3.zero)
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight);
                    animator.SetIKPosition(AvatarIKGoal.RightHand, emoteController.ikRightHand.position);
                    animator.SetIKRotation(AvatarIKGoal.RightHand, emoteController.ikRightHand.rotation);
                }
                if (emoteController.ikHead && emoteController.ikHead.localPosition != Vector3.zero)
                {
                    animator.SetLookAtWeight(1, 0.25f, 0.5f);
                    animator.SetLookAtPosition(emoteController.ikHead.position);
                }
            }
        }
    }
}
