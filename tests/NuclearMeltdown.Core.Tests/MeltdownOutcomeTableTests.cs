using NuclearMeltdown.Core;
using Xunit;

public class MeltdownOutcomeTableTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Rolls_0_to_4_give_scale10_explosion_and_contamination(int roll)
    {
        var o = MeltdownOutcomeTable.FromRoll(roll);
        Assert.True(o.Explode);
        Assert.Equal(10.0f, o.ExplosionScale);
        Assert.True(o.Contaminate);
        Assert.Equal(10.0f, o.ContaminationScale);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(19)]
    public void Rolls_5_to_19_give_scale55(int roll)
    {
        var o = MeltdownOutcomeTable.FromRoll(roll);
        Assert.True(o.Explode);
        Assert.Equal(5.5f, o.ExplosionScale);
        Assert.True(o.Contaminate);
        Assert.Equal(5.5f, o.ContaminationScale);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(64)]
    public void Rolls_20_to_64_give_scale10_base(int roll)
    {
        var o = MeltdownOutcomeTable.FromRoll(roll);
        Assert.True(o.Explode);
        Assert.Equal(1.0f, o.ExplosionScale);
        Assert.True(o.Contaminate);
        Assert.Equal(1.0f, o.ContaminationScale);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(94)]
    public void Rolls_65_to_94_give_contamination_only(int roll)
    {
        var o = MeltdownOutcomeTable.FromRoll(roll);
        Assert.False(o.Explode);
        Assert.True(o.Contaminate);
        Assert.Equal(1.0f, o.ContaminationScale);
    }

    [Theory]
    [InlineData(95)]
    [InlineData(99)]
    public void Rolls_95_to_99_give_collapse_only(int roll)
    {
        var o = MeltdownOutcomeTable.FromRoll(roll);
        Assert.False(o.Explode);
        Assert.False(o.Contaminate);
    }

    [Fact]
    public void Probability_buckets_sum_to_100()
    {
        int scale10 = 0, scale55 = 0, scale1 = 0, contamOnly = 0, collapseOnly = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            var o = MeltdownOutcomeTable.FromRoll(roll);
            if (o.Explode && o.ExplosionScale == 10.0f) scale10++;
            else if (o.Explode && o.ExplosionScale == 5.5f) scale55++;
            else if (o.Explode && o.ExplosionScale == 1.0f) scale1++;
            else if (!o.Explode && o.Contaminate) contamOnly++;
            else collapseOnly++;
        }
        Assert.Equal(5, scale10);
        Assert.Equal(15, scale55);
        Assert.Equal(45, scale1);
        Assert.Equal(30, contamOnly);
        Assert.Equal(5, collapseOnly);
    }
}
