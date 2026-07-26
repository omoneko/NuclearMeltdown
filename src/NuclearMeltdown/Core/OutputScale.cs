using System;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// 原発の発電出力から災害規模(Scale)を求める純粋関数（UnityEngine非依存）。
    /// Scale は出力に単純比例する: Scale = output / referenceOutput
    /// （出力2倍なら被害半径も2倍）。ただし出力が桁違いのアセット(数億)では半径がマップを
    /// 大きく超えて DisasterHelpers が暴走するため、maxScale で安全側にクランプする。
    /// </summary>
    public static class OutputScale
    {
        /// <summary>バニラ原子力発電所の m_electricityProduction。</summary>
        public const int VanillaNuclearOutput = 640;

        /// <summary>
        /// 出力 output（電力生産量）から Scale を返す（出力に比例）。
        /// output が0以下なら minScale。結果は [minScale, maxScale] にクランプ。
        /// </summary>
        public static float FromOutput(int output, int referenceOutput, float minScale, float maxScale)
        {
            if (referenceOutput <= 0) referenceOutput = VanillaNuclearOutput;
            if (minScale < 0f) minScale = 0f;
            if (maxScale < minScale) maxScale = minScale;

            if (output <= 0) return minScale;

            float scale = output / (float)referenceOutput; // 単純比例
            if (scale < minScale) return minScale;
            if (scale > maxScale) return maxScale;
            return scale;
        }
    }
}
