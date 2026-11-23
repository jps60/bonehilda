using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal static class SupplyInteraction
{
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
                    var hover = hit.collider.GetComponentInParent<Hoverable>();
                    var method = hover == null ? null : AccessTools.Method(hover.GetType(), "GetHoverOffset")
                        ?? AccessTools.Method(typeof(Hoverable), "GetHoverOffset");
                    var offset = method?.Invoke(hover, null) is float value ? value : 0f;
                    if (MaintenancePolicy.InRepairReach((hit.point - eye).sqrMagnitude, reach + offset)) return true;
                    break; // An obstruction or out-of-range surface blocks this aim.
                }
            }
        }
        return false;
    }
}
