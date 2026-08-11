using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    public class Mod : IUserMod
    {
        // The mod's name is its Workshop title, so it stays in English; everything else is
        // localizable. The getter loads the locale because the Content Manager can read this
        // before the options screen has ever been opened.
        public string Name => "Nuclear Meltdown";
        public string Description
        {
            get { LocaleLoader.EnsureLoaded(); return MeltdownStrings.Mod_Description; }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                LocaleLoader.EnsureLoaded();
                ModSettings.Ensure();

                UIHelperBase g = helper.AddGroup(MeltdownStrings.Options_ScaleGroup);
                g.AddDropdown(MeltdownStrings.Options_ScaleMode, MeltdownStrings.ScaleModeLabels(),
                    ModSettings.ScaleModeSetting.value,
                    i => ModSettings.ScaleModeSetting.value = i);
                g.AddButton(MeltdownStrings.Options_ScaleHelp, () => { });
                g.AddSlider(MeltdownStrings.Options_FixedScale, ModSettings.FixedScaleMin, ModSettings.FixedScaleMax, 1,
                    ModSettings.FixedScaleSetting.value,
                    v => ModSettings.FixedScaleSetting.value = (int)v);

                UIHelperBase e = helper.AddGroup(MeltdownStrings.Options_EffectsGroup);
                e.AddCheckbox(MeltdownStrings.Options_Explosion, ModSettings.ExplosionEnabled,
                    b => ModSettings.ExplosionEnabledSetting.value = b ? 1 : 0);
                e.AddCheckbox(MeltdownStrings.Options_Contamination, ModSettings.ContaminationEnabled,
                    b => ModSettings.ContaminationEnabledSetting.value = b ? 1 : 0);
                e.AddButton(MeltdownStrings.Options_EffectsHelp, () => { });
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
