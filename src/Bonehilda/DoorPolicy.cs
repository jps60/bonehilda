namespace Bonehilda;

internal static class DoorPolicy
{
    internal static bool EnemyNear(float doorDistanceSquared, float maidDistanceSquared, float radius) =>
        doorDistanceSquared <= radius * radius || maidDistanceSquared <= radius * radius;

    internal static bool RetreatExpired(double seconds) => seconds >= 6;
}
