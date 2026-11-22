namespace Bonehilda;

internal enum MovementPace { Idle, Supply, Repair }

internal static class MovementPolicy
{
    internal static MovementPace Select(bool engaged, bool repair, bool carrying) =>
        engaged ? repair ? MovementPace.Repair : MovementPace.Supply : carrying ? MovementPace.Supply : MovementPace.Idle;

    // BaseAI's running path follower has a larger arrival tolerance. Walk the
    // final steps to keep precise repair stances, doors, and coffin entry safe.
    internal static bool Run(MovementPace pace, float distanceSquared, bool precision) =>
        pace == MovementPace.Repair && !precision && distanceSquared > 4f;
}
