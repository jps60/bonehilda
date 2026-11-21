using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Bonehilda;

[BepInPlugin(Guid, "Bonehilda", Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.bonehilda.valheim";
    public const string Version = "0.4.1";
    internal const string CoffinPrefab = "BH_Coffin";
    internal const string MaidPrefab = "BH_Bonehilda";
    internal static Plugin Instance = null!;
    internal static ConfigEntry<float> Radius = null!;
    internal static ConfigEntry<float> ScanInterval = null!;
    internal static ConfigEntry<float> WorkInterval = null!;
    internal static ConfigEntry<float> SupplyInterval = null!;
    internal static ConfigEntry<float> RespawnDelay = null!;
    internal static ConfigEntry<bool> Repair = null!;
    internal static ConfigEntry<bool> Refuel = null!;
    internal static ConfigEntry<float> JobTimeout = null!;
    internal static ConfigEntry<bool> SupplyMachines = null!;
    internal static ConfigEntry<bool> Wander = null!;
    internal static ConfigEntry<float> WanderRadius = null!;
    internal static HashSet<string> LegacySkeletonNames = new HashSet<string>(StringComparer.Ordinal);
    private Harmony harmony = null!;
    private bool registered;

    private void Awake()
    {
        Instance = this;
        Radius = Setting("Work", "Radius", 30f, "Work radius in meters, centered on the coffin.", 5f, 100f);
        ScanInterval = Setting("Work", "ScanInterval", 5f, "Seconds between searches for work.", 1f, 60f);
        WorkInterval = Setting("Work", "ActionInterval", 2f, "Minimum seconds between repairs; the draw/swing/holster animation must also finish.", 0.5f, 30f);
        SupplyInterval = Setting("Work", "SupplyInterval", 1f, "Minimum seconds between accepted supply items per maid, across all destinations. Travel and interaction animations can take longer.", 0.5f, 60f);
        RespawnDelay = Setting("Maid", "RespawnDelay", 60f, "Seconds before replacing a dead Bonehilda.", 5f, 3600f);
        JobTimeout = Setting("Work", "JobTimeout", 30f, "Skip jobs she cannot reach within this many seconds.", 5f, 120f);
        Repair = Config.Bind("Work", "RepairStructures", true, new ConfigDescription("Repair damaged player-built pieces for free, like a hammer.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        Refuel = Config.Bind("Work", "RefuelStructures", true, new ConfigDescription("Supply fireplaces, torches, hearths, and braziers from the coffin.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        SupplyMachines = Config.Bind("Work", "SupplyMachines", true, new ConfigDescription("Deliver accepted inputs and fuel to processors, fermenters, shields, and ballistae. Cooking stations and obliterators are excluded.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        Wander = Config.Bind("Maid", "IdleWander", true, new ConfigDescription("Free will enabled: wander near the coffin when there are no chores.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        WanderRadius = Setting("Maid", "WanderRadius", 10f, "Idle wandering distance from the coffin, limited by the work radius.", 2f, 50f);
        harmony = new Harmony(Guid);
        harmony.PatchAll();
        PrefabManager.OnVanillaPrefabsAvailable += Register;
    }

    private ConfigEntry<float> Setting(string section, string key, float value, string description, float min, float max) =>
        Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max),
            new ConfigurationManagerAttributes { IsAdminOnly = true }));

    private void Register()
    {
        if (registered) return;
        try
        {
            WorkSounds.Configure();
            var maid = new CustomCreature(MaidPrefab, "Skeleton_Friendly", new CreatureConfig { Name = "Bonehilda" });
            var prefab = maid.Prefab;
            var character = prefab.GetComponent<Humanoid>();
            character.m_name = "Bonehilda";
            character.m_faction = Character.Faction.Players;
            // Keep the skeleton avatar and level effects, retargeting player animations.
            character.m_defaultItems = Array.Empty<GameObject>();
            character.m_randomWeapon = Array.Empty<GameObject>();
            character.m_randomShield = Array.Empty<GameObject>();
            character.m_randomArmor = Array.Empty<GameObject>();
            WorkAnimations.Configure(prefab, PrefabManager.Instance.GetPrefab("Player"));
            Visuals.Dress(prefab.transform);
            // Apply after binding the garment so both the body and cloth grow together.
            prefab.GetComponentInChildren<Animator>(true).transform.localScale *= 1.04f;
            prefab.AddComponent<FeatherDuster>();
            var drops = prefab.GetComponent<CharacterDrop>();
            if (drops) drops.m_drops.Clear();
            var tame = prefab.GetComponent<Tameable>() ?? prefab.AddComponent<Tameable>();
            LegacySkeletonNames = new HashSet<string>(tame.m_randomStartingName, StringComparer.Ordinal);
            if (Localization.instance != null)
                foreach (var name in tame.m_randomStartingName) LegacySkeletonNames.Add(Localization.instance.Localize(name));
            tame.m_randomStartingName = new List<string>();
            tame.m_commandable = false;
            // Summoned skeletons normally expire. This one belongs to her coffin indefinitely.
            tame.m_startsTamed = true;
            tame.m_unsummonDistance = 0f;
            tame.m_unsummonOnOwnerLogoutSeconds = 0f;
            var ai = prefab.GetComponent<MonsterAI>();
            ai.m_despawnInDay = false;
            ai.m_randomMoveInterval = 2f;
            ai.m_consumeItems.Clear();
            prefab.AddComponent<Maid>();
            if (!CreatureManager.Instance.AddCreature(maid))
                throw new InvalidOperationException("Jotunn rejected the Bonehilda creature prefab.");

            foreach (var tier in CoffinTier.All) RegisterCoffin(tier);
            registered = true;
            Logger.LogInfo("Registered Bonehilda and three upright coffin tiers (2/4/8 slots).");
        }
        catch (Exception e)
        {
            Logger.LogError($"Could not register Bonehilda: {e}");
        }
    }

    private void RegisterCoffin(CoffinTier tier)
    {
        var requirements = new List<RequirementConfig>
        {
            new RequirementConfig("Wood", tier.Wood, 0, true),
            new RequirementConfig("BoneFragments", tier.Bones, 0, true)
        };
        if (tier.Bronze > 0) requirements.Add(new RequirementConfig("Bronze", tier.Bronze, 0, true));
        var coffin = new CustomPiece(tier.Prefab, "piece_chest_wood", new PieceConfig
        {
            Name = tier.Name,
            Description = $"A little Makin' Magic. Summons a {tier.Stars}-star Bonehilda; {tier.Slots} supply slots. Free will included. No bladder bar.",
            PieceTable = "Hammer",
            Category = "Furniture",
            Usage = new[] { "Furniture", "Storage" },
            CraftingStation = "piece_workbench",
            Requirements = requirements.ToArray()
        });
        var container = coffin.PiecePrefab.GetComponent<Container>();
        container.m_name = tier.Name;
        container.m_width = tier.Width;
        container.m_height = tier.Height;
        Visuals.BuildCoffin(coffin.PiecePrefab, tier.Stars);
        var furnishing = coffin.PiecePrefab.AddComponent<Coffin>();
        furnishing.Stars = tier.Stars;
        furnishing.TakeDoorSounds(container);
        if (!PieceManager.Instance.AddPiece(coffin))
            throw new InvalidOperationException("Jotunn rejected the coffin build piece.");
    }

    private void OnDestroy()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Register;
        harmony?.UnpatchSelf();
    }

    internal void LogWarning(string message) => Logger.LogWarning(message);
}

// The vanilla AI would also issue movement/combat orders. Only replace its decision loop
// for our creature; retain BaseAI's movement, pathfinding, and animation machinery.
[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
internal static class MaidAiPatch
{
    private static bool Prefix(MonsterAI __instance) => !__instance.GetComponent<Maid>();
}

[HarmonyPatch(typeof(Tameable), "UpdateSummon")]
internal static class MaidSummonPatch
{
    private static bool Prefix(Tameable __instance) => !__instance.GetComponent<Maid>();
}

[HarmonyPatch(typeof(Tameable), "GetStatusString")]
internal static class MaidStatusPatch
{
    private static bool Prefix(Tameable __instance, ref string __result)
    {
        if (!__instance.TryGetComponent<Maid>(out var maid)) return true;
        __result = maid.OffDuty ? "Off duty" : "On duty";
        return false;
    }
}

[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
internal static class MaidActionTextPatch
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        if (!__instance.TryGetComponent<Maid>(out var maid) || string.IsNullOrEmpty(__result)) return;
        var lines = __result.Split('\n');
        var status = maid.OffDuty ? "Off duty" : "On duty";
        var suffix = Localization.instance.Localize(" ( $hud_tame, " + status + " )");
        if (lines[0].EndsWith(suffix, StringComparison.Ordinal))
            lines[0] = lines[0].Substring(0, lines[0].Length - suffix.Length) + " (" + status + ")";
        // Replace only the action label: a renamed maid may have "Pet" in her name.
        var pet = Localization.instance.Localize("$hud_pet");
        if (lines.Length > 1 && lines[1].EndsWith(pet, StringComparison.Ordinal))
            lines[1] = lines[1].Substring(0, lines[1].Length - pet.Length) + (maid.OffDuty ? "Recall" : "Dismiss");
        __result = string.Join("\n", lines);
    }
}

[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
internal static class MaidPetPatch
{
    private static void Postfix(Tameable __instance, Humanoid user, bool hold, bool alt, bool __result)
    {
        if (__result && !hold && !alt && __instance.TryGetComponent<Maid>(out var maid)) maid.RequestReturn(user);
    }
}
