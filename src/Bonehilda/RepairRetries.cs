namespace Bonehilda;

// This budget belongs to the job, not the current path. Replanning or moving
// away from an obstruction must not replenish it.
internal sealed class RepairRetries
{
    private int retries;
    internal void Reset() => retries = 0;
    internal bool TryAgain()
    {
        if (retries >= 3) return false;
        retries++;
        return true;
    }
}
