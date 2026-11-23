namespace Bonehilda;

internal readonly struct CoffinIngredient
{
    internal readonly string Item;
    internal readonly int Amount;
    internal CoffinIngredient(string item, int amount) { Item = item; Amount = amount; }
}

internal static class CoffinRecipes
{
    internal static CoffinIngredient[] ForTier(int stars)
    {
        switch (stars)
        {
            case 0: return new[] {
                new CoffinIngredient("FineWood", 20), new CoffinIngredient("BoneFragments", 10),
                new CoffinIngredient("Ectoplasm", 2), new CoffinIngredient("TrophySkeleton", 1),
                new CoffinIngredient("ArmorDress10", 1) // Sentinel: one alternative Hildir dress.
            };
            case 1: return new[] {
                new CoffinIngredient("ElderBark", 25), new CoffinIngredient("BoneFragments", 10),
                new CoffinIngredient("WitheredBone", 5), new CoffinIngredient("Ectoplasm", 4),
                new CoffinIngredient("TrophySkeleton", 1), new CoffinIngredient("ArmorDress10", 1)
            };
            default: return new[] {
                new CoffinIngredient("ElderBark", 30), new CoffinIngredient("BoneFragments", 20),
                new CoffinIngredient("WitheredBone", 10), new CoffinIngredient("Ectoplasm", 8),
                new CoffinIngredient("TrophySkeleton", 1), new CoffinIngredient("ArmorDress10", 1)
            };
        }
    }
}
