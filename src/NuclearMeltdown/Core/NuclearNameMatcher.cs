using System;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Decides from its name whether a building is a nuclear power plant. Besides the vanilla
    /// "Nuclear Power Plant" this also picks up Workshop assets that spell it differently
    /// (for instance "Chernobyl NPP Units 3-4"). Case-insensitive. Depends on no game types,
    /// so it is unit testable.
    /// </summary>
    public static class NuclearNameMatcher
    {
        /// <summary>
        /// Default keywords: a name containing any of them counts as a nuclear plant.
        /// Narrowing by AI type (PowerPlantAI) is the caller's job.
        /// </summary>
        public static readonly string[] DefaultKeywords =
        {
            "Nuclear",  // vanilla "Nuclear Power Plant"
            "NPP",      // short for Nuclear Power Plant, common in Workshop assets
            "Reactor",  // reactor-themed assets
            "Atom",     // "Atomic ..." names
            // Japanese for "nuclear power" and "nuclear plant". Kept as-is on purpose:
            // Workshop assets are often named in the author's own language, and these two
            // words cover the Japanese-named plants.
            "原子力",
            "原発",
        };

        /// <summary>Tests against the default keywords.</summary>
        public static bool Matches(string name)
        {
            return Matches(name, DefaultKeywords);
        }

        /// <summary>Tests against the given keywords (any one match wins). Case-insensitive.</summary>
        public static bool Matches(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name) || keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                string kw = keywords[i];
                if (!string.IsNullOrEmpty(kw) &&
                    name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
