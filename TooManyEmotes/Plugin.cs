using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using System.IO;
using UnityEngine;
using TooManyEmotes.Config;
using TooManyEmotes.Input;
using BepInEx.Logging;
using System.Reflection;
using TooManyEmotes.Audio;
using TooManyEmotes.Props;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes
{
    [BepInPlugin("FlipMods.TooManyEmotes", "TooManyEmotes", "2.1.5")]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.swipez.melonloader.morecompany", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;
        public static ManualLogSource defaultLogger { get { return instance.Logger; } }

        public static List<AnimationClip> customAnimationClips;
        public static HashSet<AnimationClip> customAnimationClipsHash;
        public static Dictionary<string, AnimationClip> customAnimationClipsLoopDict;

        public static List<AnimationClip> complementaryAnimationClips;
        public static List<AnimationClip> animationClipsTier0;
        public static List<AnimationClip> animationClipsTier1;
        public static List<AnimationClip> animationClipsTier2;
        public static List<AnimationClip> animationClipsTier3;
        public static List<AnimationClip> animationClipsSpecial;

        public static GameObject radialMenuPrefab;

        public static RuntimeAnimatorController humanoidAnimatorController;
        public static Avatar humanoidAvatar;
        public static GameObject humanoidSkeletonPrefab;


        //public static HashSet<RuntimeAnimatorController> emotePropAnimatorControllers;


        void Awake()
        {
            instance = this;
            InitLogger();
            ConfigSettings.BindConfigSettings();
            Keybinds.InitKeybinds();

            LoadEmoteAssets();
            LoadMiscAnimationAssets();
            LoadRadialMenuAsset();

            AudioManager.LoadAudioAssets();
            EmotePropManager.LoadPropAssets();

            EmotesManager.BuildEmotesList();
            AudioManager.BuildAudioClipList();
            EmotePropManager.BuildEmotePropList();

            AdditionalEmoteData.SetAdditionalEmoteData();
            AdditionalEmoteData.SetAdditionalPropData();
            AdditionalEmoteData.SetAdditionalMusicData();

            this._harmony = new Harmony("TooManyEmotes");
            PatchAll();
            Log("TooManyEmotes finished loading!");
        }


        static void LoadEmoteAssets()
        {
            customAnimationClips = new List<AnimationClip>();
            customAnimationClipsHash = new HashSet<AnimationClip>();
            customAnimationClipsLoopDict = new Dictionary<string, AnimationClip>();

            complementaryAnimationClips = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_complementary"));
            animationClipsTier0 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_0"));
            animationClipsTier1 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_1"));
            animationClipsTier2 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_2"));
            animationClipsTier3 = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_3"));
            animationClipsSpecial = new List<AnimationClip>(LoadEmoteAssetBundle("Assets/emotes_special"));
            complementaryAnimationClips.AddRange(animationClipsSpecial); // for now

            customAnimationClipsHash.UnionWith(complementaryAnimationClips);
            customAnimationClipsHash.UnionWith(animationClipsTier0);
            customAnimationClipsHash.UnionWith(animationClipsTier1);
            customAnimationClipsHash.UnionWith(animationClipsTier2);
            customAnimationClipsHash.UnionWith(animationClipsTier3);

            foreach (var clip in customAnimationClipsHash)
            {
                if (clip.name.EndsWith("_loop"))
                {
                    if (customAnimationClipsLoopDict.ContainsKey(clip.name))
                        LogWarning("Attempted to add duplicate emote in CustomAnimationClipsLoopDict. AnimationClip: " + clip.name);
                    else
                        customAnimationClipsLoopDict.Add(clip.name, clip);
                }

            }

            customAnimationClips = new List<AnimationClip>(customAnimationClipsHash);
            customAnimationClipsHash = new HashSet<AnimationClip>(customAnimationClips);

            foreach (var animationClipLoop in customAnimationClipsLoopDict.Values)
                customAnimationClips.Remove(animationClipLoop);
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

                if (humanoidAvatar)
                    animator.avatar = humanoidAvatar;

                if (!humanoidAnimatorController)
                    LogError("Failed to load humanoid animator controller from asset bundle: misc");
                if (!humanoidAvatar)
                    LogError("Failed to load humanoid avatar from asset bundle: misc");
                if (!humanoidSkeletonPrefab)
                    LogError("Failed to load humanoid skeleton prefab from asset bundle: misc");
            }
            catch
            {
                LogError("Failed to load misc Asset Bundle.");
            }
        }


        public static void LoadRadialMenuAsset()
        {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(instance.Info.Location), "Assets/radial_menu");
                AssetBundle assetBundle = AssetBundle.LoadFromFile(assetsPath);
                radialMenuPrefab = assetBundle.LoadAsset<GameObject>("RadialMenu");
                Log("Successfully loaded radial menu asset.");
            }
            catch
            {
                LogError("Failed to load radial menu asset.");
            }
        }


        private void PatchAll()
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


        public static bool IsModLoaded(string guid) => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
    }
}
