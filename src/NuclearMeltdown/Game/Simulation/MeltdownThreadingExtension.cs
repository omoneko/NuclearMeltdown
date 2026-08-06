using System;
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game.Simulation
{
    /// <summary>
    /// Maintains the contamination zones every tick and lifts them after 50 years. The
    /// contamination is only removed - 5% relative per in-game month - while a dedicated
    /// "Decontamination facility" building is operating near the zone.
    /// <b>A water treatment plant does not decontaminate</b>, matching the nuclear missile mod.
    /// The game discovers and drives any IThreadingExtension in a mod assembly on its own.
    /// </summary>
    public class MeltdownThreadingExtension : ThreadingExtensionBase
    {
        private static readonly long TicksPerMonth = TimeSpan.FromDays(30).Ticks; // an in-game month is 30 days

        private int _tickCounter;
        private long _lastTicks;                // game time at the last pass, for the elapsed months
        private const int ProcessInterval = 16; // process every 16 ticks to keep the cost down

        public override void OnAfterSimulationTick()
        {
            try
            {
                if (++_tickCounter < ProcessInterval) return;
                _tickCounter = 0;

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                // Elapsed months are measured between passes. The clock advances even with no
                // zones, so a newly created zone is not charged for the time before it existed.
                double deltaMonths = _lastTicks == 0 ? 0.0 : (nowTicks - _lastTicks) / (double)TicksPerMonth;
                _lastTicks = nowTicks;

                List<ContaminationZone> zones = ContaminationManager.Zones; // snapshot
                if (zones.Count == 0) return;

                // Walk backwards so removing by index stays valid.
                for (int i = zones.Count - 1; i >= 0; i--)
                {
                    ContaminationZone zone = zones[i];

                    if (MeltdownClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ExpiryYears))
                    {
                        ContaminationManager.ClearZone(zone);
                        ContaminationManager.RemoveZoneAt(i);
                        ModConfig.Log("zone expired (50y) and cleared");
                        continue;
                    }

                    if (deltaMonths > 0.0 && IsDecontaminationActive(zone))
                    {
                        DecontaminateZone(zone, i, deltaMonths); // 5% per month, relative
                        continue;
                    }

                    ContaminationManager.ReassertZone(zone); // hold it against the natural decay
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("threading error: " + e);
            }
        }

        /// <summary>
        /// Whether a decontamination facility (name contains Decontamination, completed and not
        /// destroyed) stands near the centre of the zone.
        /// </summary>
        private bool IsDecontaminationActive(ContaminationZone zone)
        {
            var bm = BuildingManager.instance;
            ushort[] grid = bm.m_buildingGrid;
            int gx = Mathf.Clamp((int)(zone.CenterX / 64f + 135f), 0, 269);
            int gz = Mathf.Clamp((int)(zone.CenterZ / 64f + 135f), 0, 269);
            // The building grid is 270x270. Even a huge contamination radius (a very high
            // output plant) never needs to scan more than the whole grid, so the radius is
            // capped. Without this it becomes tens of millions of empty iterations every
            // 16 ticks and the game freezes.
            const int gridMax = 270;
            long rawCellRadius = (long)(zone.Radius / 64f) + 1;
            int cellRadius = rawCellRadius > gridMax ? gridMax : (int)rawCellRadius;
            const Building.Flags dead = Building.Flags.Abandoned | Building.Flags.BurnedDown
                | Building.Flags.Collapsed | Building.Flags.Deleted;
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                int cz = gz + dz;
                if (cz < 0 || cz > 269) continue;
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int cx = gx + dx;
                    if (cx < 0 || cx > 269) continue;
                    int cell = cz * 270 + cx;
                    if (cell < 0 || cell >= grid.Length) continue;
                    ushort id = grid[cell];
                    int guard = 0;
                    while (id != 0 && guard++ < 32768)
                    {
                        var flags = bm.m_buildings.m_buffer[id].m_flags;
                        var info = bm.m_buildings.m_buffer[id].Info;
                        // Custom assets do not always get the Active flag, so the test is
                        // Completed plus not destroyed.
                        if (info != null && info.name != null &&
                            info.name.IndexOf(ModConfig.DecontaminationNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            (flags & Building.Flags.Completed) != Building.Flags.None &&
                            (flags & dead) == Building.Flags.None)
                        {
                            return true;
                        }
                        id = bm.m_buildings.m_buffer[id].m_nextGridBuilding;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Removes deltaMonths worth of contamination from the zone's float intensity (5% per
        /// month, relative) and writes the lowered value back over the grid. Because the factor
        /// is applied to a float, even very short intervals lose nothing to rounding and the
        /// decay is steady; once it reaches the floor the zone is removed.
        /// </summary>
        private void DecontaminateZone(ContaminationZone zone, int index, double deltaMonths)
        {
            double factor = Math.Pow(1.0 - ModConfig.DecontaminationMonthlyFraction, deltaMonths);
            if (factor < 0.0) factor = 0.0;
            if (factor > 1.0) factor = 1.0;

            zone.Intensity = (float)(zone.Intensity * factor);
            if (zone.Intensity <= ModConfig.DecontaminationMinIntensity)
            {
                ContaminationManager.ClearZone(zone);
                ContaminationManager.RemoveZoneAt(index);
                ModConfig.Log("zone decontaminated and removed");
            }
            else
            {
                ContaminationManager.SetZoneAt(index, zone); // store the lowered value in the ledger
                ContaminationManager.SetZone(zone);          // and write it over the grid
            }
        }
    }
}
