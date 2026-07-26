namespace NuclearMeltdown.Core
{
    /// <summary>災害規模の決定方式（オプションで選択）。</summary>
    public enum MeltdownScaleMode
    {
        /// <summary>従来どおり確率テーブルで抽選する。</summary>
        Random = 0,
        /// <summary>原発の発電出力に応じて規模を決める（cbrt スケール）。</summary>
        ByOutput = 1,
        /// <summary>常に固定倍率（オプションのスライダー値）。</summary>
        Fixed = 2
    }

    /// <summary>
    /// モードに応じて MeltdownOutcome を組み立てる純粋ロジック（UnityEngine非依存）。
    /// Random は既存の確率テーブルをそのまま使う。ByOutput/Fixed は規模を入力値で決める。
    /// 爆発・汚染はそれぞれ allowExplosion / allowContamination で個別に抑止でき、
    /// 「爆発なし・汚染のみ」「爆発のみ・汚染なし」も表現できる。
    /// </summary>
    public static class MeltdownOutcomeSelector
    {
        /// <param name="mode">規模の決定方式。</param>
        /// <param name="roll">Random 用の 0-99 乱数。</param>
        /// <param name="scale">ByOutput/Fixed で使う規模倍率。</param>
        /// <param name="allowExplosion">false なら爆発（クレーター/破壊）を一切起こさない。</param>
        /// <param name="allowContamination">false なら汚染（放射性降下物）を一切発生させない。</param>
        public static MeltdownOutcome Select(MeltdownScaleMode mode, int roll, float scale,
            bool allowExplosion, bool allowContamination)
        {
            MeltdownOutcome o;
            switch (mode)
            {
                case MeltdownScaleMode.ByOutput:
                case MeltdownScaleMode.Fixed:
                    if (scale < 0f) scale = 0f;
                    o = new MeltdownOutcome(scale > 0f, scale, scale > 0f, scale);
                    break;
                default:
                    o = MeltdownOutcomeTable.FromRoll(roll);
                    break;
            }

            if (!allowExplosion)
            {
                o.Explode = false;
                o.ExplosionScale = 0f;
            }
            if (!allowContamination)
            {
                o.Contaminate = false;
                o.ContaminationScale = 0f;
            }
            return o;
        }
    }
}
