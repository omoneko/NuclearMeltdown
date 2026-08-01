using ColossalFramework;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>
    /// 永続設定（災害規模モード・固定倍率・爆発/汚染のON/OFF）。ColossalFramework の SavedInt を使う。
    /// 設定ファイル名は MOD/アセンブリ名("NuclearMeltdown")と別名にする。同名だと CS の設定辞書で
    /// MOD登録キーと衝突し「同じキーが既に存在」例外→設定削除ループになる。
    /// </summary>
    public static class ModSettings
    {
        public const string FileName = "NuclearMeltdownSettings";

        /// <summary>規模モードの表示名（インデックス = MeltdownScaleMode の値）。</summary>
        public static readonly string[] ScaleModeNames =
        {
            "Random (probability table)",
            "Based on plant output",
            "Fixed scale"
        };

        // 固定倍率スライダーは 0.5〜10.0 を 10倍の整数(5〜100)で保存する。
        public const int FixedScaleMin = 5;
        public const int FixedScaleMax = 100;
        public const int FixedScaleDefault = 10; // = 1.0

        private static SavedInt _scaleMode;
        private static SavedInt _fixedScaleX10;
        private static SavedInt _explosionEnabled;
        private static SavedInt _contaminationEnabled;

        // 設定ファイルの登録は1回だけ行う。Ensure() は各getterから呼ばれるため、毎回
        // AddSettingsFile すると CS 内部が「同じキー」例外→**設定ファイルを削除**して空で
        // 作り直すループに入り、プレイヤーの設定が保存されなくなる（Siren Alert で発覚）。
        private static bool _fileRegistered;

        public static void Ensure()
        {
            if (!_fileRegistered)
            {
                _fileRegistered = true; // 例外時も再試行しない
                try
                {
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = FileName });
                }
                catch (System.Exception e)
                {
                    ModConfig.LogError("AddSettingsFile(" + FileName + "): " + e.Message);
                }
            }
            if (_scaleMode == null) _scaleMode = new SavedInt("scaleMode", FileName, (int)MeltdownScaleMode.Random, true);
            if (_fixedScaleX10 == null) _fixedScaleX10 = new SavedInt("fixedScaleX10", FileName, FixedScaleDefault, true);
            if (_explosionEnabled == null) _explosionEnabled = new SavedInt("explosionEnabled", FileName, 1, true);       // 既定ON
            if (_contaminationEnabled == null) _contaminationEnabled = new SavedInt("contaminationEnabled", FileName, 1, true); // 既定ON
        }

        public static SavedInt ScaleModeSetting { get { Ensure(); return _scaleMode; } }
        public static SavedInt FixedScaleSetting { get { Ensure(); return _fixedScaleX10; } }
        public static SavedInt ExplosionEnabledSetting { get { Ensure(); return _explosionEnabled; } }
        public static SavedInt ContaminationEnabledSetting { get { Ensure(); return _contaminationEnabled; } }

        /// <summary>災害規模の決定方式。</summary>
        public static MeltdownScaleMode ScaleMode
        {
            get
            {
                int v = ScaleModeSetting.value;
                if (v < 0 || v > (int)MeltdownScaleMode.Fixed) v = (int)MeltdownScaleMode.Random;
                return (MeltdownScaleMode)v;
            }
        }

        /// <summary>Fixed モードで使う倍率（0.5〜10.0）。</summary>
        public static float FixedScale { get { return FixedScaleSetting.value / 10f; } }

        /// <summary>爆発（クレーター/範囲破壊）を発生させるか。</summary>
        public static bool ExplosionEnabled { get { return ExplosionEnabledSetting.value != 0; } }

        /// <summary>放射性降下物（土壌汚染）を発生させるか。</summary>
        public static bool ContaminationEnabled { get { return ContaminationEnabledSetting.value != 0; } }
    }
}
