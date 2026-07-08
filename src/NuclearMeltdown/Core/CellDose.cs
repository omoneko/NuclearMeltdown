namespace NuclearMeltdown.Core
{
    /// <summary>汚染を適用する単一セル（グリッドindex）とその濃度(0-255)。</summary>
    public struct CellDose
    {
        public int Index;
        public byte Intensity;

        public CellDose(int index, byte intensity)
        {
            Index = index;
            Intensity = intensity;
        }
    }
}
