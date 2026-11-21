using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class ChairVisits
{
    private Chair? target;
    private Vector3 approach;
    private float deadline;
    private float nextRoll;
    private bool sitting;

    internal bool TryBegin(Maid maid, Coffin home, List<Piece> pieces)
    {
        target = null; sitting = false;
        var best = float.PositiveInfinity;
        var range = MaintenancePolicy.WanderRange(Plugin.WanderRadius.Value, Plugin.Radius.Value);
        foreach (var piece in pieces)
        {
            if (!Eligible(piece, home, range)) continue;
            var chair = piece.GetComponent<Chair>();
            if (!chair || chair.m_inShip || !chair.m_attachPoint || !MaidSeating.SupportedPose(chair) || MaidSeating.IsInUse(chair)) continue;
            var point = chair.m_attachPoint.position + chair.transform.forward;
            if (!IdleActivities.Floor(point, piece.transform.position.y, out point)) continue;
            if ((point - home.transform.position).sqrMagnitude > Plugin.Radius.Value * Plugin.Radius.Value) continue;
            var distance = (point - maid.transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance; target = chair; approach = point;
        }
        deadline = Time.time + 20f;
        return target;
    }

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, float delta)
    {
        var seat = maid.GetComponent<MaidSeating>();
        if (!target || !Eligible(target!.GetComponent<Piece>(), home,
                MaintenancePolicy.WanderRange(Plugin.WanderRadius.Value, Plugin.Radius.Value))) return false;
        if (sitting)
        {
            if (!seat.Attached) return false;
            ai.StopMoving(); maid.StopNavigation();
            if (Time.time < nextRoll) return true;
            nextRoll = Time.time + Plugin.SittingCheckInterval.Value;
            return IdleDeck.ContinueSitting(Random.value, Plugin.SittingContinueChance.Value);
        }
        if (Time.time >= deadline || MaidSeating.IsInUse(target)) return false;
        var offset = approach - maid.transform.position; offset.y = 0;
        if (offset.magnitude > .4f)
        {
            var clear = !Physics.SphereCast(maid.transform.position + Vector3.up * .75f, .25f,
                offset.normalized, out _, offset.magnitude, LayerMask.GetMask("piece", "terrain", "static_solid"), QueryTriggerInteraction.Ignore);
            maid.Navigate(approach, delta, .3f, offset.magnitude < 1f && clear);
            return true;
        }
        if (Vector3.Distance(maid.transform.position, target.m_attachPoint.position) > target.m_useDistance
            || !maid.CanReach(target.GetComponent<Piece>(), target.m_attachPoint.position)) return false;
        ai.StopMoving(); maid.StopNavigation();
        if (!seat.Sit(target)) return false;
        sitting = true;
        nextRoll = Time.time + Plugin.SittingCheckInterval.Value;
        return true;
    }

    internal void Cancel(Maid maid)
    {
        maid.GetComponent<MaidSeating>().Stand();
        target = null; sitting = false;
    }

    private static bool Eligible(Piece piece, Coffin home, float range) => piece && piece.IsPlacedByPlayer()
        && !piece.GetComponentInParent<Ship>()
        && (piece.transform.position - home.transform.position).sqrMagnitude <= range * range
        && Maid.Allowed(piece.transform.position, home.Piece.GetCreator());
}
