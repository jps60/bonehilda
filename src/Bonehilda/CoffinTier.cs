namespace Bonehilda;

internal sealed class CoffinTier
{
    internal readonly string Prefab;
    internal readonly int Stars;
    internal readonly int Slots;
    internal CoffinIngredient[] Recipe => CoffinRecipes.ForTier(Stars);
    internal int Width => Slots == 2 ? 2 : 4;
    internal int Height => Slots / Width;
    internal string Name => $"Bonehilda's coffin ({Stars} star{(Stars == 1 ? "" : "s")})";

    private CoffinTier(string prefab, int stars, int slots)
    {
        Prefab = prefab; Stars = stars; Slots = slots;
    }

    // Retain the original prefab ID for existing worlds and their linked two-star maid.
    internal static readonly CoffinTier[] All =
    {
        new CoffinTier("BH_Coffin_0Star", 0, 2),
        new CoffinTier("BH_Coffin_1Star", 1, 4),
        new CoffinTier(Plugin.CoffinPrefab, 2, 8)
    };

    internal static bool IsCoffin(int prefabHash)
    {
        foreach (var tier in All) if (tier.Prefab.GetStableHashCode() == prefabHash) return true;
        return false;
    }
}
