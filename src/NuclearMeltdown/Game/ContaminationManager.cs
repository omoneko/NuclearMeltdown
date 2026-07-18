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

        /// <summary>台帳内のゾーンを差し替える（除染で下げた濃度を書き戻す用）。</summary>
        public static void SetZoneAt(int index, ContaminationZone zone)
        {
            if (index >= 0 && index < _zones.Count) _zones[index] = zone;
        }

        /// <summary>float 濃度を土壌汚染セルの上限濃度(byte)へ丸める。</summary>
        private static byte ToByteIntensity(float intensity)
        {
            int v = (int)(intensity + 0.5f);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        /// <summary>汚染を維持する（自然減衰で下がったセルを zone.Intensity まで引き上げる）。変化があった時だけ再描画。</summary>
        public static void ReassertZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ApplyDose(doses[i]);
            if (changed) RefreshZoneTexture(zone); // 定常状態(無変化)では再描画しない＝オーバーレイの点滅を防ぐ
        }

        /// <summary>汚染を上書き設定する（除染で下げた濃度を反映）。変化があった時だけ再描画。</summary>
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
