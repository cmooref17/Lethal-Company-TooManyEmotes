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

        //public readonly static string audioFileExtension = ".ogg";

        public static bool AudioExists(string audioName) => loadedAudioClipsDict.ContainsKey(audioName); //audioAssetNames != null && audioAssetNames.Contains(audioName);

        public static bool muteEmoteAudio = false;
        public static float emoteVolumeMultiplier = 1;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void Init()
        {
            LoadPreferences();
            //ClearAudioClipCache();
        }


        public static void SavePreferences()
        {
            ES3.Save("TooManyEmotes.MuteEmoteAudio", muteEmoteAudio);
            ES3.Save("TooManyEmotes.EmoteAudioVolume", emoteVolumeMultiplier);
        }


        public static void LoadPreferences()
        {
            muteEmoteAudio = ES3.Load("TooManyEmotes.MuteEmoteAudio", false);
            emoteVolumeMultiplier = ES3.Load("TooManyEmotes.EmoteAudioVolume", 1.0f);
        }


        public static void BuildAudioClipList()
        {
            LoadAllAudioClips();
            /*

            foreach (var audioClip in loadedAudioClips)
            {
                string emoteName = audioClip.name;
                if (audioClip.name.Contains("."))
                {
                    var args = audioClip.name.Split('.');
                    if (args.Length > 0 && args[0].Length > 0)
                        emoteName = args[0];
                }
                emoteName = emoteName.Replace("_start", "").Replace("_loop", "");
                if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote))
                {
                    if (emote.animationClip != null && emote.animationClip.name == audioClip.name)
                        emote.
                    emote.propNamesInEmote.Add(propPrefab.name);
                }

            }
            */
            /*
            foreach (var audioClip in loadedAudioClips)
            {
                string emoteName = audioClip.name.Replace(audioFileExtension, "");
                if (emoteName.EndsWith("_start"))
                    emoteName = emoteName.Substring(0, emoteName.Length - 6);
                else if (emoteName.EndsWith("_loop"))
                    emoteName = emoteName.Substring(0, emoteName.Length - 5);

                if (EmotesManager.allUnlockableEmotesDict.TryGetValue(emoteName, out var emote)) { }
            }
            */
        }


        public static void LoadAudioAssets()
        {
            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(Plugin.instance.Info.Location), "Assets/compressed_audio");
                audioAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                audioAssetNames.UnionWith(audioAssetBundle.GetAllAssetNames());
            }
            catch
            {
                Plugin.LogWarning("Failed to load emotes audio asset bundle: compressed_audio.");
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
                Plugin.Log("Loading " + loadedAudioClips.Count + " audio clips.");
                foreach (var clip in loadedAudioClips)
                {
                    if (!loadedAudioClipsDict.ContainsKey(clip.name))
                    {
                        loadedAudioClipsDict[clip.name] = clip;
                    }
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
