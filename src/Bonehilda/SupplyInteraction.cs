using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal static class SupplyInteraction
{
    private static float HoverOffset(Collider collider)
    {
        var hover = collider.GetComponentInParent<Hoverable>();
        var method = hover == null ? null : AccessTools.Method(hover.GetType(), "GetHoverOffset")
            ?? AccessTools.Method(typeof(Hoverable), "GetHoverOffset");
        return method?.Invoke(hover, null) is float value ? value : 0f;
    }

    internal static float Reach(Piece piece, Transform? port, float nativeReach)
    {
        var reach = nativeReach;
        foreach (var collider in (port ? port! : piece.transform).GetComponentsInChildren<Collider>())
            if (collider.enabled && collider.gameObject.activeInHierarchy) reach = Mathf.Max(reach, nativeReach + HoverOffset(collider));
        return reach;
    }
    // Player.FindHoverObject uses this mask, the actual hit surface, a strict
    // eye-distance check, and the hit Hoverable's optional distance offset.
    private static int Mask => LayerMask.GetMask("item", "piece", "piece_nonsolid", "Default", "static_solid",
        "Default_small", "character", "character_net", "terrain", "vehicle", "character_ghost");

    internal static bool Visible(Piece piece, Transform? port, Transform self, Vector3 eye, float reach)
    {
        var root = port ? port! : piece.transform;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            foreach (var aim in RepairGeometry.Probes(collider, eye))
            {
                var direction = aim - eye;
                if (direction.sqrMagnitude < .0001f) continue;
                foreach (var hit in Physics.RaycastAll(eye, direction.normalized, 50f, Mask).OrderBy(h => h.distance))
                {
                    if (hit.collider.transform.IsChildOf(self)) continue;
                    if (hit.collider.GetComponentInParent<Piece>() != piece
                        || port && !hit.collider.transform.IsChildOf(port!)) break;
                    if (MaintenancePolicy.InRepairReach((hit.point - eye).sqrMagnitude, reach + HoverOffset(hit.collider))) return true;
                    break; // An obstruction or out-of-range surface blocks this aim.
                }
            }
        }
        return false;
    }
}
