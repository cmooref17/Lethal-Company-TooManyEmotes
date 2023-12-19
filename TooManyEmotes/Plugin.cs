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
    [BepInPlugin("FlipMods.TooManyEmotes", "TooManyEmotes", "1.4.4")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;

        public static List<AnimationClip> customAnimationClips;
        public static Dictionary<string, AnimationClip> customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

        public static List<AnimationClip> complementaryAnimationClips;
        public static List<AnimationClip> commonAnimationClips;
        public static List<AnimationClip> uncommonAnimationClips;
        public static List<AnimationClip> rareAnimationClips;
        public static List<AnimationClip> legendaryAnimationClips;

        public static GameObject radialMenuPrefab;
        public static RuntimeAnimatorController previewAnimatorController;

        private void Awake()
        {
            instance = this;
            ConfigSettings.BindConfigSettings();

            //Path.Combine(Path.GetDirectoryName(Info.Location), "Assets", "")

            customAnimationClips = new List<AnimationClip>();
            customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

            complementaryAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_complementary"));
            commonAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_0"));
            uncommonAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_1"));
            rareAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_2"));
            legendaryAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_3"));

            customAnimationClips.AddRange(complementaryAnimationClips);
            customAnimationClips.AddRange(commonAnimationClips);
            customAnimationClips.AddRange(uncommonAnimationClips);
            customAnimationClips.AddRange(rareAnimationClips);
            customAnimationClips.AddRange(legendaryAnimationClips);

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


        public static bool IsModLoaded(string guid)
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
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
        public static void LogWarning(string message) => instance.Logger.LogWarning(message);
        public static void LogError(string message) => instance.Logger.LogError(message);
    }
}
