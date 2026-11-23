using System;
using System.Collections.Generic;
using System.Linq;
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
    public const string Version = "0.4.24";
    internal const string CoffinPrefab = "BH_Coffin";
    internal const string MaidPrefab = "BH_Bonehilda";
    internal static Plugin Instance = null!;
    internal static float RepairReach = 5f;
    internal static float InteractionReach = 5f;
    internal static float PlayerWalkSpeed = 2f;
    internal static ConfigEntry<float> Radius = null!;
    internal static ConfigEntry<float> ScanInterval = null!;
    internal static ConfigEntry<float> WorkInterval = null!;
    internal static ConfigEntry<float> SupplyInterval = null!;
    internal static ConfigEntry<float> FuelLowFraction = null!;
    internal static ConfigEntry<float> FuelRunway = null!;
    internal static ConfigEntry<float> RespawnDelay = null!;
    internal static ConfigEntry<bool> Repair = null!;
    internal static ConfigEntry<bool> Refuel = null!;
    internal static ConfigEntry<float> JobTimeout = null!;
    internal static ConfigEntry<bool> SupplyMachines = null!;
    internal static ConfigEntry<bool> Wander = null!;
    internal static ConfigEntry<bool> PreferShelter = null!;
    internal static ConfigEntry<float> WanderRadius = null!;
    internal static ConfigEntry<float> WanderInterval = null!;
    internal static ConfigEntry<float> DustingInterval = null!;
    internal static ConfigEntry<float> DustingChainRadius = null!;
    internal static ConfigEntry<float> StuckTime = null!;
    internal static ConfigEntry<bool> TrophyReactions = null!;
    internal static ConfigEntry<float> TrophyDistance = null!;
    internal static ConfigEntry<bool> IdleSitting = null!;
    internal static ConfigEntry<bool> SocialVisits = null!;
    internal static ConfigEntry<float> SocialRadius = null!;
    internal static ConfigEntry<float> SocialTargetCooldown = null!;
    internal static ConfigEntry<bool> NaturalRoutines = null!;
    internal static ConfigEntry<float> IdleVoiceChance = null!;
    internal static ConfigEntry<float> IdleVoiceCooldown = null!;
    internal static ConfigEntry<float> SittingCheckInterval = null!;
    internal static ConfigEntry<float> SittingContinueChance = null!;
    internal static ConfigEntry<bool> UseDoors = null!;
    internal static ConfigEntry<float> DoorEnemyRadius = null!;
    internal static ConfigEntry<bool> ActivityLogging = null!;
    internal static HashSet<string> LegacySkeletonNames = new HashSet<string>(StringComparer.Ordinal);
    private Harmony harmony = null!;
    private bool registered;

    private void Awake()
    {
        Instance = this;
        Radius = Setting("Work", "Radius", 30f, "Work radius in meters, centered on the coffin.", 5f, 100f);
        ScanInterval = Setting("Work", "ScanInterval", 5f, "Seconds between searches for work.", 1f, 60f);
        WorkInterval = Setting("Work", "ActionInterval", 2f, "Minimum seconds between repairs. She can walk to the next damaged piece with her hammer drawn during this cooldown.", 0.5f, 30f);
        SupplyInterval = Setting("Work", "SupplyInterval", 1f, "Minimum seconds between accepted supply items per maid, across all destinations. Travel and interaction animations can take longer.", 0.5f, 60f);
        FuelLowFraction = Setting("Work", "FuelLowFraction", .25f, "Start refueling below this fraction of capacity, or below FuelRunwaySeconds. Once started, continue until full. Full items must fit.", 0f, 1f);
        FuelRunway = Setting("Work", "FuelRunwaySeconds", 120f, "Start refueling below this estimated remaining burn time. Uses native configured burn rate; damage-driven shields use capacity only. 0 disables the time trigger.", 0f, 3600f);
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
        WanderRadius = Setting("Maid", "WanderRadius", 0f, "Idle wandering, dusting, and sitting radius from the coffin. 0 follows the full work radius; a positive value sets a smaller idle area, capped by the work radius.", 0f, 100f);
        PreferShelter = Config.Bind("Maid", "PreferShelteredIdle", true, new ConfigDescription("Prefer sheltered idle destinations, then roofed spots, with outdoor fallback. Does not restrict real work or expand the idle/work radius.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        WanderInterval = Setting("Maid", "WanderInterval", 12f, "Minimum seconds between idle destinations; actual interval varies up to 50% longer. Does not slow real work.", 5f, 120f);
        DustingInterval = Setting("Maid", "DustingInterval", 8f, "Seconds between performed idle activities (dusting, trophies, sitting, or social visits); varies up to 50% longer. Failed approaches retry after 3-4.5 seconds. Real jobs take priority.", 3f, 600f);
        DustingChainRadius = Setting("Maid", "DustingChainRadius", 10f, "Search distance from the maid for a different piece after each completed dusting action. Limited to her idle/work area; SittingContinueChance controls continuation.", 1f, 50f);
        StuckTime = Setting("Work", "NavigationStuckTime", 6f, "Seconds of attempted navigation without moving 25 cm before backing away to a safe point and retrying.", 2f, 30f);
        TrophyReactions = Config.Bind("Maid", "TrophyReactions", true, new ConfigDescription("Occasionally visit mounted boss and selected creature trophies within the work radius and perform player emotes.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        TrophyDistance = Setting("Maid", "TrophyInteractionDistance", 1.6f, "Standing distance in front of a trophy's visible surface, in meters.", 1f, 2.5f);
        IdleSitting = Config.Bind("Maid", "IdleSitting", true, new ConfigDescription("Use nearby unoccupied chairs, or sit/relax on clear ground when none is suitable, preferring a burning fire. Work and dismissal interrupt sitting.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        SittingCheckInterval = Setting("Maid", "SittingCheckInterval", 10f, "Seconds between rolls to leave an idle chair.", 5f, 120f);
        SocialVisits = Config.Bind("Maid", "SocialVisits", true, new ConfigDescription("Cosmetic greetings to calm friendly skeletons, tames, available maids, and Odin. Work and nearby enemies interrupt visits; other NPC behavior is unchanged.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        SocialRadius = Setting("Maid", "SocialRadius", 10f, "NPC search radius around the maid's current position. Both NPC and standing spot must remain inside the coffin's work radius. Moving acquaintances are not chased.", 2f, 50f);
        SocialTargetCooldown = Setting("Maid", "SocialTargetCooldown", 90f, "Minimum seconds between greetings to the same acquaintance by this maid.", 15f, 600f);
        NaturalRoutines = Config.Bind("Maid", "NaturalRoutines", true, new ConfigDescription("Pause to consider idle destinations, revisit favorite resting places, and alternate ordinary chores in bouts of three. Critical repairs and depleted fuel/ammo take priority.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        IdleVoiceChance = Setting("Maid", "IdleVoiceChance", .55f, "Chance of a native skeleton vocalization at a social/trophy gesture. Dusting and sitting use lower chances; 0 disables idle voices.", 0f, 1f);
        IdleVoiceCooldown = Setting("Maid", "IdleVoiceCooldown", 12f, "Minimum seconds between animation-linked skeleton noises per maid.", 5f, 120f);
        SittingContinueChance = Setting("Maid", "SittingContinueChance", .8f, "Probability of continuing to sit at each check, or chaining dusting to a different eligible piece within DustingChainRadius. 0.8 means 80% continue, 20% stop. Work interrupts both.", 0f, .95f);
        UseDoors = Config.Bind("Maid", "UseDoors", true, new ConfigDescription("Open permitted player-built doors and gates while travelling, then prioritize closing them. Locked and permanently open doors are excluded.", null,
            new ConfigurationManagerAttributes { IsAdminOnly = true }));
        DoorEnemyRadius = Setting("Maid", "DoorEnemyCheckRadius", 10f, "Do not open a door if an enemy is this close to the maid or door, in meters.", 5f, 50f);
        ActivityLogging = Config.Bind("Debugging", "ActivityLogging", false, "Write loaded maid activity, target, path, and cargo snapshots every two seconds to Bonehilda.activity.json and a bounded history log. Local diagnostics only.");
        gameObject.AddComponent<ActivityDiagnostics>();
        CommandManager.Instance.AddConsoleCommand(new ActivityCommand());
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
            TrophyVisits.Configure();
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
            var player = PrefabManager.Instance.GetPrefab("Player");
            RepairReach = player.GetComponent<Player>().m_maxPlaceDistance;
            InteractionReach = player.GetComponent<Player>().m_maxInteractDistance;
            PlayerWalkSpeed = player.GetComponent<Player>().m_walkSpeed;
            WorkAnimations.Configure(prefab, player);
            Visuals.Dress(prefab.transform);
            // Apply after binding the garment so both the body and cloth grow together.
            prefab.GetComponentInChildren<Animator>(true).transform.localScale *= 1.04f;
            prefab.AddComponent<FeatherDuster>();
            prefab.AddComponent<MaidSeating>();
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
            ai.m_randomMoveInterval = WanderInterval.Value;
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
        var requirements = tier.Recipe.Select(r => new RequirementConfig(r.Item, r.Amount, 0, true)).ToArray();
        var coffin = new CustomPiece(tier.Prefab, "piece_chest_wood", new PieceConfig
        {
            Name = tier.Name,
            Description = $"A little Makin' Magic. Summons a {tier.Stars}-star Bonehilda; {tier.Slots} supply slots. Free will included. No bladder bar.",
            PieceTable = "Hammer",
            Category = "Furniture",
            Usage = new[] { "Furniture", "Storage" },
            CraftingStation = "piece_workbench",
            Requirements = requirements
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
    internal void LogInfo(string message) => Logger.LogInfo(message);
}

// The vanilla AI would also issue movement/combat orders. Only replace its decision loop
// for our creature; retain BaseAI's movement, pathfinding, and animation machinery.
[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
internal static class MaidAiPatch
{
    private static bool Prefix(MonsterAI __instance) => !__instance.GetComponent<Maid>();
}

// Damage/projectile callbacks still run outside MonsterAI.UpdateAI. A worker
// must never enter an alert state that the replaced combat loop cannot clear.
[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.SetAlerted))]
internal static class MaidPassivePatch
{
    private static void Prefix(MonsterAI __instance, ref bool __0)
    {
        if (__instance.GetComponent<Maid>()) __0 = false;
    }
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
