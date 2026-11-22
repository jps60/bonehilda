using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

internal sealed class GroundRest
{
    private Piece? target;
    private Vector3 point, look;
    private float deadline, nextRoll;
    private bool resting;
    private string emote = "sit";
    internal Piece? Target => target;
    internal bool Performed { get; private set; }
    internal bool Resting => resting;
    internal string Describe => target ? "ground near " + Utils.GetPrefabName(target!.gameObject)
        + (resting ? " " + emote : " approaching") : "none";

    internal bool TryBegin(Maid maid, Coffin home, List<Piece> pieces)
    {
        target = null; resting = Performed = false;
        var range = MaintenancePolicy.WanderRange(Plugin.WanderRadius.Value, Plugin.Radius.Value);
        var revisit = Plugin.NaturalRoutines.Value && Random.value < .6f;
        // Warmth is a preference; an unobstructed ordinary patch is the fallback.
        foreach (var piece in pieces.Where(p => p && p.IsPlacedByPlayer() && !p.GetComponentInParent<Ship>())
                     .OrderBy(p => RoutinePolicy.RestScore((p.transform.position - maid.transform.position).sqrMagnitude, IdleRoutine.Favorite(maid, p), revisit)))
        {
            var fire = piece.GetComponentInChildren<Fireplace>();
            var fireView = fire ? fire!.GetComponent<ZNetView>() : null;
            if (!fire || !fireView || !fireView!.IsValid() || !maid.CanIdleVisit(piece) || !TaskClaims.Available(maid, piece, work: false)
                || !fire.IsBurning()
                || (piece.transform.position - home.transform.position).sqrMagnitude > range * range) continue;
            var bounds = new Bounds(piece.transform.position, Vector3.zero);
            foreach (var collider in piece.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + 1.5f;
            if (ChooseSpot(maid, home, piece, bounds.center, radius, range, fire: true)) return true;
        }
        if (!maid.CanIdleVisit(home.Piece) || !TaskClaims.Available(maid, home.Piece, work: false)) return false;
        return ChooseSpot(maid, home, home.Piece, home.transform.position, Mathf.Min(2.5f, range), range, fire: false);
    }

    private bool ChooseSpot(Maid maid, Coffin home, Piece anchor, Vector3 center, float radius, float range, bool fire)
    {
        var best = float.PositiveInfinity;
        var favorite = Plugin.NaturalRoutines.Value && IdleRoutine.Favorite(maid, anchor) && Random.value < .6f;
        for (var i = -1; i < 12; i++)
        {
            if (i == -1 && !favorite) continue;
            var angle = i * Mathf.PI * 2f / 12f;
            var candidate = i == -1 ? anchor.transform.TransformPoint(maid.View.GetZDO().GetVec3("BH_favoriteRestPoint", Vector3.zero))
                : center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            if (!IdleActivities.Floor(candidate, anchor.transform.position.y, out candidate)
                || (candidate - home.transform.position).sqrMagnitude > range * range
                || (fire && Mathf.Abs(candidate.y - anchor.transform.position.y) > 1.5f)
                || !Maid.Allowed(candidate, home.Piece.GetCreator())) continue;
            var local = home.transform.InverseTransformPoint(candidate);
            if (Mathf.Abs(local.x) < 1f && local.z > -.3f && local.z < 4f) continue;
            var distance = (candidate - maid.transform.position).sqrMagnitude;
            if (distance >= best || !Safe(maid, candidate, center) || !maid.CanIdleApproach(home, candidate)) continue;
            if (fire && Physics.Linecast(candidate + Vector3.up * .8f, center, out var hit, RepairGeometry.Solids,
                    QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Piece>() != anchor) continue;
            best = distance; point = candidate; look = center; target = anchor;
            if (i == -1) break; // A remembered spot still passes every safety/path check.
        }
        deadline = Time.time + 20f;
        return target;
    }

    private static bool Safe(Maid maid, Vector3 feet, Vector3 look)
    {
        if (!Physics.Raycast(feet + Vector3.up * .15f, Vector3.down, out var floor, .3f, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)
            || floor.normal.y < .9f || !RepairGeometry.Fits(maid, feet + Vector3.up * .03f)) return false;
        var facing = look - feet; facing.y = 0;
        if (facing.sqrMagnitude < .01f) facing = maid.transform.forward;
        facing.Normalize();
        return !Physics.OverlapCapsule(feet - facing * .65f + Vector3.up * .5f, feet + facing * .65f + Vector3.up * .5f, .4f,
            RepairGeometry.Solids | LayerMask.GetMask("character", "character_net", "character_ghost"), QueryTriggerInteraction.Ignore)
            .Any(c => c.GetComponentInParent<Maid>() != maid);
    }

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, float delta)
    {
        if (!target || !Maid.Allowed(point, home.Piece.GetCreator())) return false;
        if (resting)
        {
            ai.StopMoving(); maid.StopNavigation();
            if (Time.time < nextRoll) return true;
            nextRoll = Time.time + Plugin.SittingCheckInterval.Value;
            return IdleDeck.ContinueSitting(Random.value, Plugin.SittingContinueChance.Value);
        }
        if (Time.time >= deadline) return false;
        var offset = point - maid.transform.position; offset.y = 0;
        if (offset.magnitude > .4f || Mathf.Abs(point.y - maid.transform.position.y) > .5f)
        {
            maid.Navigate(point, delta, .25f, offset.magnitude < 1f && RepairGeometry.ClearLastStep(maid, point));
            return true;
        }
        if (!Safe(maid, maid.transform.position, look)) return false;
        maid.StopNavigation(); ai.StopMoving(); ai.LookAt(look);
        var facing = look - maid.transform.position; facing.y = 0;
        if (facing.sqrMagnitude > .01f && Vector3.Dot(maid.transform.forward, facing.normalized) < .85f) return true;
        emote = IdleRoutine.Gesture(maid, "rest", new[] { "sit", "relax" });
        maid.GetComponent<WorkAnimations>().RestEmote(emote);
        resting = Performed = true;
        IdleRoutine.RememberRest(maid, target!, point);
        nextRoll = Time.time + Plugin.SittingCheckInterval.Value;
        return true;
    }

    internal void Cancel(Maid maid)
    {
        if (resting) { maid.GetComponent<WorkAnimations>().StopEmote(); maid.PauseToStand(); }
        target = null; resting = false;
    }
}
