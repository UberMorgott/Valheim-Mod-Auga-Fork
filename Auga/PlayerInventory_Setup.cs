using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Inventory, container and crafting (spec 2026-09-11-auga-native-rework, Phase 5, decision D0: full vanilla skin).
    // The vanilla InventoryGui is the one and only inventory UI: root/Player (grid, armor, weight, player preview),
    // root/Container, root/Crafting (recipe list, requirements, quality, craft/upgrade/repair, amount, variant),
    // root/Info, the split dialog and every vanilla field and listener wired in InventoryGui.Awake
    // (InventoryGui.cs:353-422). Auga only restyles them in place; the Auga inventory layout, RightPanel, crafting
    // replacement and their Dummy objects are gone, so mods reading vanilla paths (EpicLoot m_crafting/RepairButton/Glow,
    // EAQS m_player) find them.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    public static class PlayerInventory_Setup
    {
        [UsedImplicitly]
        public static void Postfix(InventoryGui __instance)
        {
            // The Auga store (Phase 4) still tints its item icons with the vanilla icon material.
            AddItemIconMaterialFrom(__instance);

            // RuneSkin owns the inventory layout (Compat\RuneSkinOwnership.Inventory): its player panel (with the
            // container under it), the info header and the grid slot templates are RuneSkin's; AugaSkin keeps
            // styling the rest of the screen (skills, texts, trophies, achievements, split/variant dialogs; crafting too
            // unless RuneSkin also owns Crafting, M3).
            var runeInventory = Compat.RuneSkinOwnership.Inventory;
            if (runeInventory)
                RestyleExcept(__instance.transform, __instance);
            else
                AugaStyle.Restyle(__instance.transform);
            // Templates vanilla instantiates at runtime: grid slots (InventoryGrid.UpdateGui), recipe rows
            // (InventoryGui.AddRecipeToList), trophies and achievements, the drag icon.
            foreach (var template in new[]
                     {
                         runeInventory ? null : __instance.m_playerGrid.m_elementPrefab, runeInventory ? null : __instance.m_containerGrid.m_elementPrefab,
                         Compat.RuneSkinOwnership.Crafting ? null : __instance.m_recipeElementPrefab, __instance.m_trophieElementPrefab,
                         __instance.m_achievementsElementPrefab, __instance.m_dragItemPrefab,
                         // skill rows, instantiated by SkillsDialog.Setup (SkillsDialog.cs:119)
                         __instance.m_skillsDialog ? __instance.m_skillsDialog.m_elementPrefab : null,
                     })
                if (template)
                    AugaStyle.Restyle(template.transform);
            // Equipped / queued marks of the grid slots: Auga's own slot prefab (bundle Inventory_screen grid).
            var augaGrid = Auga.Assets.InventoryScreen ? Auga.Assets.InventoryScreen.GetComponentInChildren<InventoryGrid>(true) : null;
            if (!runeInventory)
                foreach (var grid in new[] { __instance.m_playerGrid, __instance.m_containerGrid })
                    AugaStyle.SlotMarks(grid.m_elementPrefab ? grid.m_elementPrefab.transform : null, augaGrid ? augaGrid.m_elementPrefab : null);

            // 5th Info button + character stats window, cloned from the restyled vanilla ones.
            CharacterStatsPanel.Create(__instance);
        }

        // Restyle of the InventoryGui hierarchy minus RuneSkin's subtrees: walks down only along ancestors of an owned
        // panel and restyles every other subtree whole.
        private static void RestyleExcept(Transform t, InventoryGui gui)
        {
            foreach (Transform child in t)
            {
                if (child == gui.m_player || child == gui.m_info || Compat.RuneSkinOwnership.IsRuneSkinObject(child) ||
                    Compat.RuneSkinOwnership.Crafting && child == gui.m_crafting)
                    continue;
                if (gui.m_player.IsChildOf(child) || gui.m_info.IsChildOf(child))
                    RestyleExcept(child, gui);
                else
                    AugaStyle.Restyle(child);
            }
        }

        private static void AddItemIconMaterialFrom(InventoryGui gui)
        {
            var icon = gui.m_dragItemPrefab ? gui.m_dragItemPrefab.transform.Find("icon") : null;
            if (icon && icon.TryGetComponent<Image>(out var image))
                AugaUnity.AddItemIconMaterial.IconMaterial = image.material;
        }
    }
}
