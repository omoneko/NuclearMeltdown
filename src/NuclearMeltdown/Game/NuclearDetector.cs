using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>Decides whether a building is a nuclear power plant.</summary>
    public static class NuclearDetector
    {
        public static bool IsNuclearPlant(ushort buildingID)
        {
            if (buildingID == 0) return false;
            var info = BuildingManager.instance.m_buildings.m_buffer[buildingID].Info;
            if (info == null || info.m_buildingAI == null) return false;
            if (!(info.m_buildingAI is PowerPlantAI)) return false;
            // Besides the vanilla "Nuclear", this also matches Workshop plants named
            // differently ("NPP" and so on).
            return NuclearNameMatcher.Matches(info.name);
        }

        /// <summary>
        /// The plant's power output (PowerPlantAI.m_electricityProduction), or 0 if it cannot
        /// be read. Feeds the "scale from output" mode, where the vanilla plant's 640 is the
        /// baseline.
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
