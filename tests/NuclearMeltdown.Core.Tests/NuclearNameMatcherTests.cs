using NuclearMeltdown.Core;
using Xunit;

public class NuclearNameMatcherTests
{
    [Theory]
    // Vanilla
    [InlineData("Nuclear Power Plant")]
    [InlineData("Nuclear Power Plant_Data")]
    // Workshop assets (the real info.name format is "<steamid>.<AssetName>_Data")
    [InlineData("3569778758.Chernobyl NPP Units 3-4_Data")]
    [InlineData("Chernobyl NPP Units 3-4")]
    // Abbreviations and alternative wordings
    [InlineData("Small Modular Reactor")]
    [InlineData("Atomic Power Station")]
    // Japanese-named assets: "nuclear power plant" and "next-generation nuclear plant".
    // Workshop authors name assets in their own language, so these must match too.
    [InlineData("原子力発電所")]
    [InlineData("次世代原発")]
    // Case is ignored
    [InlineData("chernobyl npp")]
    [InlineData("NUCLEAR reactor")]
    public void Matches_nuclear_names(string name)
    {
        Assert.True(NuclearNameMatcher.Matches(name));
    }

    [Theory]
    [InlineData("Coal Power Plant")]
    [InlineData("Wind Turbine")]
    [InlineData("Solar Power Plant")]
    [InlineData("Hydro Power Plant")]
    [InlineData("Oil Power Plant")]
    [InlineData("")]
    [InlineData(null)]
    public void Does_not_match_non_nuclear(string name)
    {
        Assert.False(NuclearNameMatcher.Matches(name));
    }

    [Fact]
    public void Custom_keywords_are_honored()
    {
        Assert.True(NuclearMatcherWith("Fukushima").Matches("Fukushima Daiichi"));
        Assert.False(NuclearMatcherWith("Fukushima").Matches("Chernobyl NPP"));
    }

    // Helper: a wrapper that tests against an explicit keyword list
    private static Matcher NuclearMatcherWith(params string[] kws) => new Matcher(kws);

    private readonly struct Matcher
    {
        private readonly string[] _kws;
        public Matcher(string[] kws) { _kws = kws; }
        public bool Matches(string name) => NuclearNameMatcher.Matches(name, _kws);
    }
}
