using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using System.Reflection;
using static TooManyEmotesScrap.CustomLogging;
using TooManyEmotesScrap.Config;
using BepInEx.Logging;

namespace TooManyEmotesScrap
{
    [BepInPlugin("FlipMods.TooManyEmotesScrap", "TooManyEmotesScrap", "1.0.2")]
    [BepInDependency("FlipMods.TooManyEmotes", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        public static Plugin instance;

        public static ManualLogSource defaultLogger { get { return instance.Logger; } }

        private void Awake()
        {
            instance = this;
            InitLogger();
            ConfigSettings.BindConfigSettings();
            this._harmony = new Harmony("TooManyEmotesScrap");

            PatchAll();
            Log("TooManyEmotesScrap loaded");
            LogWarning("NOTE: You will be unable to join other players (and they will be unable to join you) unless you either both have this mod enabled, or both have this mod disabled.\nIf you are hosting a lobby for random players to join, or you are looking to join random servers, it might be best to disable this mod for the best compatibility.");
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
    }
}
