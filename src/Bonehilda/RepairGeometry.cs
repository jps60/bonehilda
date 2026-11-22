using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

internal static class RepairGeometry
{
    internal static int Solids => LayerMask.GetMask("piece", "terrain", "static_solid");
    internal static int SightMask => Solids | LayerMask.GetMask("piece_nonsolid");
    private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

    internal static bool VisibleSurface(Piece piece, Vector3 eye, float reach, out Vector3 point)
    {
        point = default;
        foreach (var collider in piece.GetComponentsInChildren<Collider>()
                     .Where(c => c.enabled && !c.isTrigger && (c.bounds.ClosestPoint(eye) - eye).sqrMagnitude < reach * reach)
                     .OrderBy(c => (c.bounds.ClosestPoint(eye) - eye).sqrMagnitude))
            foreach (var aim in Probes(collider, eye))
            {
                var direction = aim - eye;
                if (direction.sqrMagnitude < .0001f) continue;
                if (Physics.Raycast(eye, direction.normalized, out var hit, reach, SightMask, QueryTriggerInteraction.Ignore)
                    && (hit.point - eye).sqrMagnitude < reach * reach && hit.collider.GetComponentInParent<Piece>() == piece)
                { point = hit.point; return true; }
            }
        return false;
    }

    private static IEnumerable<Vector3> Probes(Collider collider, Vector3 eye)
    {
        var bounds = collider.bounds;
        yield return collider is MeshCollider mesh && !mesh.convex ? bounds.ClosestPoint(eye) : collider.ClosestPoint(eye);
        yield return bounds.center;
        foreach (var axis in Axes)
        {
            var offset = Vector3.Scale(axis, bounds.extents) * .85f;
            yield return bounds.center + offset;
            yield return bounds.center - offset;
        }
    }

    internal static Vector3 Aim(Piece piece, Vector3 eye)
    {
        if (VisibleSurface(piece, eye, Plugin.RepairReach, out var visible)) return visible;
        var point = piece.transform.position;
        var distance = float.PositiveInfinity;
        foreach (var collider in piece.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            Vector3 candidate;
            if (collider is MeshCollider mesh && !mesh.convex)
            {
                var direction = collider.bounds.ClosestPoint(eye) - eye;
                if (direction.sqrMagnitude < .001f) direction = collider.bounds.center - eye;
                if (!collider.Raycast(new Ray(eye, direction.normalized), out var hit, direction.magnitude + collider.bounds.size.magnitude + 1f))
                {
                    direction = collider.bounds.center - eye;
                    if (!collider.Raycast(new Ray(eye, direction.normalized), out hit, direction.magnitude + collider.bounds.size.magnitude + 1f)) continue;
                }
                candidate = hit.point;
            }
            else candidate = collider.ClosestPoint(eye);
            var d = (candidate - eye).sqrMagnitude;
            if (d >= distance) continue;
            point = candidate; distance = d;
        }
        return point;
    }

    internal static void Body(Maid maid, Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        => Body(maid.GetComponent<CapsuleCollider>(), feet, out bottom, out top, out radius);

    private static void Body(CapsuleCollider capsule, Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
    {
        var bounds = capsule.bounds;
        radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + .025f;
        var height = Mathf.Max(bounds.size.y, radius * 2f + .05f);
        bottom = feet + Vector3.up * (radius + .07f);
        top = feet + Vector3.up * (height - radius + .02f);
    }

    internal static bool Fits(Maid maid, Vector3 feet) => Fits(maid.GetComponent<CapsuleCollider>(), feet);

    internal static bool Fits(CapsuleCollider capsule, Vector3 feet)
    {
        Body(capsule, feet, out var bottom, out var top, out var radius);
        return !Physics.CheckCapsule(bottom, top, radius, Solids, QueryTriggerInteraction.Ignore);
    }

    internal static bool ClearLastStep(Maid maid, Vector3 destination)
        => ClearWalk(maid, destination, 1.2f);

    internal static bool ClearDoorway(Maid maid, Vector3 from, Vector3 to, Door door)
        => ClearDoorway(maid.GetComponent<CapsuleCollider>(), from, to, door);

    internal static bool ClearDoorway(CapsuleCollider capsule, Vector3 from, Vector3 to, Door door)
    {
        // Ignore only this door's colliders in this query. Adjacent walls,
        // furniture, terrain, and unsupported thresholds still reject a route.
        var offset = to - from;
        if (Mathf.Abs(offset.y) > .45f) return false;
        var samples = Mathf.Max(1, Mathf.CeilToInt(offset.magnitude / .4f));
        var previousY = from.y;
        for (var i = 0; i <= samples; i++)
        {
            var feet = Vector3.Lerp(from, to, i / (float)samples);
            var floor = Physics.RaycastAll(feet + Vector3.up * .4f, Vector3.down, .85f, Solids, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.GetComponentInParent<Door>() != door).OrderBy(h => h.distance).FirstOrDefault();
            if (!floor.collider || !RepairApproachPolicy.SupportsStep(previousY, floor.point.y, floor.normal.y)) return false;
            feet.y = floor.point.y + .03f; previousY = floor.point.y;
            Body(capsule, feet, out var bottom, out var top, out var radius);
            if (Physics.OverlapCapsule(bottom, top, radius, Solids, QueryTriggerInteraction.Ignore)
                .Any(c => c.GetComponentInParent<Door>() != door)) return false;
        }
        Body(capsule, from, out var startBottom, out var startTop, out var startRadius);
        return !Physics.CapsuleCastAll(startBottom, startTop, startRadius, offset.normalized, offset.magnitude,
            Solids, QueryTriggerInteraction.Ignore).Any(h => h.collider.GetComponentInParent<Door>() != door);
    }

    internal static bool ClearWalk(Maid maid, Vector3 destination, float maxDistance = 4.5f)
        => ClearWalk(maid.GetComponent<CapsuleCollider>(), maid.transform.position, destination, maxDistance);

    internal static bool ClearWalk(CapsuleCollider capsule, Vector3 from, Vector3 destination, float maxDistance)
    {
        var offset = destination - from;
        if (offset.magnitude > maxDistance) return false;
        if (Mathf.Abs(from.y - destination.y) > .45f) return false;
        var samples = Mathf.Max(1, Mathf.CeilToInt(offset.magnitude / .4f));
        var previousY = from.y;
        for (var i = 1; i <= samples; i++)
        {
            var sample = Vector3.Lerp(from, destination, i / (float)samples);
            if (!Physics.Raycast(sample + Vector3.up * .4f, Vector3.down, out var floor, .85f, Solids, QueryTriggerInteraction.Ignore)
                || !RepairApproachPolicy.SupportsStep(previousY, floor.point.y, floor.normal.y)) return false;
            previousY = floor.point.y;
        }
        Body(capsule, from, out var bottom, out var top, out var radius);
        return !Physics.CapsuleCast(bottom, top, radius, offset.normalized, out _, offset.magnitude, Solids, QueryTriggerInteraction.Ignore);
    }
}
