namespace NuclearMeltdown.Core
{
    /// <summary>
    /// 原発破壊時の抽選結果。爆発の有無・規模(Scale)、汚染の有無・規模(Scale)を保持する。
    /// Scale は現在の基準汚染(700m)を 1.0 とした倍率。
    /// </summary>
    public struct MeltdownOutcome
    {
        public bool Explode;
        public float ExplosionScale;
        public bool Contaminate;
        public float ContaminationScale;

        public MeltdownOutcome(bool explode, float explosionScale, bool contaminate, float contaminationScale)
        {
            Explode = explode;
            ExplosionScale = explosionScale;
            Contaminate = contaminate;
            ContaminationScale = contaminationScale;
        }
    }

    /// <summary>
    /// 0-99 の乱数から破壊結果を決定する確率テーブル。
    ///   5%(0-4)  : Scale 10.0 の核爆発 + Scale 10.0 の汚染
    ///  15%(5-19) : Scale 5.5 の核爆発 + Scale 5.5 の汚染
    ///  45%(20-64): Scale 1.0 の核爆発 + Scale 1.0 の汚染
    ///  30%(65-94): 爆発なし + Scale 1.0 の汚染のみ
    ///   5%(95-99): 倒壊のみ（爆発なし・汚染なし）
    /// </summary>
    public static class MeltdownOutcomeTable
    {
        /// <param name="roll">0-99 の乱数（範囲外は 0-99 にクランプ）。</param>
        public static MeltdownOutcome FromRoll(int roll)
        {
            if (roll < 0) roll = 0;
            if (roll > 99) roll = 99;

            if (roll < 5) return new MeltdownOutcome(true, 10.0f, true, 10.0f);
            if (roll < 20) return new MeltdownOutcome(true, 5.5f, true, 5.5f);
            if (roll < 65) return new MeltdownOutcome(true, 1.0f, true, 1.0f);
            if (roll < 95) return new MeltdownOutcome(false, 0f, true, 1.0f);
            return new MeltdownOutcome(false, 0f, false, 0f);
        }
    }
}
