using System;
using System.Collections.Generic;

namespace Bonehilda;

internal readonly struct TaskClaim
{
    internal readonly string Maid, Target, Kind;
    internal readonly bool Work;
    internal readonly long Started, Until;

    internal TaskClaim(string maid, string target, string kind, bool work, long started, long until)
    { Maid = maid; Target = target; Kind = kind; Work = work; Started = started; Until = until; }
}

internal static class TaskClaimPolicy
{
    internal static bool Prefer(long recentStart, float distance, long bestStart, float bestDistance) =>
        recentStart < bestStart || (recentStart == bestStart && distance < bestDistance);

    internal static TaskClaim? Winner(IEnumerable<TaskClaim> claims, string target, long now)
    {
        TaskClaim? winner = null;
        foreach (var claim in claims)
        {
            if (claim.Target != target || claim.Until <= now || claim.Started > now) continue;
            if (!winner.HasValue || Before(claim, winner.Value)) winner = claim;
        }
        return winner;
    }

    internal static bool Available(IEnumerable<TaskClaim> claims, string target, string maid, bool work, long now)
    {
        var winner = Winner(claims, target, now);
        return !winner.HasValue || winner.Value.Maid == maid || (work && !winner.Value.Work);
    }

    private static bool Before(TaskClaim a, TaskClaim b) => a.Work != b.Work ? a.Work
        : a.Started != b.Started ? a.Started < b.Started : string.CompareOrdinal(a.Maid, b.Maid) < 0;
}
