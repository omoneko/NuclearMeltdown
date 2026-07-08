using System.Collections.Generic;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>汚染ゾーン台帳と、グリッドへの適用/維持/除去。</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();

        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        public static void AddZone(ContaminationZone zone)
        {
            _zones.Add(zone);
            ReassertZone(zone);
        }

        public static void RemoveZoneAt(int index)
        {
            if (index >= 0 && index < _zones.Count) _zones.RemoveAt(index);
        }

        public static void ReassertZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            for (int i = 0; i < doses.Count; i++) PollutionField.ApplyDose(doses[i]);
            RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            for (int i = 0; i < doses.Count; i++) PollutionField.ClearCell(doses[i].Index);
            RefreshZoneTexture(zone);
        }

        public static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(zone.CenterX);
            int cz = PollutionGrid.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > PollutionGrid.Resolution - 1) return PollutionGrid.Resolution - 1;
            return v;
        }
    }
}
