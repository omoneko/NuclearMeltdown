using NuclearMeltdown.Core;
using Xunit;

public class OutputScaleTests
{
    const int Ref = OutputScale.VanillaNuclearOutput; // 640
    const float Min = 0.25f, Max = 5f;

    [Fact]
    public void Vanilla_output_is_scale_one()
    {
        Assert.Equal(1f, OutputScale.FromOutput(Ref, Ref, Min, Max), 3);
    }

    [Fact]
    public void Cube_root_relation()
    {
        // 出力8倍 -> cbrt(8)=2倍
        Assert.Equal(2f, OutputScale.FromOutput(Ref * 8, Ref, Min, Max), 3);
        // 出力1/8 -> 0.5倍
        Assert.Equal(0.5f, OutputScale.FromOutput(Ref / 8, Ref, Min, Max), 3);
    }

    [Fact]
    public void Clamped_to_min_and_max()
    {
        Assert.Equal(Max, OutputScale.FromOutput(Ref * 100000, Ref, Min, Max), 3); // 上限
        Assert.Equal(Min, OutputScale.FromOutput(1, Ref, Min, Max), 3);            // 下限
        Assert.Equal(Min, OutputScale.FromOutput(0, Ref, Min, Max), 3);            // 出力0
        Assert.Equal(Min, OutputScale.FromOutput(-5, Ref, Min, Max), 3);           // 負値
    }

    [Fact]
    public void Invalid_reference_falls_back_to_vanilla()
    {
        Assert.Equal(1f, OutputScale.FromOutput(Ref, 0, Min, Max), 3);
    }
}
