using System;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Bonehilda;

// Alternate build resources are scoped to coffin calls. No synthetic inventory
// item and no global CountItems/RemoveItem patch is needed.
internal static class AnyDressCost
{
    internal const string RecordedKey = "BH_dressCostRecorded", PaidKey = "BH_buildDress";
    internal sealed class Attempt
    {
        internal Player Player = null!;
        internal Piece Template = null!;
        internal Piece.Requirement[] Original = null!;
        internal ItemDrop.ItemData? Dress;
        internal Piece? Placed;
        internal bool Free;
        internal int Frame;
    }
    internal static Attempt? Current, Pending;
    internal static Piece? UiPiece;
    internal static void Reset() { Current = Pending = null; UiPiece = null; }
    internal static bool IsCoffin(Piece piece) => piece && piece.GetComponent<Coffin>();
    internal static string Name(ItemDrop.ItemData item) => item.m_dropPrefab ? Utils.GetPrefabName(item.m_dropPrefab) : "";
    internal static bool IsDress(Piece.Requirement req) => req.m_resItem && DressCostPolicy.IsDress(Utils.GetPrefabName(req.m_resItem.gameObject));
    internal static ItemDrop.ItemData? Select(Player player) => player.GetInventory().GetAllItems()
        .Where(item => DressCostPolicy.Eligible(Name(item), item.m_equipped, item.m_stack))
        .OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x).FirstOrDefault();
    internal static bool Free(Player player, Piece piece) => player.NoCostCheat() || ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey());
    internal static ItemDrop? Prefab(string name) => PrefabManager.Instance.GetPrefab(name)?.GetComponent<ItemDrop>();

    internal static Piece.Requirement[] Replace(Piece.Requirement[] original, ItemDrop? dress)
        => original.Where(req => !IsDress(req) || dress).Select(req => IsDress(req)
            ? new Piece.Requirement { m_resItem = dress!, m_amount = 1, m_amountPerLevel = 0, m_recover = true } : req).ToArray();
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
internal static class AnyDressRequirements
{
    internal sealed class Scope
    {
        internal Piece Piece = null!;
        internal Piece.Requirement[] Original = null!;
        internal bool Eligible;
    }
    private static void Prefix(Player __instance, Piece __0, Player.RequirementMode __1, out Scope? __state)
    {
        __state = null;
        if (!SessionSupport.Active || !AnyDressCost.IsCoffin(__0)) return;
        ItemDrop? dress;
        if (__1 == Player.RequirementMode.IsKnown)
            dress = Enumerable.Range(1, 10).Select(n => AnyDressCost.Prefab("ArmorDress" + n))
                .FirstOrDefault(item => item && __instance.IsMaterialKnown(item!.m_itemData.m_shared.m_name));
        else
        {
            var selected = AnyDressCost.Select(__instance);
            dress = selected == null ? null : selected.m_dropPrefab.GetComponent<ItemDrop>();
        }
        __state = new Scope { Piece = __0, Original = __0.m_resources,
            Eligible = dress || __1 != Player.RequirementMode.IsKnown && AnyDressCost.Free(__instance, __0) };
        // Remove the sentinel if none is available, then veto in Postfix. This
        // still lets native station/DLC checks run without accepting worn dresses.
        __0.m_resources = AnyDressCost.Replace(__state.Original, dress);
    }
    private static void Postfix(Piece __0, ref bool __result, Scope? __state)
    {
        if (!SessionSupport.Active && AnyDressCost.IsCoffin(__0) || __state != null && !__state.Eligible) __result = false;
    }
    private static Exception? Finalizer(Exception? __exception, Scope? __state)
    {
        if (__state != null && __state.Piece) __state.Piece.m_resources = __state.Original;
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
internal static class AnyDressPlacement
{
    private static bool Prefix(Player __instance, Piece __0, ref bool __result, out AnyDressCost.Attempt? __state)
    {
        __state = AnyDressCost.Current;
        if (!AnyDressCost.IsCoffin(__0)) return true;
        if (!SessionSupport.Active) { __result = false; return false; }
        var dress = AnyDressCost.Select(__instance);
        var free = AnyDressCost.Free(__instance, __0);
        if (!free && dress == null) { __result = false; return false; }
        AnyDressCost.Pending = null;
        AnyDressCost.Current = new AnyDressCost.Attempt { Player = __instance, Template = __0,
            Original = __0.m_resources, Dress = dress, Free = free, Frame = Time.frameCount };
        // Native placement inspects resource provenance and clones the piece.
        // Let it see the actual material, not just our display sentinel.
        __0.m_resources = AnyDressCost.Replace(__0.m_resources, dress == null ? null : dress.m_dropPrefab.GetComponent<ItemDrop>());
        return true;
    }
    private static void Postfix(Player __instance, Piece __0, bool __result)
    {
        var attempt = AnyDressCost.Current;
        if (__result && attempt != null && attempt.Player == __instance && attempt.Template == __0)
            AnyDressCost.Pending = attempt;
    }
    private static Exception? Finalizer(Exception? __exception, AnyDressCost.Attempt? __state)
    {
        var active = AnyDressCost.Current;
        if (active != null && active != __state && active.Template) active.Template.m_resources = active.Original;
        AnyDressCost.Current = __state;
        if (__exception != null) AnyDressCost.Pending = null;
        return __exception;
    }
}

[HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
internal static class AnyDressPlacedPiece
{
    private static void Postfix(Piece __instance)
    {
        var attempt = AnyDressCost.Current;
        if (attempt == null || !AnyDressCost.IsCoffin(__instance) || __instance.GetCreator() != attempt.Player.GetPlayerID()
            || Utils.GetPrefabName(__instance.gameObject) != Utils.GetPrefabName(attempt.Template.gameObject)) return;
        var view = __instance.GetComponent<ZNetView>();
        if (!view || !view.IsValid() || !view.IsOwner()) return;
        attempt.Placed = __instance;
        // Explicit empty payment distinguishes new free builds from legacy recipes.
        view.GetZDO().Set(AnyDressCost.RecordedKey, true);
        view.GetZDO().Set(AnyDressCost.PaidKey, "");
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
internal static class AnyDressConsumption
{
    private static bool Prefix(Player __instance, ref Piece.Requirement[] __0)
    {
        var attempt = AnyDressCost.Pending;
        if (attempt == null || attempt.Player != __instance || attempt.Frame != Time.frameCount
            || !ReferenceEquals(__0, attempt.Template.m_resources)) return true;
        AnyDressCost.Pending = null;
        if (!attempt.Placed) return false;
        var view = attempt.Placed!.GetComponent<ZNetView>();
        var dress = attempt.Dress;
        if (!view || !view.IsValid() || !view.IsOwner()) return false;
        if (!attempt.Free)
        {
            // Charge this exact unequipped item, never a worn duplicate with the
            // same display name. Native consumption handles all other materials.
            if (dress == null || !DressCostPolicy.Eligible(AnyDressCost.Name(dress), dress.m_equipped, dress.m_stack)
                || !__instance.GetInventory().ContainsItem(dress) || !__instance.GetInventory().RemoveItem(dress, 1))
            {
                ZNetScene.instance.Destroy(attempt.Placed.gameObject);
                Plugin.Instance.LogWarning("Coffin placement rolled back: selected dress was no longer available.");
                return false;
            }
            view.GetZDO().Set(AnyDressCost.PaidKey, AnyDressCost.Name(dress));
        }
        __0 = AnyDressCost.Replace(__0, null);
        return true;
    }
}

[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
internal static class AnyDressRefund
{
    private static void Prefix(Piece __instance, out Piece.Requirement[]? __state)
    {
        __state = null;
        var coffin = __instance.GetComponent<Coffin>();
        var view = __instance.GetComponent<ZNetView>();
        if (!coffin || !view || !view.IsValid()) return;
        var data = view.GetZDO();
        var name = DressCostPolicy.Refund(data.GetBool(AnyDressCost.RecordedKey), data.GetString(AnyDressCost.PaidKey), coffin.Stars);
        __state = __instance.m_resources;
        __instance.m_resources = AnyDressCost.Replace(__state, name.Length == 0 ? null : AnyDressCost.Prefab(name));
    }
    private static Exception? Finalizer(Exception? __exception, Piece __instance, Piece.Requirement[]? __state)
    {
        if (__state != null && __instance) __instance.m_resources = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
internal static class AnyDressHudScope
{
    private static void Prefix(Piece __0, out Piece? __state) { __state = AnyDressCost.UiPiece; AnyDressCost.UiPiece = AnyDressCost.IsCoffin(__0) ? __0 : null; }
    private static Exception? Finalizer(Exception? __exception, Piece? __state) { AnyDressCost.UiPiece = __state; return __exception; }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
internal static class AnyDressCostDisplay
{
    private static void Set(Transform root, string child, string componentType, string property, object value)
    {
        var type = AccessTools.TypeByName(componentType) ?? throw new InvalidOperationException("Missing build UI component: " + componentType);
        var component = root.Find(child).GetComponent(type);
        AccessTools.Property(type, property).SetValue(component, value, null);
    }
    private static void Postfix(Transform __0, Piece.Requirement __1, Player __2)
    {
        if (!AnyDressCost.UiPiece || !AnyDressCost.IsDress(__1)) return;
        var selected = AnyDressCost.Select(__2);
        Set(__0, "res_name", "TMPro.TMP_Text", "text", "Any dress");
        Set(__0, "res_amount", "TMPro.TMP_Text", "text", "1");
        Set(__0, "res_amount", "TMPro.TMP_Text", "color", selected != null || AnyDressCost.Free(__2, AnyDressCost.UiPiece!) ? Color.white
            : Mathf.Sin(Time.time * 10f) > 0 ? Color.red : Color.white);
        if (selected != null) Set(__0, "res_icon", "UnityEngine.UI.Image", "sprite", selected.GetIcon());
        var tooltip = __0.GetComponent(AccessTools.TypeByName("UITooltip"));
        AccessTools.Field(tooltip.GetType(), "m_text").SetValue(tooltip, "Any Hildir dress (1). Equipped dresses are not consumed."
            + (selected == null ? "" : "\nWill use: " + Localization.instance.Localize(selected.m_shared.m_name)));
    }
}
