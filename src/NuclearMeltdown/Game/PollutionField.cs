using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>NaturalResourceManager の土壌汚染セルへの読み書きラッパ。</summary>
    public static class PollutionField
    {
        public static byte GetPollution(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return 0;
            return arr[index].m_pollution;
        }

        public static void ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
            }
        }

        public static void ClearCell(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return;
            arr[index].m_pollution = 0;
        }

        public static void ReducePollution(int index, int step)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return;
            int v = arr[index].m_pollution - step;
            arr[index].m_pollution = (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
        }

        /// <summary>汚染テクスチャを更新（cellX/cellZ範囲）。</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
