using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

// Called by the isolated startup probe after native prefabs are available.
// Never run this against a player's live session: it temporarily substitutes
// inventory/knowledge on the inactive Player prefab, restoring them in finally.
internal static class DressCostRuntimeChecks
{
    internal static void Run(Assembly assembly)
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var costs = assembly.GetType("Bonehilda.AnyDressCost", true);
        var select = costs.GetMethod("Select", flags);
        var replace = costs.GetMethod("Replace", flags);
        var uiScope = costs.GetField("UiPiece", flags);
        var player = PrefabManager.Instance.GetPrefab("Player").GetComponent<Player>();
        var inventoryField = AccessTools.Field(typeof(Humanoid), "m_inventory");
        var knowledgeField = AccessTools.Field(typeof(Player), "m_knownMaterial");
        var originalInventory = inventoryField.GetValue(player);
        var originalKnowledge = knowledgeField.GetValue(player);
        var coffin = PieceManager.Instance.GetPiece("BH_Coffin").PiecePrefab.GetComponent<Piece>();
        Jotunn.PrefabExtension.FixReferences(coffin.gameObject);
        var originalStation = coffin.m_craftingStation;
        var originalResources = coffin.m_resources;
        GameObject root = null;
        try
        {
            coffin.m_craftingStation = null;
            var worn = PrefabManager.Instance.GetPrefab("ArmorDress10").GetComponent<ItemDrop>().m_itemData.Clone();
            worn.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorDress10"); worn.m_equipped = true; worn.m_stack = 1;
            for (var i = 1; i <= 10; i++)
            {
                var prefab = PrefabManager.Instance.GetPrefab("ArmorDress" + i);
                if (!prefab) throw new Exception("Missing Hildir dress alternative " + i);
                var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                item.m_dropPrefab = prefab; item.m_equipped = false; item.m_stack = 1;
                var inventory = new Inventory("Dress checks", null, 8, 4);
                inventoryField.SetValue(player, inventory);
                if (!inventory.AddItem(worn.Clone()) || !inventory.AddItem(item)) throw new Exception("Could not populate dress test inventory");
                var selected = select.Invoke(null, new object[] { player }) as ItemDrop.ItemData;
                if (selected != item) throw new Exception("Dress selection consumed an equipped item instead of alternative " + i);
                var known = new HashSet<string>(originalResources.Where(r => !r.m_resItem.name.StartsWith("ArmorDress", StringComparison.Ordinal))
                    .Select(r => r.m_resItem.m_itemData.m_shared.m_name));
                known.Add(item.m_shared.m_name);
                knowledgeField.SetValue(player, known);
                if (!player.HaveRequirements(coffin, Player.RequirementMode.IsKnown)) throw new Exception("Alternative dress did not unlock coffin: " + i);
                if (!ReferenceEquals(coffin.m_resources, originalResources)) throw new Exception("Requirement scope did not restore prefab resources");
                var substituted = (Piece.Requirement[])replace.Invoke(null, new object[] { originalResources, prefab.GetComponent<ItemDrop>() });
                var dress = substituted.Single(r => r.m_resItem.name.StartsWith("ArmorDress", StringComparison.Ordinal));
                if (dress.m_resItem.gameObject != prefab || dress.m_amount != 1 || !dress.m_recover || substituted.Length != originalResources.Length)
                    throw new Exception("Alternative/refund substitution failed: " + i);
            }
            var testInventory = new Inventory("UI dress checks", null, 8, 4);
            inventoryField.SetValue(player, testInventory);
            testInventory.AddItem(worn.Clone());
            if (select.Invoke(null, new object[] { player }) != null) throw new Exception("An equipped-only inventory paid a dress cost");
            root = new GameObject("Isolated dress requirement UI", typeof(RectTransform)); root.SetActive(false);
            root.AddComponent(AccessTools.TypeByName("UITooltip"));
            foreach (var name in new[] { "res_name", "res_amount", "res_icon" })
            {
                var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(root.transform, false);
                child.AddComponent(AccessTools.TypeByName(name == "res_icon" ? "UnityEngine.UI.Image" : "TMPro.TextMeshProUGUI"));
            }
            uiScope.SetValue(null, coffin);
            var setup = AccessTools.Method(typeof(InventoryGui), "SetupRequirement");
            var requirement = originalResources.Single(r => r.m_resItem.name.StartsWith("ArmorDress", StringComparison.Ordinal));
            setup.Invoke(null, new object[] { root.transform, requirement, player, false, 0, 1 });
            var textType = AccessTools.TypeByName("TMPro.TMP_Text");
            if ((string)AccessTools.Property(textType, "text").GetValue(root.transform.Find("res_name").GetComponent(textType), null) != "Any dress"
                || (string)AccessTools.Property(textType, "text").GetValue(root.transform.Find("res_amount").GetComponent(textType), null) != "1")
                throw new Exception("Native requirement UI did not show Any dress / 1");
            var tooltip = root.GetComponent(AccessTools.TypeByName("UITooltip"));
            if (!((string)AccessTools.Field(tooltip.GetType(), "m_text").GetValue(tooltip)).Contains("Equipped dresses are not consumed"))
                throw new Exception("Missing dress protection tooltip");
            var unequipped = PrefabManager.Instance.GetPrefab("ArmorDress6").GetComponent<ItemDrop>().m_itemData.Clone();
            unequipped.m_dropPrefab = PrefabManager.Instance.GetPrefab("ArmorDress6"); unequipped.m_equipped = false; unequipped.m_stack = 1;
            testInventory.AddItem(unequipped);
            setup.Invoke(null, new object[] { root.transform, requirement, player, false, 0, 1 });
            var imageType = AccessTools.TypeByName("UnityEngine.UI.Image");
            if ((Sprite)AccessTools.Property(imageType, "sprite").GetValue(root.transform.Find("res_icon").GetComponent(imageType), null) != unequipped.GetIcon())
                throw new Exception("Dress cost icon did not follow available dress");
            var placement = assembly.GetType("Bonehilda.AnyDressPlacement", true);
            foreach (var error in new Exception[] { null, new InvalidOperationException("Isolated failed placement") })
            {
                var args = new object[] { player, coffin, false, null };
                if (!(bool)placement.GetMethod("Prefix", flags).Invoke(null, args)) throw new Exception("Available dress rejected by placement preflight");
                if (coffin.m_resources.Single(r => r.m_resItem.name.StartsWith("ArmorDress", StringComparison.Ordinal)).m_resItem.gameObject != unequipped.m_dropPrefab)
                    throw new Exception("Native placement provenance still references the sentinel dress");
                var returned = placement.GetMethod("Finalizer", flags).Invoke(null, new object[] { error, args[3] });
                if (!ReferenceEquals(returned, error) || !ReferenceEquals(coffin.m_resources, originalResources)
                    || !testInventory.ContainsItem(unequipped)) throw new Exception("Placement success/error scope failed to restore resources without charging early");
            }
        }
        finally
        {
            uiScope.SetValue(null, null); coffin.m_craftingStation = originalStation; coffin.m_resources = originalResources;
            inventoryField.SetValue(player, originalInventory); knowledgeField.SetValue(player, originalKnowledge);
            if (root) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
