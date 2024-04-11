using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.Props
{
    public class PropObject : MonoBehaviour
    {
        bool initialized = false;
        public bool active { get { return gameObject.activeSelf; } set { gameObject.SetActive(value); } }
        public Animator animator;
        public RuntimeAnimatorController animatorController { get { return animator != null ? animator.runtimeAnimatorController : null; } }


        private void Awake()
        {
            if (!initialized)
                InitializeEmoteProp();
            foreach (var collider in GetComponentsInChildren<Collider>())
                GameObject.Destroy(collider);
        }


        public void InitializeEmoteProp()
        {
            if (initialized)
                return;
            animator = GetComponentInChildren<Animator>();
            initialized = true;
        }


        public void SyncWithEmoteController(EmoteController emoteController)
        {
            if (animator != null)
            {
                animator.enabled = true;
                animator.SetBool("loop", emoteController.isLooping);
                animator.Play(emoteController.currentStateHash, 0, emoteController.currentAnimationTimeNormalized % 1);
            }
        }


        public void SetPropLayer(int layer)
        {
            SetPropLayerRecursive(gameObject, layer);
        }


        private void SetPropLayerRecursive(GameObject obj, int layer)
        {
            if (obj.layer != 22 && obj.layer != 23)
                obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
                SetPropLayerRecursive(obj.transform.GetChild(i).gameObject, layer);
        }
    }
}
