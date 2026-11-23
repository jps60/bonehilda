using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

// ZDO.Load assigns fresh IDs. Persist a GUID in ordinary saved data and use
// transient ZDO IDs only as repaired runtime pointers, never as the identity.
internal static class HomeIdentity
{
    internal const string Key = "BH_homeIdentity";
    private static readonly System.Reflection.FieldInfo Objects = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");
    internal static string Ensure(ZDO coffin)
    {
        var token = coffin.GetString(Key);
        if (token.Length == 0) { token = Guid.NewGuid().ToString("N"); coffin.Set(Key, token); }
        return token;
    }

    internal static bool Repair(ZDO holder)
    {
        if (!ZNet.instance.IsServer()) return false;
        var all = ((Dictionary<ZDOID, ZDO>)Objects.GetValue(ZDOMan.instance)).Values;
        var coffin = CoffinTier.IsCoffin(holder.GetPrefab()) ? holder : null;
        var maid = coffin == null ? holder : null;
        if (maid != null)
        {
            var token = maid.GetString(Key);
            var position = maid.GetVec3("BH_homePosition", new Vector3(float.PositiveInfinity, 0, 0));
            var matches = all.Where(z => CoffinTier.IsCoffin(z.GetPrefab())
                && HomeIdentityPolicy.HomeMatches(token, z.GetString(Key), (z.GetPosition() - position).sqrMagnitude)).Take(2).ToArray();
            if (!HomeIdentityPolicy.Unique(matches.Length)) return false;
            coffin = matches[0];
        }
        var identity = coffin!.GetString(Key);
        // A new identity is issued only after finding an unambiguous legacy
        // maid. This keeps position-based migration usable from either end.
        var candidates = all.Where(z => z.GetPrefab() == Plugin.MaidPrefab.GetStableHashCode()
            && HomeIdentityPolicy.MaidMatches(z.GetString(Key), identity,
                (z.GetVec3("BH_homePosition", new Vector3(float.PositiveInfinity, 0, 0)) - coffin.GetPosition()).sqrMagnitude)).Take(2).ToArray();
        if (!HomeIdentityPolicy.Unique(candidates.Length) || maid != null && candidates[0].m_uid != maid.m_uid) return false;
        maid = candidates[0];
        identity = Ensure(coffin);
        maid.Set(Key, identity);
        // Saved task/door IDs were remapped by the same load. Never resume a
        // crossing against a different object that inherited the old ID.
        maid.Set("BH_openDoor", ZDOID.None); maid.Set("BH_doorStage", "");
        maid.Set("BH_taskTarget", ZDOID.None); maid.Set("BH_taskUntil", 0L);
        maid.Set(Maid.HomeKey, coffin.m_uid); maid.Set("BH_homePosition", coffin.GetPosition());
        coffin.Set(Coffin.MaidKey, maid.m_uid);
        ZDOMan.instance.ForceSendZDO(coffin.m_uid); ZDOMan.instance.ForceSendZDO(maid.m_uid);
        return true;
    }
}
