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
            string name = info.name;
            return name != null && name.Contains(ModConfig.NuclearNameKeyword);
        }
    }
}
