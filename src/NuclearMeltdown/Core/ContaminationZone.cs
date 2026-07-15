namespace NuclearMeltdown.Core
{
    /// <summary>
    /// ワールド座標中心・半径(m)・発生ゲーム内時刻(Ticks)・現在濃度(0-255)の汚染ゾーン。
    /// Intensity は float（除染で連続的に低下し、微小な減衰も端数として蓄積される。書き込み時に整数へ丸める）。
    /// </summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;
        public float Intensity;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
            : this(centerX, centerZ, radius, startTicks, 255f)
        {
        }

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks, float intensity)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
            Intensity = intensity;
        }
    }
}
