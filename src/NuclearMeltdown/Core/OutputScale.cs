using System;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Turns a plant's power output into a disaster scale (no UnityEngine dependency).
    /// The scale is directly proportional to the output: scale = output / referenceOutput,
    /// so twice the output means twice the damage radius. Assets with outputs orders of
    /// magnitude larger (hundreds of millions) would push the radius far past the map and send
    /// DisasterHelpers into a spin, so the result is clamped by maxScale for safety.
    /// </summary>
    public static class OutputScale
    {
        /// <summary>m_electricityProduction of the vanilla nuclear power plant.</summary>
        public const int VanillaNuclearOutput = 640;

        /// <summary>
        /// Scale for the given output (electricity production), proportional to it.
        /// An output of zero or less yields minScale. The result is clamped to
        /// [minScale, maxScale].
        /// </summary>
        public static float FromOutput(int output, int referenceOutput, float minScale, float maxScale)
        {
            if (referenceOutput <= 0) referenceOutput = VanillaNuclearOutput;
            if (minScale < 0f) minScale = 0f;
            if (maxScale < minScale) maxScale = minScale;

            if (output <= 0) return minScale;

            float scale = output / (float)referenceOutput; // directly proportional
            if (scale < minScale) return minScale;
            if (scale > maxScale) return maxScale;
            return scale;
        }
    }
}
