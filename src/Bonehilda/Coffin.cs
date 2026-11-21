using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

public sealed class Coffin : MonoBehaviour
{
    internal const string MaidKey = "BH_maid";
    internal const string RespawnKey = "BH_respawn";
    internal const string EntranceKey = "BH_entrance";
    internal const string DismissedKey = "BH_dismissed";
    public int Stars = 2;
    internal ZNetView View { get; private set; } = null!;
    internal Container Chest { get; private set; } = null!;
    internal Piece Piece { get; private set; } = null!;
    private float nextCheck;
    private float nextServerQuery;
    private Transform door = null!;
    private float doorAngle;
    [SerializeField] private EffectList openSound = new EffectList();
    [SerializeField] private EffectList latchSound = new EffectList();
    internal void TakeDoorSounds(Container chest)
    {
        openSound = chest.m_openEffects;
        latchSound = chest.m_closeEffects;
        chest.m_openEffects = new EffectList();
        chest.m_closeEffects = new EffectList();
    }
    internal int Level => Mathf.Clamp(Stars, 0, 2) + 1;
    internal Vector3 ApproachPosition => transform.position + transform.forward * 1.7f;
    internal Vector3 ExitPosition => transform.position + transform.forward * (2.9f * CoffinVisuals.Size(Stars));
    internal Vector3 InteriorPosition => transform.position + transform.forward * 0.04f + Vector3.up * 0.18f;
    internal bool DoorOpen => doorAngle < -95f;

    private void Awake()
    {
        View = GetComponent<ZNetView>();
        Chest = GetComponent<Container>();
        Piece = GetComponent<Piece>();
        door = transform.Find("BonehildaCoffin/BH_DoorHinge");
        var wear = GetComponent<WearNTear>();
        if (wear) wear.m_onDestroyed += Retire;
    }

    private void Update()
    {
        CheckMaid();
        AnimateDoor();
    }

    private void CheckMaid()
    {
        if (Time.time < nextCheck || !View || !View.IsValid() || !View.IsOwner() || !Piece.IsPlacedByPlayer()) return;
        nextCheck = Time.time + 0.25f;
        FitInventory();
        var data = View.GetZDO();
        var id = data.GetZDOID(MaidKey);
        if (data.GetBool(DismissedKey))
        {
            // Keep her visible inside until the door has actually closed.
            if (id != ZDOID.None && doorAngle == 0f && !Chest.IsInUse())
            {
                var stored = ZNetScene.instance.FindInstance(id);
                if (stored && stored.TryGetComponent<Maid>(out var storedMaid))
                {
                    data.Set(MaidKey, ZDOID.None);
                    storedMaid.Retire();
                }
                else if (Time.time >= nextServerQuery)
                {
                    nextServerQuery = Time.time + 10f;
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), Network.CheckRpc, data.m_uid, id);
                }
            }
            return;
        }
        if (id != ZDOID.None)
        {
            if (Time.time >= nextServerQuery)
            {
                nextServerQuery = Time.time + 10f;
                // Only the server can distinguish a dead NPC from one outside this client's sectors.
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), Network.CheckRpc, data.m_uid, id);
            }
            return;
        }
        if (ZNet.instance.GetTime().Ticks < data.GetLong(RespawnKey)) return;
        var now = ZNet.instance.GetTime();
        data.Set(EntranceKey, now.Ticks);
        var prefab = ZNetScene.instance.GetPrefab(Plugin.MaidPrefab);
        if (!prefab) return;
        // Leave the uncompressed first pose clear of the rear panel.
        var spawn = InteriorPosition + transform.forward * (.04f * CoffinVisuals.Size(Stars));
        var maid = Instantiate(prefab, spawn, transform.rotation).GetComponent<Maid>();
        maid.Bind(this, true);
        if (data.GetBool("BH_restoreStored"))
        {
            maid.RestoreStored(data);
            data.Set("BH_restoreStored", false);
        }
        data.Set(MaidKey, maid.View.GetZDO().m_uid);
    }

    internal void ScheduleReplacement(ZDOID expected)
    {
        if (!View.IsValid() || !View.IsOwner() || View.GetZDO().GetZDOID(MaidKey) != expected) return;
        View.GetZDO().Set(MaidKey, ZDOID.None);
        View.GetZDO().Set(RespawnKey, ZNet.instance.GetTime().AddSeconds(Plugin.RespawnDelay.Value).Ticks);
        View.GetZDO().Set(EntranceKey, 0L);
    }

    private void AnimateDoor()
    {
        if (!door || !View || !View.IsValid() || !ZNet.instance) return;
        var data = View.GetZDO();
        var start = data.GetLong(EntranceKey);
        var elapsed = (ZNet.instance.GetTime().Ticks - start) / (double)TimeSpan.TicksPerSecond;
        var maid = ZNetScene.instance ? ZNetScene.instance.FindInstance(data.GetZDOID(MaidKey)) : null;
        var exiting = maid && maid!.GetComponent<ZNetView>().IsValid() && maid.GetComponent<ZNetView>().GetZDO().GetBool(Maid.ExitingKey);
        var opening = data.GetInt(ZDOVars.s_inUse) == 1 || Chest.IsInUse()
            || ZNet.instance.GetTime().Ticks < data.GetLong("BH_doorUntil") || (start != 0 && (elapsed < 5 || exiting));
        var previous = doorAngle;
        doorAngle = Mathf.MoveTowards(doorAngle, opening ? -110f : 0f, Time.deltaTime * 160f);
        door.localRotation = Quaternion.Euler(0, doorAngle, 0);
        if (View.IsOwner() && previous == 0f && doorAngle < 0f) WorkSounds.Play(openSound, door.position, door.rotation);
        if (View.IsOwner() && previous < 0f && doorAngle == 0f) WorkSounds.Play(latchSound, door.position, door.rotation);
    }

    internal void HoldDoor()
    {
        var now = ZNet.instance.GetTime();
        if (View.GetZDO().GetLong("BH_doorUntil") < now.AddSeconds(0.5).Ticks)
            View.GetZDO().Set("BH_doorUntil", now.AddSeconds(1.5).Ticks);
    }

    internal bool Store(Maid maid)
    {
        if (Chest.IsInUse() || !maid.View.IsOwner() || maid.HasCargo) return false;
        if (!View.IsOwner()) View.ClaimOwnership();
        if (!View.IsOwner() || View.GetZDO().GetZDOID(MaidKey) != maid.View.GetZDO().m_uid) return false;
        var data = View.GetZDO();
        maid.SaveStored(data);
        data.Set(DismissedKey, true);
        data.Set(EntranceKey, 0L);
        data.Set("BH_doorUntil", 0L);
        maid.View.GetZDO().Set("BH_stowing", true);
        return true;
    }

    internal void Resume()
    {
        if (!View || !View.IsValid() || !View.IsOwner() || !View.GetZDO().GetBool(DismissedKey)) return;
        var data = View.GetZDO();
        data.Set(DismissedKey, false);
        data.Set("BH_restoreStored", data.GetZDOID(MaidKey) == ZDOID.None);
        data.Set(RespawnKey, 0L);
        data.Set(EntranceKey, ZNet.instance.GetTime().Ticks);
        nextCheck = 0;
    }

    internal void FitInventory()
    {
        // Native Inventory.Load preserves old grid coordinates even outside the new
        // width. Reflow them before saving; retain temporary overflow rows, never discard stacks.
        if (!Chest || !View || !View.IsValid() || Chest.IsInUse()) return;
        var inventory = Chest.GetInventory();
        if (inventory == null) return;
        var items = inventory.GetAllItems();
        var width = Chest.m_width;
        var rows = MaintenancePolicy.StorageRows(items.Count, width, Chest.m_height);
        var invalid = items.Any(item => item.m_gridPos.x < 0 || item.m_gridPos.x >= width || item.m_gridPos.y < 0 || item.m_gridPos.y >= rows)
            || items.Select(item => item.m_gridPos).Distinct().Count() != items.Count;
        if (invalid)
            for (var i = 0; i < items.Count; i++) items[i].m_gridPos = new Vector2i(i % width, i / width);
        AccessTools.Field(typeof(Inventory), "m_height").SetValue(inventory, rows);
        if (invalid && View.IsOwner()) AccessTools.Method(typeof(Container), "Save").Invoke(Chest, null);
    }

    private void Retire()
    {
        if (!View.IsValid() || !View.IsOwner()) return;
        var id = View.GetZDO().GetZDOID(MaidKey);
        var go = ZNetScene.instance.FindInstance(id);
        if (go && go.TryGetComponent<Maid>(out var maid)) maid.Retire();
        // An unloaded maid checks the server for her missing coffin when she next loads.
    }
}

[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class Network
{
    internal const string CheckRpc = "BH_CheckLink";
    private const string MissingRpc = "BH_MissingLink";

    private static void Postfix()
    {
        ZRoutedRpc.instance.Register<ZDOID, ZDOID>(CheckRpc, Check);
        ZRoutedRpc.instance.Register<ZDOID, ZDOID>(MissingRpc, Missing);
    }

    private static void Check(long sender, ZDOID holderId, ZDOID linkedId)
    {
        if (!ZNet.instance.IsServer()) return;
        var holder = ZDOMan.instance.GetZDO(holderId);
        if (holder == null || holder.GetOwner() != sender) return;
        var prefab = holder.GetPrefab();
        if (!CoffinTier.IsCoffin(prefab) && prefab != Plugin.MaidPrefab.GetStableHashCode()) return;
        var key = CoffinTier.IsCoffin(prefab) ? Coffin.MaidKey : Maid.HomeKey;
        if (holder.GetZDOID(key) != linkedId) return;
        var linked = ZDOMan.instance.GetZDO(linkedId);
        if (linked != null && (key != Maid.HomeKey || linked.GetZDOID(Coffin.MaidKey) == holderId)) return;
        ZRoutedRpc.instance.InvokeRoutedRPC(sender, MissingRpc, holderId, linkedId);
    }

    private static void Missing(long sender, ZDOID holderId, ZDOID linkedId)
    {
        if (sender != ZRoutedRpc.instance.GetServerPeerID()) return;
        var go = ZNetScene.instance.FindInstance(holderId);
        if (!go) return;
        if (go.TryGetComponent<Coffin>(out var coffin)) coffin.ScheduleReplacement(linkedId);
        else if (go.TryGetComponent<Maid>(out var maid) && maid.View.IsOwner() && maid.View.GetZDO().GetZDOID(Maid.HomeKey) == linkedId)
            maid.Retire();
    }
}

[HarmonyPatch(typeof(Container), "Load")]
internal static class CoffinStorageMigration
{
    private static void Postfix(Container __instance, bool __result)
    {
        if (__result && __instance.TryGetComponent<Coffin>(out var coffin)) coffin.FitInventory();
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
internal static class CoffinOpenWakeup
{
    private static void Postfix(Container __instance, bool inUse)
    {
        if (inUse && __instance.TryGetComponent<Coffin>(out var coffin)) coffin.Resume();
    }
}
