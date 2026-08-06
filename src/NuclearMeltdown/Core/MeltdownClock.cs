using System;

namespace NuclearMeltdown.Core
{
    /// <summary>Decides when a contamination zone has aged out, based on in-game time.</summary>
    public static class MeltdownClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int years)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddYears(years);
            return nowTicks >= expiry.Ticks;
        }
    }
}
