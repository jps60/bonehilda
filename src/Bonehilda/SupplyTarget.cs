using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

// Explicit consumer adapters avoid UseItem side effects such as configuring ballista targets,
// displaying valuables on item stands, or automatically firing a loaded catapult.
internal sealed class SupplyTarget
{
    internal enum SupplyKind { FireplaceFuel, MachineFuel, MachineInput, FermenterInput, ShieldFuel, Ammunition }
    internal readonly Component Consumer;
    internal readonly SupplyKind Kind;
    internal readonly ItemDrop Resource;
    internal readonly Transform? Port;
    private readonly Func<float> amount;
    private readonly Func<bool> needsItem;
    private readonly Action<ItemDrop.ItemData> add;
    private readonly Func<float>? capacity, burnSeconds;

    private SupplyTarget(Component consumer, SupplyKind kind, ItemDrop resource, Transform? port,
        Func<float> amount, Func<bool> needsItem, Action<ItemDrop.ItemData> add, Func<float>? capacity = null, Func<float>? burnSeconds = null)
    {
        Consumer = consumer;
        Kind = kind;
        Resource = resource;
        Port = port;
        this.amount = amount;
        this.needsItem = needsItem;
        this.add = add;
        this.capacity = capacity; this.burnSeconds = burnSeconds;
    }

    internal bool NeedsItem => Consumer && Resource && needsItem();
    internal bool Fuel => Kind == SupplyKind.FireplaceFuel || Kind == SupplyKind.MachineFuel || Kind == SupplyKind.ShieldFuel;
    internal bool LowFuel => Fuel && NeedsItem && FuelPolicy.Start(amount(), capacity?.Invoke() ?? 0f,
        burnSeconds?.Invoke() ?? 0f, Plugin.FuelLowFraction.Value, Plugin.FuelRunway.Value);
    internal float FuelFraction => Fuel && capacity != null && capacity() > 0 ? amount() / capacity() : -1f;
    internal float FuelSeconds => Fuel && burnSeconds != null && burnSeconds() > 0 ? amount() * burnSeconds() : -1f;
    internal bool Depleted => NeedsItem && (Kind == SupplyKind.FireplaceFuel || Kind == SupplyKind.MachineFuel
        || Kind == SupplyKind.ShieldFuel || Kind == SupplyKind.Ammunition) && amount() < 1f;
    internal bool Matches(ItemDrop.ItemData item) => Resource && item.m_dropPrefab &&
        Utils.GetPrefabName(item.m_dropPrefab) == Utils.GetPrefabName(Resource.gameObject);

    internal bool Deliver(ItemDrop.ItemData item)
    {
        if (!NeedsItem || !Matches(item)) return false;
        var before = amount();
        try { add(item); }
        catch (Exception e) { Plugin.Instance.LogWarning($"Could not supply {Consumer.name}: {e.GetBaseException().Message}"); }
        // A handler can change the ZDO before throwing in a visual effect. Still consume the
        // carried item when the accepted count increased, so it cannot be delivered twice.
        var after = amount();
        // Fermenters store an item hash rather than a quantity; valid hashes can be negative.
        return MaintenancePolicy.AcceptedSupply(before, after, Kind == SupplyKind.FermenterInput);
    }

    private static void Invoke(Component target, string method, params object[] args) =>
        AccessTools.Method(target.GetType(), method).Invoke(target, args);
    private static T Read<T>(Component target, string method) => (T)AccessTools.Method(target.GetType(), method).Invoke(target, null);
    private static bool Cheated(ItemDrop.ItemData item) =>
        AccessTools.Field(typeof(ItemDrop.ItemData), "m_cheated")?.GetValue(item) is bool value && value;

    private static void AddInput(Component consumer, string method, object itemId, ItemDrop.ItemData item)
    {
        var handler = AccessTools.Method(consumer.GetType(), method);
        // The cheat-origin parameter was added after the baseline reference assemblies.
        handler.Invoke(consumer, handler.GetParameters().Length == 3
            ? new object[] { ZNet.GetUID(), itemId, Cheated(item) }
            : new object[] { ZNet.GetUID(), itemId });
    }

    internal static bool Excluded(Piece piece) => MaintenancePolicy.ExcludedConsumer(
        piece.GetComponentInChildren<CookingStation>(true), piece.GetComponentInChildren<Incinerator>(true));

    internal static IEnumerable<SupplyTarget> ForPiece(Piece piece)
    {
        if (Excluded(piece)) yield break;
        var view = piece.GetComponent<ZNetView>();
        if (!view || !view.IsValid()) yield break;
        var data = view.GetZDO();
        var fire = piece.GetComponentInChildren<Fireplace>(true);
        if (fire && fire.m_fuelItem)
        {
            float Amount() => data.GetFloat("fuel", fire.m_startFuel);
            yield return new SupplyTarget(fire, SupplyKind.FireplaceFuel, fire.m_fuelItem, null, Amount,
                () => fire.m_canRefill && MaintenancePolicy.NeedsFuel(Amount(), fire.m_maxFuel, fire.m_infiniteFuel),
                _ => Invoke(fire, "RPC_AddFuel", ZNet.GetUID()), () => fire.m_maxFuel, () => fire.m_secPerFuel);
        }
        var machine = piece.GetComponentInChildren<Smelter>(true);
        if (machine)
        {
            float Fuel() => data.GetFloat("fuel");
            float Queue() => data.GetInt("queued");
            if (machine.m_fuelItem && machine.m_maxFuel > 0)
                yield return new SupplyTarget(machine, SupplyKind.MachineFuel, machine.m_fuelItem,
                    machine.m_addWoodSwitch ? machine.m_addWoodSwitch.transform : null, Fuel,
                    () => MaintenancePolicy.NeedsFuel(Fuel(), machine.m_maxFuel, false),
                    _ => Invoke(machine, "RPC_AddFuel", ZNet.GetUID()), () => machine.m_maxFuel,
                    () => Queue() > 0 && machine.m_fuelPerProduct > 0 ? machine.m_secPerProduct / machine.m_fuelPerProduct : 0f);
            foreach (var conversion in machine.m_conversion)
            {
                var resource = conversion.m_from;
                if (!resource) continue;
                yield return new SupplyTarget(machine, SupplyKind.MachineInput, resource,
                    machine.m_addOreSwitch ? machine.m_addOreSwitch.transform : null, Queue,
                    () => MaintenancePolicy.NeedsInput((int)Queue(), machine.m_maxOre),
                    item => AddInput(machine, "RPC_AddOre", Utils.GetPrefabName(resource.gameObject), item));
            }
        }
        var barrel = piece.GetComponentInChildren<Fermenter>(true);
        if (barrel)
        {
            float Content() => Read<int>(barrel, "GetContent");
            foreach (var conversion in barrel.m_conversion)
            {
                var resource = conversion.m_from;
                if (!resource) continue;
                yield return new SupplyTarget(barrel, SupplyKind.FermenterInput, resource,
                    barrel.m_addSwitch ? barrel.m_addSwitch.transform : null, Content,
                    () => Content() == 0,
                    item => AddInput(barrel, "RPC_AddItem", Utils.GetPrefabName(resource.gameObject).GetStableHashCode(), item));
            }
        }
        var shield = piece.GetComponentInChildren<ShieldGenerator>(true);
        if (shield)
        {
            float Fuel() => Read<float>(shield, "GetFuel");
            foreach (var resource in shield.m_fuelItems)
            {
                if (!resource) continue;
                yield return new SupplyTarget(shield, SupplyKind.ShieldFuel, resource,
                    shield.m_addFuelSwitch ? shield.m_addFuelSwitch.transform : null, Fuel,
                    () => MaintenancePolicy.NeedsFuel(Fuel(), shield.m_maxFuel, false),
                    _ => Invoke(shield, "RPC_AddFuel", ZNet.GetUID()), () => shield.m_maxFuel);
            }
        }
        var turret = piece.GetComponentInChildren<Turret>(true);
        if (turret)
        {
            float Ammo() => data.GetInt("ammo");
            foreach (var option in turret.m_allowedAmmo)
            {
                var resource = option.m_ammo;
                if (!resource) continue;
                var prefab = Utils.GetPrefabName(resource.gameObject);
                yield return new SupplyTarget(turret, SupplyKind.Ammunition, resource, null, Ammo,
                    () => MaintenancePolicy.CanLoadAmmo((int)Ammo(), turret.m_maxAmmo, data.GetString("ammoType"), prefab),
                    _ => Invoke(turret, "RPC_AddAmmo", ZNet.GetUID(), prefab));
            }
        }
    }
}
