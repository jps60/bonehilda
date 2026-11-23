namespace Bonehilda;

internal static class TameCrowdPolicy
{
    // Price extra walking time against a 1.5-second disruption penalty. Never
    // shoo until actual crowd immobility has exceeded the configured delay.
    internal static bool PreferShoo(float extraMeters, float speed, float stalled, float delay) =>
        stalled >= delay && (float.IsPositiveInfinity(extraMeters) || extraMeters / System.Math.Max(.5f, speed) > 1.5f);
    internal static bool WithinSegment(float remaining, float rejoin) => rejoin <= remaining + .1f;
}
