using ColossalFramework;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>
    /// Persisted settings (scale mode, fixed multiplier, explosion and contamination on/off),
    /// stored through ColossalFramework's SavedInt.
    /// The settings file must NOT be named after the mod/assembly ("NuclearMeltdown"): an
    /// identical name collides with the mod's own key in the CS settings dictionary, which
    /// throws "an item with the same key already exists" and puts the game into a loop of
    /// deleting the settings file.
    /// </summary>
    public static class ModSettings
    {
        public const string FileName = "NuclearMeltdownSettings";

        // The scale-mode display names live in MeltdownStrings.ScaleModeLabels(). They were a
        // static readonly array here, which is built once at class load and would have kept
        // whatever language the game started in.

        // The fixed-scale slider covers 0.5 to 10.0, stored as an integer times ten (5 to 100).
        public const int FixedScaleMin = 5;
        public const int FixedScaleMax = 100;
        public const int FixedScaleDefault = 10; // = 1.0

        private static SavedInt _scaleMode;
        private static SavedInt _fixedScaleX10;
        private static SavedInt _explosionEnabled;
        private static SavedInt _contaminationEnabled;

        // The settings file is registered exactly once. Ensure() runs from every getter, and
        // calling AddSettingsFile each time makes CS throw "same key" internally, then **delete
        // the settings file** and recreate it empty - a loop in which the player's options are
        // never saved (found while working on Siren Alert).
        private static bool _fileRegistered;

        public static void Ensure()
        {
            if (!_fileRegistered)
            {
                _fileRegistered = true; // do not retry, even if it threw
                try
                {
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = FileName });
                }
                catch (System.Exception e)
                {
                    ModConfig.LogError("AddSettingsFile(" + FileName + "): " + e.Message);
                }
            }
            if (_scaleMode == null) _scaleMode = new SavedInt("scaleMode", FileName, (int)MeltdownScaleMode.Random, true);
            if (_fixedScaleX10 == null) _fixedScaleX10 = new SavedInt("fixedScaleX10", FileName, FixedScaleDefault, true);
            if (_explosionEnabled == null) _explosionEnabled = new SavedInt("explosionEnabled", FileName, 1, true);       // on by default
            if (_contaminationEnabled == null) _contaminationEnabled = new SavedInt("contaminationEnabled", FileName, 1, true); // on by default
        }

        public static SavedInt ScaleModeSetting { get { Ensure(); return _scaleMode; } }
        public static SavedInt FixedScaleSetting { get { Ensure(); return _fixedScaleX10; } }
        public static SavedInt ExplosionEnabledSetting { get { Ensure(); return _explosionEnabled; } }
        public static SavedInt ContaminationEnabledSetting { get { Ensure(); return _contaminationEnabled; } }

        /// <summary>How the scale of the disaster is decided.</summary>
        public static MeltdownScaleMode ScaleMode
        {
            get
            {
                int v = ScaleModeSetting.value;
                if (v < 0 || v > (int)MeltdownScaleMode.Fixed) v = (int)MeltdownScaleMode.Random;
                return (MeltdownScaleMode)v;
            }
        }

        /// <summary>Multiplier used by the Fixed mode (0.5 to 10.0).</summary>
        public static float FixedScale { get { return FixedScaleSetting.value / 10f; } }

        /// <summary>Whether the explosion (crater and area destruction) happens at all.</summary>
        public static bool ExplosionEnabled { get { return ExplosionEnabledSetting.value != 0; } }

        /// <summary>Whether fallout (ground contamination) happens at all.</summary>
        public static bool ContaminationEnabled { get { return ContaminationEnabledSetting.value != 0; } }
    }
}
