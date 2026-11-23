using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

// Player.AttachStart cannot be used on a tame Humanoid. Keep the same native
// attach animation and anchor semantics without giving this creature a Player component.
internal sealed class MaidSeating : MonoBehaviour
{
    private const string SeatKey = "BH_seat", OccupantKey = "BH_sitter", LeaseKey = "BH_seatUntil";
    private ZNetView view = null!;
    private Humanoid actor = null!;
    private Rigidbody body = null!;
    private Chair? chair;
    private ZNetView? seatView;
    private string pose = "";
    private Vector3 returnPoint;
    private bool gravity;
    private float nextLease;
    private sealed class Pair { internal Collider Body = null!; internal Collider Chair = null!; }
    private readonly List<Pair> ignored = new List<Pair>();
    private static readonly System.Reflection.MethodInfo InUse = AccessTools.Method(typeof(Chair), "IsInUse");
    internal static bool IsInUse(Chair target) => (bool)InUse.Invoke(target, null);
    internal static bool SupportedPose(Chair target) => target.m_attachAnimation == "attach_chair"
        || target.m_attachAnimation == "attach_throne";
    internal bool Attached => chair && view && view.IsValid() && view.IsOwner() && !actor.IsDead();

    private void Awake()
    {
        view = GetComponent<ZNetView>();
        actor = GetComponent<Humanoid>();
        body = GetComponent<Rigidbody>();
        actor.m_onDeath += Stand;
    }

    internal void RecoverSavedPose()
    {
        if (!view || !view.IsValid() || !view.IsOwner() || pose.Length != 0) return;
        var sync = GetComponent<ZSyncAnimation>();
        sync.SetBool("attach_chair", false); sync.SetBool("attach_throne", false);
        if (view.GetZDO().GetZDOID(SeatKey) == ZDOID.None) return;
        view.GetZDO().Set(SeatKey, ZDOID.None);
        var exit = view.GetZDO().GetVec3("BH_seatReturn", transform.position);
        if (SafeExit(exit)) { transform.position = exit; body.position = exit; }
    }

    internal bool Sit(Chair target)
    {
        if (Attached || !view.IsValid() || !view.IsOwner() || !SupportedPose(target) || IsInUse(target) || !target.m_attachPoint) return false;
        var owner = target.GetComponent<ZNetView>();
        if (!owner || !owner.IsValid()) return false;
        if (!owner.IsOwner()) owner.ClaimOwnership();
        if (!owner.IsOwner() || IsInUse(target)) return false;
        returnPoint = transform.position;
        chair = target; seatView = owner; pose = target.m_attachAnimation;
        view.GetZDO().Set(SeatKey, owner.GetZDO().m_uid);
        view.GetZDO().Set("BH_seatReturn", returnPoint);
        owner.GetZDO().Set(OccupantKey, view.GetZDO().m_uid);
        RefreshLease();
        gravity = body.useGravity;
        body.useGravity = false;
        foreach (var own in GetComponentsInChildren<Collider>())
            foreach (var other in target.GetComponentsInChildren<Collider>())
            {
                if (!own.enabled || own.isTrigger || !other.enabled || other.isTrigger || Physics.GetIgnoreCollision(own, other)) continue;
                Physics.IgnoreCollision(own, other, true);
                ignored.Add(new Pair { Body = own, Chair = other });
            }
        GetComponent<WorkAnimations>().StopEmote();
        GetComponent<ZSyncAnimation>().SetBool(pose, true);
        Anchor();
        return true;
    }

    private void FixedUpdate()
    {
        if (pose.Length == 0)
        {
            // A new owner discards a former peer's idle plan and returns to work.
            if (view && view.IsValid() && view.IsOwner() && view.GetZDO().GetZDOID(SeatKey) != ZDOID.None)
            {
                // Clear supported poses even if the chair's sector has unloaded.
                var sync = GetComponent<ZSyncAnimation>();
                sync.SetBool("attach_chair", false);
                sync.SetBool("attach_throne", false);
                view.GetZDO().Set(SeatKey, ZDOID.None);
                var exit = view.GetZDO().GetVec3("BH_seatReturn", transform.position);
                if (SafeExit(exit)) { transform.position = exit; body.position = exit; }
            }
            return;
        }
        if (!Attached || !seatView || !seatView!.IsValid()
            || seatView.GetZDO().GetZDOID(OccupantKey) != view.GetZDO().m_uid)
        { Stand(); return; }
        if (Time.time >= nextLease)
        {
            if (!seatView.IsOwner()) seatView.ClaimOwnership();
            if (!seatView.IsOwner()) { Stand(); return; }
            RefreshLease();
        }
        Anchor();
    }

    private void Anchor()
    {
        if (!chair || !chair!.m_attachPoint) return;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        transform.SetPositionAndRotation(chair.m_attachPoint.position, chair.m_attachPoint.rotation);
        body.position = transform.position;
        body.rotation = transform.rotation;
    }

    private void RefreshLease()
    {
        seatView!.GetZDO().Set(LeaseKey, ZNet.instance.GetTime().Ticks + 10L * TimeSpan.TicksPerSecond);
        nextLease = Time.time + 2f;
    }

    internal void Stand()
    {
        if (pose.Length == 0) return;
        var owned = view && view.IsValid() && view.IsOwner();
        if (owned)
        {
            GetComponent<ZSyncAnimation>().SetBool(pose, false);
            view.GetZDO().Set(SeatKey, ZDOID.None);
            if (seatView && seatView!.IsValid() && seatView.IsOwner()
                && seatView.GetZDO().GetZDOID(OccupantKey) == view.GetZDO().m_uid)
            {
                seatView.GetZDO().Set(OccupantKey, ZDOID.None);
                seatView.GetZDO().Set(LeaseKey, 0L);
            }
            // Native chair detachment also repositions the actor. Use the known
            // ground point she approached from, provided it remains unobstructed.
            if (!actor.IsDead() && SafeExit(returnPoint))
            { transform.position = returnPoint; body.position = returnPoint; }
            body.velocity = Vector3.zero;
        }
        foreach (var pair in ignored)
            if (pair.Body && pair.Chair) Physics.IgnoreCollision(pair.Body, pair.Chair, false);
        ignored.Clear();
        if (body) body.useGravity = gravity;
        chair = null; seatView = null; pose = "";
    }

    private bool SafeExit(Vector3 point)
    {
        foreach (var collider in Physics.OverlapCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.5f,
                     .28f, LayerMask.GetMask("piece", "terrain", "static_solid", "character"), QueryTriggerInteraction.Ignore))
            if (collider.GetComponentInParent<Maid>() != GetComponent<Maid>()) return false;
        return true;
    }

    internal static bool Occupied(Chair target)
    {
        var owner = target.GetComponent<ZNetView>();
        if (!owner || !owner.IsValid()) return false;
        var data = owner.GetZDO();
        var id = data.GetZDOID(OccupantKey);
        if (id == ZDOID.None || !ZNet.instance || data.GetLong(LeaseKey) <= ZNet.instance.GetTime().Ticks) return false;
        var instance = ZNetScene.instance ? ZNetScene.instance!.FindInstance(id) : null;
        if (!instance) return true; // Brief unloads must not allow a second occupant.
        var occupant = instance!.GetComponent<ZNetView>();
        return occupant && occupant!.IsValid() && occupant.GetZDO().GetZDOID(SeatKey) == data.m_uid;
    }

    private void OnDestroy() => Stand();
}

[HarmonyPatch(typeof(Chair), "IsInUse")]
internal static class MaidChairOccupancyPatch
{
    private static void Postfix(Chair __instance, ref bool __result) => __result |= MaidSeating.Occupied(__instance);
}

[HarmonyPatch(typeof(Character), "UpdateMotion")]
internal static class MaidSeatedMotionPatch
{
    private static bool Prefix(Character __instance) => !__instance.TryGetComponent<MaidSeating>(out var seat) || !seat.Attached;
}

[HarmonyPatch(typeof(Character), nameof(Character.IsAttached))]
internal static class MaidSeatedAttachmentPatch
{
    private static void Postfix(Character __instance, ref bool __result)
    {
        if (__instance.TryGetComponent<MaidSeating>(out var seat) && seat.Attached) __result = true;
    }
}
