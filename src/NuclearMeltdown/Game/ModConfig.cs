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
        // クレーターの半径/深さに上限は設けない（規模に比例してどこまでも大きくなる）。
        public const float RemoveRadiusBase = 60f;         // 内側=全破壊半径
        public const float DestructionRadiusMinBase = 100f; // 破壊減衰の内縁
        public const float DestructionRadiusMaxBase = 160f; // 破壊減衰の外縁
        public const float BurnRadiusMaxBase = 200f;       // 延焼の外縁

        // 「原発の出力に応じた災害規模」モードの設定。Scale = 出力 / ReferenceOutput（単純比例）。
        // 基準はバニラ原子力発電所(640MW)＝Scale 1.0。出力2倍なら被害半径も2倍。
        // 実在アセットの上限は3200MW前後＝Scale 5.0。上限は設けない（極端な出力でマップが
        // 消し飛ぶのは仕様として許容する）。
        public const int ReferenceOutput = 640;            // Scale 1.0 の基準出力（バニラ原発 640MW）
        public const float OutputScaleMin = 0.1f;          // 規模の下限（小出力でも最低限の被害）
        public const float OutputScaleMax = float.MaxValue; // 上限なし（青天井）
        // 汚染半径にも上限は設けない（基準700m×Scale がそのまま適用される）。
        // ただし DisasterHelpers に渡す破壊/延焼半径だけは、マップ全域を十分覆う値で丸める。
        // マップは約17.3km四方なので、これ以上は走査コストが増えるだけで見た目は変わらない
        // （マップ消失という結果は同じ。ゲームが固まるのを防ぐための実務上の上限）。
        public const float EffectRadiusMax = 20000f;

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
