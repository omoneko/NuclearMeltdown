using System.Collections.Generic;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>The ledger of contamination zones, and applying, holding and clearing them on the grid.</summary>
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

        /// <summary>Replaces a zone in the ledger, used to store an intensity lowered by decontamination.</summary>
        public static void SetZoneAt(int index, ContaminationZone zone)
        {
            if (index >= 0 && index < _zones.Count) _zones[index] = zone;
        }

        /// <summary>Rounds the float intensity to the byte a ground pollution cell holds.</summary>
        private static byte ToByteIntensity(float intensity)
        {
            int v = (int)(intensity + 0.5f);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        /// <summary>
        /// Holds the contamination in place, raising cells the game's natural decay pulled down
        /// back to zone.Intensity. Redraws only when something actually changed.
        /// </summary>
        public static void ReassertZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ApplyDose(doses[i]);
            // Not redrawing in the steady state (nothing changed) is what stops the overlay from flickering.
            if (changed) RefreshZoneTexture(zone);
        }

        /// <summary>Writes the contamination over the grid, to apply an intensity lowered by decontamination. Redraws only on a change.</summary>
        public static void SetZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.SetDose(doses[i]);
            if (changed) RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ClearCell(doses[i].Index);
            if (changed) RefreshZoneTexture(zone);
        }

        public static void RefreshZoneTexture(ContaminationZone zone)
        {
            // Computed as a long so a huge radius (a very high output plant) cannot overflow an
            // int, then rounded down. The Clamp below keeps it inside the grid anyway, so
            // anything past Resolution is meaningless.
            long rawCellRadius = (long)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cellRadius = rawCellRadius > PollutionGrid.Resolution
                ? PollutionGrid.Resolution : (int)rawCellRadius;
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
