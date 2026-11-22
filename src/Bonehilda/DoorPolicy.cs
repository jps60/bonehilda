namespace Bonehilda;

internal static class DoorPolicy
{
    internal static bool EnemyNear(float doorDistanceSquared, float maidDistanceSquared, float radius) =>
        doorDistanceSquared <= radius * radius || maidDistanceSquared <= radius * radius;

    internal static bool RetreatExpired(double seconds) => seconds >= 6;

    // Signed distances along the doorway normal. Do not divert a same-side
    // job through a door, or open one merely to interact with the door itself.
    internal static bool CrossesPlane(float fromSide, float goalSide) =>
        fromSide * goalSide < 0f && System.Math.Abs(goalSide) > .35f;

    internal static bool SmallLedge(float height, float normalY) =>
        height >= .12f && height <= .65f && normalY >= .65f;
}
