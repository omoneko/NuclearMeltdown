using NuclearMeltdown.Core;
using Xunit;

public class SmokeTest
{
    [Fact]
    public void CellDose_stores_fields()
    {
        var d = new CellDose(5, 200);
        Assert.Equal(5, d.Index);
        Assert.Equal((byte)200, d.Intensity);
    }

    [Fact]
    public void ContaminationZone_stores_fields()
    {
        var z = new ContaminationZone(10f, 20f, 700f, 123L);
        Assert.Equal(10f, z.CenterX);
        Assert.Equal(20f, z.CenterZ);
        Assert.Equal(700f, z.Radius);
        Assert.Equal(123L, z.StartTicks);
    }
}
