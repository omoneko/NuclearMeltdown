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

        public static bool ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        /// <summary>セルの汚染を dose.Intensity に上書き設定する（除染で濃度を下げる用）。実際に書き換えたら true。</summary>
        public static bool SetDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution != dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        public static bool ClearCell(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return false;
            if (arr[index].m_pollution != 0)
            {
                arr[index].m_pollution = 0;
                return true;
            }
            return false;
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
