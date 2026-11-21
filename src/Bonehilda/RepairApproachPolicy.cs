namespace Bonehilda;

internal static class RepairApproachPolicy
{
    internal static bool SupportsStep(float previousHeight, float floorHeight, float normalY) =>
        normalY >= .65f && System.Math.Abs(floorHeight - previousHeight) <= .4f;

    // Navmesh sampling can silently move the requested point to another floor.
    internal static bool EndpointMatches(float horizontalSquared, float heightDifference) =>
        horizontalSquared <= .65f * .65f && System.Math.Abs(heightDifference) <= .5f;

    internal static float Cost(float pathLength, float heightDifference) =>
        pathLength + System.Math.Abs(heightDifference) * .5f;

    internal static bool SameFailedSpot(float horizontalSquared, float heightDifference) =>
        horizontalSquared < .8f * .8f && System.Math.Abs(heightDifference) < .75f;
}
