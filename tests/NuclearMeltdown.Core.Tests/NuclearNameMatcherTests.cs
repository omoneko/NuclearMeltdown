using NuclearMeltdown.Core;
using Xunit;

public class NuclearNameMatcherTests
{
    [Theory]
    // バニラ
    [InlineData("Nuclear Power Plant")]
    [InlineData("Nuclear Power Plant_Data")]
    // Workshop アセット（実際の info.name 形式: "<steamid>.<AssetName>_Data"）
    [InlineData("3569778758.Chernobyl NPP Units 3-4_Data")]
    [InlineData("Chernobyl NPP Units 3-4")]
    // 略語・別名・日本語
    [InlineData("Small Modular Reactor")]
    [InlineData("Atomic Power Station")]
    [InlineData("原子力発電所")]
    [InlineData("次世代原発")]
    // 大文字小文字を無視
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

    // ヘルパ: 明示キーワードで判定するラッパ
    private static Matcher NuclearMatcherWith(params string[] kws) => new Matcher(kws);

    private readonly struct Matcher
    {
        private readonly string[] _kws;
        public Matcher(string[] kws) { _kws = kws; }
        public bool Matches(string name) => NuclearNameMatcher.Matches(name, _kws);
    }
}
