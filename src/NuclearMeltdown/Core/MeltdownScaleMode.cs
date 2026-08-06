namespace NuclearMeltdown.Core
{
    /// <summary>How the scale of the disaster is decided (chosen in the options).</summary>
    public enum MeltdownScaleMode
    {
        /// <summary>Draw from the probability table, as it has always worked.</summary>
        Random = 0,
        /// <summary>Derive the scale from the plant's power output (proportional).</summary>
        ByOutput = 1,
        /// <summary>Always the same multiplier (the options slider).</summary>
        Fixed = 2
    }

    /// <summary>
    /// Builds a MeltdownOutcome according to the mode (no UnityEngine dependency).
    /// Random keeps using the existing probability table; ByOutput and Fixed take the scale
    /// from the given value. The explosion and the contamination can be suppressed
    /// independently through allowExplosion / allowContamination, which is what makes
    /// "fallout but no blast" and "blast but no fallout" possible.
    /// </summary>
    public static class MeltdownOutcomeSelector
    {
        /// <param name="mode">How the scale is decided.</param>
        /// <param name="roll">0-99 random number, used by Random.</param>
        /// <param name="scale">Scale multiplier used by ByOutput and Fixed.</param>
        /// <param name="allowExplosion">False suppresses the explosion (crater and destruction) entirely.</param>
        /// <param name="allowContamination">False suppresses the fallout entirely.</param>
        public static MeltdownOutcome Select(MeltdownScaleMode mode, int roll, float scale,
            bool allowExplosion, bool allowContamination)
        {
            MeltdownOutcome o;
            switch (mode)
            {
                case MeltdownScaleMode.ByOutput:
                case MeltdownScaleMode.Fixed:
                    if (scale < 0f) scale = 0f;
                    o = new MeltdownOutcome(scale > 0f, scale, scale > 0f, scale);
                    break;
                default:
                    o = MeltdownOutcomeTable.FromRoll(roll);
                    break;
            }

            if (!allowExplosion)
            {
                o.Explode = false;
                o.ExplosionScale = 0f;
            }
            if (!allowContamination)
            {
                o.Contaminate = false;
                o.ContaminationScale = 0f;
            }
            return o;
        }
    }
}
