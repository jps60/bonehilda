using UnityEngine;

namespace Bonehilda;

internal sealed class LedgeAssist
{
    private readonly NavigationProgress progress = new NavigationProgress();
    private float nextJump;

    internal void Reset() => progress.Reset();

    internal bool Tick(Maid maid, MonsterAI ai, Vector3 goal, float delta)
    {
        var feet = maid.transform.position;
        if (!progress.Advance(delta, feet.x, feet.z, goal.x, goal.y, goal.z, 1.1f) || Time.time < nextJump) return false;
        var actor = maid.GetComponent<Humanoid>();
        if (!actor.IsOnGround()) return false;
        var direction = goal - feet;
        foreach (var node in ai.m_path)
            if ((node - feet).sqrMagnitude > .25f) { direction = node - feet; break; }
        direction.y = 0;
        if (direction.magnitude < .3f) return false;
        direction.Normalize();
        if (!Physics.SphereCast(feet + Vector3.up * .18f, .12f, direction, out _, .8f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        var landing = feet + direction * 1.05f;
        if (!Physics.Raycast(landing + Vector3.up * .85f, Vector3.down, out var floor, 1.1f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)
            || !DoorPolicy.SmallLedge(floor.point.y - feet.y, floor.normal.y)) return false;
        landing = floor.point + Vector3.up * .03f;
        var homeObject = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
        var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
        if (!home || !MaintenancePolicy.InRadius((landing - home!.transform.position).sqrMagnitude, Plugin.Radius.Value)
            || !Maid.Allowed(landing, home.Piece.GetCreator()) || !RepairGeometry.Fits(maid, landing)) return false;
        var lift = floor.point.y - feet.y + .15f;
        RepairGeometry.Body(maid, feet, out var bottom, out var top, out var radius);
        if (Physics.CapsuleCast(bottom, top, radius, Vector3.up, lift, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        bottom += Vector3.up * lift; top += Vector3.up * lift;
        if (Physics.CapsuleCast(bottom, top, radius, direction, 1.05f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)) return false;
        actor.Jump();
        nextJump = Time.time + 3f;
        progress.Reset();
        var data = maid.View.GetZDO();
        data.Set("BH_stepJumps", data.GetInt("BH_stepJumps") + 1);
        return true;
    }
}
