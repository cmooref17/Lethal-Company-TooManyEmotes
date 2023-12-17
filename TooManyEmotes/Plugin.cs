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
    [BepInPlugin("FlipMods.TooManyEmotes", "TooManyEmotes", "1.3.8")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;

        public static List<AnimationClip> complementaryAnimationClips;
        public static List<AnimationClip> customAnimationClips;
        public static Dictionary<string, AnimationClip> customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

        public static GameObject radialMenuPrefab;
        public static RuntimeAnimatorController previewAnimatorController;

        private void Awake()
        {
            instance = this;
            ConfigSettings.BindConfigSettings();
            
            //Path.Combine(Path.GetDirectoryName(Info.Location), "Assets", "")

            complementaryAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_complementary"));
            customAnimationClips = new List<AnimationClip>(complementaryAnimationClips);
            customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();
            customAnimationClips.AddRange(LoadEmoteAssetBundle("Assets/emotes_common"));
            customAnimationClips.AddRange(LoadEmoteAssetBundle("Assets/emotes_dance"));
            customAnimationClips.AddRange(LoadEmoteAssetBundle("Assets/emotes_fortnite"));

            foreach (var clip in customAnimationClips)
            {
                if (clip.name.StartsWith("fn_")) clip.name = clip.name.Replace("fn_", "");
                if (clip.name.EndsWith("_loop")) customAnimationClipsLoopDict.Add(clip.name, clip);
            }

            foreach (var animationClipLoop in customAnimationClipsLoopDict.Values)
                customAnimationClips.Remove(animationClipLoop);

            LoadRadialMenuAsset();

            this._harmony = new Harmony("TooManyEmotes");
            this._harmony.PatchAll();
            base.Logger.LogInfo("TooManyEmotes loaded");
        }

        static AnimationClip[] LoadEmoteAssetBundle(string assetBundleName) {

            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), assetBundleName);
                AssetBundle emotesAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                var animationClips = emotesAssetBundle.LoadAllAssets<AnimationClip>();
                Log(string.Format("Successfully loaded {0} animation clips from asset bundle: {1}", animationClips.Length, assetBundleName));
                return animationClips;
            }
            catch
            {
                LogError("Failed to load emotes asset bundle: " + assetBundleName + ".");
                return new AnimationClip[0];
            }
        }


        public static void LoadRadialMenuAsset() {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "Assets/radial_menu");
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
