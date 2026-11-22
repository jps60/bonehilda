using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

// A maid writes only her own ZDO. Peers resolve simultaneous claims by the
// same ordering, without taking ownership of a machine just to reserve it.
internal static class TaskClaims
{
    private const string TargetKey = "BH_taskTarget", KindKey = "BH_taskKind", WorkKey = "BH_taskWork";
    private const string StartKey = "BH_taskStart", UntilKey = "BH_taskUntil";
    private const string LastTargetKey = "BH_lastTaskTarget", LastStartKey = "BH_lastTaskStart";
    private const long Lease = 15L * TimeSpan.TicksPerSecond;
    private static readonly Dictionary<ZDOID, long> recent = new Dictionary<ZDOID, long>();
    private static long Now => ZNet.instance.GetTime().Ticks;

    private static TaskClaim Read(Maid maid)
    {
        var data = maid.View.GetZDO();
        return new TaskClaim(data.m_uid.ToString(), data.GetZDOID(TargetKey).ToString(), data.GetString(KindKey),
            data.GetBool(WorkKey), data.GetLong(StartKey), data.GetLong(UntilKey));
    }

    private static IEnumerable<TaskClaim> Active()
    {
        foreach (var maid in Maid.Loaded)
            if (maid && maid.View && maid.View.IsValid() && !maid.GetComponent<Character>().IsDead()
                && maid.View.GetZDO().GetZDOID(TargetKey) != ZDOID.None) yield return Read(maid);
    }

    internal static bool Available(Maid maid, Piece target, bool work)
        => Available(maid, target.GetComponent<ZNetView>(), work);

    internal static bool Available(Maid maid, ZNetView view, bool work)
    {
        return view && view.IsValid() && TaskClaimPolicy.Available(Active(), view.GetZDO().m_uid.ToString(),
            maid.View.GetZDO().m_uid.ToString(), work, Now);
    }

    internal static bool TryClaim(Maid maid, Piece target, string kind, bool work)
        => TryClaim(maid, target.GetComponent<ZNetView>(), kind, work);

    internal static bool TryClaim(Maid maid, ZNetView target, string kind, bool work)
    {
        if (!maid.View.IsOwner() || !Available(maid, target, work)) return false;
        var data = maid.View.GetZDO();
        var id = target.GetZDO().m_uid;
        // Reasserting the same claim must not change its place in arbitration.
        if (data.GetZDOID(TargetKey) != id || data.GetString(KindKey) != kind || data.GetLong(UntilKey) <= Now)
            data.Set(StartKey, Now);
        data.Set(TargetKey, id); data.Set(KindKey, kind); data.Set(WorkKey, work); data.Set(UntilKey, Now + Lease);
        if (!Holds(maid, target, work)) { Release(maid); return false; }
        data.Set(LastTargetKey, id); data.Set(LastStartKey, data.GetLong(StartKey));
        Remember(id, data.GetLong(StartKey));
        return true;
    }

    internal static void ObserveRecent()
    {
        foreach (var maid in Maid.Loaded)
            if (maid && maid.View && maid.View.IsValid())
            {
                var data = maid.View.GetZDO();
                Remember(data.GetZDOID(LastTargetKey), data.GetLong(LastStartKey));
            }
        // History is only a scheduling preference, not a saved work queue.
        foreach (var key in new List<ZDOID>(recent.Keys))
            if (recent[key] > Now || recent[key] <= Now - 30L * TimeSpan.TicksPerSecond) recent.Remove(key);
    }

    private static void Remember(ZDOID id, long started)
    {
        if (id != ZDOID.None && (!recent.TryGetValue(id, out var previous) || started > previous)) recent[id] = started;
    }

    internal static long Recent(Piece target)
    {
        var view = target.GetComponent<ZNetView>();
        return view && view.IsValid() && recent.TryGetValue(view.GetZDO().m_uid, out var started) ? started : 0;
    }

    internal static bool Holds(Maid maid, Piece target, bool work)
        => Holds(maid, target.GetComponent<ZNetView>(), work);

    internal static bool Holds(Maid maid, ZNetView view, bool work)
    {
        if (!view || !view.IsValid()) return false;
        var own = Read(maid);
        if (own.Work != work || own.Target != view.GetZDO().m_uid.ToString()) return false;
        return TaskClaimPolicy.Winner(Active(), own.Target, Now)?.Maid == own.Maid;
    }

    internal static void Refresh(Maid maid)
    {
        var data = maid.View.GetZDO();
        if (data.GetZDOID(TargetKey) == ZDOID.None) return;
        var own = Read(maid);
        // Never revive an expired claim ahead of a maid that has taken over.
        if (TaskClaimPolicy.Winner(Active(), own.Target, Now)?.Maid != own.Maid) { Release(maid); return; }
        if (own.Until < Now + Lease / 2) data.Set(UntilKey, Now + Lease);
    }

    internal static void Release(Maid maid, Piece? target = null, bool idleOnly = false)
    {
        if (!ZNet.instance || ZDOMan.instance == null || !maid.View || !maid.View.IsValid() || !maid.View.IsOwner()) return;
        var data = maid.View.GetZDO();
        if (idleOnly && data.GetBool(WorkKey)) return;
        if (target)
        {
            var view = target!.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || view.GetZDO().m_uid != data.GetZDOID(TargetKey)) return;
        }
        data.Set(TargetKey, ZDOID.None); data.Set(UntilKey, 0L); data.Set(KindKey, "");
    }

    internal static string Describe(Maid maid)
    {
        var data = maid.View.GetZDO();
        return data.GetZDOID(TargetKey) == ZDOID.None ? "none"
            : data.GetString(KindKey) + " " + data.GetZDOID(TargetKey) + " (" + Math.Max(0, (data.GetLong(UntilKey) - Now) / TimeSpan.TicksPerSecond) + "s)";
    }
}
