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

            AugaStyle.Restyle(__instance.transform);
            // Templates vanilla instantiates at runtime: grid slots (InventoryGrid.UpdateGui), recipe rows
            // (InventoryGui.AddRecipeToList), trophies and achievements, the drag icon.
            foreach (var template in new[]
                     {
                         __instance.m_playerGrid.m_elementPrefab, __instance.m_containerGrid.m_elementPrefab,
                         __instance.m_recipeElementPrefab, __instance.m_trophieElementPrefab,
                         __instance.m_achievementsElementPrefab, __instance.m_dragItemPrefab,
                         // skill rows, instantiated by SkillsDialog.Setup (SkillsDialog.cs:119)
                         __instance.m_skillsDialog ? __instance.m_skillsDialog.m_elementPrefab : null,
                     })
                if (template)
                    AugaStyle.Restyle(template.transform);
        }

        private static void AddItemIconMaterialFrom(InventoryGui gui)
        {
            var icon = gui.m_dragItemPrefab ? gui.m_dragItemPrefab.transform.Find("icon") : null;
            if (icon && icon.TryGetComponent<Image>(out var image))
                AugaUnity.AddItemIconMaterial.IconMaterial = image.material;
        }
    }
}
