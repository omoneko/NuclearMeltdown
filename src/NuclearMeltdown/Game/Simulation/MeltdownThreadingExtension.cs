using System;
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game.Simulation
{
    /// <summary>
    /// 毎tickで汚染ゾーンを維持し、50年経過で解除する。専用の「Decontamination facility」建物が
    /// ゾーン付近で稼働している場合のみ、汚染をゲーム内1か月あたり5%（相対）除去する。
    /// <b>汚水処理場では除染されない</b>（核ミサイルMODと同一仕様に統一）。
    /// ゲームがModアセンブリ内のIThreadingExtension実装を自動検出して駆動する。
    /// </summary>
    public class MeltdownThreadingExtension : ThreadingExtensionBase
    {
        private static readonly long TicksPerMonth = TimeSpan.FromDays(30).Ticks; // ゲーム内1か月=30日相当

        private int _tickCounter;
        private long _lastTicks;                // 前回処理時のゲーム内時刻（除染の経過月算出用）
        private const int ProcessInterval = 16; // 16tickに1回処理（負荷軽減）

        public override void OnAfterSimulationTick()
        {
            try
            {
                if (++_tickCounter < ProcessInterval) return;
                _tickCounter = 0;

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                // 経過月は処理サイクル間で測る。ゾーンが無くても時刻は前進させる（新ゾーンに空白期間を課さない=P2対策）。
                double deltaMonths = _lastTicks == 0 ? 0.0 : (nowTicks - _lastTicks) / (double)TicksPerMonth;
                _lastTicks = nowTicks;

                List<ContaminationZone> zones = ContaminationManager.Zones; // スナップショット
                if (zones.Count == 0) return;

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

                    if (deltaMonths > 0.0 && IsDecontaminationActive(zone))
                    {
                        DecontaminateZone(zone, i, deltaMonths); // 5%/月 相対除去
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

        /// <summary>ゾーン中心付近に除染施設(名称に Decontamination を含む・完成/非破壊)が存在するか。</summary>
        private bool IsDecontaminationActive(ContaminationZone zone)
        {
            var bm = BuildingManager.instance;
            ushort[] grid = bm.m_buildingGrid;
            int gx = Mathf.Clamp((int)(zone.CenterX / 64f + 135f), 0, 269);
            int gz = Mathf.Clamp((int)(zone.CenterZ / 64f + 135f), 0, 269);
            int cellRadius = (int)(zone.Radius / 64f) + 1;
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
                        // カスタムアセットは Active が立たないことがあるため Completed＋非破壊で判定する。
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
        /// ゾーン濃度(float)を deltaMonths 分だけ相対除去（1か月で5%）し、下げた濃度をグリッドへ上書き反映する。
        /// float 濃度に係数を掛け続けるので微小間隔でも端数が失われず着実に減衰し、下限まで下がればゾーン除去。
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
                ContaminationManager.SetZoneAt(index, zone); // 台帳に下げた濃度を書き戻す
                ContaminationManager.SetZone(zone);          // グリッドへ上書き反映
            }
        }
    }
}
