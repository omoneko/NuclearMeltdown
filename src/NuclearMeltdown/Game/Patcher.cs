using HarmonyLib;

namespace NuclearMeltdown.Game
{
    /// <summary>Applies and removes the Harmony patches.</summary>
    public static class Patcher
    {
        private static bool _patched;

        public static void PatchAll()
        {
            if (_patched) return;
            var harmony = new Harmony(ModConfig.HarmonyId);
            harmony.PatchAll(typeof(Patcher).Assembly);
            _patched = true;
            ModConfig.Log("Harmony patches applied");
        }

        public static void UnpatchAll()
        {
            if (!_patched) return;
            var harmony = new Harmony(ModConfig.HarmonyId);
            harmony.UnpatchAll(ModConfig.HarmonyId);
            _patched = false;
            ModConfig.Log("Harmony patches removed");
        }
    }
}
