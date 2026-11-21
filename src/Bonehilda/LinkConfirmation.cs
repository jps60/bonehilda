using System;
using System.Collections.Generic;

namespace Bonehilda;

// Require repeated server observations: a single incomplete spawn/update
// packet must not delete a maid or make a coffin spawn a duplicate.
internal sealed class LinkConfirmation
{
    private sealed class Pending
    {
        internal string Link = "";
        internal long Owner, Since, Seen;
    }
    private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
    internal void Clear() => pending.Clear();

    internal bool Confirm(string holder, string link, long owner, bool valid, long now)
    {
        foreach (var key in new List<string>(pending.Keys))
            if (now < pending[key].Seen || now - pending[key].Seen > 60L * TimeSpan.TicksPerSecond) pending.Remove(key);
        if (valid) { pending.Remove(holder); return false; }
        if (!pending.TryGetValue(holder, out var observation) || observation.Link != link || observation.Owner != owner)
        {
            pending[holder] = new Pending { Link = link, Owner = owner, Since = now, Seen = now };
            return false;
        }
        observation.Seen = now;
        if (now - observation.Since < 5L * TimeSpan.TicksPerSecond) return false;
        pending.Remove(holder);
        return true;
    }
}
