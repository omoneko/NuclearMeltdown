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
        // Directly proportional: twice the output is 2.0, four times is 4.0.
        Assert.Equal(2f, OutputScale.FromOutput(Ref * 2, Ref, Min, Max), 3);
        Assert.Equal(4f, OutputScale.FromOutput(Ref * 4, Ref, Min, Max), 3);
        // Half the output is 0.5.
        Assert.Equal(0.5f, OutputScale.FromOutput(Ref / 2, Ref, Min, Max), 3);
    }

    [Fact]
    public void Clamped_to_min_and_max()
    {
        Assert.Equal(Max, OutputScale.FromOutput(Ref * 100000, Ref, Min, Max), 3); // a ceiling applies when one is given
        Assert.Equal(Min, OutputScale.FromOutput(1, Ref, Min, Max), 3);            // the floor
        Assert.Equal(Min, OutputScale.FromOutput(0, Ref, Min, Max), 3);            // no output
        Assert.Equal(Min, OutputScale.FromOutput(-5, Ref, Min, Max), 3);           // negative output
    }

    [Fact]
    public void Unbounded_max_lets_huge_output_scale_freely()
    {
        // With no ceiling (float.MaxValue) the scale follows the output without limit.
        Assert.Equal(1000f, OutputScale.FromOutput(640 * 1000, 640, 0.1f, float.MaxValue), 1);
        // 3200 MW, about the largest output found in real assets, comes out at 5.0.
        Assert.Equal(5f, OutputScale.FromOutput(3200, 640, 0.1f, float.MaxValue), 3);
    }

    [Fact]
    public void Invalid_reference_falls_back_to_vanilla()
    {
        Assert.Equal(1f, OutputScale.FromOutput(Ref, 0, Min, Max), 3);
    }
}
