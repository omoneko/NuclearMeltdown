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
        // 除染: DecontaminationInterval 処理サイクルに1回だけ、各セルを DecontaminationStep 減らす。
        // (処理は16tickごと。Interval を大きくするほど除染は遅くなる)
        public const int DecontaminationStep = 8;
        public const int DecontaminationInterval = 100;
        public const string NuclearNameKeyword = "Nuclear";
        public const byte MaxPollution = 255;
        public const string LogPrefix = "[NuclearMeltdown] ";

        // 爆発（クレーター＋範囲建物破壊）の基準半径(Scale 1.0 相当)。実際は Scale 倍される。
        public const float CraterRadiusBase = 60f;         // クレーター半径
        public const float CraterDepthBase = 16f;          // クレーター深さ
        public const float CraterRadiusMax = 250f;         // クレーター半径の上限(地形破壊防止)
        public const float CraterDepthMax = 50f;           // クレーター深さの上限
        public const float RemoveRadiusBase = 60f;         // 内側=全破壊半径
        public const float DestructionRadiusMinBase = 100f; // 破壊減衰の内縁
        public const float DestructionRadiusMaxBase = 160f; // 破壊減衰の外縁
        public const float BurnRadiusMaxBase = 200f;       // 延焼の外縁

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
