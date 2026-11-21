using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class IdleActivities
{
    // Retain the existing key so upgrades and dismissals preserve the cooldown.
    internal const string NextVisitKey = "BH_nextDustVisit";
    private readonly Housekeeping dust = new Housekeeping();
    private readonly TrophyVisits trophy = new TrophyVisits();
    private readonly ChairVisits chair = new ChairVisits();
    private int active;

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, List<Piece> pieces, float delta)
    {
        var data = maid.View.GetZDO();
        var now = ZNet.instance.GetTime().Ticks;
        if (active == 0)
        {
            var next = data.GetLong(NextVisitKey);
            if (next == 0) { Schedule(data); return false; }
            if (now < next) return false;
            var available = dust.TryBegin(maid, ai, home, pieces) ? IdleDeck.Dust : 0;
            if (Plugin.TrophyReactions.Value && trophy.TryBegin(maid, home, pieces)) available |= IdleDeck.Trophy;
            if (Plugin.IdleSitting.Value && chair.TryBegin(maid, home, pieces)) available |= IdleDeck.Sit;
            var remaining = data.GetInt("BH_idleDeck");
            active = IdleDeck.Choose(available, ref remaining, data.GetInt("BH_lastIdle"), Random.value);
            data.Set("BH_idleDeck", remaining);
            if (active != 0) data.Set("BH_lastIdle", active);
            Schedule(data);
            if (active != IdleDeck.Dust) dust.Cancel(maid.View);
            if (active != IdleDeck.Trophy) trophy.Cancel(maid);
            if (active != IdleDeck.Sit) chair.Cancel(maid);
            if (active == 0) return false;
        }
        var running = active == IdleDeck.Dust ? dust.Tick(maid, ai, home, delta)
            : active == IdleDeck.Trophy ? Plugin.TrophyReactions.Value && trophy.Tick(maid, ai, home, delta)
            : Plugin.IdleSitting.Value && chair.Tick(maid, ai, home, delta);
        if (running) return true;
        Cancel(maid);
        return false;
    }

    internal void Cancel(Maid maid)
    {
        dust.Cancel(maid.View);
        trophy.Cancel(maid);
        chair.Cancel(maid);
        maid.GetComponent<WorkAnimations>().StopEmote();
        if (active != 0)
        {
            if (maid.View && maid.View.IsValid() && maid.View.IsOwner()) Schedule(maid.View.GetZDO());
            maid.StopNavigation();
        }
        active = 0;
    }

    private static void Schedule(ZDO data) => data.Set(NextVisitKey, ZNet.instance.GetTime().Ticks
        + (long)(Random.Range(Plugin.DustingInterval.Value, Plugin.DustingInterval.Value * 1.5f) * System.TimeSpan.TicksPerSecond));

    internal static bool Floor(Vector3 point, float referenceY, out Vector3 floor, float depth = 2.5f)
    {
        point.y = referenceY;
        if (Physics.Raycast(point + Vector3.up * 1.2f, Vector3.down, out var hit, depth,
                LayerMask.GetMask("piece", "terrain", "static_solid"), QueryTriggerInteraction.Ignore)
            && hit.normal.y >= .65f)
        {
            floor = hit.point;
            return true;
        }
        floor = default;
        return false;
    }
}
