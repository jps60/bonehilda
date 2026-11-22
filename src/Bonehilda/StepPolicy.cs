using System;

namespace Bonehilda;

internal static class StepPolicy
{
    internal static bool Walk(float height, float normalY) => height >= .05f && height <= .4f && normalY >= .65f;
    internal static bool Jump(float height, float normalY) => height > .4f && height <= .65f && normalY >= .65f;
    internal static float Rise(float t) => Smooth(t / .35f);
    internal static float Forward(float t) => Smooth((t - .35f) / .65f);
    internal static float Duration(float distance, float speed) => Math.Max(.25f, Math.Min(.65f, .14f + distance / Math.Max(1.5f, speed)));
    internal static float JumpSpeed(float height, float gravity) => (float)Math.Sqrt(2f * Math.Max(1f, gravity) * (height + .18f));
    private static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
}
