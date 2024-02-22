using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TooManyEmotes.Patches;

namespace TooManyEmotes.Audio
{
    [HarmonyPatch]
    public static class AudioManager
    {
        public static AssetBundle audioAssetBundle;
        public static HashSet<string> audioAssetNames = new HashSet<string>();

        public static HashSet<AudioClip> loadedAudioClips = new HashSet<AudioClip>();
        public static Dictionary<string, AudioClip> loadedAudioClipsDict = new Dictionary<string, AudioClip>();

        public readonly static string audioFileExtension = ".wav";

        public static bool AudioExists(string audioName) => audioAssetNames != null && audioAssetNames.Contains(audioName);


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void Init()
        {
            ClearAudioClipCache();
        }


        public static void LoadAudioAssets()
        {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(Plugin.instance.Info.Location), "Assets/emote_audio");
                audioAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                audioAssetNames.UnionWith(audioAssetBundle.GetAllAssetNames());
            }
            catch
            {
                Plugin.LogError("Failed to load emotes audio asset bundle: emotes_audio.");
            }
        }


        public static bool LoadAllAudioClips()
        {
            if (audioAssetBundle == null)
            {
                Plugin.LogError("Cannot load audio clips with a null Asset Bundle. Did the Asset Bundle fail to load?");
                return false;
            }
            try
            {
                loadedAudioClips.UnionWith(audioAssetBundle.LoadAllAssets<AudioClip>());
                foreach (var clip in loadedAudioClips)
                {
                    if (!loadedAudioClipsDict.ContainsKey(clip.name))
                        loadedAudioClipsDict[clip.name] = clip;
                }
            }
            catch
            {
                Plugin.LogError("Failed to load all emote audio clips from asset bundle.");
                return false;
            }
            return true;
        }


        public static AudioClip LoadAudioClip(string clipName)
        {
            if (audioAssetBundle == null)
            {
                Plugin.LogError("Cannot load audio clip: " + clipName + " with a null Asset Bundle. Did the Asset Bundle fail to load?");
                return null;
            }
            if (!audioAssetNames.Contains(clipName))
            {
                Plugin.LogError("Failed to load emote audio clip. Clip does not exist in the list of valid audio clip names. Clip: " + clipName);
                return null;
            }

            AudioClip audioClip;
            if (loadedAudioClipsDict.TryGetValue(clipName, out audioClip))
                return audioClip;

            try
            {
                audioClip = audioAssetBundle.LoadAsset<AudioClip>(clipName);
                loadedAudioClips.Add(audioClip);
                loadedAudioClipsDict.Add(clipName, audioClip);
                Plugin.Log("Cached audio clip: " + clipName);
            }
            catch
            {
                Plugin.LogError("Failed to load audio clip from asset bundle. Clip: " + clipName);
                return null;
            }

            return audioClip;
        }


        public static void ClearAudioClipCache()
        {
            loadedAudioClips?.Clear();
            loadedAudioClipsDict?.Clear();

            if (EmotesManager.allUnlockableEmotes != null)
            {
                try
                {
                    //foreach (var emote in EmotesManager.allUnlockableEmotes) emote.cachedAudioClip = null;
                }
                catch { }
            }
        }
    }
}
