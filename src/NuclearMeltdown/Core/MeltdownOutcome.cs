namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Result drawn when a nuclear plant is destroyed: whether it explodes and at what scale,
    /// and whether it contaminates and at what scale. Scale is a multiplier where 1.0 is the
    /// current baseline contamination radius (700 m).
    /// </summary>
    public struct MeltdownOutcome
    {
        public bool Explode;
        public float ExplosionScale;
        public bool Contaminate;
        public float ContaminationScale;

        public MeltdownOutcome(bool explode, float explosionScale, bool contaminate, float contaminationScale)
        {
            Explode = explode;
            ExplosionScale = explosionScale;
            Contaminate = contaminate;
            ContaminationScale = contaminationScale;
        }
    }

    /// <summary>
    /// Probability table turning a 0-99 roll into an outcome.
    ///   5% (0-4)  : scale 10.0 nuclear explosion + scale 10.0 contamination
    ///  15% (5-19) : scale 5.5 nuclear explosion + scale 5.5 contamination
    ///  45% (20-64): scale 1.0 nuclear explosion + scale 1.0 contamination
    ///  30% (65-94): no explosion, scale 1.0 contamination only
    ///   5% (95-99): collapse only (no explosion, no contamination)
    /// </summary>
    public static class MeltdownOutcomeTable
    {
        /// <param name="roll">0-99 random number (values outside are clamped into 0-99).</param>
        public static MeltdownOutcome FromRoll(int roll)
        {
            if (roll < 0) roll = 0;
            if (roll > 99) roll = 99;

            if (roll < 5) return new MeltdownOutcome(true, 10.0f, true, 10.0f);
            if (roll < 20) return new MeltdownOutcome(true, 5.5f, true, 5.5f);
            if (roll < 65) return new MeltdownOutcome(true, 1.0f, true, 1.0f);
            if (roll < 95) return new MeltdownOutcome(false, 0f, true, 1.0f);
            return new MeltdownOutcome(false, 0f, false, 0f);
        }
    }
}
