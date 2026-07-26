using System.Collections.Generic;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// NaturalResourceManager の汚染グリッド(512x512, セル33.75m)に対する
    /// Unity非依存の座標計算・半径列挙。
    /// </summary>
    public static class PollutionGrid
    {
        public const float CellSize = 33.75f;
        public const int Resolution = 512;

        public static int WorldToCell(float world)
        {
            int cell = (int)(world / CellSize + 256f);
            if (cell < 0) return 0;
            if (cell > Resolution - 1) return Resolution - 1;
            return cell;
        }

        public static int CellIndex(int cellX, int cellZ)
        {
            return cellZ * Resolution + cellX;
        }

        /// <summary>
        /// 中心(centerX,centerZ)・半径radiusMetersの円内セルを列挙。
        /// 濃度は中心 maxIntensity、半径端で0への線形減衰（半径外は含めない）。
        /// </summary>
        public static List<CellDose> CellsInRadius(float centerX, float centerZ, float radiusMeters, byte maxIntensity)
        {
            var result = new List<CellDose>();
            if (radiusMeters <= 0f) return result;

            // セル半径はグリッド全体(Resolution)を超えて走査しても無意味なので上限を掛ける。
            // これが無いと巨大半径(超高出力の原発など)で数千万回の空ループになりゲームが固まる。
            long rawCellRadius = (long)(radiusMeters / CellSize) + 1;
            int cellRadius = rawCellRadius > Resolution ? Resolution : (int)rawCellRadius;
            int centerCellX = WorldToCell(centerX);
            int centerCellZ = WorldToCell(centerZ);

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                int cz = centerCellZ + dz;
                if (cz < 0 || cz > Resolution - 1) continue;
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int cx = centerCellX + dx;
                    if (cx < 0 || cx > Resolution - 1) continue;

                    // セル中心のワールド距離で判定
                    float worldDx = dx * CellSize;
                    float worldDz = dz * CellSize;
                    float dist = (float)System.Math.Sqrt(worldDx * worldDx + worldDz * worldDz);
                    if (dist > radiusMeters) continue;

                    float t = 1f - (dist / radiusMeters); // 中心1..端0
                    if (t < 0f) t = 0f;
                    byte intensity = (byte)(maxIntensity * t);
                    result.Add(new CellDose(CellIndex(cx, cz), intensity));
                }
            }
            return result;
        }
    }
}
