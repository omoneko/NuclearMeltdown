using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>建物が原子力発電所かどうかを判定する。</summary>
    public static class NuclearDetector
    {
        public static bool IsNuclearPlant(ushort buildingID)
        {
            if (buildingID == 0) return false;
            var info = BuildingManager.instance.m_buildings.m_buffer[buildingID].Info;
            if (info == null || info.m_buildingAI == null) return false;
            if (!(info.m_buildingAI is PowerPlantAI)) return false;
            // バニラ "Nuclear" に加え Workshop 原発アセット（"NPP" 等）も判定する。
            return NuclearNameMatcher.Matches(info.name);
        }

        /// <summary>
        /// 原発の発電出力（PowerPlantAI.m_electricityProduction）を返す。取得できなければ 0。
        /// 「出力に応じた災害規模」で使う（バニラ原発=640 が基準）。
        /// </summary>
        public static int GetElectricityProduction(ushort buildingID)
        {
            try
            {
                if (buildingID == 0) return 0;
                var info = BuildingManager.instance.m_buildings.m_buffer[buildingID].Info;
                if (info == null) return 0;
                PowerPlantAI ai = info.m_buildingAI as PowerPlantAI;
                return ai != null ? ai.m_electricityProduction : 0;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("GetElectricityProduction error: " + e);
                return 0;
            }
        }
    }
}
