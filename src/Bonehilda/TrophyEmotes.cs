using System.Collections.Generic;

namespace Bonehilda;

internal static class TrophyEmotes
{
    internal static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>(System.StringComparer.Ordinal)
    {
        ["TrophyEikthyr"] = new[] { "bow", "cheer" },
        ["TrophyTheElder"] = new[] { "bow", "headbang" },
        ["TrophyBonemass"] = new[] { "cower", "nonono" },
        ["TrophyDragonQueen"] = new[] { "bow", "blowkiss" },
        ["TrophyGoblinKing"] = new[] { "challenge", "laugh" },
        ["TrophySeekerQueen"] = new[] { "cower", "challenge" },
        ["TrophyFader"] = new[] { "roar", "flex" },
        ["TrophyHatchling"] = new[] { "point", "shrug" },
        ["CrownJewel"] = new[] { "bow", "cheer" },
        ["TrophySkeletonHildir"] = new[] { "headbang", "cheer" },
        ["TrophyCultist_Hildir"] = new[] { "cower", "bow" },
        ["TrophyGoblinBruteBrosBrute"] = new[] { "challenge", "cheer" },
        ["TrophyGoblinBruteBrosShaman"] = new[] { "challenge", "cheer" },
        ["TrophySkeleton"] = new[] { "point", "wave" },
        ["TrophySkeletonPoison"] = new[] { "point", "nonono" },
        ["TrophyGhost"] = new[] { "wave", "bow" },
        ["TrophyWraith"] = new[] { "cower", "point" },
        ["TrophyDraugr"] = new[] { "nonono", "shrug" },
        ["TrophyDraugrElite"] = new[] { "challenge", "shrug" },
        ["TrophyForestTroll"] = new[] { "laugh", "flex" }
    };

    internal static void AddBoss(string item)
    {
        if (!Map.ContainsKey(item)) Map.Add(item, new[] { "bow", "challenge" });
    }
}
