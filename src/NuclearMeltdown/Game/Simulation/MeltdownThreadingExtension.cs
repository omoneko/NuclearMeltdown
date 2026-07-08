using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game.Simulation
{
    /// <summary>
    /// 毎tickで汚染ゾーンを維持し、50年経過または除染施設稼働で解除する。
    /// ゲームがModアセンブリ内のIThreadingExtension実装を自動検出して駆動する。
    /// </summary>
    public class MeltdownThreadingExtension : ThreadingExtensionBase
    {
        private int _tickCounter;
        private const int ProcessInterval = 16; // 16tickに1回処理（負荷軽減）

        public override void OnAfterSimulationTick()
        {
            try
            {
                if (++_tickCounter < ProcessInterval) return;
                _tickCounter = 0;

                List<ContaminationZone> zones = ContaminationManager.Zones; // スナップショット
                if (zones.Count == 0) return;

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;

                // 後ろから走査してインデックス除去に対応
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

                    if (IsDecontaminationActive(zone))
                    {
                        DecontaminateZone(zone, i);
                        continue;
                    }

                    ContaminationManager.ReassertZone(zone); // 自然減衰対策で維持
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("threading error: " + e);
            }
        }

        /// <summary>ゾーン中心付近に除染対象建物(既定:下水処理施設)が稼働中か。</summary>
        private bool IsDecontaminationActive(ContaminationZone zone)
        {
            var bm = BuildingManager.instance;
            ushort[] grid = bm.m_buildingGrid;
            // ゾーン半径をカバーするビルディンググリッドセル範囲を走査
            int gx = Mathf.Clamp((int)(zone.CenterX / 64f + 135f), 0, 269);
            int gz = Mathf.Clamp((int)(zone.CenterZ / 64f + 135f), 0, 269);
            int cellRadius = (int)(zone.Radius / 64f) + 1;
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
                        var info = bm.m_buildings.m_buffer[id].Info;
                        if (info != null && info.name != null &&
                            info.name.Contains(ModConfig.DecontaminationNameKeyword) &&
                            (bm.m_buildings.m_buffer[id].m_flags & Building.Flags.Active) != Building.Flags.None)
                        {
                            return true;
                        }
                        id = bm.m_buildings.m_buffer[id].m_nextGridBuilding;
                    }
                }
            }
            return false;
        }

        private void DecontaminateZone(ContaminationZone zone, int index)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            bool anyRemaining = false;
            for (int i = 0; i < doses.Count; i++)
            {
                PollutionField.ReducePollution(doses[i].Index, 8); // 徐々に除去
                if (PollutionField.GetPollution(doses[i].Index) > 0) anyRemaining = true;
            }
            // テクスチャ更新
            ContaminationManager.RefreshZoneTexture(zone);
            if (!anyRemaining)
            {
                ContaminationManager.RemoveZoneAt(index);
                ModConfig.Log("zone decontaminated and removed");
            }
        }
    }
}
