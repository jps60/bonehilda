using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

// Conservative horizontal sweep around collider ancestor pivots. Includes
// intermediate swing angles, not only the open/closed end-pose bounding boxes.
internal sealed class DoorSwing
{
    private sealed class Arc { internal Vector3 Center; internal float Radius, Low, High; }
    private readonly List<Arc> arcs = new List<Arc>();
    internal float Extent { get; private set; }
    internal void Capture(Door door)
    {
        arcs.Clear(); Extent = 1f;
        foreach (var collider in door.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            var bounds = collider.bounds;
            for (var pivot = collider.transform; pivot && pivot.IsChildOf(door.transform); pivot = pivot.parent)
            {
                var dx = Mathf.Abs(bounds.center.x - pivot.position.x) + bounds.extents.x;
                var dz = Mathf.Abs(bounds.center.z - pivot.position.z) + bounds.extents.z;
                var radius = Mathf.Sqrt(dx * dx + dz * dz);
                arcs.Add(new Arc { Center = pivot.position, Radius = radius, Low = bounds.min.y, High = bounds.max.y });
                Extent = Mathf.Max(Extent, Utils.DistanceXZ(door.transform.position, pivot.position) + radius);
            }
        }
    }

    internal bool Clear(Maid maid, Vector3 feet)
    {
        var actor = maid.GetComponent<Character>();
        foreach (var arc in arcs)
            if (feet.y + actor.GetHeight() > arc.Low && feet.y < arc.High
                && !DoorPolicy.OutsideSwing(feet.x - arc.Center.x, feet.z - arc.Center.z, arc.Radius, actor.GetRadius())) return false;
        return arcs.Count > 0;
    }
}
