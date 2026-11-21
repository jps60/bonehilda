namespace Bonehilda;

// Kept independent of Unity so radius/capacity decisions can be exercised without the game.
internal static class MaintenancePolicy
{
    internal static bool NearCoffinApproach(float x, float y, float z) =>
        System.Math.Abs(x) <= 1.2f && System.Math.Abs(y) <= 0.8f && z >= 0.6f && z <= 3.2f;
    internal static bool InsideCoffin(float x, float y, float z) =>
        System.Math.Abs(x) <= 0.12f && System.Math.Abs(y) <= 0.65f && z >= -0.10f && z <= 0.07f;
    internal static int StorageRows(int stacks, int width, int baseRows) => System.Math.Max(baseRows, (stacks + width - 1) / width);
    internal static bool InRadius(float distanceSquared, float radius) => distanceSquared <= radius * radius;
    internal static bool NeedsFuel(float current, float maximum, bool infinite) =>
        !infinite && maximum > 0f && !float.IsNaN(current) && current >= 0f && current <= maximum - 1f;
    internal static bool NeedsRepair(float healthFraction) => healthFraction > 0f && healthFraction < 0.999f;
    // Native Player.Repair measures strictly less than m_maxPlaceDistance from
    // the eyes to the hit surface, including vertical distance.
    internal static bool InRepairReach(float distanceSquared, float reach) =>
        distanceSquared >= 0f && reach > 0f && distanceSquared < reach * reach;
    internal static bool NeedsInput(int queued, int capacity) => queued >= 0 && capacity > 0 && queued < capacity;
    internal static bool CanLoadAmmo(int loaded, int capacity, string currentType, string incomingType) =>
        NeedsInput(loaded, capacity) && (loaded == 0 || currentType == incomingType);
    internal static bool ExcludedConsumer(bool cookingStation, bool obliterator) => cookingStation || obliterator;
    internal static float WanderRange(float requested, float workRadius) => System.Math.Min(requested, System.Math.Max(0f, workRadius - 2f));
    internal static bool AcceptedSupply(float before, float after, bool itemIdentity) =>
        itemIdentity ? before == 0 && after != 0 : after > before;
    internal static bool SupplyReady(long now, long lastDelivery, float interval) =>
        lastDelivery == 0 || (now >= lastDelivery && (now - lastDelivery) / (double)System.TimeSpan.TicksPerSecond >= interval);
}
