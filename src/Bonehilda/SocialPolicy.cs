using System;
using System.Collections.Generic;

namespace Bonehilda;

internal enum SocialKind { None, Skeleton, Tame, Maid, Odin }

internal static class SocialPolicy
{
    internal static readonly Dictionary<SocialKind, string[]> Emotes = new Dictionary<SocialKind, string[]>
    {
        { SocialKind.Skeleton, new[] { "wave", "point" } },
        { SocialKind.Tame, new[] { "blowkiss", "loveyou" } },
        { SocialKind.Maid, new[] { "wave", "bow" } },
        { SocialKind.Odin, new[] { "bow", "kneel" } }
    };

    internal static SocialKind Classify(string prefab, bool maid, bool tame, bool player, bool enemy, bool dead, bool busy)
    {
        if (player || enemy || dead || busy) return SocialKind.None;
        if (maid) return SocialKind.Maid;
        if (prefab.IndexOf("skelet", StringComparison.OrdinalIgnoreCase) >= 0) return SocialKind.Skeleton;
        return tame ? SocialKind.Tame : SocialKind.None;
    }

    // No pursuit: a social visit is abandoned if its acquaintance wanders away.
    internal static bool InPlace(float movedSquared, float heightDifference) => movedSquared <= 1f && Math.Abs(heightDifference) <= .75f;
    internal static bool CoolingDown(double now, double until) => now < until;
}
