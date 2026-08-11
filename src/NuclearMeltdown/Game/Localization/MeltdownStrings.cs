namespace NuclearMeltdown.Game
{
    /// <summary>
    /// Every player-facing string, as a public static field whose initializer is the built-in
    /// English default.
    ///
    /// How localization works:
    ///  - The field name is the key in Locales/&lt;lang&gt;.txt, e.g. "Options_ScaleMode = ...".
    ///  - LocaleLoader.EnsureLoaded() detects the game language and overwrites these fields by
    ///    reflection from the matching file. A missing file or an unknown key leaves the English
    ///    default in place, so a half-finished translation is always safe to ship.
    ///  - UI code reads MeltdownStrings.Xxx instead of a literal.
    ///
    /// Nothing here may be copied into a `static readonly string[]`: that array would be built
    /// once at class load and keep the language it was built in. Label arrays are methods below.
    ///
    /// Log messages are deliberately NOT here. Logs should stay grep-able in English, and a bug
    /// report is far easier to read when the log says the same thing whoever sent it.
    ///
    /// To add a language: copy Locales/en.txt to Locales/&lt;code&gt;.txt using the code the game
    /// reports (de, fr, es, zh, ja, ...), translate the values, and open a pull request at
    /// https://github.com/omoneko/NuclearMeltdown - or just drop the file in the mod folder.
    /// </summary>
    public static class MeltdownStrings
    {
        // --- Content Manager -------------------------------------------------------------------
        public static string Mod_Description =
            "When a nuclear power plant burns down or collapses, it unleashes an explosion and " +
            "widespread radioactive contamination (ground pollution). The contamination clears " +
            "after 50 in-game years or when a decontamination facility is running nearby.";

        // --- Disaster scale ----------------------------------------------------------------------
        public static string Options_ScaleGroup = "Disaster scale";
        public static string Options_ScaleMode = "Scale mode";
        public static string ScaleMode_Random = "Random (probability table)";
        public static string ScaleMode_Output = "Based on plant output";
        public static string ScaleMode_Fixed = "Fixed scale";
        public static string Options_ScaleHelp =
            "Random: the original probability table (5% huge / 15% large / 45% normal / 30% " +
            "fallout only / 5% collapse only).  Based on plant output: scale is directly " +
            "proportional to the plant's power output, with a vanilla nuclear plant (640 MW) = " +
            "1.0, a 1280 MW reactor = 2.0 and a 3200 MW reactor = 5.0 - twice the output means " +
            "twice the blast radius. There is no upper limit: a monstrous reactor really can " +
            "wipe out the map.  Fixed scale: always use the multiplier below.";
        public static string Options_FixedScale = "Fixed scale x10 (10 = 1.0)";

        // --- What happens --------------------------------------------------------------------------
        public static string Options_EffectsGroup = "What happens on meltdown";
        public static string Options_Explosion = "Explosion (crater and blast damage)";
        public static string Options_Contamination = "Radioactive fallout (ground contamination)";
        public static string Options_EffectsHelp =
            "Turn either off independently - e.g. fallout only (no explosion), or explosion only " +
            "(no contamination). With both off, the plant simply collapses.";

        /// <summary>
        /// The scale-mode dropdown, indexed by the MeltdownScaleMode value. A method rather than
        /// a static array so it is rebuilt in the current language every time.
        /// </summary>
        public static string[] ScaleModeLabels()
        {
            return new[] { ScaleMode_Random, ScaleMode_Output, ScaleMode_Fixed };
        }
    }
}
