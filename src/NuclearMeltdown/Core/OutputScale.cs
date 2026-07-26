using System;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// 原発の発電出力から災害規模(Scale)を求める純粋関数（UnityEngine非依存）。
    /// 爆風半径 ∝ 威力^(1/3) の物理則に倣い、Scale = cbrt(output / referenceOutput)。
    /// バニラ原発(既定 640)を基準=1.0 とし、出力10倍でも規模は約2.15倍に収まるため
    /// 巨大な Workshop アセットでもマップが壊れにくい。上下限でクランプする。
    /// </summary>
    public static class OutputScale
    {
        /// <summary>バニラ原子力発電所の m_electricityProduction。これを Scale 1.0 の基準にする。</summary>
        public const int VanillaNuclearOutput = 640;

        /// <summary>
        /// 出力 output（電力生産量）から Scale を返す。
        /// output/referenceOutput が0以下なら minScale。結果は [minScale, maxScale] にクランプ。
        /// </summary>
        public static float FromOutput(int output, int referenceOutput, float minScale, float maxScale)
        {
            if (referenceOutput <= 0) referenceOutput = VanillaNuclearOutput;
            if (minScale < 0f) minScale = 0f;
            if (maxScale < minScale) maxScale = minScale;

            if (output <= 0) return minScale;

            float scale = (float)Math.Pow(output / (double)referenceOutput, 1.0 / 3.0);
            if (scale < minScale) return minScale;
            if (scale > maxScale) return maxScale;
            return scale;
        }
    }
}
