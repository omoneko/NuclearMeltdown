using System.Collections.Generic;
using System.IO;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Serialises the contamination zone ledger to and from byte[] for the save game.
    /// v1 carried no intensity (it is restored as 255); v2 added intensity as a float.
    /// Both are read, so old saves keep working.
    /// </summary>
    public static class ZoneSerializer
    {
        public const byte Version = 2; // v2 added Intensity (float)

        public static byte[] Serialize(List<ContaminationZone> zones)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(zones.Count);
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    w.Write(z.CenterX);
                    w.Write(z.CenterZ);
                    w.Write(z.Radius);
                    w.Write(z.StartTicks);
                    w.Write(z.Intensity); // float
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static List<ContaminationZone> Deserialize(byte[] data)
        {
            var result = new List<ContaminationZone>();
            if (data == null || data.Length < 5) return result;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != 1 && version != 2) return new List<ContaminationZone>();
                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        float cx = r.ReadSingle();
                        float cz = r.ReadSingle();
                        float radius = r.ReadSingle();
                        long start = r.ReadInt64();
                        float intensity = version >= 2 ? r.ReadSingle() : 255f; // v1 restores at full intensity
                        result.Add(new ContaminationZone(cx, cz, radius, start, intensity));
                    }
                }
            }
            catch
            {
                return new List<ContaminationZone>(); // corrupt data yields nothing
            }
            return result;
        }
    }
}
