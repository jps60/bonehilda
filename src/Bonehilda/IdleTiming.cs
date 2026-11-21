using System;

namespace Bonehilda;

internal static class IdleTiming
{
    internal static float Delay(float interval, bool performed, float roll) =>
        (performed ? interval : Math.Min(3f, interval)) * (1f + .5f * roll);

    internal static long Next(long now, float interval, bool performed, float roll) =>
        now + (long)(Delay(interval, performed, roll) * TimeSpan.TicksPerSecond);

    // Saved timestamps must not retain a long delay after lowering the setting.
    internal static long ClampPending(long now, long pending, float interval) =>
        Math.Min(pending, Next(now, interval, true, 1f));
}
