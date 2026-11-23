using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal static class IdleShelter
{
    private sealed class Sample { internal int Tier; internal float Until; }
    private static readonly Dictionary<Vector3Int, Sample> cache = new Dictionary<Vector3Int, Sample>();
    internal static int Tier(Vector3 feet)
    {
        if (!Plugin.PreferShelter.Value) return 0;
        var point = feet + Vector3.up;
        var key = Vector3Int.FloorToInt(point * 2f);
        if (cache.TryGetValue(key, out var old) && Time.time < old.Until) return old.Tier;
        Cover.GetCoverForPoint(point, out var cover, out var roof);
        var tier = !roof ? 0 : cover >= .8f ? 2 : 1;
        if (cache.Count >= 1024) cache.Clear();
        cache[key] = new Sample { Tier = tier, Until = Time.time + 10f };
        return tier;
    }
}
