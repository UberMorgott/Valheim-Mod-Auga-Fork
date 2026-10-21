using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Auga.Compat
{
    // RuneSkin (morgott.valheim.runeskin, successor skin) owns the screen families it took at startup; AugaSkin must not
    // style those, so each screen has exactly one skin. RuneSkin freezes its set in its own Awake, which runs before
    // ours (soft [BepInDependency] on its GUID in Auga.cs). Read once here, BEFORE Harmony patching, through reflection
    // (no reference to RuneSkin.API.dll): an owned family's patches are not installed at all, or, where a patch also
    // carries a feature (pause-menu Compendium entry), only its restyle is skipped.
    public static class RuneSkinOwnership
    {
        public const string Guid = "morgott.valheim.runeskin";

        public static bool Menu { get; private set; }
        public static bool Tooltips { get; private set; }
        // Player HUD cluster (bars, food, status effects, hotbar placement): RuneSkin draws its own and hides the
        // vanilla parts; AugaSkin keeps the rest of hudroot (minimap, build HUD, ship HUD, crosshair...).
        public static bool Hud { get; private set; }
        // Inventory screen layout (RuneSkin M2): player panel, doll / EAQS cells, grid cells, container dock, info
        // header and the crafting panel's place. AugaSkin keeps styling skills, texts, trophies and dialogs.
        public static bool Inventory { get; private set; }
        // Crafting column (RuneSkin M3, needs Inventory): InventoryGui.m_crafting subtree and the recipe row template.
        public static bool Crafting { get; private set; }
        // Skills dialog / trophies panel (RuneSkin M3, need Inventory): their subtrees and row templates.
        public static bool Skills { get; private set; }
        public static bool Trophies { get; private set; }

        public static void Freeze()
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid))
                return;
            var api = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RuneSkin.API");
            var isOwned = api?.GetType("RuneSkin.API.Skin")?.GetMethod("IsOwned", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (isOwned == null)
            {
                Debug.LogWarning("[Auga] RuneSkin present but RuneSkin.API.Skin.IsOwned not found; AugaSkin keeps every screen");
                return;
            }
            Menu = (bool)isOwned.Invoke(null, new object[] { "Menu" });
            Tooltips = (bool)isOwned.Invoke(null, new object[] { "Tooltips" });
            Hud = (bool)isOwned.Invoke(null, new object[] { "Hud" });
            Inventory = (bool)isOwned.Invoke(null, new object[] { "Inventory" });
            // RuneSkin before M3 throws nothing for an unknown family (false): AugaSkin then keeps crafting.
            Crafting = Inventory && (bool)isOwned.Invoke(null, new object[] { "Crafting" });
            Skills = Inventory && (bool)isOwned.Invoke(null, new object[] { "Skills" });
            Trophies = Inventory && (bool)isOwned.Invoke(null, new object[] { "Trophies" });
            Debug.Log($"[Auga] RuneSkin owns: Menu={Menu} Tooltips={Tooltips} Hud={Hud} Inventory={Inventory} Crafting={Crafting} Skills={Skills} Trophies={Trophies} (AugaSkin yields those)");
        }

        // Patch classes not installed while RuneSkin owns their family.
        // Hud: the stat-bar size prefixes and lift transpilers (AugaStatBars_Size_Patch).
        // Inventory: EAQS cell layout + backgrounds (EaqsSlotPanel grid/update patches), MT trash plate icon.
        public static bool Skips(Type patchClass) =>
            (Tooltips && patchClass == typeof(UITooltip_OnHoverStart_Patch)) ||
            (Hud && patchClass == typeof(AugaStatBars_Size_Patch)) ||
            (Inventory && (patchClass == typeof(EaqsSlotPanel.InventoryGrid_UpdateGui_Patch) ||
                           patchClass == typeof(EaqsSlotPanel.InventoryGui_Update_Patch) ||
                           patchClass == typeof(TrashPlate_Skin)));

        // RuneSkin's own objects under hudroot (RuneSkinHud, RuneSkinVanillaHidden): never restyled by AugaSkin.
        public static bool IsRuneSkinObject(UnityEngine.Transform t) => t && t.name.StartsWith("RuneSkin", StringComparison.Ordinal);
    }
}
