using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>Mod-wide constants and shared logging.</summary>
    public static class ModConfig
    {
        public const string HarmonyId = "com.omone.nuclearmeltdown";
        public const float DefaultRadiusMeters = 700f;
        public const int ExpiryYears = 50;
        // Only a dedicated "Decontamination facility" building cleans up; a water treatment
        // plant does not. An operating building whose name contains the keyword below, near a
        // zone, removes DecontaminationMonthlyFraction of each cell's contamination per in-game
        // month (the same rule the nuclear missile mod uses).
        public const string DecontaminationNameKeyword = "Decontamination";
        public const float DecontaminationMonthlyFraction = 0.05f; // 5% removed per month (relative)
        public const byte DecontaminationMinIntensity = 5;          // cells at or below this drop to 0
        // Keywords identifying a nuclear plant live in NuclearMeltdown.Core.NuclearNameMatcher,
        // so they stay testable and cover names like "NPP".
        public const byte MaxPollution = 255;
        public const string LogPrefix = "[NuclearMeltdown] ";

        // Baseline radii for the explosion (crater plus area destruction) at scale 1.0.
        // The actual values are these multiplied by the scale.
        public const float CraterRadiusBase = 60f;          // crater radius
        public const float CraterDepthBase = 16f;           // crater depth
        // The crater radius and depth are deliberately uncapped - they grow with the scale.
        public const float RemoveRadiusBase = 60f;          // inner radius, everything destroyed
        public const float DestructionRadiusMinBase = 100f; // inner edge of the destruction falloff
        public const float DestructionRadiusMaxBase = 160f; // outer edge of the destruction falloff
        public const float BurnRadiusMaxBase = 200f;        // outer edge of the fires

        // Settings for the "scale from the plant's output" mode. Scale = output / ReferenceOutput,
        // directly proportional.
        // Note: m_electricityProduction is NOT the MW figure shown in the UI but an internal
        // unit - measured, an asset the UI calls 1280 MW reports 80000 (i.e. MW x 62.5). So the
        // baseline is 40000, the equivalent of the vanilla 640 MW plant (= scale 1.0), and a
        // 3200 MW class asset reports 200000, about scale 5.0.
        // There is no upper limit: an extreme output wiping out the map is accepted behaviour.
        public const int ReferenceOutput = 40000;           // output that means scale 1.0 (vanilla 640 MW)
        public const float OutputScaleMin = 0.1f;           // floor, so even a tiny plant does something
        public const float OutputScaleMax = float.MaxValue; // no ceiling
        // The contamination radius is uncapped too - the 700 m baseline times the scale is used
        // as-is. Only the destruction and fire radii handed to DisasterHelpers are rounded down
        // to a value that still covers the whole map. The map is about 17.3 km across, so
        // anything beyond this only costs scan time without looking any different (the map is
        // destroyed either way). This is a practical limit to stop the game from freezing.
        public const float EffectRadiusMax = 20000f;

        public static void Log(string msg)
        {
            Debug.Log(LogPrefix + msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(LogPrefix + msg);
        }
    }
}
