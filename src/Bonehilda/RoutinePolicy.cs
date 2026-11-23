using System;

namespace Bonehilda;

internal static class RoutinePolicy
{
    internal static bool RepairSearchAllowed(float now, float retryAfter) => now >= retryAfter;
    internal static int Gesture(int count, int previous, float roll)
    {
        if (count <= 1) return 0;
        var choices = previous >= 0 && previous < count ? count - 1 : count;
        var index = Math.Min(choices - 1, Math.Max(0, (int)(roll * choices)));
        return previous >= 0 && previous < count && index >= previous ? index + 1 : index;
    }

    // Critical structure damage first, then depleted fuel/ammo. Ordinary chores
    // alternate in short bouts, instead of one endless repair or supply loop.
    internal static int WorkPriority(bool supply, bool criticalRepair, bool depleted, bool preferSupplies, bool fuel = false)
        => criticalRepair ? 0 : depleted ? 1 : fuel ? 2 : supply == preferSupplies ? 3 : 4;

    internal static bool NextPreference(bool supplied, int streak) => streak >= 3 ? !supplied : supplied;
    internal static float RestScore(float distanceSquared, bool favorite, bool revisit) => favorite && revisit ? distanceSquared * .15f : distanceSquared;
}
