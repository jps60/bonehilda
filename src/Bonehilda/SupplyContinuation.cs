namespace Bonehilda;

internal static class SupplyContinuation
{
    internal static bool Keep(bool valid, bool stocked, int nextPriority, int currentPriority)
        => valid && stocked && nextPriority >= currentPriority;
}
