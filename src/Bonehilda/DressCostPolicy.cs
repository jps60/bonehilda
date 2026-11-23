namespace Bonehilda;

internal static class DressCostPolicy
{
    internal static bool IsDress(string name)
    {
        const string prefix = "ArmorDress";
        return name != null && name.StartsWith(prefix, System.StringComparison.Ordinal)
            && int.TryParse(name.Substring(prefix.Length), out var number) && number >= 1 && number <= 10
            && name == prefix + number;
    }

    internal static bool Eligible(string name, bool equipped, int stack) => IsDress(name) && !equipped && stack > 0;
    internal static string Refund(bool recorded, string paid, int stars) => recorded
        ? IsDress(paid) ? paid : "" : stars == 0 ? "ArmorDress10" : stars == 1 ? "ArmorDress1" : "ArmorDress6";
}
