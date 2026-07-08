using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Nuclear Meltdown";
        public string Description => "原子力発電所の全焼/崩壊時に爆発と広範囲の放射能汚染（土壌汚染）を発生させます。汚染はゲーム内50年経過または除染施設で消滅します。";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => ModConfig.Log("enabled (patches applied in Task 6)"));
        }

        public void OnDisabled() { }
    }
}
