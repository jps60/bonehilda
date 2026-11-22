using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

// A closed door can split the native navmesh. Join complete paths on either
// side with a supported, physically clear doorway crossing. Planning never
// opens a door or disables colliders; DoorTransit still owns the interaction.
internal sealed class DoorRoutePlanner
{
    private sealed class Entry
    {
        internal Door Door = null!;
        internal Vector3 From, Exit, Center, Normal;
        internal readonly List<Vector3> Path = new List<Vector3>();
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Vector3> tail = new List<Vector3>();
    private Vector3 cachedFrom;
    private float expires;
    private bool cached;
    internal string Describe { get; private set; } = "none";

    internal bool TryPath(Maid maid, MonsterAI ai, Coffin home, Vector3 from, Vector3 goal, List<Vector3> result)
    {
        Describe = "none";
        if (Segment(maid, ai, from, goal, result)) return true;
        result.Clear();
        if (!Plugin.UseDoors.Value) return false;
        if (!cached || Time.time >= expires || (from - cachedFrom).sqrMagnitude > 1f)
            Refresh(maid, ai, home, from);
        foreach (var entry in entries.OrderBy(e => (goal - e.Exit).sqrMagnitude + (from - e.From).sqrMagnitude))
        {
            if (!entry.Door || !DoorTransit.CanPlan(maid, home, entry.Door)
                || !DoorPolicy.CrossesPlane(Vector3.Dot(from - entry.Center, entry.Normal), Vector3.Dot(goal - entry.Center, entry.Normal))
                || !Segment(maid, ai, entry.Exit, goal, tail)) continue;
            result.AddRange(entry.Path);
            result.Add(entry.Exit);
            result.AddRange(tail);
            Describe = "via " + Utils.GetPrefabName(entry.Door.gameObject) + " " + entry.Door.GetComponent<ZNetView>().GetZDO().m_uid;
            return true;
        }
        return false;
    }

    private void Refresh(Maid maid, MonsterAI ai, Coffin home, Vector3 from)
    {
        cached = true; expires = Time.time + 2f; cachedFrom = from; entries.Clear();
        foreach (var door in MaidDoorWatch.Loaded.Where(d => d && DoorTransit.CanPlan(maid, home, d))
                     .OrderBy(d => (d.transform.position - from).sqrMagnitude).Take(8))
        {
            DoorTransit.Geometry(door, out var center, out var normal, out var clearance);
            var side = Vector3.Dot(from - center, normal) >= 0 ? 1f : -1f;
            var approach = center + normal * side * (clearance + .6f);
            var exit = center - normal * side * (clearance + .6f);
            if (!IdleActivities.Floor(approach, from.y, out approach) || !IdleActivities.Floor(exit, from.y, out exit)
                || !MaintenancePolicy.InRadius((approach - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
                || !MaintenancePolicy.InRadius((exit - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
                || !Maid.Allowed(approach, home.Piece.GetCreator()) || !Maid.Allowed(exit, home.Piece.GetCreator())
                || !RepairGeometry.ClearDoorway(maid, approach, exit, door)) continue;
            var entry = new Entry { Door = door, From = approach, Exit = exit, Center = center, Normal = normal };
            if (Segment(maid, ai, from, approach, entry.Path)) entries.Add(entry);
        }
    }

    private static bool Segment(Maid maid, MonsterAI ai, Vector3 from, Vector3 goal, List<Vector3> path)
    {
        path.Clear();
        if (RepairGeometry.ClearWalk(maid.GetComponent<CapsuleCollider>(), from, goal, 4.5f))
        { path.Add(goal); return true; }
        if (!Pathfinding.instance.GetPath(from, goal, path, ai.m_pathAgentType, true, false) || path.Count == 0) return false;
        var end = path[path.Count - 1] - goal;
        return RepairApproachPolicy.EndpointMatches(end.x * end.x + end.z * end.z, end.y);
    }
}
