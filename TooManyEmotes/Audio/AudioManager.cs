using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TooManyEmotes.Patches;
using static TooManyEmotes.CustomLogging;

namespace TooManyEmotes.Audio
{
    [HarmonyPatch]
    public static class AudioManager
    {
        public static AssetBundle audioAssetBundle;
        public static AssetBundle dmcaAudioAssetBundle;
        //public static HashSet<string> audioAssetNames = new HashSet<string>();
        //public static HashSet<string> audioAssetNamesDmcaFree = new HashSet<string>();

        static HashSet<AudioClip> loadedAudioClips = new HashSet<AudioClip>();
        static HashSet<AudioClip> loadedAudioClipsDmca = new HashSet<AudioClip>();
        static HashSet<AudioClip> loadedAudioClipsDmcaFree = new HashSet<AudioClip>();

        static Dictionary<string, AudioClip> audioClipsDictDmcaFree = new Dictionary<string, AudioClip>();
        static Dictionary<string, AudioClip> audioClipsDictDmca = new Dictionary<string, AudioClip>();
        //public static Dictionary<string, AudioClip> loadedAudioClipsDict { get { return dmcaFreeMode ? dmcaAudioClips : dmcaFreeAudioClips; } }
        
        public static bool AudioExists(string audioName) => (audioClipsDictDmcaFree != null && audioClipsDictDmcaFree.ContainsKey(audioName)) || (audioClipsDictDmca != null && audioClipsDictDmca.ContainsKey(audioName));

        public static bool muteEmoteAudio = false;
        public static bool emoteOnlyMode = false;
        public static bool dmcaFreeMode = false;
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
            ES3.Save("TooManyEmotes.EmoteOnlyMode", emoteOnlyMode);
            ES3.Save("TooManyEmotes.DmcaFreeMode", dmcaFreeMode);
        }


        public static void LoadPreferences()
        {
            muteEmoteAudio = ES3.Load("TooManyEmotes.MuteEmoteAudio", false);
            emoteVolumeMultiplier = ES3.Load("TooManyEmotes.EmoteAudioVolume", 1.0f);
            emoteOnlyMode = ES3.Load("TooManyEmotes.EmoteOnlyMode", false);
            dmcaFreeMode = ES3.Load("TooManyEmotes.DmcaFreeMode", false);
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
                //audioAssetNames.UnionWith(audioAssetBundle.GetAllAssetNames());
            }
            catch
            {
                LogWarning("Failed to load emotes audio asset bundle: compressed_audio.");
            }

            try
            {
                string assetsPath = Path.Combine(Path.GetDirectoryName(Plugin.instance.Info.Location), "Assets/compressed_audio_dmca");
                dmcaAudioAssetBundle = AssetBundle.LoadFromFile(assetsPath);
                //audioAssetNamesDmcaFree.UnionWith(dmcaAudioAssetBundle.GetAllAssetNames());
                //audioAssetNames.UnionWith(dmcaAudioAssetBundle.GetAllAssetNames());
            }
            catch
            {
                LogWarning("Failed to load emotes audio asset bundle: compressed_audio_dmca.");
            }
        }


        public static void LoadAllAudioClips()
        {
            if (audioAssetBundle == null)
                LogError("Cannot load audio clips with a null Asset Bundle. Did the Asset Bundle fail to load?");
            else
            {
                try
                {
                    loadedAudioClipsDmcaFree = new HashSet<AudioClip>(audioAssetBundle.LoadAllAssets<AudioClip>());
                    loadedAudioClips.UnionWith(loadedAudioClipsDmcaFree);

                    Log("Loading " + loadedAudioClipsDmcaFree.Count + " dmca-free audio clips.");
                    foreach (var clip in loadedAudioClipsDmcaFree)
                    {
                        if (!audioClipsDictDmcaFree.ContainsKey(clip.name))
                            audioClipsDictDmcaFree[clip.name] = clip;
                    }
                }
                catch { LogError("Failed to load dmca-free emote audio clips from asset bundle."); }
            }


            if (dmcaAudioAssetBundle == null)
                LogError("Cannot load dmca audio clips with a null Asset Bundle. Did the Asset Bundle fail to load?");
            else
            {
                try
                {
                    loadedAudioClipsDmca = new HashSet<AudioClip>(dmcaAudioAssetBundle.LoadAllAssets<AudioClip>());
                    loadedAudioClips.UnionWith(loadedAudioClipsDmca);

                    Log("Loading " + loadedAudioClipsDmca.Count+ " dmca audio clips.");
                    foreach (var clip in loadedAudioClipsDmca)
                    {
                        if (!audioClipsDictDmca.ContainsKey(clip.name))
                            audioClipsDictDmca[clip.name] = clip;
                    }
                }
                catch { LogError("Failed to load dmca emote audio clips from asset bundle."); }
            }
        }


        public static AudioClip LoadAudioClip(string clipName)
        {
            if (audioAssetBundle == null)
            {
                LogError("Cannot load audio clip: " + clipName + " with a null Asset Bundle. Did the Asset Bundle fail to load?");
                return null;
            }
            /*
            if (!audioAssetNames.Contains(clipName) && !audioAssetNamesDmcaFree.Contains(clipName))
            {
                LogError("Failed to load emote audio clip. Clip does not exist in the list of valid audio clip names. Clip: " + clipName);
                return null;
            }
            */


            if (!dmcaFreeMode)
            {
                if (audioClipsDictDmca.TryGetValue(clipName, out var dmcaAudioClip))
                    return dmcaAudioClip;

                dmcaAudioClip = dmcaAudioAssetBundle.LoadAsset<AudioClip>(clipName);
                if (dmcaAudioClip != null)
                {
                    loadedAudioClips.Add(dmcaAudioClip);
                    loadedAudioClipsDmca.Add(dmcaAudioClip);
                    audioClipsDictDmca.Add(clipName, dmcaAudioClip);
                    //Log("Cached audio clip: " + clipName);
                    return dmcaAudioClip;
                }
            }
            if (audioClipsDictDmcaFree.TryGetValue(clipName, out var dmcaFreeAudioClip))
                return dmcaFreeAudioClip;

            dmcaFreeAudioClip = audioAssetBundle.LoadAsset<AudioClip>(clipName);
            if (dmcaFreeAudioClip != null)
            {
                loadedAudioClips.Add(dmcaFreeAudioClip);
                loadedAudioClipsDmcaFree.Add(dmcaFreeAudioClip);
                audioClipsDictDmcaFree.Add(clipName, dmcaFreeAudioClip);
                //Log("Cached audio clip: " + clipName);
                return dmcaFreeAudioClip;
            }

            return null;
        }

        public static bool IsClipDMCA(AudioClip audioClip) => loadedAudioClipsDmca.Contains(audioClip);
        //public static bool HasDmcaFreeAudio(UnlockableEmote emote) => emote != null && ((emote.animationClip != null && audioAssetNamesDmcaFree.Contains(emote.animationClip.name)) || (emote.transitionsToClip != null && audioAssetNamesDmcaFree.Contains(emote.transitionsToClip.name)));

        public static void ClearAudioClipCache()
        {
            loadedAudioClips?.Clear();
            audioClipsDictDmca?.Clear();
            audioClipsDictDmcaFree?.Clear();

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
