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
    private readonly SocialVisits social = new SocialVisits();
    private readonly Dictionary<ZDOID, float> blocked = new Dictionary<ZDOID, float>();
    private int active;
    private float lookUntil;
    internal bool GroundResting => active == IdleDeck.Sit && chair.GroundResting;
    private ZNetView? Target => active == IdleDeck.Social ? social.Target
        : (active == IdleDeck.Dust ? dust.Target : active == IdleDeck.Trophy ? trophy.Target : chair.Target)?.GetComponent<ZNetView>();
    internal string Describe => active == IdleDeck.Dust ? "dust: " + dust.Describe
        : active == IdleDeck.Trophy ? "trophy: " + trophy.Describe : active == IdleDeck.Sit ? "chair: " + chair.Describe
        : active == IdleDeck.Social ? "social: " + social.Describe : "wandering / cooldown";

    internal bool Available(Piece piece)
        => Available(piece.GetComponent<ZNetView>());

    internal bool Available(ZNetView view)
    {
        if (!view || !view.IsValid()) return false;
        var id = view.GetZDO().m_uid;
        if (!blocked.TryGetValue(id, out var until)) return true;
        if (Time.time < until) return false;
        blocked.Remove(id); return true;
    }

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, List<Piece> pieces, float delta)
    {
        var data = maid.View.GetZDO();
        var now = ZNet.instance.GetTime().Ticks;
        if (active == 0)
        {
            var pending = data.GetLong(NextVisitKey);
            if (pending == 0) { Schedule(data, false); return false; }
            var next = IdleTiming.ClampPending(now, pending, Plugin.DustingInterval.Value);
            if (next != pending) data.Set(NextVisitKey, next);
            if (now < next) return false;
            TaskClaims.ObserveRecent();
            var available = dust.TryBegin(maid, ai, home, pieces) ? IdleDeck.Dust : 0;
            if (Plugin.TrophyReactions.Value && trophy.TryBegin(maid, home, pieces)) available |= IdleDeck.Trophy;
            if (Plugin.IdleSitting.Value && chair.TryBegin(maid, home, pieces)) available |= IdleDeck.Sit;
            if (Plugin.SocialVisits.Value && social.TryBegin(maid, home)) available |= IdleDeck.Social;
            var remaining = data.GetInt("BH_idleDeck");
            active = IdleDeck.Choose(available, ref remaining, data.GetInt("BH_lastIdle"), Random.value);
            data.Set("BH_idleDeck", remaining);
            if (active != 0) data.Set("BH_lastIdle", active);
            if (active != IdleDeck.Dust) dust.Cancel(maid.View);
            if (active != IdleDeck.Trophy) trophy.Cancel(maid);
            if (active != IdleDeck.Sit) chair.Cancel(maid);
            if (active != IdleDeck.Social) social.Cancel(maid);
            if (active == 0) { Schedule(data, false); return false; }
            if (!Target || !TaskClaims.TryClaim(maid, Target!, "idle: " + active, work: false)) { Cancel(maid); return false; }
            lookUntil = Time.time + (Plugin.NaturalRoutines.Value ? Random.Range(.5f, 1f) : 0f);
        }
        if (!Target || !TaskClaims.Holds(maid, Target!, work: false)) { Cancel(maid); return false; }
        if (Time.time < lookUntil)
        {
            maid.StopNavigation(); ai.StopMoving(); ai.LookAt(Target!.transform.position + Vector3.up);
            return true;
        }
        var attemptedTarget = Target;
        var running = active == IdleDeck.Dust ? dust.Tick(maid, ai, home, delta)
            : active == IdleDeck.Trophy ? Plugin.TrophyReactions.Value && trophy.Tick(maid, ai, home, delta)
            : active == IdleDeck.Sit ? Plugin.IdleSitting.Value && chair.Tick(maid, ai, home, delta)
            : Plugin.SocialVisits.Value && social.Tick(maid, ai, home, delta);
        if (running) return true;
        var performed = Performed;
        if (!performed && attemptedTarget)
        {
            var view = attemptedTarget!;
            if (view && view.IsValid()) blocked[view.GetZDO().m_uid] = Time.time + 60f;
        }
        Cancel(maid);
        return false;
    }

    internal void Cancel(Maid maid)
    {
        TaskClaims.Release(maid, idleOnly: true);
        var performed = Performed;
        dust.Cancel(maid.View);
        trophy.Cancel(maid);
        chair.Cancel(maid);
        social.Cancel(maid);
        maid.GetComponent<WorkAnimations>().StopEmote();
        if (active != 0)
        {
            if (maid.View && maid.View.IsValid() && maid.View.IsOwner()) Schedule(maid.View.GetZDO(), performed);
            maid.StopNavigation();
        }
        active = 0;
    }

    private bool Performed => active == IdleDeck.Dust ? dust.Performed : active == IdleDeck.Trophy ? trophy.Performed
        : active == IdleDeck.Sit ? chair.Performed : active == IdleDeck.Social && social.Performed;

    private static void Schedule(ZDO data, bool performed) => data.Set(NextVisitKey,
        IdleTiming.Next(ZNet.instance.GetTime().Ticks, Plugin.DustingInterval.Value, performed, Random.value));

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
