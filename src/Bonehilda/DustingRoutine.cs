using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal enum DustingPattern { SweepThenReach, ReachThenSweep, SpotClean }

internal readonly struct DustingStroke
{
    internal readonly Vector3 Span;
    internal readonly float Cycles;
    internal readonly bool Forward;

    internal DustingStroke(Vector3 span, float cycles, bool forward)
    {
        Span = span;
        Cycles = cycles;
        Forward = forward;
    }
}

internal static class DustingRoutine
{
    private const string PatternKey = "BH_dustPattern", RemainingKey = "BH_dustRemaining";
    private const int AllPatterns = 0b111;
    internal const float WorkingSeconds = 2.90f;

    // Only the owner draws. Persist the bag and last choice across ownership changes
    // and game saves, including the boundary between two bags.
    internal static void Choose(ZDO data)
    {
        var previous = data.GetInt(PatternKey, -1);
        var lastBit = previous >= 0 && previous < 3 ? 1 << previous : 0;
        var remaining = data.GetInt(RemainingKey) & AllPatterns;
        if ((remaining & ~lastBit) == 0) remaining = AllPatterns;
        var choices = new List<int>(3);
        for (var i = 0; i < 3; i++)
            if ((remaining & ~lastBit & (1 << i)) != 0) choices.Add(i);
        var chosen = choices[Random.Range(0, choices.Count)];
        data.Set(PatternKey, chosen);
        data.Set(RemainingKey, remaining & ~(1 << chosen));
    }

    internal static DustingStroke At(ZDO data, float time, Vector3 sideways, Vector3 forward)
    {
        time = Mathf.Clamp(time, 0f, WorkingSeconds);
        switch (data.GetInt(PatternKey))
        {
            case (int)DustingPattern.ReachThenSweep:
                return time < 1.68f
                    ? new DustingStroke(forward, time / 1.68f, true)
                    : new DustingStroke(sideways, 1f + (time - 1.68f) / (WorkingSeconds - 1.68f), false);
            case (int)DustingPattern.SpotClean:
                if (time < .68f)
                    return new DustingStroke(Vector3.ClampMagnitude(sideways, .16f), time / .68f, false);
                if (time < 1.40f)
                    return new DustingStroke(Vector3.ClampMagnitude(forward, .13f), 1f + (time - .68f) / .72f, true);
                return new DustingStroke(sideways, 2f + (time - 1.40f) / (WorkingSeconds - 1.40f), false);
            default:
                const float cycle = WorkingSeconds * .5f;
                return new DustingStroke(time < cycle ? sideways : forward, time / cycle, time >= cycle);
        }
    }
}
