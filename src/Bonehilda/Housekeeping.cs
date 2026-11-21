using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

// Cosmetic chores run only while the regular work queue is empty.
internal sealed class Housekeeping
{
    private Piece? target;
    private Piece? previous;
    private Vector3 localPoint;
    private float deadline;
    private float nextChoice;
    private bool sweeping;

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, List<Piece> pieces, float delta)
    {
        if (target == null && Time.time >= nextChoice)
        {
            var distance = float.MaxValue;
            foreach (var piece in pieces)
            {
                if (!piece || piece == previous || !Eligible(piece, home, ai.m_randomMoveRange)) continue;
                var d = (piece.transform.position - maid.transform.position).sqrMagnitude;
                if (d >= distance) continue;
                if (!TrySurface(piece, maid.transform.position, out var candidatePoint)) continue;
                target = piece;
                localPoint = piece.transform.InverseTransformPoint(candidatePoint);
                distance = d;
            }
            if (target == null) previous = null;
            nextChoice = Time.time + 3f;
            deadline = Time.time + 16f;
        }
        if (target == null || Time.time >= deadline || !Eligible(target, home, ai.m_randomMoveRange))
        {
            if (target != null || sweeping) Cancel(maid.View);
            return false;
        }
        var reference = maid.transform.position + Vector3.up * 1.05f;
        var point = target.transform.TransformPoint(localPoint);
        var deltaToSurface = point - reference;
        deltaToSurface.y = 0;
        var distanceToEdge = deltaToSurface.magnitude;
        // Leave room in front of the apron for the wooden grip throughout the sweep.
        var spacing = Mathf.Lerp(.88f, .92f, Mathf.InverseLerp(.45f, 1.05f, point.y - maid.transform.position.y))
            * (1f + .06f * home.Stars);
        if (!sweeping && distanceToEdge < spacing - .09f)
        {
            var away = distanceToEdge > .01f ? -deltaToSurface.normalized : -maid.transform.forward;
            var step = maid.transform.position + away * (spacing - distanceToEdge + .15f);
            var solids = LayerMask.GetMask("piece", "terrain", "static_solid");
            if (!Physics.Raycast(step + Vector3.up * .35f, Vector3.down, .75f, solids)
                || Physics.SphereCast(maid.transform.position + Vector3.up * .55f, .25f, away,
                    out _, spacing - distanceToEdge, solids))
            {
                Cancel(maid.View);
                return false;
            }
            ai.MoveTowards(away, false);
            return true;
        }
        var reachable = maid.CanReach(target, point);
        if (distanceToEdge > spacing || !reachable)
        {
            if (sweeping) { Cancel(maid.View); return false; }
            if (reachable && distanceToEdge < 1.25f) ai.MoveTowards(deltaToSurface, false);
            else ai.MoveTo(delta, point, .5f, false);
            return true;
        }
        ai.StopMoving();
        ai.LookAt(point);
        if (!sweeping && Vector3.Dot(maid.transform.forward, deltaToSurface.normalized) > .65f)
        {
            var data = maid.View.GetZDO();
            var forward = deltaToSurface.normalized;
            using var surface = new DustingSurface(target);
            if (!surface.TryStroke(point, forward, home.Stars, out var left, out var side, out var far))
            {
                Cancel(maid.View);
                return false;
            }
            data.Set(FeatherDuster.SurfaceKey, left);
            data.Set(FeatherDuster.SideKey, side);
            data.Set(FeatherDuster.FarKey, far);
            DustingRoutine.Choose(data);
            data.Set(FeatherDuster.StartKey, ZNet.instance.GetTime().Ticks);
            sweeping = true;
            deadline = Time.time + FeatherDuster.Seconds;
        }
        return true;
    }

    private static bool Eligible(Piece piece, Coffin home, float radius)
    {
        if (!piece.IsPlacedByPlayer() || piece.GetComponent<Coffin>()
            || (piece.transform.position - home.transform.position).sqrMagnitude > radius * radius
            || !Maid.Allowed(piece.transform.position, home.Piece.GetCreator())) return false;
        var chest = piece.GetComponent<Container>();
        return chest ? !chest.IsInUse() : piece.GetComponent<CraftingStation>() || piece.GetComponent<Chair>()
            || piece.GetComponent<ItemStand>() || piece.name.StartsWith("piece_table", System.StringComparison.Ordinal);
    }

    private static bool TrySurface(Piece piece, Vector3 feet, out Vector3 point)
    {
        var reach = feet + Vector3.up * 1.05f;
        var best = float.PositiveInfinity;
        var foundTop = false;
        point = default;
        foreach (var collider in piece.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            var bounds = collider.bounds;
            var edge = collider is MeshCollider mesh && !mesh.convex
                ? bounds.ClosestPoint(reach) : collider.ClosestPoint(reach);
            var probe = Vector3.MoveTowards(edge, bounds.center, .16f);
            probe.y = bounds.max.y + .05f;
            var top = collider.Raycast(new Ray(probe, Vector3.down), out var hit, bounds.size.y + .1f)
                && hit.normal.y > .65f && hit.point.y - feet.y >= .40f && hit.point.y - feet.y <= 1.45f;
            if (!top && foundTop) continue;
            var candidate = top ? hit.point : edge;
            var distance = (candidate - reach).sqrMagnitude;
            if (top == foundTop && distance >= best) continue;
            foundTop = top;
            best = distance;
            point = candidate;
        }
        return best < float.PositiveInfinity;
    }

    internal void Cancel(ZNetView view)
    {
        if (view && view.IsValid() && view.IsOwner() && view.GetZDO().GetLong(FeatherDuster.StartKey) != 0)
            view.GetZDO().Set(FeatherDuster.StartKey, 0L);
        if (target != null) { previous = target; nextChoice = Time.time + 3f; }
        target = null;
        sweeping = false;
    }
}
