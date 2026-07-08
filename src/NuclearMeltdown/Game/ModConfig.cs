using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>Mod全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string HarmonyId = "com.omone.nuclearmeltdown";
        public const float DefaultRadiusMeters = 700f;
        public const int ExpiryYears = 50;
        public const string DecontaminationNameKeyword = "Water Treatment";
        public const string NuclearNameKeyword = "Nuclear";
        public const byte MaxPollution = 255;
        public const string LogPrefix = "[NuclearMeltdown] ";

        public static void Log(string msg)
        {
            Debug.Log(LogPrefix + msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(LogPrefix + msg);
        }
    }
}
