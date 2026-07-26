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
    public void Linear_relation()
    {
        // 出力に単純比例: 2倍 -> 2.0、4倍 -> 4.0
        Assert.Equal(2f, OutputScale.FromOutput(Ref * 2, Ref, Min, Max), 3);
        Assert.Equal(4f, OutputScale.FromOutput(Ref * 4, Ref, Min, Max), 3);
        // 出力1/2 -> 0.5
        Assert.Equal(0.5f, OutputScale.FromOutput(Ref / 2, Ref, Min, Max), 3);
    }

    [Fact]
    public void Clamped_to_min_and_max()
    {
        Assert.Equal(Max, OutputScale.FromOutput(Ref * 100000, Ref, Min, Max), 3); // 上限を渡せば効く
        Assert.Equal(Min, OutputScale.FromOutput(1, Ref, Min, Max), 3);            // 下限
        Assert.Equal(Min, OutputScale.FromOutput(0, Ref, Min, Max), 3);            // 出力0
        Assert.Equal(Min, OutputScale.FromOutput(-5, Ref, Min, Max), 3);           // 負値
    }

    [Fact]
    public void Unbounded_max_lets_huge_output_scale_freely()
    {
        // 上限なし(float.MaxValue)なら、出力に完全比例して青天井に伸びる
        Assert.Equal(1000f, OutputScale.FromOutput(640 * 1000, 640, 0.1f, float.MaxValue), 1);
        // 実在アセットの上限 3200MW は 5.0
        Assert.Equal(5f, OutputScale.FromOutput(3200, 640, 0.1f, float.MaxValue), 3);
    }

    [Fact]
    public void Invalid_reference_falls_back_to_vanilla()
    {
        Assert.Equal(1f, OutputScale.FromOutput(Ref, 0, Min, Max), 3);
    }
}
