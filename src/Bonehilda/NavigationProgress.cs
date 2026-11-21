namespace Bonehilda;

// Accumulate only frames where navigation is actually requested. Door/chest waits
// and work animations must never be mistaken for an obstructed route.
internal sealed class NavigationProgress
{
    private bool tracking;
    private float anchorX, anchorZ, goalX, goalY, goalZ, stalled;

    internal void Reset() { tracking = false; stalled = 0; }

    internal bool Advance(float delta, float x, float z, float targetX, float targetY, float targetZ, float timeout)
    {
        var changed = Squared(targetX - goalX, targetY - goalY, targetZ - goalZ) > 1f;
        if (!tracking || changed || Squared(x - anchorX, 0, z - anchorZ) >= .25f * .25f)
        {
            tracking = true;
            anchorX = x; anchorZ = z;
            goalX = targetX; goalY = targetY; goalZ = targetZ;
            stalled = 0;
            return false;
        }
        // A long frame cannot count as several seconds of observed immobility.
        stalled += System.Math.Max(0, System.Math.Min(delta, .25f));
        if (stalled < timeout) return false;
        stalled = 0;
        anchorX = x; anchorZ = z;
        return true;
    }

    private static float Squared(float x, float y, float z) => x * x + y * y + z * z;
}
