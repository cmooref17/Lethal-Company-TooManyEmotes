using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BepInEx;
using HarmonyLib;
using TooManyEmotes.Config;
using System.IO;
using UnityEngine;

namespace TooManyEmotes
{
    [BepInPlugin("FlipMods.TooManyEmotes", "TooManyEmotes", "1.3.2")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;

        public static RuntimeAnimatorController customAnimationController;

        public static HashSet<AnimationClip> customAnimationClips;
        public static Dictionary<string, AnimationClip> customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

        public static GameObject radialMenuPrefab;
        public static RuntimeAnimatorController previewAnimatorController;

        private void Awake()
        {
            instance = this;
            ConfigSettings.BindConfigSettings();
            this._harmony = new Harmony("TooManyEmotes");
            this._harmony.PatchAll();
            base.Logger.LogInfo("TooManyEmotes loaded");

            LoadAssetBundles();
            LoadRadialMenuAsset();
        }

        static void LoadAssetBundles() {

            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "custom_emotes");
                Log("Loading custom emote asset bundle at path: " + assetsPath);

                AssetBundle emotesAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                customAnimationClips = new HashSet<AnimationClip>(emotesAssetBundle.LoadAllAssets<AnimationClip>());
                foreach (var animationClip in customAnimationClips)
                    if (animationClip.name.EndsWith("_loop"))
                        customAnimationClipsLoopDict.Add(animationClip.name, animationClip);
                foreach (var animationClipLoop in customAnimationClipsLoopDict.Values)
                    customAnimationClips.Remove(animationClipLoop);
                Log(string.Format("Successfully loaded {0} animation clips.", customAnimationClips.Count));
            }
            catch
            {
                LogError("Failed to load emotes asset bundle. You should probably call the cops.");
            }
        }


        public static void LoadRadialMenuAsset() {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "radial_menu");
                AssetBundle assetBundle = AssetBundle.LoadFromFile(assetsPath);
                radialMenuPrefab = assetBundle.LoadAsset<GameObject>("RadialMenu");
                previewAnimatorController = assetBundle.LoadAsset<RuntimeAnimatorController>("PreviewAnimatorController");
                Log("Successfully loaded radial menu asset.");
            }
            catch
            {
                LogError("Failed to load radial menu asset.");
            }
        }

        public static void Log(string message) => instance.Logger.LogInfo(message);
        public static void LogError(string message) => instance.Logger.LogError(message);
    }
}
