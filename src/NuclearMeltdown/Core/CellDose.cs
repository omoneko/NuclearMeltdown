namespace NuclearMeltdown.Core
{
    /// <summary>One grid cell (by index) to contaminate, and the intensity to apply (0-255).</summary>
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
