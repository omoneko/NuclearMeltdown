using System.Collections.Generic;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Coordinate maths and radius enumeration for NaturalResourceManager's pollution grid
    /// (512x512, 33.75 m cells). No Unity dependency.
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
        /// Lists the cells inside the circle at (centerX, centerZ) with the given radius.
        /// Intensity falls off linearly from maxIntensity at the centre to zero at the edge;
        /// cells outside the radius are not included.
        /// </summary>
        public static List<CellDose> CellsInRadius(float centerX, float centerZ, float radiusMeters, byte maxIntensity)
        {
            var result = new List<CellDose>();
            if (radiusMeters <= 0f) return result;

            // Scanning further than the whole grid (Resolution) achieves nothing, so the cell
            // radius is capped. Without this, a huge radius - from a very high output plant,
            // say - turns into tens of millions of empty iterations and freezes the game.
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

                    // Test against the world distance from cell centre to cell centre.
                    float worldDx = dx * CellSize;
                    float worldDz = dz * CellSize;
                    float dist = (float)System.Math.Sqrt(worldDx * worldDx + worldDz * worldDz);
                    if (dist > radiusMeters) continue;

                    float t = 1f - (dist / radiusMeters); // 1 at the centre .. 0 at the edge
                    if (t < 0f) t = 0f;
                    byte intensity = (byte)(maxIntensity * t);
                    result.Add(new CellDose(CellIndex(cx, cz), intensity));
                }
            }
            return result;
        }
    }
}
