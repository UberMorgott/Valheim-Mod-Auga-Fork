# Auga fork — handoff (2026-09-11 night session)

Repo: https://github.com/UberMorgott/Valheim-Mod-AugaFork (fork of RandyKnapp/Auga), local `E:\DEV\Valheim\Auga`.
Target: Valheim 1.0.12 (Unity 6, network 40), BepInEx 5.4.23.5. Personal build.

## Native rework: Phase 3 HUD, minimap, build menu (2026-09-11 night)

- One HUD: the vanilla Hud, Minimap and build menu (BuildUIV2) keep every object, field, Canvas and update path. `Hud_Setup` now only calls `AugaStyle.Restyle` on `hudroot`, `m_pieceIconPrefab`, `HotkeyBar.m_elementPrefab` and, in a `BuildUi.Awake` prefix, `m_tagButtonPrefab`/`m_pieceButtonPrefab` (instantiated from `BuildUi.cs:154`).
- Deleted:
  - all Hud replaces (hotbar, status effects, save/connection icons, damage, crosshair, food, health, stamina and eitr bars, action bar, stagger, ship HUD)
  - the 7 skip prefixes, the 15 nulled bar fields and the rudder dummy
  - the `SetupPieceInfo` transpiler and the `UpdatePieceList`/`PieceTable` prefixes
  - `UseAugaBuildMenu`, `BuildMenuShow` and the Auga build menu
  - `StaminaBarEmptyFlash`, the `UpdateShipHud` postfix and the `HotkeyBar.UpdateIcons` postfix
  - the minimap replace, its ping dummy and the `ShowPinNameInput` transpiler
  - the Jewelcrafting hotbar prefix and `Compat/SearsCatalog.cs`
  - both `Thread.Sleep` failure paths
- Stat-bar options (`[StatBars]` text mode, ticks, fixed size) were dropped, not ported. Vanilla bars show their own value.
- Kept: the gold crosshair on hover (postfix on vanilla `UpdateCrosshair`) and the build-hint colour transpiler.
- Movable HUD (D5): `MovableHudElement.InitOffset` keeps the vanilla anchor. Config `[HudLayout] <Name>Offset`/`<Name>Scale` shifts and scales from the vanilla position, applied once and on SettingChanged.
  - Elements: HotKeyBar, KeyHints, StatusEffects, HealthPanel, the stamina/eitr/adrenaline parent, GuardianPower, EventBar, ActionProgress, StaggerPanel, MountPanel, ShipHud, Minimap.
  - The stamina, eitr and adrenaline bars themselves are not movable: vanilla sets their `anchoredPosition` every frame (`Hud.cs:1109-1186`).
- Phase 2 polish (`AugaStyle`):
  - The list selection uses Auga's `RecipeElement/selected` colour instead of blue: world, server and save rows, the compendium list, and the build-menu tag rows via `BuildUiTagButton.m_toggledOnObject`.
  - Button labels that vanilla auto-sizes get the Auga art's label inset as a TMP margin (bundle `ButtonFancy/Label` -56, `ButtonSettings/Label` -28), and vanilla auto-size shrinks them. The RectTransforms stay vanilla.
- Audit:
  - `BuildUi.m_debugUi` is ignored, because vanilla destroys it itself (`BuildUi.cs:140`).
  - Allowlist entries: `small_biome`, `iconhints` and `AdventureToggleContainer`. The vanilla large map draws KeyHints under the Quests/Treasure toggles without Auga too: baseline run with Auga disabled, `tools\out\20260911-184749\shots\23-map.png`.
- Driver (outer repo): in-world shots `20-hud`, `21-build` (hammer), `22-buildmenu`, `23-map`, plus `dump-hud`, `dump-pieceicon`, `dump-map-layout` and TMP auto-size in the dumps.
- Deployed `Auga.dll` SHA256 `4F268231389EDDB114614FA211DBE2F8F3848308E8B4E89017FB85FEED54B462`.
- Autotest `20260911-185332`: build/hash/patches(81)/smoke PASS. World FAIL from later phases only: Chat 7 (Phase 4), InventoryGui 8 plus 2 EpicLoot errors (Phase 5). Hud/Minimap findings: 0.
- In-game check (user):
  - HP, stamina, eitr and food update; status effects show.
  - The ship HUD works.
  - The minimap and large map work, including pins and ping.
  - The hammer build menu places pieces.
  - `[HudLayout]` offsets move elements.
  - The old `[StatBars]` and `[BuildMenu]` config sections are dead and can be deleted.

## Native rework: Phase 1 fixes and Phase 2 (2026-09-11 night)

- Shared restyle map `Auga/AugaStyle.cs` (`a26fdc0`). `Restyle(root)` maps vanilla sprite names (runtime dump) to Auga art and fonts. It's used by Settings, FejdStartup (plus the world-row template), ServerListGui (server-row template), UnifiedPopup, TextsDialog and the pause menu.
  - `woodpanel_*` → Auga panel (sprite-less quad + JoshH UIGradient + corner ornaments).
  - `panel_interior`/`panel_bkg`/`item_background` → TextBackdrop.
  - `button` → ButtonFancy sprite swap; `button_tab` → ButtonSettings.
  - `checkbox` → Container_Diamond; also Knob, TextInputBG, knots, the blue row selection.
  - Fonts: labels/headers → Norsebold SDF, body → SourceSansPro-Regular SDF.
- A1 (white Settings): Phase 1 copied the null sprite/white colour of AugaPanelBase/Background and the pale MediumButtonUp. The gradient and corners are now copied, and buttons use Fancy/Settings sprite swap.
- A2 (Compendium raw `$` keys, empty list): the entry now opens the vanilla Texts dialog (`3e44bdd`): Menu.Hide → InventoryGui.Show → OnOpenTexts once the root is active. Crutches 13-14 are deleted.
- Deployed `Auga.dll` SHA256 `8E560BD4F92D6E9F9A78CEE98EDEB82335C6BB0D3D421EE8DE2DF1ADE4E1DD42`.
- Autotest `20260911-182509`: build/hash/patches(101)/smoke PASS. World FAIL, later-phase findings only (Hud, Minimap, Chat, InventoryGui). Menu/Settings/MainMenu: 0 findings.
- Screenshots in `tools\out\<ts>\shots` (driver `-autotestshots`, outer repo `d2b84c2`). The driver also skips the intro cinematic via `CinematicsManager.Stop` (Esc path, `CinematicsManager.cs:123-125`). No launch arg or pref exists: `m_introOnStartup` is checked at `FejdStartup.cs:477`.
- Remaining errors (EpicLoot, Phase 5): `MagicSearchField..ctor` reads `InventoryGui.m_crafting.Find("RepairButton/Glow")`, which is missing because Auga replaces `root/Crafting`. `MagicPages.Reset` NRE is its follow-on.
- Missing-script warnings (`Fishlabs.GuiInputField`, ChatInput, 2 unnamed): bundle prefabs, Phase 6.
- In-game check (user): Settings from the main and pause menus look dark Auga and save. Character select/create, start game (tabs, world list, server options, password), join tab, manage saves and the cloud notice use Auga art. Compendium: localized, list filled, Esc closes.

## Native rework, Phases 0-1 (2026-09-11 evening)

Spec: `docs/superpowers/specs/2026-09-11-auga-native-rework.md`. §5 now records the user's decisions. **D0: full vanilla skin** (inventory included, Auga layout deleted). **D0a: new GUID and assembly name, no `Auga.API`**, so consumers take their vanilla path (Phase 4b, not started). The EAQS patch is moot.
- Phase 0 (`17925ed`; tools `85af006` in the outer repo, branch `master`):
  - dev console command `auga_audit` (needs `devcommands`), `Auga/AugaAudit.cs`. It reports missing scripts, dead vanilla refs, Graphics without a Canvas, and sibling overlaps.
  - TraceEsc/TraceInput and the debug warnings are gone.
  - autotest layer 6 **world**: the `tools\AutotestDriver` plugin runs only for that layer with `-autotest <savedir>`. Saves go to `E:\DEV\Valheim\tools\out\autotest-saves` (`AutotestChar`/`AutotestWorld`, seed `autotest`), never to AppData. See `tools\AUTOTEST.md`.
- Phase 1 (`feat(ui): restyle vanilla pause menu and Settings in place`):
  - The vanilla `Menu` is kept with its Canvas and all fields, restyled from the AugaMenu prefab.
  - The Compendium is an extra entry, spliced into gamepad navigation.
  - Settings: vanilla sprites and labels restyled, the stacked backdrop is deleted.
- Crutches removed: #12, #24, #26, #32, #33, #34 (spec §2.2).
- Deployed `Auga.dll` SHA256 `D60AA45015612B333574C47E0797F325A1A993DFEEEF9E48EA82F2B7C7406C9D`.
- Autotest: build/hash/patches (106)/smoke PASS. world FAIL = later-phase audit findings only:
  - Hud dead refs (`m_foodTime[]`, `m_foodIcon`, `m_statusEffect*`)
  - Minimap `m_mapSmall`/`m_mapLarge`
  - BuildUi `m_debugUi`
  - AugaChat 4 Graphics without Canvas
  - InventoryGui dead refs plus the overlaps Info x RightPanel and EAQS
  - Minimap IconPanel overlap
  - `Fishlabs.GuiInputField` missing script
  - Menu and Settings: 0 findings.
- In-game check (user):
  - Esc: the menu shows and pauses. Continue/Save/Settings/Compendium/Logout/Exit and both confirm dialogs work.
  - Gamepad: up/down passes through Compendium.
  - Settings from the pause and main menus: Auga panel art, all tabs, OK saves.
  - The Compendium opens and closes with Esc.
- Do not click in autotest-launched windows. The smoke layer uses the real save dir (quality-gate#27).

## 1.0.12 update (2026-09-11)

- Rebuilt against 1.0.12 with no source changes: 0 errors. Autotest: patches 107/107 resolve, smoke PASS.
- Decompile refreshed in `E:\DEV\Valheim\ValheimDecompiled`; the 1.0.7 copy is kept as `ValheimDecompiled-1.0.7`.
- Most of the 1.0.7→1.0.12 source diff is compiler noise (`&&` became `&`, primary constructors). Real UI changes:
  - `InventoryGui`: new cheat-bypass text `$achievements_permanently_cheated_bypass` on `m_achievementsCheatedText`. Auga passes the vanilla Info panel through, so no Auga change is needed.
  - `AchievementUnlockPopup`: SFX gate (`TryPlaySfx`, `m_forcePlayUnlockSound`). Auga does not touch it.
  - `Terminal`/`Chat`: new `yesiuseddevcommandsbutiwantmyachievementsanyway` command, and `HideBehindDevCommands` visibility reworked. No UI impact.
- `Hud.UpdateBuild` transpiler anchor `IL_00c1 ldstr "{0} [<color=yellow>{1}</color>]"` is unchanged in 1.0.12.
- Third-party (report only): Jotunn `GameVersions.GetNetworkVersion` reads a missing `Version::m_networkVersion`, and ConfigurationManager's `Start` reads a missing `UnityEngine.Screen::showCursor`.

- Spec: `docs/superpowers/specs/2026-09-11-auga-fork-design.md`
- Plan: `docs/superpowers/plans/2026-09-11-auga-port.md` (T9 = in-game checklist)

## State

- Build: `dotnet build Auga\Auga.csproj -c Release -p:ValheimDir=D:\Steam\steamapps\common\Valheim` → 0 errors.
- Deployed: `D:\Steam\steamapps\common\Valheim\BepInEx\plugins\Auga\Auga.dll` (hash-verified after each change).
- AugaLite (ZenDragon) deleted from plugins.
- AAACrafting disabled again: `plugins\AAACrafting\AzuAntiArthriticCrafting.dll.disabled`. 2.1.6 (latest on Thunderstore as of 2026-09-11) fails on 1.0.7 with Harmony `Undefined target method` - private `Inventory.AddItem` gained `bool skipValidPositionCheck`. Re-enable (rename back) once `update-mods.ps1` pulls a newer version.
- AdventureBackpacks fork: `2d5688a` skips its 54px durability-bar override under Auga; old DLL kept as `plugins\AdventureBackpacks\AdventureBackpacks.dll.bak`.
- Main menu decision (user, 2026-09-11): the main menu (FejdStartup: menu, character select/creation, start game, join) is pure vanilla. Auga's main-menu replacement (`MainMenu_Setup.cs`, ~1000 lines) is deleted. `MainMenu_Setup.cs` now only has a `FejdStartup.Awake` postfix that:
  - puts the font of Auga's old main-menu buttons (bundle TMP `Norsebold SDF`, 74 Cyrillic chars, loaded as `AugaAssets.NorseboldTMP`) on vanilla TMP texts inside a `Button`, with the vanilla fonts as glyph fallback. It is a display font, so body/list texts (world/server/character names) keep the vanilla font. The first try, Source Sans Pro on everything, was rejected as plain.
  - hides the button whose onClick calls `OnCinematics`, so the 1.0.7 cinematics list (Black Forest / Locked / Back) is unreachable. Vanilla `HideAll` keeps the list hidden.
  - Autotest smoke passes (menu reached, 40/40 assets). In-game recheck: the menu looks vanilla with Norse-style button labels, the Cinematics button is gone, and Cyrillic and icons render (no boxes).
  - `AugaAssets.MainMenuPrefab`/`AugaLogo`/`WorldListElement`/`ServerListElement` still load (public fields, kept for API compat) but are unused.
- APIManager `Failed patching ... InvalidCastException ... AddOverrides` for EpicLoot/EquipmentAndQuickSlots: `VisitMethod` re-owned `MethodDefinition`s nested in their `Auga.API` stub (`<Transpiler>d__2`). It is now guarded like `VisitField`.
- AAACrafting `Undefined target method ... InventoryAddItemPatchDataIntIntInt`: NOT Auga. Vanilla 1.0.7 changed private `Inventory.AddItem(ItemData,int,int,int)` to `(ItemData,int,int,int,bool skipValidPositionCheck=false)`. The exception aborts AAACrafting's `PatchAll`, so its later patch classes (ServerSync RPC, favoriting, paginator, ...) are not applied either. The mod is third-party (Azumatt, 2.1.6), so the options are: a newer AAACrafting build for 1.0.7, or disable it again (`.dll.disabled`).
- AdventureBackpacks `RegisterSlot` NRE: the statically read EAQS `AddSlot` path is null-safe once EAQS `Awake` has run, so a stack is needed. AB commit (see below) now logs the full exception. AB also ships its own APIManager patcher copy with the same `VisitMethod` bug. It is fixed the same way as Auga 76853a8 and deployed, SHA256 `D390B9727B6FFE86C453B76D9C5E8316F57234B9DEB6E9C1727EDD0DBBEDC9BF`. In-game: confirm that no `Failed patching` appears and that `Registered the 'Backpack' equipment slot` is logged. Otherwise read the logged stack.

## In-world crash (camera in the sky, placeholder HUD), fixed 2026-09-11

What the user's run log showed: Chat, Hud, and InventoryGui postfixes threw on world load, followed by ~3700x `Chat.HasFocus` NRE from `Player.TakeInput`/`Minimap.Update`/`HotkeyBar.Update`, which stalled player input, the camera and the minimap. Causes and fixes:
- Chat: the bundle's `AugaChat/Chat_box/ChatInput` uses `Fishlabs.GuiInputField` (ui_lib.dll, removed in 1.0.7), so `Chat.m_input` was null. `Chat_Setup.FixChatInput` now adds `GUIFramework.GuiInputField` to the prefab before instantiation, and falls back to vanilla chat if the layout is unexpected.
- Hud: the bundle (both mrcook1e's Unity 6000.0.61 build and upstream's 2020.3 build) has no `StatusEffectsExt/StatusEffectsInt`, which killed the postfix at the first `.gameObject`. Now guarded. Vanilla GuardianPower, LoadingBlack, EventBar and KeyHints are kept: the bundle has legacy `Text` where the 1.0.7 fields are `TMP_Text`, and it lacks `m_loadingIndicator`, `m_gpTouchButton`, `m_radialKeyHints` and `m_buildMenuHints*`.
- Build menu: `Auga.UseAugaBuildMenu => false`. The BuildHud replacement left about 15 1.0.7 fields (`m_buildSelection`, `m_pieceListRoot`, `m_requirementItems`, hovered-author...) on destroyed objects, so the vanilla build menu is used. Re-enabling means rewiring those fields.
- Inventory: 1.0.7 nests `root/Container` under `root/Player`, so the container is lifted to root before the Player replace.
- Minimap: `m_selectedIconPing`, `m_touchPingPanel` and `m_pingImageObject` now point to a hidden dummy.
- Tools: UnityPy dumps of the bundle vs the vanilla scene (`SoftRef/Bundles/17245031` = main.unity) map vanilla field→path. Autotest smoke is menu-only (quality-gate#25).
- Recheck in world: the log should have no `Chat.HasFocus`/`Hud_Awake_Postfix` errors. Check `[Auga] Hud:`/`InventoryGui:` dead-ref lines, that chat Enter sends exactly once, that the inventory and container open, that the minimap renders, and that the build menu is vanilla.

## Inventory placeholders / 7 rows / empty EAQS panel, fixed 2026-09-11 (084056b)

- Log: 3147x NRE in `InventoryGrid.UpdateGui`. Cause: 1.0.7 `InventoryGui.Awake` wires `CanDropDragOntoItem` (called unchecked, `InventoryGrid.cs:375`), `m_onReleased`, `m_onEnter`, `OnSetTouchSelection`, `OnMoveTo*` on the vanilla grids only; Auga's replaced grids had none. The NRE after the first item left empty slots with bundle placeholder art, skipped all UpdateGui postfixes and the rest of `InventoryGui.Update` (container, drag, stats, weight, recipe).
- 7 rows = EAQS 3.1.1: `m_inventory.m_height = BaseRows + 3` hidden rows for equipment/quick slots. Its `UpdateGui` postfix moves those cells into its Auga panel (`API.Panel_Create` on `m_player`), which never ran because of the NRE, so the cells stayed in the grid and the EAQS panel was empty.
- Fix: wire the delegates in the `InventoryGui.Awake` postfix. Auga's `UpdateGui` postfix now reparents only elements still under `m_gridRoot`, so it no longer fights EAQS for its cells.
- Tab "double click": no double listener found (tab buttons have no persistent onClick; `AugaTabController.Awake` is the only listener; `SelectTab` is idempotent). Suspected side effect of the per-frame NRE. Recheck.
- Recheck in world: no `UpdateGui` NRE; empty slots blank; 4 rows in the panel; EAQS equipment/quick slots in the middle panel; weight/armor labels in place; tabs switch on one click; drag/drop, split, and container take-all work.

## In-world retest fixes, 2026-09-11 afternoon (b792ec9..)

Deployed `Auga.dll` SHA256 `5128F5CEE7A747EBA0A0D3ACFCD8FC837CE0774681462C4683A7707CD0AEBBC2`; autotest build/hash/patches(107)/smoke PASS.
- Esc/pause menu (b792ec9): NOT fixed, no static cause. Checked: AugaMenu `m_root`=MenuRoot (not the Menu GO), all `Menu.Update` open-condition terms (inventory, minimap, TextInput/Barber/Store replacements, ZNet dialogs, chat focus, radial, build UI); none stuck on paper; user log has no exception. Added config `[Logging] TraceInput = true` (`BepInEx\config\randyknapp.mods.auga.cfg`; add the line under `[Logging]` if absent): each Esc logs `[Auga] Esc: ...` with every term; the `true` one is the blocker (or `Menu.instance is null` / `root=True`).
- Tab double click (ab08cc3): bundle `ButtonSfx` sets `m_selectSfxPrefab` on ~130 buttons (vanilla 2/59). Mouse-down selects (select sfx), mouse-up clicks (click sfx) >2 frames later, past `SfxTimer`. `ButtonSfx.OnSelect` now skips `PointerEventData`. If tabs still act twice (not just sound), trace listeners next.
- Inventory scroll (6214414): bundle Main viewport = exactly 3 rows (224px, pad 10+10, 64+6). Postfix (now `Priority.Last`, after EAQS) grows `m_player` by any content overflow. One-shot log `[Auga] PlayerGrid: N main cells, content, viewport, grew` shows why it overflowed.
- EAQS slots (6214414, log only): EAQS 3.1.1 calls only `Panel_Create`/`Divider_CreateSmall`; panel at `m_player` top-left + (752,-166), but `Panel_Create` centres the pivot (upstream since 2021). Cells go under its own `EaqsSlotRoot` at `m_gridRoot`'s anchor point (vanilla `ResetView` flips that pivot). Offsets don't reconcile on paper, so no fix yet; the same one-shot log dumps EAQS panel/slotRoot/cell rects in `m_player` space. Fix from those numbers (likely pin `EaqsSlotRoot` or adjust `Panel_Create` pivot for EAQS).
- Settings (this commit): `Settings.Awake` postfix hides `Settings/Panel` Image (`woodpanel_settings`) and puts `AugaPanelBase` behind the tabs; `TabContent` inner bkg (`panel_interior_bkg_128`) left as is.
- Log (user run 16:12): zero exceptions. Auga warnings left: known dead refs (Hud/InventoryGui/Minimap), `Fishlabs.GuiInputField` missing script x2 (bundle ChatInput + one more prefab). Third-party: Epic Loot `missing ItemDrop` (FrozenKing_Summon, PropFeastDeepNorth, SnowRoller; 21x each), Jotunn ambiguous-asset/mock warnings.
- In-game recheck: set TraceInput, press Esc in world, send `[Auga] Esc:` + `[Auga] PlayerGrid:`/`EAQS` lines; tab click = one sound; inventory no scrollbar; Settings from main + pause menu is Auga-styled and still saves.

## Done (main)

- T0 merge mrcook1e-ai/Auga (Unity 6 port) + cleanup.
- T1 build against 1.0.7 API; InventoryGrid uses vanilla `UpdateGui` with runtime `InventoryElement` wiring on Auga's slot prefab.
- T2 split dialog on 1.0.7 `SplitDialog`.
- T3 vanilla Settings window (Auga settings replacement removed; pause menu copies vanilla `m_settingsPrefab`).
- T4 `Hud.UpdateBuild` transpiler exact-match.
- T6 real APIManager patcher vendored (AAACrafting binding).
- T7 API compat: `Auga.API` unchanged (72 methods); consumers' calls all present.
- T8 SmoothRegen HP bar: `FastBar.m_changeDelay = 0` (`tools\guibar-delay-check.ps1`).
- T5 vanilla UI audit: pause menu wired to 1.0.7 fields (would have crashed), vanilla Info panel kept (Texts/Trophies/Achievements), mount panel + adrenaline bar passed through, main-menu vanilla elements passed through instead of dummies, `m_uiGroups` order fixed for 1.0.7.
- Vanilla main menu with Auga font; cinematics button hidden (see State).
- Review fixes: arrival log only on first spawn, chat scroll via `ZInput`, no double assembly loads, snapping icon like vanilla, HUD restyle independent of build-menu option.

## First in-game run

1. Start the game and play through the T9 checklist in the plan.
2. Collect `D:\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log` and grep `[Auga]`:
   - `all 39 assets found` means the bundle loaded; otherwise it lists what is missing.
   - `MainMenu: Norsebold SDF missing` means the main menu fell back to the vanilla font.
   - `FixDeadFields` / dead-ref warnings mean a field points at a destroyed object.
   - A transpiler hit-count error means the `UpdateBuild` anchor is wrong.

## Known open risks

- The 2022.3 asset bundle has never been loaded in the Unity 6 runtime.
- Passed-through vanilla elements keep vanilla anchors and may overlap Auga panels.
- The adrenaline bar keeps its world position (no Auga layout yet).
- Backpacks wider than about 8 columns may overflow Auga's fixed container panel.
- Chat scroll speed with `ZInput.GetMouseScrollWheel()` is not checked.
