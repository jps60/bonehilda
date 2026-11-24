namespace Bonehilda;

internal static class TameCrowdPolicy
{
    internal static bool HasPlanarDirection(float x, float z)
    {
        var squared = x * x + z * z;
        return !float.IsNaN(squared) && !float.IsInfinity(squared) && squared > .000001f;
    }

    // Price extra walking time against a 1.5-second disruption penalty. Never
    // shoo until actual crowd immobility has exceeded the configured delay.
    internal static bool PreferShoo(float extraMeters, float speed, float stalled, float delay) =>
        stalled >= delay && (float.IsPositiveInfinity(extraMeters) || extraMeters / System.Math.Max(.5f, speed) > 1.5f);
    internal static bool WithinSegment(float remaining, float rejoin) => rejoin <= remaining + .1f;

    internal static bool InShooCone(float x, float z, float forwardX, float forwardZ, float radius, float cosine)
    {
        var distance = System.Math.Sqrt(x * x + z * z);
        var forwardLength = System.Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
        return distance > .01 && distance <= radius && forwardLength > .01
            && (x * forwardX + z * forwardZ) / (distance * forwardLength) >= cosine;
    }

    // A tame already touching the maid may move sideways or away, never closer.
    // Otherwise its entire segment must stay outside her personal space.
    internal static bool AvoidsMaid(float fromX, float fromZ, float toX, float toZ, float clearance)
    {
        var x = toX - fromX; var z = toZ - fromZ;
        var lengthSquared = x * x + z * z;
        if (lengthSquared < .01f) return false;
        var fraction = System.Math.Max(0, System.Math.Min(1, -(fromX * x + fromZ * z) / lengthSquared));
        var nearestX = fromX + fraction * x; var nearestZ = fromZ + fraction * z;
        var minimum = System.Math.Max(0, System.Math.Min(clearance, System.Math.Sqrt(fromX * fromX + fromZ * fromZ)) - .02);
        return nearestX * nearestX + nearestZ * nearestZ >= minimum * minimum
            && toX * toX + toZ * toZ >= clearance * clearance;
    }
}
