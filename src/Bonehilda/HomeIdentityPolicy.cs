namespace Bonehilda;

internal static class HomeIdentityPolicy
{
    internal static bool HomeMatches(string maidToken, string homeToken, float savedDistanceSquared)
        => maidToken.Length > 0 ? maidToken == homeToken : savedDistanceSquared >= 0 && savedDistanceSquared <= .0625f;
    internal static bool MaidMatches(string maidToken, string homeToken, float savedDistanceSquared)
        => homeToken.Length > 0 && maidToken == homeToken
            || maidToken.Length == 0 && HomeMatches("", homeToken, savedDistanceSquared);
    internal static bool Unique(int count) => count == 1;
    internal static bool Linked(bool reciprocal, string maidToken, string homeToken, float savedDistanceSquared)
        => reciprocal && HomeMatches(maidToken, homeToken, savedDistanceSquared);
}
