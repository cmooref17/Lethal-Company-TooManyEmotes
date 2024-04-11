using BepInEx.Logging;

namespace TooManyEmotesScrap
{
    internal static class CustomLogging
    {
        private static ManualLogSource logger;

        public static void InitLogger()
        {
            try { logger = BepInEx.Logging.Logger.CreateLogSource(string.Format("{0}-{1}", Plugin.instance.Info.Metadata.Name, Plugin.instance.Info.Metadata.Version)); }
            catch { logger =  Plugin.defaultLogger; }
        }

        public static void Log(string message) => logger.LogInfo(message);
        public static void LogError(string message) => logger.LogError(message);
        public static void LogWarning(string message) => logger.LogWarning(message);
        /// <summary>
        /// Returns true if it succeeds
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="failMessage"></param>
        /// <returns></returns>
        public static bool Assert(bool condition, string failMessage) { if (!condition) LogWarning(failMessage); return condition; }
    }
}
