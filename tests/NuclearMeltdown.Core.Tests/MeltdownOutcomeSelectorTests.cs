using NuclearMeltdown.Core;
using Xunit;

public class MeltdownOutcomeSelectorTests
{
    [Fact]
    public void Random_mode_matches_probability_table()
    {
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Random, 0, 99f, true, true);
        var t = MeltdownOutcomeTable.FromRoll(0);
        Assert.Equal(t.Explode, o.Explode);
        Assert.Equal(t.ExplosionScale, o.ExplosionScale, 3);
        Assert.Equal(t.Contaminate, o.Contaminate);
        Assert.Equal(t.ContaminationScale, o.ContaminationScale, 3);
    }

    [Fact]
    public void ByOutput_and_Fixed_use_given_scale()
    {
        foreach (var mode in new[] { MeltdownScaleMode.ByOutput, MeltdownScaleMode.Fixed })
        {
            var o = MeltdownOutcomeSelector.Select(mode, 99, 2.5f, true, true);
            Assert.True(o.Explode);
            Assert.Equal(2.5f, o.ExplosionScale, 3);
            Assert.True(o.Contaminate);
            Assert.Equal(2.5f, o.ContaminationScale, 3);
        }
    }

    [Fact]
    public void Contamination_can_be_disabled()
    {
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Fixed, 0, 3f, true, false);
        Assert.True(o.Explode);              // 爆発は残る
        Assert.Equal(3f, o.ExplosionScale, 3);
        Assert.False(o.Contaminate);         // 降下物なし
        Assert.Equal(0f, o.ContaminationScale, 3);
    }

    [Fact]
    public void Explosion_can_be_disabled_contamination_only()
    {
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Fixed, 0, 3f, false, true);
        Assert.False(o.Explode);             // 爆発なし
        Assert.Equal(0f, o.ExplosionScale, 3);
        Assert.True(o.Contaminate);          // 汚染のみ
        Assert.Equal(3f, o.ContaminationScale, 3);
    }

    [Fact]
    public void Both_disabled_means_collapse_only()
    {
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Fixed, 0, 3f, false, false);
        Assert.False(o.Explode);
        Assert.False(o.Contaminate);
    }

    [Fact]
    public void Random_mode_also_respects_toggles()
    {
        // roll=0 は本来 explode+contaminate だが、両方OFFなら倒壊のみになる
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Random, 0, 0f, false, false);
        Assert.False(o.Explode);
        Assert.False(o.Contaminate);
    }

    [Fact]
    public void Zero_scale_yields_nothing()
    {
        var o = MeltdownOutcomeSelector.Select(MeltdownScaleMode.Fixed, 0, 0f, true, true);
        Assert.False(o.Explode);
        Assert.False(o.Contaminate);
    }
}
