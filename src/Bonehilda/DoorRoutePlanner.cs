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
        internal readonly List<Vector3> Tail = new List<Vector3>();
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> goalEntries = new List<Entry>();
    private readonly List<Vector3> middle = new List<Vector3>();
    private readonly List<Vector3> candidate = new List<Vector3>();
    private readonly List<Vector3> tail = new List<Vector3>();
    private Vector3 cachedFrom, cachedGoal;
    private float expires;
    private bool cached;
    internal string Describe { get; private set; } = "none";
    internal int Candidates { get; private set; }
    internal int RejectedThreshold { get; private set; }
    internal int RejectedApproach { get; private set; }

    internal bool TryPath(Maid maid, MonsterAI ai, Coffin home, Vector3 from, Vector3 goal, List<Vector3> result)
    {
        Describe = "none";
        if (Segment(maid, ai, from, goal, result)) return true;
        result.Clear();
        if (!Plugin.UseDoors.Value) return false;
        if (!cached || Time.time >= expires || (from - cachedFrom).sqrMagnitude > 1f || (goal - cachedGoal).sqrMagnitude > 1f)
            Refresh(maid, ai, home, from, goal);
        var bestCost = float.PositiveInfinity;
        var description = "none";
        void Consider(int doors, string label)
        {
            var cost = Length(candidate, from) + doors * 3f;
            if (cost >= bestCost) return;
            bestCost = cost; description = label;
            result.Clear(); result.AddRange(candidate);
        }
        foreach (var entry in entries.OrderBy(e => (goal - e.Exit).sqrMagnitude + (from - e.From).sqrMagnitude))
        {
            // A supported crossing joins two complete paths. The destination
            // can lie back on the starting side after the route bends around
            // a building; its side of the infinite door plane is irrelevant.
            if (!entry.Door || !DoorTransit.CanPlan(maid, home, entry.Door)
                || !Segment(maid, ai, entry.Exit, goal, tail)) continue;
            candidate.Clear(); candidate.AddRange(entry.Path);
            candidate.Add(entry.Exit); candidate.AddRange(tail);
            Consider(1, "via " + Utils.GetPrefabName(entry.Door.gameObject) + " " + entry.Door.GetComponent<ZNetView>().GetZDO().m_uid);
        }
        foreach (var entry in entries)
        foreach (var finish in goalEntries.OrderBy(e => (entry.Exit - e.From).sqrMagnitude))
        {
            var lowerBound = Vector3.Distance(from, entry.From) + Vector3.Distance(entry.From, entry.Exit)
                + Vector3.Distance(entry.Exit, finish.From) + Vector3.Distance(finish.From, finish.Exit)
                + Vector3.Distance(finish.Exit, goal) + 6f;
            if (lowerBound >= bestCost) continue;
            if (entry.Door == finish.Door || !entry.Door || !finish.Door
                || !DoorTransit.CanPlan(maid, home, entry.Door) || !DoorTransit.CanPlan(maid, home, finish.Door)
                || !Segment(maid, ai, entry.Exit, finish.From, middle)
                || !Segment(maid, ai, finish.Exit, goal, tail)) continue;
            candidate.Clear(); candidate.AddRange(entry.Path); candidate.Add(entry.Exit);
            candidate.AddRange(middle); candidate.Add(finish.Exit); candidate.AddRange(tail);
            Consider(2, "via two doors: " + Utils.GetPrefabName(entry.Door.gameObject) + ", " + Utils.GetPrefabName(finish.Door.gameObject));
        }
        Describe = description;
        return result.Count > 0;
    }

    private static float Length(List<Vector3> nodes, Vector3 from)
    {
        var length = 0f;
        foreach (var node in nodes) { length += Vector3.Distance(from, node); from = node; }
        return length;
    }

    private void Refresh(Maid maid, MonsterAI ai, Coffin home, Vector3 from, Vector3 goal)
    {
        cached = true; expires = Time.time + 2f; cachedFrom = from; cachedGoal = goal; entries.Clear(); goalEntries.Clear();
        Candidates = RejectedThreshold = RejectedApproach = 0;
        var available = MaidDoorWatch.Loaded.Where(d => d && DoorTransit.CanPlan(maid, home, d)).ToArray();
        // A return route needs the house entrance even when many farm gates
        // are nearer the machine. Bound both endpoint neighborhoods.
        foreach (var door in available.OrderBy(d => (d.transform.position - from).sqrMagnitude).Take(8)
                     .Concat(available.OrderBy(d => (d.transform.position - goal).sqrMagnitude).Take(8)).Distinct())
        {
            Candidates++;
            DoorTransit.Geometry(door, out var center, out var normal, out var halfDepth);
            var clearance = DoorPolicy.CrossingDistance(maid.GetComponent<Character>().GetRadius(), halfDepth);
            foreach (var side in new[] { 1f, -1f })
            {
                var approach = center + normal * side * clearance;
                var exit = center - normal * side * clearance;
                // Sample the threshold's floor, not the maid's current storey.
                if (!IdleActivities.Floor(approach, door.transform.position.y, out approach) || !IdleActivities.Floor(exit, door.transform.position.y, out exit)
                    || !MaintenancePolicy.InRadius((approach - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
                    || !MaintenancePolicy.InRadius((exit - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
                    || !Maid.Allowed(approach, home.Piece.GetCreator()) || !Maid.Allowed(exit, home.Piece.GetCreator())
                    || !RepairGeometry.ClearDoorway(maid, approach, exit, door)) { RejectedThreshold++; continue; }
                var entry = new Entry { Door = door, From = approach, Exit = exit, Center = center, Normal = normal };
                if (Segment(maid, ai, from, approach, entry.Path)) entries.Add(entry);
                else RejectedApproach++;
                if (Segment(maid, ai, exit, goal, entry.Tail)) goalEntries.Add(entry);
            }
        }
    }

    internal static bool Segment(Maid maid, MonsterAI ai, Vector3 from, Vector3 goal, List<Vector3> path)
    {
        path.Clear();
        if (RepairGeometry.ClearWalk(maid.GetComponent<CapsuleCollider>(), from, goal, 4.5f))
        { path.Add(goal); return true; }
        if (!Pathfinding.instance.GetPath(from, goal, path, ai.m_pathAgentType, true, false) || path.Count == 0) return false;
        var end = path[path.Count - 1] - goal;
        return RepairApproachPolicy.EndpointMatches(end.x * end.x + end.z * end.z, end.y);
    }
}
