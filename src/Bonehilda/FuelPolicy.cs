namespace Bonehilda;

internal static class FuelPolicy
{
    internal static bool Start(float current, float capacity, float secondsPerFuel, float lowFraction, float runway)
        => MaintenancePolicy.NeedsFuel(current, capacity, false) && (current <= 0
            || current / capacity < lowFraction
            || secondsPerFuel > 0 && current * secondsPerFuel < runway);
}
