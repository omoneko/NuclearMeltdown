using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>Mod全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string HarmonyId = "com.omone.nuclearmeltdown";
        public const float DefaultRadiusMeters = 700f;
        public const int ExpiryYears = 50;
        // 除染は専用の「Decontamination facility」建物のみ（汚水処理場では除染されない）。
        // 名称に下記キーワードを含む稼働中の建物がゾーン付近にあると、各セルの汚染を
        // ゲーム内1か月あたり DecontaminationMonthlyFraction 相対除去する（核ミサイルMODと同一仕様）。
        public const string DecontaminationNameKeyword = "Decontamination";
        public const float DecontaminationMonthlyFraction = 0.05f; // 1か月で5%除去（相対）
        public const byte DecontaminationMinIntensity = 5;          // これ以下のセルは0にする
        // 原発の名称判定キーワードは NuclearMeltdown.Core.NuclearNameMatcher に集約（テスト可能・"NPP"等に対応）。
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
