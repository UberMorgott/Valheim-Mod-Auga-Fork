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
        // The Almanac mod's window (RuneSkin family "Almanac", restyle only): Compat.Almanac stays out.
        public static bool Almanac { get; private set; }
        // RuneSkin M4 live vanilla screens: the minimap (hudroot/MiniMap subtree, its movable offset and Jotunn's
        // map-overlay panel), the enemy / player / boss overhead HUDs, the centre + top-left messages, and the
        // in-world damage numbers. AugaSkin then swaps no prefab and restyles nothing there.
        public static bool Minimap { get; private set; }
        public static bool EnemyHud { get; private set; }
        public static bool MessageHud { get; private set; }
        public static bool DamageText { get; private set; }
        // RuneSkin M5 pre-world screens: FejdStartup's screens + the scene UnifiedPopup + the server / world row
        // templates and the save / credits panels inside FejdStartup (MainMenu), the Settings window (Settings), ZNet's
        // password / connecting dialogs (Connection, restyled in place: no Auga prefab swap).
        public static bool MainMenu { get; private set; }
        public static bool Settings { get; private set; }
        public static bool Connection { get; private set; }
        // RuneSkin M6 in-game HUD parts (restyled in place): build menu + selected-piece strip (BuildHud), crosshair /
        // hover / piece health / stealth (Crosshair), action progress / stagger / event line / mount panel (ActionHud),
        // ship HUD (ShipHud), key hints (KeyHints). AugaSkin leaves those hudroot subtrees, their movable offsets and
        // their patches alone.
        public static bool BuildHud { get; private set; }
        public static bool Crosshair { get; private set; }
        public static bool ActionHud { get; private set; }
        public static bool ShipHud { get; private set; }
        public static bool KeyHints { get; private set; }

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
            Almanac = (bool)isOwned.Invoke(null, new object[] { "Almanac" });
            // RuneSkin before M4 returns false for these: AugaSkin keeps them.
            Minimap = (bool)isOwned.Invoke(null, new object[] { "Minimap" });
            EnemyHud = (bool)isOwned.Invoke(null, new object[] { "EnemyHud" });
            MessageHud = (bool)isOwned.Invoke(null, new object[] { "MessageHud" });
            DamageText = (bool)isOwned.Invoke(null, new object[] { "DamageText" });
            // RuneSkin before M5 returns false: AugaSkin keeps the pre-world screens.
            MainMenu = (bool)isOwned.Invoke(null, new object[] { "MainMenu" });
            Settings = (bool)isOwned.Invoke(null, new object[] { "Settings" });
            Connection = (bool)isOwned.Invoke(null, new object[] { "Connection" });
            // RuneSkin before M6 returns false: AugaSkin keeps them.
            BuildHud = (bool)isOwned.Invoke(null, new object[] { "BuildHud" });
            Crosshair = (bool)isOwned.Invoke(null, new object[] { "Crosshair" });
            ActionHud = (bool)isOwned.Invoke(null, new object[] { "ActionHud" });
            ShipHud = (bool)isOwned.Invoke(null, new object[] { "ShipHud" });
            KeyHints = (bool)isOwned.Invoke(null, new object[] { "KeyHints" });
            Debug.Log($"[Auga] RuneSkin owns: Menu={Menu} Tooltips={Tooltips} Hud={Hud} Inventory={Inventory} Crafting={Crafting} Skills={Skills} Trophies={Trophies} Almanac={Almanac} Minimap={Minimap} EnemyHud={EnemyHud} MessageHud={MessageHud} DamageText={DamageText} MainMenu={MainMenu} Settings={Settings} Connection={Connection} BuildHud={BuildHud} Crosshair={Crosshair} ActionHud={ActionHud} ShipHud={ShipHud} KeyHints={KeyHints} (AugaSkin yields those)");
        }

        // Patch classes not installed while RuneSkin owns their family.
        // Hud: the stat-bar size prefixes and lift transpilers (AugaStatBars_Size_Patch).
        // Inventory: EAQS cell layout + backgrounds (EaqsSlotPanel grid/update patches), MT trash plate icon.
        // Almanac: the Almanac window restyle (Compat.Almanac: InventoryGui.Awake postfix + lazy AlmanacPanel.Start patch).
        // MessageHud also drops the AugaLog hooks: AugaMessageLog lives in the skipped MessageHud swap.
        // Minimap / EnemyHud / MessageHud / DamageText: the whole *_Setup class with its nested patch classes (Harmony
        // processes each nested type on its own, so the declaring chain is checked).
        // MainMenu: the FejdStartup Start / ServerListGui / UnifiedPopup restyles (FejdStartup.Awake keeps its features:
        // the cinematics button stays hidden, game font fallbacks stay linked; only its restyle is skipped there).
        // Settings: Settings.Awake restyle. Connection: the ZNet dialog prefab swap.
        // BuildHud: the BuildUi template restyle and the gold build-hint anchor. Crosshair: the hover colour patch.
        public static bool Skips(Type patchClass) =>
            (BuildHud && (patchClass == typeof(BuildUi_Awake_Patch) || patchClass == typeof(Hud_UpdateBuild_Patch))) ||
            (Crosshair && patchClass == typeof(Hud_UpdateCrosshair_Patch)) ||
            (MainMenu && (patchClass == typeof(FejdStartup_Awake_Patch.FejdStartup_Start_Patch) ||
                          patchClass == typeof(FejdStartup_Awake_Patch.ServerListGui_Awake_Patch) ||
                          patchClass == typeof(FejdStartup_Awake_Patch.UnifiedPopup_Awake_Patch))) ||
            (Settings && patchClass == typeof(Settings_Awake_Patch)) ||
            (Connection && patchClass == typeof(ZNet_Awake_Patch)) ||
            (Minimap && Within(patchClass, typeof(Minimap_Setup))) ||
            (EnemyHud && Within(patchClass, typeof(EnemyHud_Setup))) ||
            (MessageHud && (Within(patchClass, typeof(MessageHud_Setup)) || Array.IndexOf(AugaLog_Patches.All, patchClass) >= 0)) ||
            (DamageText && Within(patchClass, typeof(DamageText_Setup))) ||
            (Almanac && patchClass == typeof(global::Auga.Compat.Almanac)) ||
            (Tooltips && patchClass == typeof(UITooltip_OnHoverStart_Patch)) ||
            (Hud && patchClass == typeof(AugaStatBars_Size_Patch)) ||
            (Inventory && (patchClass == typeof(EaqsSlotPanel.InventoryGrid_UpdateGui_Patch) ||
                           patchClass == typeof(EaqsSlotPanel.InventoryGui_Update_Patch) ||
                           patchClass == typeof(TrashPlate_Skin)));

        private static bool Within(Type type, Type outer)
        {
            for (var t = type; t != null; t = t.DeclaringType)
                if (t == outer)
                    return true;
            return false;
        }

        // hudroot children RuneSkin owns (M6 families): Hud_Setup restyles hudroot child by child without them, gives
        // them no movable offset, and HudParts leaves their bars / ship icons.
        public static bool OwnsHudPart(Hud hud, Transform child)
        {
            if (!hud || !child) return false;
            bool Under(Component c) => c && (c.transform == child || c.transform.IsChildOf(child));
            bool UnderGo(GameObject g) => g && (g.transform == child || g.transform.IsChildOf(child));
            return (BuildHud && (Under(hud.m_buildUi) || UnderGo(hud.m_buildHud))) ||
                   (Crosshair && Under(hud.m_crosshair)) ||
                   (ActionHud && (UnderGo(hud.m_actionBarRoot) || Under(hud.m_staggerAnimator) || UnderGo(hud.m_eventBar) || UnderGo(hud.m_mountPanel))) ||
                   (ShipHud && UnderGo(hud.m_shipHudRoot)) ||
                   (KeyHints && Under(hud.GetComponentInChildren<KeyHints>(true)));
        }

        // RuneSkin's own objects under hudroot (RuneSkinHud, RuneSkinVanillaHidden): never restyled by AugaSkin.
        public static bool IsRuneSkinObject(UnityEngine.Transform t) => t && t.name.StartsWith("RuneSkin", StringComparison.Ordinal);
    }
}
