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
        // RuneSkin M6 batch 2 (restyled in place): black screens (Hud.m_loadingScreen: respawn / sleep / teleport),
        // chat (window + world / NPC text templates), small popups (achievement, join code, session player list,
        // closed captions), text dialogs (TextInput, TextViewer: AugaSkin swaps no prefab for it then), compendium
        // (TextsDialog, Epic Loot compendium pages), split dialog (InventoryGui.m_splitDialog).
        public static bool LoadScreen { get; private set; }
        public static bool Chat { get; private set; }
        public static bool Popups { get; private set; }
        public static bool TextDialogs { get; private set; }
        public static bool Compendium { get; private set; }
        public static bool SplitDialog { get; private set; }
        // RuneSkin M6 batch 3: trader store (vanilla StoreGui, no Auga store swap; Epic Loot's trader panels stay ours),
        // barber (no AugaBarber swap), F5 console + F2 connect panel (DevPanels), feedback dialog.
        public static bool Store { get; private set; }
        public static bool Barber { get; private set; }
        public static bool DevPanels { get; private set; }
        public static bool Feedback { get; private set; }
        // Compendium active effects page: RuneSkin >= 0.9.1 draws its own list there (RuneSkin ActiveEffectsPage, same
        // MtActiveEffectRowsV1 contract); with an older RuneSkin owning Compendium AugaSkin keeps its page.
        // RuneSkin M7 (0.11.0, API 8): the fallback restyler for every UI no family owns (ModUi: Jotunn GUIManager
        // windows, IMGUI fonts, Epic Loot popups, other mods' windows), Epic Loot's enchanting table / adventure board /
        // tempering panel, other mods' HUD parts (ModHud), the achievements panel / variant dialog / drag icon
        // (InventoryExtras) and the radial menus (Radial).
        public static bool ModUi { get; private set; }
        public static bool EpicLootEnchanting { get; private set; }
        public static bool EpicLootBoard { get; private set; }
        public static bool EpicLootTemper { get; private set; }
        public static bool ModHud { get; private set; }
        public static bool InventoryExtras { get; private set; }
        public static bool Radial { get; private set; }
        // RuneSkin 0.13.0 (API 9, batch P): features that were AugaSkin's: the pause-menu Compendium entry, summon frames,
        // other players' shield strips (EnergyShieldHud), the mouse cursor.
        public static bool MenuCompendium { get; private set; }
        public static bool SummonFrames { get; private set; }
        public static bool PlayerShields { get; private set; }
        public static bool Cursor { get; private set; }
        // RuneSkin API 11: the character stats window (5th info button, CharacterStatsPanel + its FoodTick feed).
        public static bool CharacterStats { get; private set; }
        public static bool ActiveEffects { get; private set; }
        public static readonly System.Version ActiveEffectsSince = new System.Version(0, 9, 1);

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
            // RuneSkin before 0.8.0 (API 6) returns false: AugaSkin keeps them.
            LoadScreen = (bool)isOwned.Invoke(null, new object[] { "LoadScreen" });
            Chat = (bool)isOwned.Invoke(null, new object[] { "Chat" });
            Popups = (bool)isOwned.Invoke(null, new object[] { "Popups" });
            TextDialogs = (bool)isOwned.Invoke(null, new object[] { "TextDialogs" });
            Compendium = (bool)isOwned.Invoke(null, new object[] { "Compendium" });
            SplitDialog = (bool)isOwned.Invoke(null, new object[] { "SplitDialog" });
            // RuneSkin before 0.9.0 (API 7) returns false: AugaSkin keeps them.
            Store = (bool)isOwned.Invoke(null, new object[] { "Store" });
            Barber = (bool)isOwned.Invoke(null, new object[] { "Barber" });
            DevPanels = (bool)isOwned.Invoke(null, new object[] { "DevPanels" });
            Feedback = (bool)isOwned.Invoke(null, new object[] { "Feedback" });
            // RuneSkin before 0.11.0 (API 8) returns false: AugaSkin keeps them.
            ModUi = (bool)isOwned.Invoke(null, new object[] { "ModUi" });
            EpicLootEnchanting = (bool)isOwned.Invoke(null, new object[] { "EpicLootEnchanting" });
            EpicLootBoard = (bool)isOwned.Invoke(null, new object[] { "EpicLootBoard" });
            EpicLootTemper = (bool)isOwned.Invoke(null, new object[] { "EpicLootTemper" });
            ModHud = (bool)isOwned.Invoke(null, new object[] { "ModHud" });
            InventoryExtras = (bool)isOwned.Invoke(null, new object[] { "InventoryExtras" });
            Radial = (bool)isOwned.Invoke(null, new object[] { "Radial" });
            // RuneSkin before 0.13.0 (API 9) returns false: AugaSkin keeps them.
            MenuCompendium = (bool)isOwned.Invoke(null, new object[] { "MenuCompendium" });
            SummonFrames = (bool)isOwned.Invoke(null, new object[] { "SummonFrames" });
            PlayerShields = (bool)isOwned.Invoke(null, new object[] { "PlayerShields" });
            Cursor = (bool)isOwned.Invoke(null, new object[] { "Cursor" });
            // RuneSkin before API 11 returns false: AugaSkin keeps its stats window.
            CharacterStats = Inventory && (bool)isOwned.Invoke(null, new object[] { "CharacterStats" });
            ActiveEffects = Compendium && Chainloader.PluginInfos[Guid].Metadata.Version >= ActiveEffectsSince;
            Debug.Log($"[Auga] RuneSkin owns: Menu={Menu} Tooltips={Tooltips} Hud={Hud} Inventory={Inventory} Crafting={Crafting} Skills={Skills} Trophies={Trophies} Almanac={Almanac} Minimap={Minimap} EnemyHud={EnemyHud} MessageHud={MessageHud} DamageText={DamageText} MainMenu={MainMenu} Settings={Settings} Connection={Connection} BuildHud={BuildHud} Crosshair={Crosshair} ActionHud={ActionHud} ShipHud={ShipHud} KeyHints={KeyHints} LoadScreen={LoadScreen} Chat={Chat} Popups={Popups} TextDialogs={TextDialogs} Compendium={Compendium} SplitDialog={SplitDialog} Store={Store} Barber={Barber} DevPanels={DevPanels} Feedback={Feedback} ModUi={ModUi} EpicLootEnchanting={EpicLootEnchanting} EpicLootBoard={EpicLootBoard} EpicLootTemper={EpicLootTemper} ModHud={ModHud} InventoryExtras={InventoryExtras} Radial={Radial} MenuCompendium={MenuCompendium} SummonFrames={SummonFrames} PlayerShields={PlayerShields} Cursor={Cursor} CharacterStats={CharacterStats} ActiveEffects={ActiveEffects} (AugaSkin yields those)");
        }

        // Patch classes not installed while RuneSkin owns their family.
        // Hud: the stat-bar size prefixes and lift transpilers (AugaStatBars_Size_Patch).
        // Inventory: EAQS cell layout + backgrounds (EaqsSlotPanel grid/update patches), MT trash plate icon.
        // Almanac: the Almanac window restyle (Compat.Almanac: InventoryGui.Awake postfix + lazy AlmanacPanel.Start patch).
        // MessageHud also drops the AugaLog hooks: AugaMessageLog lives in the skipped MessageHud swap; with them the
        // LastHitTracker feed (Player.OnDamaged postfix adding a component per hit), whose only reader is AugaLog's death
        // line (RuneSkin batch P: the message log is not ported, MorgottTweaks DeathMessages announces deaths).
        // Minimap / EnemyHud / MessageHud / DamageText: the whole *_Setup class with its nested patch classes (Harmony
        // processes each nested type on its own, so the declaring chain is checked).
        // MainMenu: the FejdStartup Start / ServerListGui / UnifiedPopup restyles (FejdStartup.Awake keeps its features:
        // the cinematics button stays hidden, game font fallbacks stay linked; only its restyle is skipped there).
        // Settings: Settings.Awake restyle. Connection: the ZNet dialog prefab swap.
        // BuildHud: the BuildUi template restyle and the gold build-hint anchor. Crosshair: the hover colour patch.
        // Chat: Chat.Awake restyle + NPC text colours. TextDialogs: TextInput restyle, TextViewer prefab swap.
        // Compendium: TextsDialog restyle + topic colour (Epic Loot compendium pages: Compat.EpicLootEnchanting).
        // Popups / LoadScreen / SplitDialog: left out inside VanillaPanels, Hud_Setup and PlayerInventory_Setup.
        // Store: the Auga store swap + its row tooltips (Store_Setup; Store_EpicLootPanels stays). Barber: the AugaBarber
        // swap. DevPanels / Feedback: left out inside VanillaPanels. ActiveEffects: the compendium active effects page
        // (ActiveEffectsPage patches; RuneSkin draws its own).
        // MenuCompendium: the Compendium entry's gamepad splice (Menu_UpdateNavigation_Patch; Menu.Start skips the entry).
        // PlayerShields: the other players' shield strips (EnergyShieldHud).
        // CharacterStats: the stats window's Escape / Hide patches and its FoodTick heal feed (PlayerInventory_Setup skips
        // CharacterStatsPanel.Create: no 5th button, no window).
        public static bool Skips(Type patchClass) =>
            (CharacterStats && (patchClass == typeof(CharacterStatsPanel_InventoryGui_Patches) || patchClass == typeof(FoodTick))) ||
            (MenuCompendium && patchClass == typeof(PauseMenu_Setup.Menu_UpdateNavigation_Patch)) ||
            (PlayerShields && patchClass == typeof(EnergyShieldHud)) ||
            (ActiveEffects && Within(patchClass, typeof(ActiveEffectsPage))) ||
            (Store && patchClass == typeof(Store_Setup)) ||
            (EpicLootBoard && EpicLootTemper && patchClass == typeof(Store_EpicLootPanels)) ||
            (ModHud && patchClass == typeof(Hud_Awake_ModParts)) ||
            (Barber && Within(patchClass, typeof(Barber_Setup))) ||
            (Chat && Within(patchClass, typeof(Chat_Setup))) ||
            (TextDialogs && (patchClass == typeof(TextInput_Setup) || Within(patchClass, typeof(TextViewer_Setup)))) ||
            (Compendium && (patchClass == typeof(PauseMenu_Setup.TextsDialog_Awake_Patch) || patchClass == typeof(PauseMenu_Setup.TextsDialog_ShowText_Patch))) ||
            (BuildHud && (patchClass == typeof(BuildUi_Awake_Patch) || patchClass == typeof(Hud_UpdateBuild_Patch))) ||
            (Crosshair && patchClass == typeof(Hud_UpdateCrosshair_Patch)) ||
            (MainMenu && (patchClass == typeof(FejdStartup_Awake_Patch.FejdStartup_Start_Patch) ||
                          patchClass == typeof(FejdStartup_Awake_Patch.ServerListGui_Awake_Patch) ||
                          patchClass == typeof(FejdStartup_Awake_Patch.UnifiedPopup_Awake_Patch))) ||
            (Settings && patchClass == typeof(Settings_Awake_Patch)) ||
            (Connection && patchClass == typeof(ZNet_Awake_Patch)) ||
            (Minimap && Within(patchClass, typeof(Minimap_Setup))) ||
            (EnemyHud && Within(patchClass, typeof(EnemyHud_Setup))) ||
            (MessageHud && (Within(patchClass, typeof(MessageHud_Setup)) || Array.IndexOf(AugaLog_Patches.All, patchClass) >= 0 ||
                            patchClass == typeof(Player_OnDamaged_Patch))) ||
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
            return (BuildHud && (Under(hud.m_buildUi) || UnderGo(hud.m_buildHud) || UnderGo(hud.m_hoveredPieceAuthorWindow))) ||
                   (Crosshair && Under(hud.m_crosshair)) ||
                   (ActionHud && (UnderGo(hud.m_actionBarRoot) || Under(hud.m_staggerAnimator) || UnderGo(hud.m_eventBar) || UnderGo(hud.m_mountPanel))) ||
                   (ShipHud && UnderGo(hud.m_shipHudRoot)) ||
                   (KeyHints && Under(hud.GetComponentInChildren<KeyHints>(true))) ||
                   (child.parent == hud.m_rootObject.transform && IsModHudPart(child));
        }

        // InventoryGui subtrees RuneSkin owns outside the inventory layout (M6 batch 2 dialogs): AugaSkin leaves them.
        public static bool OwnsInventoryPart(InventoryGui gui, Transform t) =>
            gui && t && ((Compendium && gui.m_textsDialog && t == gui.m_textsDialog.transform) ||
                         (SplitDialog && gui.m_splitDialog && t == gui.m_splitDialog.transform) ||
                         (InventoryExtras && gui.m_achievementsPanel && t == gui.m_achievementsPanel.transform) ||
                         (InventoryExtras && gui.m_variantDialog && t == gui.m_variantDialog.transform));

        // hudroot children other mods add (no game component on the child itself, not AugaSkin's / RuneSkin's, not
        // BossAwakening's BA_* boss bar): RuneSkin's ModHud family styles them.
        public static bool IsModHudPart(Transform child)
        {
            if (!ModHud || !child || child.name.StartsWith("BA_", StringComparison.Ordinal) || IsRuneSkinObject(child)) return false;
            var foreign = false;
            foreach (var b in child.GetComponents<MonoBehaviour>())
            {
                if (!b) continue;
                var asm = b.GetType().Assembly.GetName().Name;
                if (asm.StartsWith("assembly_", StringComparison.Ordinal) || asm.StartsWith("UnityEngine", StringComparison.Ordinal) || asm.StartsWith("Unity.", StringComparison.Ordinal) || asm == "gui_framework") return false;
                if (asm != "AugaSkin" && asm != "RuneSkin") foreign = true;
            }
            return foreign;
        }

        // Vanilla panels RuneSkin's Popups family styles (VanillaPanels list).
        public static bool OwnsPopup(MonoBehaviour panel) =>
            (Popups && (panel is AchievementUnlockPopup || panel is JoinCode || panel is Valheim.UI.SessionPlayerList || panel is ClosedCaptions)) ||
            (DevPanels && (panel is global::Console || panel is ConnectPanel)) ||
            (Feedback && panel is global::Feedback);

        // RuneSkin's own objects under hudroot (RuneSkinHud, RuneSkinVanillaHidden): never restyled by AugaSkin.
        public static bool IsRuneSkinObject(UnityEngine.Transform t) => t && t.name.StartsWith("RuneSkin", StringComparison.Ordinal);
    }
}
