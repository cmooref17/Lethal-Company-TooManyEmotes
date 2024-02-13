using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BepInEx;
using HarmonyLib;
using System.IO;
using UnityEngine;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using BepInEx.Logging;
using System.Reflection;
using TooManyEmotes.Audio;

namespace TooManyEmotes
{
    [BepInPlugin("FlipMods.TooManyEmotes", "TooManyEmotes", "1.9.0")]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.swipez.melonloader.morecompany", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;
        static ManualLogSource logger;

        public static List<AnimationClip> customAnimationClips;
        public static HashSet<AnimationClip> customAnimationClipsHash;
        public static Dictionary<string, AnimationClip> customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

        public static List<AnimationClip> complementaryAnimationClips;
        public static List<AnimationClip> animationClipsTier0;
        public static List<AnimationClip> animationClipsTier1;
        public static List<AnimationClip> animationClipsTier2;
        public static List<AnimationClip> animationClipsTier3;

        public static AnimationClip idleClip;

        public static GameObject radialMenuPrefab;
        public static RuntimeAnimatorController previewAnimatorController;

        public static RuntimeAnimatorController humanoidAnimatorController;
        public static Avatar humanoidAvatar;
        public static GameObject humanoidSkeletonPrefab;

        //public static Dictionary<string, AnimationClip> miscAnimationClips;
        public static Dictionary<string, AudioClip> musicClips;
        public static Dictionary<string, GameObject> emotePropPrefabs;


        void Awake()
        {
            instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();
            Keybinds.InitKeybinds();

            LoadEmoteAssets();
            LoadMiscAnimationAssets();
            LoadRadialMenuAsset();
            //LoadEmotePropAssets();
            //AudioManager.LoadAudioAssets();

            this._harmony = new Harmony("TooManyEmotes");
            PatchAll();
            Log("TooManyEmotes loaded");
        }


        static void LoadEmoteAssets()
        {
            customAnimationClips = new List<AnimationClip>();
            customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

            complementaryAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_complementary"));
            animationClipsTier0 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_0"));
            animationClipsTier1 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_1"));
            animationClipsTier2 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_2"));
            animationClipsTier3 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_3"));

            /*
            var miscClips = LoadEmoteAssetBundle("Assets/emotes_misc");
            if (miscClips != null && miscClips.Length >= 1)
            {
                foreach (var clip in miscClips)
                {
                    if (clip.name.Contains("idle"))
                        idleClip = clip;
                }
            }
            */

            customAnimationClips.AddRange(complementaryAnimationClips);
            customAnimationClips.AddRange(animationClipsTier0);
            customAnimationClips.AddRange(animationClipsTier1);
            customAnimationClips.AddRange(animationClipsTier2);
            customAnimationClips.AddRange(animationClipsTier3);

            foreach (var clip in customAnimationClips)
            {
                if (clip.name.StartsWith("fn_")) clip.name = clip.name.Replace("fn_", "");
                if (clip.name.EndsWith("_loop")) customAnimationClipsLoopDict.Add(clip.name, clip);
            }

            foreach (var animationClipLoop in customAnimationClipsLoopDict.Values)
                customAnimationClips.Remove(animationClipLoop);

            customAnimationClipsHash = new HashSet<AnimationClip>(customAnimationClips);
            customAnimationClipsHash.UnionWith(customAnimationClipsLoopDict.Values);
        }


        static AnimationClip[] LoadEmoteAssetBundle(string assetBundleName)
        {
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
                LogError("Failed to load emotes Asset Bundle.");
                return new AnimationClip[0];
            }
        }


        static void LoadMiscAnimationAssets()
        {
            try
            {
                string miscAssetBundlePath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "Assets/misc");
                AssetBundle miscAssetBundle = AssetBundle.LoadFromFile(miscAssetBundlePath);
                humanoidAnimatorController = miscAssetBundle.LoadAsset<RuntimeAnimatorController>("humanoid_animator_controller");
                humanoidAvatar = miscAssetBundle.LoadAsset<Avatar>("humanoid_avatar");
                humanoidSkeletonPrefab = miscAssetBundle.LoadAsset<GameObject>("humanoid_skeleton");

                Animator animator = humanoidSkeletonPrefab.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = humanoidSkeletonPrefab.AddComponent<Animator>();


                if (humanoidAnimatorController == null)
                    LogError("Failed to load humanoid animator controller from asset bundle: misc");
                if (humanoidAvatar == null)
                    LogError("Failed to load humanoid avatar from asset bundle: misc");
                if (humanoidSkeletonPrefab == null)
                    LogError("Failed to load humanoid skeleton prefab from asset bundle: misc");
            }
            catch
            {
                LogError("Failed to load misc Asset Bundle.");
            }
        }


        static void LoadEmotePropAssets()
        {
            try
            {
                string propsAssetBundlePath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "Assets/props");
                AssetBundle prefabAssetBundle = AssetBundle.LoadFromFile(propsAssetBundlePath);
                var prefabs = prefabAssetBundle.LoadAllAssets<GameObject>();
                foreach (var prefab in prefabs)
                    emotePropPrefabs.Add(prefab.name, prefab);
            }
            catch
            {
                LogError("Failed to load emote props Asset Bundle.");
            }
        }


        public static void LoadRadialMenuAsset()
        {
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


        void PatchAll()
        {
            IEnumerable<Type> types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null);
            }
            foreach (var type in types)
                this._harmony.PatchAll(type);
        }

        void CreateCustomLogger()
        {
            try { logger = BepInEx.Logging.Logger.CreateLogSource(string.Format("{0}-{1}", Info.Metadata.Name, Info.Metadata.Version)); }
            catch { logger = Logger; }
        }

        public static void Log(string message) => logger.LogInfo(message);
        public static void LogError(string message) => logger.LogError(message);
        public static void LogWarning(string message) => logger.LogWarning(message);

        public static bool IsModLoaded(string guid) => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
    }
}
