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
    // Authored beats: gather the hand, guide away, relax, then a shorter second
    // invitation. Unequal spacing avoids a metronomic back-and-forth wave.
    private static readonly float[] Times = { 0f, .28f, .50f, .87f, 1.13f, 1.34f, 1.68f, 1.92f, 2.4f };
    private static readonly float[] Reach = { .30f, .48f, .60f, .87f, .53f, .60f, .79f, .55f, .30f };
    private static readonly float[] Lift = { -.42f, -.24f, -.13f, -.28f, -.20f, -.15f, -.31f, -.27f, -.42f };
    private static readonly float[] Wrist = { -.3f, -.6f, -1f, .85f, -.45f, -.8f, .60f, -.25f, -.3f };
    internal static ShooPose Sample(int variant, float seconds)
    {
        if (variant < 0 || variant > 2 || seconds <= 0 || seconds >= Seconds)
            return new ShooPose(0, 0, 0, 0, 0, false, false);
        var blend = Math.Min(1, Math.Min(seconds / .30f, (Seconds - seconds) / .40f));
        blend = blend * blend * (3 - 2 * blend);
        // Right-hand delivery is a little quicker; both arms make a wider,
        // lower ushering gesture. Left/right are not identical mirrored loops.
        var time = variant == 1 ? Math.Min(Seconds, seconds * 1.07f) : seconds;
        return new ShooPose(blend, At(Reach, time) * (variant == 2 ? .91f : 1f),
            At(Lift, time) - (variant == 2 ? .06f : 0f),
            (variant == 2 ? .72f : .66f) + .10f * At(Wrist, time),
            At(Wrist, time), variant != 1, variant != 0);
    }

    private static float At(float[] values, float time)
    {
        for (var i = 1; i < Times.Length; i++)
        {
            if (time > Times[i]) continue;
            var t = Math.Max(0, Math.Min(1, (time - Times[i - 1]) / (Times[i] - Times[i - 1])));
            t = t * t * (3 - 2 * t);
            return values[i - 1] + (values[i] - values[i - 1]) * t;
        }
        return values[values.Length - 1];
    }
}
