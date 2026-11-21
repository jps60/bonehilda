using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

internal sealed class RepairApproach
{
    internal enum Result { Searching, Ready, Failed }
    private readonly List<Vector3> columns = new List<Vector3>();
    private readonly List<Vector3> failed = new List<Vector3>();
    private readonly List<Vector3> path = new List<Vector3>();
    private readonly List<Vector3> bestPath = new List<Vector3>();
    private Piece? target;
    private int cursor, solutions;
    private float topY, bottomY, bestCost, nextSearch, nextValidation;
    private bool chosen, initialized, bestDirect;
    private Vector3 best, sight;
    internal string State { get; private set; } = "none";
    internal Vector3 Position => chosen ? best : Vector3.zero;
    internal Vector3 SightPoint => sight;
    internal int Probes => cursor;
    internal bool Direct => chosen && bestDirect;
    internal int RejectedSpace { get; private set; }
    internal int RejectedSight { get; private set; }
    internal int RejectedPath { get; private set; }

    internal void Reset()
    {
        target = null; failed.Clear(); chosen = initialized = false;
        best = sight = Vector3.zero;
        RejectedSpace = RejectedSight = RejectedPath = 0;
        nextSearch = 0; cursor = 0; State = "none";
    }

    internal void Retry()
    {
        if (chosen) failed.Add(best);
        chosen = initialized = false; nextSearch = Time.time + 1f; State = "retry delay";
    }

    internal Result Tick(Maid maid, MonsterAI ai, Coffin home, Piece piece, out Vector3 destination)
    {
        destination = best;
        if (target != piece) { Reset(); target = piece; }
        if (Time.time < nextSearch) return Result.Searching;
        if (chosen)
        {
            var offset = best - maid.transform.position; offset.y = 0;
            if (Time.time >= nextValidation || offset.magnitude < .3f)
            {
                nextValidation = Time.time + 1f;
                if (!Usable(maid, home, piece, best, out sight) || offset.magnitude < .3f
                    || (bestDirect && !RepairGeometry.ClearWalk(maid, best)))
                { State = "standing spot obstructed"; return Result.Failed; }
            }
            State = "walking to visible surface";
            return Result.Ready;
        }
        if (!initialized) Begin(maid, piece);
        // Spread unsuccessful searches across frames, rather than performing
        // dozens of native path calculations for each maid in one frame.
        for (var count = 0; cursor < columns.Count && count < 4; count++, cursor++)
        {
            var probe = columns[cursor]; probe.y = topY;
            var floors = Physics.RaycastAll(probe, Vector3.down, topY - bottomY, RepairGeometry.Solids, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y >= .65f).OrderBy(h => Mathf.Abs(h.point.y - maid.transform.position.y)).Take(3);
            foreach (var floor in floors)
            {
                var feet = floor.point + Vector3.up * .03f;
                if (failed.Any(p => RepairApproachPolicy.SameFailedSpot(HorizontalSquared(p - feet), p.y - feet.y))
                    || !Usable(maid, home, piece, feet, out var aim)) continue;
                path.Clear();
                var direct = RepairGeometry.ClearWalk(maid, feet);
                if (direct) path.Add(feet);
                else
                {
                    if (!Pathfinding.instance.GetPath(maid.transform.position, feet, path, ai.m_pathAgentType, true, false)
                        || path.Count == 0) { RejectedPath++; continue; }
                    var end = path[path.Count - 1] - feet;
                    if (!RepairApproachPolicy.EndpointMatches(HorizontalSquared(end), end.y)) { RejectedPath++; continue; }
                }
                var length = 0f; var previous = maid.transform.position;
                foreach (var node in path) { length += Vector3.Distance(previous, node); previous = node; }
                length += Vector3.Distance(previous, feet);
                var cost = RepairApproachPolicy.Cost(length, feet.y - maid.transform.position.y);
                if (cost < bestCost)
                {
                    best = feet; bestCost = cost; sight = aim; bestDirect = direct;
                    bestPath.Clear(); bestPath.AddRange(path);
                }
                solutions++;
            }
            if (solutions >= 3) break;
        }
        if (solutions > 0 && (solutions >= 3 || cursor >= columns.Count))
        {
            chosen = true; nextValidation = Time.time + 1f; destination = best;
            // Follow the complete path just validated, rather than an older
            // cached route or a newly calculated partial path.
            ai.m_path.Clear(); ai.m_path.AddRange(bestPath);
            ai.m_lastFindPathTarget = best; ai.m_lastFindPathTime = Time.time; ai.m_lastFindPathResult = true;
            State = "walking to visible surface";
            return Result.Ready;
        }
        State = cursor >= columns.Count ? "no reachable sightline" : "searching standing spots";
        return cursor >= columns.Count ? Result.Failed : Result.Searching;
    }

    private void Begin(Maid maid, Piece piece)
    {
        initialized = true; cursor = solutions = 0; columns.Clear(); bestCost = float.PositiveInfinity;
        RejectedSpace = RejectedSight = RejectedPath = 0;
        var feet = maid.transform.position;
        var anchor = RepairGeometry.Aim(piece, maid.GetComponent<Humanoid>().GetEyePoint());
        var bounds = new Bounds(piece.transform.position, Vector3.zero);
        foreach (var collider in piece.GetComponentsInChildren<Collider>())
            if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
        topY = Mathf.Max(feet.y + 1f, bounds.max.y + .5f);
        bottomY = bounds.min.y - Plugin.RepairReach - 2f;
        foreach (var center in new[] { feet, anchor })
        {
            columns.Add(center);
            foreach (var radius in new[] { 1f, 2f, 3.3f, 4.3f })
                for (var i = 0; i < 16; i++)
                {
                    var angle = i * Mathf.PI * 2f / 16f;
                    columns.Add(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                }
        }
        columns.Sort((a, b) => HorizontalSquared(a - feet).CompareTo(HorizontalSquared(b - feet)));
    }

    private bool Usable(Maid maid, Coffin home, Piece piece, Vector3 feet, out Vector3 aim)
    {
        aim = default;
        if (!MaintenancePolicy.InRadius((feet - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
            || !Maid.Allowed(feet, home.Piece.GetCreator()) || !maid.HasRepairStation(piece, feet) || !RepairGeometry.Fits(maid, feet))
        { RejectedSpace++; return false; }
        var eyeOffset = maid.GetComponent<Humanoid>().GetEyePoint() - maid.transform.position;
        if (RepairGeometry.VisibleSurface(piece, feet + eyeOffset, Plugin.RepairReach - .4f, out aim)) return true;
        RejectedSight++; return false;
    }

    private static float HorizontalSquared(Vector3 delta) => delta.x * delta.x + delta.z * delta.z;
}
