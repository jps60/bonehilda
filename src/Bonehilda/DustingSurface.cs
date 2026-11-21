using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

// Piece colliders can sit several centimetres above boards or below a closed lid.
// Query the visible meshes once at the start of the chore. The temporary colliders
// are disabled before the next physics step and never participate in gameplay.
internal sealed class DustingSurface : IDisposable
{
    private readonly List<MeshCollider> probes = new List<MeshCollider>();
    private readonly IReadOnlyList<Collider> faces;

    internal DustingSurface(Piece piece)
    {
        var lowerLods = new HashSet<Renderer>();
        foreach (var group in piece.GetComponentsInChildren<LODGroup>())
        {
            var levels = group.GetLODs();
            for (var i = 1; i < levels.Length; i++)
                foreach (var renderer in levels[i].renderers) lowerLods.Add(renderer);
        }
        foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!filter.sharedMesh || !renderer || !renderer.enabled || lowerLods.Contains(renderer)) continue;
            var probe = new GameObject("BH_DustingSurface") { layer = 2, hideFlags = HideFlags.DontSave };
            probe.transform.SetParent(filter.transform, false);
            var collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            probes.Add(collider);
        }
        // Some modded or headless prefabs expose only physical surfaces.
        if (probes.Count > 0) faces = probes;
        else faces = piece.GetComponentsInChildren<Collider>();
    }

    internal bool TryStroke(Vector3 point, Vector3 forward, int stars, out Vector3 left, out Vector3 side, out Vector3 far)
    {
        left = side = far = point;
        if (!TryTop(point, out var top)) return false;
        var right = Vector3.Cross(Vector3.up, forward);
        var center = SweepEnd(top, forward * .10f);
        left = SweepEnd(center, -right * .23f, .055f);
        side = SweepEnd(left, right * .60f, .060f);
        var advance = forward * (.20f + .025f * stars) + right * .12f;
        far = SweepEnd(left, advance, .025f);
        // If a groove or tool blocks the reach, begin a little farther along
        // the same board so both passes share a patch of usable wood.
        var originalLeft = left;
        for (var i = 1; i <= 2 && Vector3.Dot(far - left, forward) < .12f; i++)
        {
            if (!TryTop(Vector3.Lerp(originalLeft, side, i * .18f), out var candidate)) continue;
            var reach = SweepEnd(candidate, advance, .025f);
            if (Vector3.Dot(reach - candidate, forward) <= Vector3.Dot(far - left, forward)) continue;
            left = candidate;
            far = reach;
        }
        if ((side - left).sqrMagnitude < .01f) side = far;
        if ((far - left).sqrMagnitude < .01f) far = side;
        return (side - left).sqrMagnitude >= .01f;
    }

    private bool TryTop(Vector3 near, out Vector3 point)
    {
        var ray = new Ray(near + Vector3.up * .12f, Vector3.down);
        var distance = .24f;
        point = near;
        var found = false;
        foreach (var face in faces)
        {
            if (!face.enabled || face.isTrigger || !face.Raycast(ray, out var hit, distance) || hit.normal.y < .80f) continue;
            distance = hit.distance;
            point = hit.point;
            found = true;
        }
        return found;
    }

    private Vector3 SweepEnd(Vector3 from, Vector3 offset, float inset = 0f)
    {
        var end = from;
        var steps = Mathf.CeilToInt(offset.magnitude / .04f);
        for (var i = 1; i <= steps; i++)
        {
            var sample = from + offset * ((float)i / steps);
            sample.y = end.y;
            if (!TryTop(sample, out var top))
            {
                // The fan bridges narrow joints between planks when wood
                // supports both sides. An open edge still ends the stroke.
                var span = offset.normalized * .025f;
                if (!TryTop(sample - span, out var before) || !TryTop(sample + span, out var after)) break;
                top = (before + after) * .5f;
            }
            if (Mathf.Abs(top.y - end.y) > .035f) break;
            end = top;
        }
        // Leave the body of the feather fan over the wood at the turnaround.
        var inside = Vector3.MoveTowards(end, from, inset);
        return TryTop(inside, out var restingPoint) ? restingPoint : end;
    }

    public void Dispose()
    {
        foreach (var face in probes)
        {
            face.enabled = false;
            UnityEngine.Object.Destroy(face.gameObject);
        }
    }
}
