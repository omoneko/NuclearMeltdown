using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Nuclear Meltdown";
        public string Description => "When a nuclear power plant burns down or collapses, it unleashes an explosion and widespread radioactive contamination (ground pollution). The contamination clears after 50 in-game years or when a decontamination facility is running nearby.";

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                ModSettings.Ensure();

                UIHelperBase g = helper.AddGroup("Disaster scale");
                g.AddDropdown("Scale mode", ModSettings.ScaleModeNames, ModSettings.ScaleModeSetting.value,
                    i => ModSettings.ScaleModeSetting.value = i);
                g.AddButton(
                    "Random: the original probability table (5% huge / 15% large / 45% normal / 30% fallout only / 5% collapse only).  " +
                    "Based on plant output: scale is directly proportional to output (output / 640), so a vanilla nuclear " +
                    "plant = 1.0, a 3200 MW reactor = 5.0, and twice the output means twice the blast radius. " +
                    "There is no upper limit - a monstrous reactor really can wipe out the map.  " +
                    "Fixed scale: always use the multiplier below.",
                    () => { });
                g.AddSlider("Fixed scale x10 (10 = 1.0)", ModSettings.FixedScaleMin, ModSettings.FixedScaleMax, 1,
                    ModSettings.FixedScaleSetting.value,
                    v => ModSettings.FixedScaleSetting.value = (int)v);

                UIHelperBase e = helper.AddGroup("What happens on meltdown");
                e.AddCheckbox("Explosion (crater and blast damage)", ModSettings.ExplosionEnabled,
                    b => ModSettings.ExplosionEnabledSetting.value = b ? 1 : 0);
                e.AddCheckbox("Radioactive fallout (ground contamination)", ModSettings.ContaminationEnabled,
                    b => ModSettings.ContaminationEnabledSetting.value = b ? 1 : 0);
                e.AddButton(
                    "Turn either off independently - e.g. fallout only (no explosion), or explosion only (no contamination). " +
                    "With both off, the plant simply collapses.",
                    () => { });
            }
            catch (System.Exception ex)
            {
                ModConfig.LogError("OnSettingsUI error: " + ex);
            }
        }

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
