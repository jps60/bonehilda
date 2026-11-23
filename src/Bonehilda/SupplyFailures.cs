using System;
using System.Collections.Generic;

namespace Bonehilda;

internal sealed class SupplyFailures
{
    private readonly Dictionary<string, int> failures = new Dictionary<string, int>();
    internal float Failed(string target)
    {
        failures.TryGetValue(target, out var count);
        if (failures.Count >= 256 && count == 0) failures.Clear();
        count = Math.Min(5, count + 1); failures[target] = count;
        return Math.Min(600f, 60f * (1 << (count - 1)));
    }
    internal void Succeeded(string target) => failures.Remove(target);
}
