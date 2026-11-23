namespace Bonehilda;

// Only a new best remaining route renews the stall timer. Walking in circles,
// repeated path recalculations, and tiny movement at an obstruction do not.
internal sealed class WorkProgress
{
    private float best = float.PositiveInfinity, at;
    internal void Reset(float now = 0) { best = float.PositiveInfinity; at = now; }
    internal void Observe(float remaining, float now)
    {
        if (remaining >= best - .5f || float.IsNaN(remaining)) return;
        best = remaining; at = now;
    }
    internal bool Expired(float now, float started, float timeout) =>
        now - System.Math.Max(started, at) > timeout;
}
