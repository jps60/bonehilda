using System;

namespace Bonehilda;

internal readonly struct ShooPose
{
    internal readonly float Blend, Reach, Lift, Spread, Wrist;
    internal readonly bool Left, Right;
    internal ShooPose(float blend, float reach, float lift, float spread, float wrist, bool left, bool right)
    { Blend = blend; Reach = reach; Lift = lift; Spread = spread; Wrist = wrist; Left = left; Right = right; }
}

internal static class ShooMotion
{
    internal const float Seconds = 2.4f;
    internal static ShooPose Sample(int variant, float seconds)
    {
        if (variant < 0 || variant > 2 || seconds <= 0 || seconds >= Seconds)
            return new ShooPose(0, 0, 0, 0, 0, false, false);
        var blend = Math.Min(1, Math.Min(seconds / .30f, (Seconds - seconds) / .40f));
        blend = blend * blend * (3 - 2 * blend);
        // Two relaxed forward flicks, with a slower preparation and soft return.
        var cycle = Math.Max(0, Math.Min(1, (seconds - .30) / 1.70));
        var wave = (float)Math.Sin(cycle * Math.PI * 4 - Math.PI / 2);
        return new ShooPose(blend, .65f + .18f * wave, -.16f + .04f * wave,
            variant == 2 ? .66f : .78f, wave, variant != 1, variant != 0);
    }
}
