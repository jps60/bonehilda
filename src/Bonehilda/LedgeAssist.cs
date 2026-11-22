using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal sealed class LedgeAssist
{
    private readonly NavigationProgress progress = new NavigationProgress();
    private float nextJump;
    private Rigidbody? body;
    private bool gravity;
    private Vector3 stepFrom, stepTo;
    private float elapsed, duration;
    internal bool Stepping => body;

    internal void Reset()
    {
        progress.Reset();
        if (body) body!.useGravity = gravity;
        body = null;
    }

    internal void FixedTick(Maid maid, float delta)
    {
        if (!body) return;
        var actor = maid.GetComponent<Humanoid>();
        if (actor.IsDead() || actor.IsKnockedBack() || actor.IsStaggering()) { Reset(); return; }
        elapsed += delta;
        var t = Mathf.Clamp01(elapsed / duration);
        var next = Vector3.Lerp(stepFrom, stepTo, StepPolicy.Forward(t));
        next.y = Mathf.Lerp(stepFrom.y, stepTo.y, StepPolicy.Rise(t));
        if (!ClearIncrement(maid.GetComponent<CapsuleCollider>(), body!.position, next)
            || !Physics.Raycast(stepTo + Vector3.up * .12f, Vector3.down, out var floor, .24f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)
            || floor.normal.y < .65f) { Reset(); return; }
        body.velocity = Vector3.zero;
        body.MovePosition(next);
        var sync = maid.GetComponent<ZSyncAnimation>();
        sync.SetBool("onGround", true);
        sync.SetFloat("forward_speed", actor.m_speed);
        sync.SetFloat("sideway_speed", 0);
        if (t >= 1f) Reset();
    }

    internal static bool ClearIncrement(CapsuleCollider capsule, Vector3 from, Vector3 to)
    {
        RepairGeometry.Body(capsule, from, out var bottom, out var top, out var radius);
        var offset = to - from;
        return offset.sqrMagnitude < .000001f || !Physics.CapsuleCast(bottom, top, radius, offset.normalized,
            offset.magnitude, RepairGeometry.Solids, QueryTriggerInteraction.Ignore);
    }

    internal static bool Landing(CapsuleCollider capsule, Vector3 feet, Vector3 direction, out RaycastHit floor, out float distance)
    {
        foreach (var probeDistance in new[] { .55f, .75f, 1.05f })
        {
            var probe = feet + direction * probeDistance;
            if (Physics.Raycast(probe + Vector3.up * .85f, Vector3.down, out floor, 1.1f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)
                && (StepPolicy.Walk(floor.point.y - feet.y, floor.normal.y) || StepPolicy.Jump(floor.point.y - feet.y, floor.normal.y))
                && RepairGeometry.Fits(capsule, floor.point + Vector3.up * .03f))
            { distance = probeDistance; return true; }
        }
        floor = default; distance = 0; return false;
    }

    internal bool Tick(Maid maid, MonsterAI ai, Vector3 goal, float delta)
    {
        var feet = maid.transform.position;
        if (Stepping) return true;
        if (!progress.Advance(delta, feet.x, feet.z, goal.x, goal.y, goal.z, .4f) || Time.time < nextJump) return false;
        var actor = maid.GetComponent<Humanoid>();
        if (!actor.IsOnGround()) return false;
        var direction = goal - feet;
        foreach (var node in ai.m_path)
        {
            var offset = node - feet; offset.y = 0;
            if (offset.sqrMagnitude > .09f) { direction = offset; break; }
        }
        direction.y = 0;
        if (direction.magnitude < .3f) return false;
        direction.Normalize();
        if (!Physics.SphereCast(feet + Vector3.up * .12f, .08f, direction, out _, .8f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        if (!Landing(maid.GetComponent<CapsuleCollider>(), feet, direction, out var floor, out var landingDistance)) return false;
        var landing = floor.point + Vector3.up * .03f;
        var homeObject = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
        var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
        if (!home || !MaintenancePolicy.InRadius((landing - home!.transform.position).sqrMagnitude, Plugin.Radius.Value)
            || !Maid.Allowed(landing, home.Piece.GetCreator()) || !RepairGeometry.Fits(maid, landing)) return false;
        var height = floor.point.y - feet.y;
        var lift = height + .22f;
        RepairGeometry.Body(maid, feet, out var bottom, out var top, out var radius);
        if (Physics.CapsuleCast(bottom, top, radius, Vector3.up, lift, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        bottom += Vector3.up * lift; top += Vector3.up * lift;
        if (Physics.CapsuleCast(bottom, top, radius, direction, landingDistance, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        if (StepPolicy.Walk(height, floor.normal.y))
        {
            body = maid.GetComponent<Rigidbody>(); gravity = body.useGravity;
            stepFrom = body.position; stepTo = landing; elapsed = 0;
            duration = StepPolicy.Duration(Vector3.Distance(stepFrom, stepTo), actor.m_speed);
            body.useGravity = false; body.velocity = Vector3.zero;
            maid.View.GetZDO().Set("BH_walkSteps", maid.View.GetZDO().GetInt("BH_walkSteps") + 1);
        }
        else if (StepPolicy.Jump(height, floor.normal.y))
        {
            var g = Mathf.Max(1f, -Physics.gravity.y);
            var up = StepPolicy.JumpSpeed(height, g);
            var flight = up / g + Mathf.Sqrt(2f * .18f / g);
            actor.ForceJump(direction * (landingDistance / flight) + Vector3.up * up);
            maid.View.GetZDO().Set("BH_stepJumps", maid.View.GetZDO().GetInt("BH_stepJumps") + 1);
        }
        nextJump = Time.time + (StepPolicy.Walk(height, floor.normal.y) ? .15f : 3f);
        progress.Reset();
        return true;
    }
}

[HarmonyPatch(typeof(Character), "UpdateMotion")]
internal static class MaidSteppingMotionPatch
{
    private static bool Prefix(Character __instance) => !__instance.TryGetComponent<Maid>(out var maid) || !maid.Stepping;
}
